using MeteoMesh5.Shared.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MeteoMesh5.Shared.Services;

// Simulation time provider with configurable speed and start time
public class SimulationTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _startTime;
    private readonly double _speedMultiplier;
    private readonly long _createdTimestamp;
    private readonly TimeZoneInfo _timeZone;

    public SimulationTimeProvider(IOptions<SimulationOptions> options, TimeZoneInfo? timeZone = null)
    {
        var opts = options.Value;
        _startTime = opts.StartTime;
        _speedMultiplier = opts.SpeedMultiplier <= 0 ? 1.0 : opts.SpeedMultiplier;
        _createdTimestamp = Stopwatch.GetTimestamp();
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    public override DateTimeOffset GetUtcNow()
    {
        var realElapsed = Stopwatch.GetElapsedTime(_createdTimestamp);
        var simulatedElapsed = TimeSpan.FromTicks((long)(realElapsed.Ticks * _speedMultiplier));
        return _startTime.Add(simulatedElapsed);
    }

    public override TimeZoneInfo LocalTimeZone => _timeZone;

    public override long GetTimestamp() => (long)((Stopwatch.GetTimestamp() - _createdTimestamp) * _speedMultiplier) + _createdTimestamp;

    // Additional control methods
    public void SetCurrentTime(DateTimeOffset time)
    {
        // For advanced scenarios - would need to adjust the base calculations
        // Implementation depends on whether you want to preserve speed multiplier
    }
}