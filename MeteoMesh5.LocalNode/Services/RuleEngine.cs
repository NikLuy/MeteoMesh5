using System.Collections.Concurrent;
using MeteoMesh5.Grpc;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Models;
using Microsoft.EntityFrameworkCore;

namespace MeteoMesh5.LocalNode.Services;

public class RuleEngine
{
    private readonly StationRegistry _registry;
    private readonly ConcurrentQueue<ControlCommand> _pending = new();
    private readonly ILogger<RuleEngine> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public RuleEngine(
        StationRegistry registry, 
        ILogger<RuleEngine> logger, 
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _registry = registry;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public IEnumerable<ControlCommand> DequeueCommands()
    {
        while (_pending.TryDequeue(out var c)) yield return c;
    }

    public void Evaluate()
    {
        var states = _registry.All.ToList();
        var lidarStations = states.Where(s => s.Type.Equals("Lidar", StringComparison.OrdinalIgnoreCase)).ToList();
        var lidarRain = lidarStations.Any(s => s.Flag);
        var pressureHigh = states.Any(s => s.Type.Equals("Pressure", StringComparison.OrdinalIgnoreCase) && (s.LastValue ?? 0) > 950);

        _logger.LogDebug("Rule evaluation at {Time}: {LidarCount} lidar stations, Rain={RainDetected}, Pressure high={PressureHigh}", 
            _timeProvider.GetUtcNow().ToString("HH:mm:ss"), lidarStations.Count, lidarRain, pressureHigh);

        // Log lidar station details
        foreach (var lidar in lidarStations)
        {
            _logger.LogDebug("Lidar {StationId}: Flag={Flag}, LastValue={Value}, LastUpdate={LastUpdate}", 
                lidar.StationId, lidar.Flag, lidar.LastValue, lidar.LastTimestamp.ToString("HH:mm:ss"));
        }

        // Humidity suspend/resume
        var humidityStations = states.Where(s => s.Type.Equals("Humidity", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var h in humidityStations)
        {
            _logger.LogDebug("Humidity {StationId}: Suspended={Suspended}, Rain={Rain}", h.StationId, h.Suspended, lidarRain);
            
            if (lidarRain && !h.Suspended) 
            {
                _logger.LogInformation("Rain detected - suspending humidity station {StationId}", h.StationId);
                Enqueue(Command("Suspend", h.StationId));
            }
            if (!lidarRain && h.Suspended) 
            {
                _logger.LogInformation("Rain cleared - resuming humidity station {StationId}", h.StationId);
                Enqueue(Command("Resume", h.StationId));
            }
        }

        // Temperature interval
        foreach (var t in states.Where(s => s.Type.Equals("Temperature", StringComparison.OrdinalIgnoreCase)))
        {
            var desired = pressureHigh ? 7.5 : 15.0;
            if (Math.Abs(t.IntervalMinutes - desired) > 0.01) Enqueue(Command("SetInterval", t.StationId, desired));
        }
    }

    private ControlCommand Command(string action, string stationId, double val = 0) => new()
    {
        CommandId = Guid.NewGuid().ToString(),
        TargetStationId = stationId,
        Action = action,
        NumericValue = val,
        IssuedUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds()
    };

    private void Enqueue(ControlCommand cmd)
    {
        _pending.Enqueue(cmd);
        _logger.LogInformation("Cmd {Action} -> {Target} at {Time} (Queue depth: {QueueDepth})", 
            cmd.Action, cmd.TargetStationId, _timeProvider.GetUtcNow().ToString("HH:mm:ss"), _pending.Count);
        
        // Apply command immediately to registry so future rule evaluations see the updated state
        _registry.ApplyCommand(cmd);
        
        Persist(cmd);
    }

    private void Persist(ControlCommand cmd)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            
            var entity = new CommandLog
            {
                CommandId = cmd.CommandId,
                TargetStationId = cmd.TargetStationId ?? "",
                TargetType = cmd.TargetType ?? "",
                Action = cmd.Action,
                NumericValue = cmd.NumericValue,
                Issued = timeProvider.GetUtcNow().UtcDateTime
            };
            db.CommandLogs.Add(entity);
            
            var st = db.Stations.FirstOrDefault(s => s.StationId == cmd.TargetStationId);
            if (st != null)
            {
                if (cmd.Action == "Suspend") st.Suspended = true;
                else if (cmd.Action == "Resume") st.Suspended = false;
                else if (cmd.Action == "SetInterval") st.IntervalMinutes = cmd.NumericValue;
            }
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persist cmd failed");
        }
    }
}

public class RuleEngineBackgroundService : BackgroundService
{
    private readonly RuleEngine _engine;
    private readonly ILogger<RuleEngineBackgroundService> _logger;

    public RuleEngineBackgroundService(RuleEngine engine, ILogger<RuleEngineBackgroundService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _engine.Evaluate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rule eval fail");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
