using System.Diagnostics;

namespace MeteoMesh5.Shared.Services;

// System time provider - uses .NET 9's built-in TimeProvider.System
public class SystemTimeProvider : TimeProvider
{
    public static readonly SystemTimeProvider Instance = new();

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    public override long GetTimestamp() => Stopwatch.GetTimestamp();
}