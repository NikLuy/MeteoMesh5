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
    private readonly TimeAlignmentService _timeAlignment;
    private GrpcChannel? _channel; 
    private StationIngressService.StationIngressServiceClient? _client; 
    private TimeSpan _interval = TimeSpan.FromMinutes(15);
    private readonly string _stationId; 
    private readonly string _stationType; 
    private readonly string _nodeUrl;
    
    // Pressure system state tracking for realistic behavior
    private bool _isInHighPressureSystem = false;
    private DateTimeOffset _highPressureSystemStart;
    private double _highPressureBaseline = 960; // Base pressure during high pressure system
    private TimeSpan _highPressureSystemDuration = TimeSpan.FromHours(2); // Typical duration
    
    public StationWorker(ILogger<StationWorker> logger, IOptions<SimulationOptions> sim, IConfiguration cfg, 
        TimeProvider timeProvider, TimeAlignmentService timeAlignment)
    { 
        _logger = logger; 
        _sim = sim; 
        _cfg = cfg; 
        _timeProvider = timeProvider;
        _timeAlignment = timeAlignment;
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
            
            var currentTime = _timeProvider.GetUtcNow();
            var nextMeasurementTime = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, _interval);
            
            _logger.LogInformation("Beginning measurement loop - Interval: {IntervalMinutes} minutes, High pressure threshold: >950 hPa, Next measurement: {NextTime}", 
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
                        
                        // Generate realistic pressure with persistent high-pressure systems
                        var minutesSinceStart = _timeAlignment.GetElapsedMinutes(_sim.Value.StartTime);
                        
                        // Check if we should transition pressure systems
                        if (!_isInHighPressureSystem)
                        {
                            // Normal pressure - check for high pressure system start (5% chance per measurement)
                            if (Random.Shared.NextDouble() < 0.05)
                            {
                                _isInHighPressureSystem = true;
                                _highPressureSystemStart = currentTime;
                                _highPressureBaseline = 955 + Random.Shared.NextDouble() * 20; // 955-975 hPa
                                _highPressureSystemDuration = TimeSpan.FromMinutes(30 + Random.Shared.NextDouble() * 90); // 30-120 minutes
                                
                                _logger.LogWarning("HIGH PRESSURE SYSTEM starting at {Time} - Baseline: {Baseline:F1} hPa, Duration: {Duration:F0} minutes", 
                                    currentTime.ToString("HH:mm:ss"), _highPressureBaseline, _highPressureSystemDuration.TotalMinutes);
                            }
                        }
                        else
                        {
                            // In high pressure system - check if it should end
                            if (currentTime >= _highPressureSystemStart.Add(_highPressureSystemDuration))
                            {
                                _isInHighPressureSystem = false;
                                _logger.LogInformation("High pressure system ending at {Time} after {Duration:F0} minutes", 
                                    currentTime.ToString("HH:mm:ss"), (currentTime - _highPressureSystemStart).TotalMinutes);
                            }
                        }
                        
                        double pressure;
                        if (_isInHighPressureSystem)
                        {
                            // High pressure system: stable high pressure with small variations
                            var systemAge = (currentTime - _highPressureSystemStart).TotalMinutes;
                            var systemProgress = systemAge / _highPressureSystemDuration.TotalMinutes;
                            
                            // Pressure peaks in the middle of the system, tapers at edges
                            var systemIntensity = Math.Sin(systemProgress * Math.PI); // 0 -> 1 -> 0
                            var baselinePressure = _highPressureBaseline + systemIntensity * 10; // +0 to +10 hPa at peak
                            var microVariation = Random.Shared.NextDouble() * 4 - 2; // ±2 hPa small changes
                            
                            pressure = baselinePressure + microVariation;
                        }
                        else
                        {
                            // Normal pressure system: lower baseline with larger variations
                            var basePressure = 920 + 15 * Math.Sin(minutesSinceStart / 180.0 * Math.PI * 2); // 905-935 hPa base
                            var normalVariation = Random.Shared.NextDouble() * 12 - 6; // ±6 hPa normal variation
                            
                            pressure = basePressure + normalVariation;
                        }
                        
                        var isHighPressure = pressure > 950;
                        if (isHighPressure) highPressureCount++;
                        
                        var req = new SubmitMeasurementRequest
                        { 
                            Measurement = new SensorMeasurement 
                            {
                                StationId = _stationId,
                                StationType = _stationType,
                                TimestampUnix = nextMeasurementTime.ToUnixTimeSeconds(), // Use aligned time, not current time
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
                                _logger.LogWarning("Measurement #{Count} HIGH PRESSURE: P={Pressure:F1} hPa at aligned time {AlignedTime} (gRPC: {ElapsedMs}ms) - May trigger temp frequency change!", 
                                    measurementCount, pressure, nextMeasurementTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
                            else
                            {
                                _logger.LogInformation("Measurement #{Count} submitted successfully: P={Pressure:F1} hPa at aligned time {AlignedTime} (gRPC: {ElapsedMs}ms)", 
                                    measurementCount, pressure, nextMeasurementTime.ToString("HH:mm:ss"), sw.ElapsedMilliseconds);
                            }
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
