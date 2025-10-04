namespace MeteoMesh5.Shared.Abstractions;

public interface IClock
{
    // core
    DateTimeOffset UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly Today { get; }

    // helpful extras
    long UnixTimeMilliseconds { get; }
    long UnixTimeSeconds { get; }

    // task-friendly time (so tests don’t actually sleep)
    Task Delay(TimeSpan delay, CancellationToken ct = default);

    // high-resolution/monotonic ticks (not wall clock)
    long GetTimestamp();               // like Stopwatch.GetTimestamp()
    TimeSpan GetElapsed(long startTs); // elapsed since a timestamp
}
