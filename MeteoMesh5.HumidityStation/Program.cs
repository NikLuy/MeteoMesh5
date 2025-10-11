using Grpc.Net.Client;
using MeteoMesh5.Grpc;
using MeteoMesh5.Shared.Extensions;
using MeteoMesh5.Shared.Models;
using MeteoMesh5.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
// Logging
builder.Services.AddSerilog(cfg => cfg.ReadFrom.Configuration(builder.Configuration));

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));

// Add TimeProvider - will automatically use SimulationTimeProvider if UseSimulation=true
builder.Services.AddTimeProvider();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSingleton<StationWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StationWorker>());

var app = builder.Build();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class App : ComponentBase {}

public class StationWorker : BackgroundService
{
    private readonly ILogger<StationWorker> _logger; 
    private readonly IOptions<SimulationOptions> _sim; 
    private readonly IConfiguration _cfg; 
    private readonly TimeProvider _timeProvider;
    private readonly TimeAlignmentService _timeAlignment;
    private GrpcChannel? _channel; 
    private StationIngressService.StationIngressServiceClient? _client; 
    private TimeSpan _interval = TimeSpan.FromMinutes(15);
    private readonly string _stationId; 
    private readonly string _stationType; 
    private readonly string _nodeUrl; 
    private bool _suspended = false;
    
    public StationWorker(ILogger<StationWorker> logger, IOptions<SimulationOptions> sim, IConfiguration cfg, 
        TimeProvider timeProvider, TimeAlignmentService timeAlignment)
    { 
        _logger = logger; 
        _sim = sim; 
        _cfg = cfg; 
        _timeProvider = timeProvider;
        _timeAlignment = timeAlignment;
        _stationId = _cfg["Station:Id"] ?? "HUM_SIM"; 
        _stationType = "Humidity"; 
        _nodeUrl = _cfg["Station:NodeUrl"] ?? "https://localhost:7101"; 
        
        _logger.LogInformation("Humidity Station {StationId} initialized - Target: {NodeUrl}", _stationId, _nodeUrl);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Humidity Station {StationId} with simulation config: Start={Start}, Speed={Speed}x", 
            _stationId, _sim.Value.StartTime, _sim.Value.SpeedMultiplier);
            
        var measurementCount = 0;
        var suspendedCount = 0;
        
        try
        {
            _channel = GrpcChannel.ForAddress(_nodeUrl, new GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }
            });
            _client = new StationIngressService.StationIngressServiceClient(_channel);
            
            _logger.LogInformation("gRPC channel established to {NodeUrl}", _nodeUrl);
            
            // Start command stream listener
            _ = Task.Run(async () => await ListenForCommands(stoppingToken), stoppingToken);
            
            var currentTime = _timeProvider.GetUtcNow();
            var nextMeasurementTime = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, _interval);
            
            _logger.LogInformation("Beginning measurement loop - Interval: {IntervalMinutes} minutes, Next measurement: {NextTime}", 
                _interval.TotalMinutes, nextMeasurementTime.ToString("HH:mm:ss"));
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    currentTime = _timeProvider.GetUtcNow();
                    
                    // Check if we've reached the next aligned measurement time
                    if (currentTime >= nextMeasurementTime)
                    {
                        measurementCount++;
                        
                        if (!_suspended)
                        {
                            // Generate humidity value 40-80% + noise using aligned time
                            var minutesSinceStart = _timeAlignment.GetElapsedMinutes(_sim.Value.StartTime);
                            var humidity = 60 + 15 * Math.Sin(minutesSinceStart / 120.0 * Math.PI * 2) + Random.Shared.NextDouble() * 2;
                            
                            var req = new SubmitMeasurementRequest
                            { 
                                Measurement = new SensorMeasurement 
                                {
                                    StationId = _stationId,
                                    StationType = _stationType,
                                    TimestampUnix = nextMeasurementTime.ToUnixTimeSeconds(), // Use aligned time, not current time
                                    Value = humidity,
                                    Quality = "Good"
                                }
                            };
                            
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var resp = await _client.SubmitMeasurementAsync(req, cancellationToken: stoppingToken);
                            sw.Stop();
                            
                            if (!resp.Success)
                            {
                                _logger.LogWarning("Measurement #{Count} submission failed: {Message} (took {ElapsedMs}ms)", 
                                    measurementCount, resp.Message, sw.ElapsedMilliseconds);
                            }
                            else
                            {
                                _logger.LogInformation("Measurement #{Count} submitted successfully: H={Humidity:F1}% at aligned time {AlignedTime} (gRPC: {ElapsedMs}ms)", 
                                    measurementCount, humidity, nextMeasurementTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
                        }
                        else
                        {
                            suspendedCount++;
                            _logger.LogWarning("Measurement #{Count} SUSPENDED due to rain detection - Skip #{SkipCount} at aligned time {AlignedTime}", 
                                measurementCount, suspendedCount, nextMeasurementTime.ToString("HH:mm:ss"));
                        }
                        
                        // Calculate next aligned measurement time
                        nextMeasurementTime = _timeAlignment.GetNextAlignedMeasurementTime(nextMeasurementTime, _interval);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Measurement #{Count} failed - Sim time: {SimTime}", measurementCount, _timeProvider.GetUtcNow());
                }
                
                // Wait a short period before checking again
                var checkInterval = TimeSpan.FromMilliseconds(100);
                try { await Task.Delay(checkInterval, stoppingToken); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Humidity Station {StationId} stopped by cancellation", _stationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Humidity Station {StationId} failed", _stationId);
        }
        finally
        {
            _logger.LogInformation("Humidity Station {StationId} final stats: {TotalMeasurements} total, {SuspendedCount} suspended", 
                _stationId, measurementCount, suspendedCount);
        }
    }
    
    private async Task ListenForCommands(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting command stream listener for {StationId}", _stationId);
            
            var controlClient = new StationControlService.StationControlServiceClient(_channel!);
            var stream = controlClient.StreamCommands(new CommandStreamRequest 
            { 
                StationId = _stationId, 
                StationType = _stationType 
            }, cancellationToken: ct);
            
            while (await stream.ResponseStream.MoveNext(ct))
            {
                var cmd = stream.ResponseStream.Current;
                _logger.LogInformation("Command received: {CommandId} - {Action} issued={IssuedTime}", 
                    cmd.CommandId, cmd.Action, DateTimeOffset.FromUnixTimeSeconds(cmd.IssuedUnix));
                    
                switch (cmd.Action)
                {
                    case "Suspend":
                        _suspended = true;
                        _logger.LogWarning("Humidity measurements SUSPENDED due to rain detection");
                        break;
                    case "Resume":
                        _suspended = false;
                        _logger.LogInformation("Humidity measurements RESUMED - rain cleared");
                        break;
                    default:
                        _logger.LogWarning("Unknown command action: {Action}", cmd.Action);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Command stream for {StationId} cancelled", _stationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command stream failed for {StationId}", _stationId);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    { 
        _logger.LogInformation("Stopping Humidity Station {StationId}", _stationId);
        await base.StopAsync(cancellationToken); 
        try { _channel?.Dispose(); } catch { }
        _logger.LogInformation("Humidity Station {StationId} stopped", _stationId);
    }
}
