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
        _stationId = _cfg["Station:Id"] ?? "LIDAR_SIM"; 
        _stationType = "Lidar"; 
        _nodeUrl = _cfg["Station:NodeUrl"] ?? "https://localhost:7101"; 
        
        _logger.LogInformation("Lidar Station {StationId} initialized - Target: {NodeUrl}", _stationId, _nodeUrl);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Lidar Station {StationId} with simulation config: Start={Start}, Speed={Speed}x", 
            _stationId, _sim.Value.StartTime, _sim.Value.SpeedMultiplier);
            
        var measurementCount = 0;
        var rainCount = 0;
        var totalRainfall = 0.0;
        
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
            
            var lastMeasurementTime = _timeProvider.GetUtcNow();
            
            _logger.LogInformation("Beginning measurement loop - Initial interval: {IntervalMinutes} minutes, Rain threshold: >0 mm/h, Start time: {StartTime}", 
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
                        
                        // Generate rain detection (analog value >0 = rain for this project)
                        var minutesSinceStart = (currentTime - _sim.Value.StartTime).TotalMinutes;
                        var rainChance = 0.2 + 0.3 * Math.Sin(minutesSinceStart / 240.0 * Math.PI * 2); // varying rain probability
                        var isRaining = Random.Shared.NextDouble() < rainChance;
                        var rainIntensity = isRaining ? Random.Shared.NextDouble() * 10 + 0.5 : 0; // 0.5-10.5 mm/h when raining
                        var visibility = isRaining ? 2 + Random.Shared.NextDouble() * 3 : 10 + Random.Shared.NextDouble() * 5; // reduced visibility in rain
                        
                        if (isRaining) 
                        {
                            rainCount++;
                            totalRainfall += rainIntensity;
                        }
                        
                        var req = new SubmitMeasurementRequest
                        { 
                            Measurement = new SensorMeasurement 
                            {
                                StationId = _stationId,
                                StationType = _stationType,
                                TimestampUnix = currentTime.ToUnixTimeSeconds(),
                                Value = rainIntensity, // analog rain value (>0 = rain)
                                Aux1 = visibility, // visibility in km
                                Flag = isRaining, // boolean rain flag
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
                            if (isRaining)
                            {
                                _logger.LogWarning("Measurement #{Count} RAIN DETECTED: Intensity={Rain:F2} mm/h, Visibility={Vis:F1}km at {SimTime} (gRPC: {ElapsedMs}ms) - Will trigger humidity suspension!", 
                                    measurementCount, rainIntensity, visibility, currentTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
                            else
                            {
                                _logger.LogInformation("Measurement #{Count} submitted successfully: No rain, Visibility={Vis:F1}km at {SimTime} (gRPC: {ElapsedMs}ms)", 
                                    measurementCount, visibility, currentTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
                        }
                        
                        lastMeasurementTime = currentTime;
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
            _logger.LogInformation("Lidar Station {StationId} stopped by cancellation", _stationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lidar Station {StationId} failed", _stationId);
        }
        finally
        {
            _logger.LogInformation("Lidar Station {StationId} final stats: {TotalMeasurements} total, {RainCount} rain detections, {TotalRainfall:F2} mm total rainfall", 
                _stationId, measurementCount, rainCount, totalRainfall);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    { 
        _logger.LogInformation("Stopping Lidar Station {StationId}", _stationId);
        await base.StopAsync(cancellationToken); 
        try { _channel?.Dispose(); } catch { }
        _logger.LogInformation("Lidar Station {StationId} stopped", _stationId);
    }
}
