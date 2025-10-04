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
        _stationId = _cfg["Station:Id"] ?? "PRES_SIM"; 
        _stationType = "Pressure"; 
        _nodeUrl = _cfg["Station:NodeUrl"] ?? "https://localhost:7101"; 
        
        _logger.LogInformation("Pressure Station {StationId} initialized - Target: {NodeUrl}", _stationId, _nodeUrl);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Pressure Station {StationId} with simulation config: Start={Start}, Speed={Speed}x", 
            _stationId, _sim.Value.StartTime, _sim.Value.SpeedMultiplier);
            
        var measurementCount = 0;
        var highPressureCount = 0;
        
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
            
            _logger.LogInformation("Beginning measurement loop - Initial interval: {IntervalMinutes} minutes, High pressure threshold: >950 hPa, Start time: {StartTime}", 
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
                        
                        // Generate pressure value 980-1040 hPa with occasional spikes >950
                        var minutesSinceStart = (currentTime - _sim.Value.StartTime).TotalMinutes;
                        var basePressure = 1010 + 20 * Math.Sin(minutesSinceStart / 180.0 * Math.PI * 2);
                        var spike = Random.Shared.NextDouble() < 0.3 ? Random.Shared.NextDouble() * 15 : 0; // 30% chance of spike
                        var pressure = basePressure + spike + Random.Shared.NextDouble() * 2;
                        
                        var isHighPressure = pressure > 950;
                        if (isHighPressure) highPressureCount++;
                        
                        var req = new SubmitMeasurementRequest
                        { 
                            Measurement = new SensorMeasurement 
                            {
                                StationId = _stationId,
                                StationType = _stationType,
                                TimestampUnix = currentTime.ToUnixTimeSeconds(),
                                Value = pressure,
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
                            if (isHighPressure)
                            {
                                _logger.LogWarning("Measurement #{Count} HIGH PRESSURE: P={Pressure:F1} hPa at {SimTime} (gRPC: {ElapsedMs}ms) - May trigger temp frequency change!", 
                                    measurementCount, pressure, currentTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
                            else
                            {
                                _logger.LogInformation("Measurement #{Count} submitted successfully: P={Pressure:F1} hPa at {SimTime} (gRPC: {ElapsedMs}ms)", 
                                    measurementCount, pressure, currentTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
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
            _logger.LogInformation("Pressure Station {StationId} stopped by cancellation", _stationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pressure Station {StationId} failed", _stationId);
        }
        finally
        {
            _logger.LogInformation("Pressure Station {StationId} final stats: {TotalMeasurements} total, {HighPressureCount} high pressure readings (>950 hPa)", 
                _stationId, measurementCount, highPressureCount);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    { 
        _logger.LogInformation("Stopping Pressure Station {StationId}", _stationId);
        await base.StopAsync(cancellationToken); 
        try { _channel?.Dispose(); } catch { }
        _logger.LogInformation("Pressure Station {StationId} stopped", _stationId);
    }
}
