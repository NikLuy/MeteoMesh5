using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Grpc.Net.Client;
using MeteoMesh5.Grpc;
using MeteoMesh5.Shared.Models;
using MeteoMesh5.Shared.Services;
using MeteoMesh5.Shared.Extensions;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
    private GrpcChannel? _channel; 
    private StationIngressService.StationIngressServiceClient? _client; 
    private TimeSpan _interval = TimeSpan.FromMinutes(15);
    private readonly string _stationId; 
    private readonly string _stationType; 
    private readonly string _nodeUrl;
    
    public StationWorker(ILogger<StationWorker> logger, IOptions<SimulationOptions> sim, IConfiguration cfg, TimeProvider timeProvider)
    { 
        _logger = logger; 
        _sim = sim; 
        _cfg = cfg; 
        _timeProvider = timeProvider;
        _stationId = _cfg["Station:Id"] ?? "TEMP_SIM"; 
        _stationType = "Temperature"; 
        _nodeUrl = _cfg["Station:NodeUrl"] ?? "https://localhost:7101"; 
        
        _logger.LogInformation("Temperature Station {StationId} initialized - Target: {NodeUrl}", _stationId, _nodeUrl);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Temperature Station {StationId} with simulation config: Start={Start}, Speed={Speed}x", 
            _stationId, _sim.Value.StartTime, _sim.Value.SpeedMultiplier);
            
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
            
            // Start command stream listener for interval changes
            _ = Task.Run(async () => await ListenForCommands(stoppingToken), stoppingToken);
            
            var measurementCount = 0;
            var lastMeasurementTime = _timeProvider.GetUtcNow();
            
            _logger.LogInformation("Beginning measurement loop - Initial interval: {IntervalMinutes} minutes, Start time: {StartTime}", 
                _interval.TotalMinutes, lastMeasurementTime.ToString("HH:mm:ss"));
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var currentTime = _timeProvider.GetUtcNow();
                    
                    // Check if it's time for the next measurement
                    if (currentTime >= lastMeasurementTime.Add(_interval))
                    {
                        measurementCount++;
                        
                        // Generate temperature value simple sinus + noise
                        var minutesSinceStart = (currentTime - _sim.Value.StartTime).TotalMinutes;
                        var temp = 15 + 10 * Math.Sin(minutesSinceStart / 60.0 * Math.PI * 2) + Random.Shared.NextDouble() * 0.5;
                        
                        var req = new SubmitMeasurementRequest
                        { 
                            Measurement = new SensorMeasurement 
                            {
                                StationId = _stationId,
                                StationType = _stationType,
                                TimestampUnix = currentTime.ToUnixTimeSeconds(),
                                Value = temp,
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
                            _logger.LogInformation("Measurement #{Count} submitted successfully: T={Temp:F2}°C at {SimTime} (gRPC: {ElapsedMs}ms, interval: {IntervalMin}min)", 
                                measurementCount, temp, currentTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds, _interval.TotalMinutes);
                        }
                        
                        lastMeasurementTime = currentTime;
                    }
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "Measurement #{Count} failed - Sim time: {SimTime}", measurementCount, _timeProvider.GetUtcNow());
                }
                
                // Wait a short period before checking again (much shorter than measurement interval)
                var checkInterval = TimeSpan.FromMilliseconds(100); // Check every 100ms
                try { await Task.Delay(checkInterval, stoppingToken); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Temperature Station {StationId} stopped by cancellation", _stationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Temperature Station {StationId} failed", _stationId);
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
                _logger.LogInformation("Command received: {CommandId} - {Action} value={Value} issued={IssuedTime}", 
                    cmd.CommandId, cmd.Action, cmd.NumericValue, DateTimeOffset.FromUnixTimeSeconds(cmd.IssuedUnix));
                    
                switch (cmd.Action)
                {
                    case "SetInterval": 
                        var oldInterval = _interval.TotalMinutes;
                        _interval = TimeSpan.FromMinutes(cmd.NumericValue);
                        _logger.LogWarning("Measurement interval changed from {OldMin} to {NewMin} minutes due to pressure conditions", 
                            oldInterval, cmd.NumericValue);
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
        _logger.LogInformation("Stopping Temperature Station {StationId}", _stationId);
        await base.StopAsync(cancellationToken); 
        try { _channel?.Dispose(); } catch { }
        _logger.LogInformation("Temperature Station {StationId} stopped", _stationId);
    }
}
