using Grpc.Net.Client;
using MeteoMesh5.CentralServer.Grpc;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Models;
using MeteoMesh5.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeteoMesh5.LocalNode.Services;

public class NodeRegistrationService : BackgroundService
{
    private readonly ILogger<NodeRegistrationService> _logger;
    private readonly LocalNodeConfig _nodeConfig;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<SimulationOptions> _simulationOptions;
    
    private GrpcChannel? _channel;
    private CentralServerService.CentralServerServiceClient? _client;
    private Timer? _heartbeatTimer;
    private bool _isRegistered = false;

    public NodeRegistrationService(
        ILogger<NodeRegistrationService> logger,
        IOptions<LocalNodeConfig> nodeConfig,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        IOptions<SimulationOptions> simulationOptions)
    {
        _logger = logger;
        _nodeConfig = nodeConfig.Value;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _simulationOptions = simulationOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var centralServerUrl = _nodeConfig.CentralUrl;
        if (string.IsNullOrEmpty(centralServerUrl))
        {
            _logger.LogWarning("CentralServer:Url not configured - Node will not register with central server");
            return;
        }

        _logger.LogInformation("Starting node registration service - Central Server: {Url}", centralServerUrl);

        try
        {
            _channel = GrpcChannel.ForAddress(centralServerUrl, new GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }
            });
            _client = new CentralServerService.CentralServerServiceClient(_channel);

            // Initial registration
            await RegisterWithCentralServer();

            // Calculate heartbeat interval based on simulation speed
            var baseHeartbeatSeconds = 120.0;
            var simulationSpeed = _simulationOptions.Value?.SpeedMultiplier ?? 1.0;
            var useSimulation = _simulationOptions.Value?.UseSimulation ?? false;
            
            // In simulation mode, adjust the real-world timer interval
            var realWorldHeartbeatInterval = useSimulation && simulationSpeed > 1.0
                ? TimeSpan.FromSeconds(baseHeartbeatSeconds / simulationSpeed)
                : TimeSpan.FromSeconds(baseHeartbeatSeconds);

            _logger.LogInformation("Heartbeat interval: {Interval}s (simulation: {UseSimulation}, speed: {Speed}x)", 
                realWorldHeartbeatInterval.TotalSeconds, useSimulation, simulationSpeed);

            // Start heartbeat timer with simulation-adjusted interval
            _heartbeatTimer = new Timer(async _ => await SendHeartbeat(), null, 
                realWorldHeartbeatInterval, realWorldHeartbeatInterval);

            // Keep service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Node registration service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in node registration service");
        }
    }

    private async Task RegisterWithCentralServer()
    {
        try
        {
            var currentTime = _timeProvider.GetUtcNow();
            
            var request = new LocalNodeRegistrationRequest
            {
                NodeId = $"NODE_{_nodeConfig.Id}",
                NodeName = _nodeConfig.Name,
                Version = "5.0",
                NodeUrl = $"https://localhost:{_nodeConfig.Port}",
                Location = _nodeConfig.Name.Replace("Knoten","").Trim(), // Could be enhanced
                Latitude = _nodeConfig.Coordinates?.Latitude ?? 0.0,
                Longitude = _nodeConfig.Coordinates?.Longitude ?? 0.0,
                StartupTime = currentTime.ToUnixTimeSeconds()
            };

            request.Capabilities.Add("Temperature");
            request.Capabilities.Add("Humidity");
            request.Capabilities.Add("Pressure");
            request.Capabilities.Add("Lidar");

            var response = await _client!.RegisterLocalNodeAsync(request);

            if (response.Success)
            {
                _isRegistered = true;
                _logger.LogInformation("Successfully registered with Central Server at {Time}: {Message}", 
                    currentTime.ToString("HH:mm:ss"), response.Message);
                _logger.LogInformation("Assigned Node ID: {NodeId}, Heartbeat interval: {Interval}s", 
                    response.AssignedNodeId, response.HeartbeatIntervalSeconds);
            }
            else
            {
                _logger.LogError("Failed to register with Central Server: {Message}", response.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering with Central Server");
        }
    }

    private async Task SendHeartbeat()
    {
        if (!_isRegistered || _client == null)
        {
            _logger.LogDebug("Skipping heartbeat - not registered or no client");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Get current stations and their status
            var stations = await db.Stations.AsNoTracking().ToListAsync();
            var totalMeasurements = await db.Measurements.CountAsync();
            
            var currentTime = _timeProvider.GetUtcNow();

            var request = new HeartbeatRequest
            {
                NodeId = $"NODE_{_nodeConfig.Id}",
                Status = new NodeStatus
                {
                    IsHealthy = true,
                    CpuUsage = 0.0, // Could implement actual monitoring
                    MemoryUsage = 0.0,
                    ActiveConnections = stations.Count,
                    LastDataReceived = currentTime.ToUnixTimeSeconds(),
                    StatusMessage = "Running",
                    TotalMeasurements = totalMeasurements
                }
            };

            foreach (var station in stations)
            {
                request.Stations.Add(new StationStatus
                {
                    StationId = station.StationId,
                    StationName = station.StationId,
                    StationType = station.Type.ToString(),
                    IsActive = !station.Suspended,
                    LastMeasurement = ((DateTimeOffset)station.LastUpdated).ToUnixTimeSeconds(),
                    MeasurementCount = 0, // Could implement actual count
                    LastValue = station.LastValue ?? 0.0,
                    Quality = "Good"
                });
            }

            var response = await _client.SendHeartbeatAsync(request);

            if (response.Acknowledged)
            {
                _logger.LogDebug("Heartbeat acknowledged by Central Server at {Time} (sim time)", 
                    currentTime.ToString("HH:mm:ss"));
            }
            else
            {
                _logger.LogWarning("Heartbeat not acknowledged by Central Server");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat to Central Server");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping node registration service");
        
        _heartbeatTimer?.Dispose();
        _channel?.Dispose();
        
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _channel?.Dispose();
        base.Dispose();
    }
}