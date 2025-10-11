using System;

namespace MeteoMesh5.Shared.Services;

/// <summary>
/// Service for aligning meteorological measurements to standard time intervals.
/// Ensures measurements happen at consistent times like 00:00, 00:15, 00:30, 00:45 for better aggregation and comparison.
/// </summary>
public class TimeAlignmentService
{
    private readonly TimeProvider _timeProvider;

    public TimeAlignmentService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Calculate the next aligned measurement time based on the specified interval.
    /// For 15-minute intervals: aligns to 00:00, 00:15, 00:30, 00:45
    /// For 7.5-minute intervals: aligns to 00:00, 00:07:30, 00:15, 00:22:30, 00:30, 00:37:30, 00:45, 00:52:30
    /// For 30-minute intervals: aligns to 00:00, 00:30
    /// For 60-minute intervals: aligns to 00:00
    /// </summary>
    /// <param name="currentTime">The current time reference</param>
    /// <param name="intervalMinutes">Measurement interval in minutes (supports fractional minutes)</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(DateTimeOffset currentTime, double intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentException($"Interval must be positive. Got: {intervalMinutes}");
        }

        // Convert interval to total seconds for more precise calculations
        var intervalSeconds = intervalMinutes * 60;
        
        // Get total seconds since start of hour
        var currentSecondsInHour = currentTime.Minute * 60 + currentTime.Second;
        
        // Calculate the next aligned time in seconds since start of hour
        // If we're exactly on an aligned time, move to the next one
        var nextAlignedSeconds = Math.Ceiling((currentSecondsInHour + 0.1) / intervalSeconds) * intervalSeconds;
        
        // Handle overflow to next hour
        if (nextAlignedSeconds >= 3600) // 60 minutes * 60 seconds
        {
            // Move to next hour
            var nextHour = currentTime.AddHours(1);
            return new DateTimeOffset(
                nextHour.Year, nextHour.Month, nextHour.Day,
                nextHour.Hour, 0, 0, currentTime.Offset);
        }
        else
        {
            // Same hour, calculate aligned minute and second
            var alignedMinutes = (int)(nextAlignedSeconds / 60);
            var alignedSeconds = (int)(nextAlignedSeconds % 60);
            
            return new DateTimeOffset(
                currentTime.Year, currentTime.Month, currentTime.Day,
                currentTime.Hour, alignedMinutes, alignedSeconds, currentTime.Offset);
        }
    }

    /// <summary>
    /// Calculate the next aligned measurement time based on the specified interval (integer minutes).
    /// </summary>
    /// <param name="currentTime">The current time reference</param>
    /// <param name="intervalMinutes">Measurement interval in minutes (must be a divisor of 60)</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(DateTimeOffset currentTime, int intervalMinutes)
    {
        return GetNextAlignedMeasurementTime(currentTime, (double)intervalMinutes);
    }

    /// <summary>
    /// Calculate the next aligned measurement time using a TimeSpan interval
    /// </summary>
    /// <param name="currentTime">The current time reference</param>
    /// <param name="interval">Measurement interval as TimeSpan</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(DateTimeOffset currentTime, TimeSpan interval)
    {
        return GetNextAlignedMeasurementTime(currentTime, interval.TotalMinutes);
    }

    /// <summary>
    /// Get the next aligned measurement time based on current provider time
    /// </summary>
    /// <param name="intervalMinutes">Measurement interval in minutes (supports fractional minutes)</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(double intervalMinutes)
    {
        return GetNextAlignedMeasurementTime(_timeProvider.GetUtcNow(), intervalMinutes);
    }

    /// <summary>
    /// Get the next aligned measurement time based on current provider time (integer minutes)
    /// </summary>
    /// <param name="intervalMinutes">Measurement interval in minutes</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(int intervalMinutes)
    {
        return GetNextAlignedMeasurementTime(_timeProvider.GetUtcNow(), (double)intervalMinutes);
    }

    /// <summary>
    /// Get the next aligned measurement time based on current provider time
    /// </summary>
    /// <param name="interval">Measurement interval as TimeSpan</param>
    /// <returns>The next aligned measurement time</returns>
    public DateTimeOffset GetNextAlignedMeasurementTime(TimeSpan interval)
    {
        return GetNextAlignedMeasurementTime(_timeProvider.GetUtcNow(), interval.TotalMinutes);
    }

    /// <summary>
    /// Check if the specified time is aligned to the measurement interval
    /// </summary>
    /// <param name="time">Time to check</param>
    /// <param name="intervalMinutes">Measurement interval in minutes (supports fractional minutes)</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the time is aligned within tolerance</returns>
    public bool IsAlignedMeasurementTime(DateTimeOffset time, double intervalMinutes, int toleranceSeconds = 1)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentException($"Interval must be positive. Got: {intervalMinutes}");
        }

        // Convert interval to seconds
        var intervalSeconds = intervalMinutes * 60;
        
        // Get total seconds since start of hour
        var currentSecondsInHour = time.Minute * 60 + time.Second;
        
        // Check if current time is aligned to interval within tolerance
        var remainder = currentSecondsInHour % intervalSeconds;
        
        // The time is aligned if it's exactly on an alignment point OR within tolerance BEFORE the next alignment point
        // For example, with 1-second tolerance:
        // - 00:15:00 -> remainder = 0, which is <= 1 -> TRUE
        // - 00:15:01 -> remainder = 1, which is > 1 -> FALSE  
        // - 00:14:59 -> remainder = 899 (for 15min), which is >= 899 -> TRUE
        return remainder == 0 || remainder >= (intervalSeconds - toleranceSeconds);
    }

    /// <summary>
    /// Check if the specified time is aligned to the measurement interval (integer minutes)
    /// </summary>
    /// <param name="time">Time to check</param>
    /// <param name="intervalMinutes">Measurement interval in minutes</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the time is aligned within tolerance</returns>
    public bool IsAlignedMeasurementTime(DateTimeOffset time, int intervalMinutes, int toleranceSeconds = 1)
    {
        return IsAlignedMeasurementTime(time, (double)intervalMinutes, toleranceSeconds);
    }

    /// <summary>
    /// Check if the specified time is aligned to the measurement interval
    /// </summary>
    /// <param name="time">Time to check</param>
    /// <param name="interval">Measurement interval as TimeSpan</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the time is aligned within tolerance</returns>
    public bool IsAlignedMeasurementTime(DateTimeOffset time, TimeSpan interval, int toleranceSeconds = 1)
    {
        return IsAlignedMeasurementTime(time, interval.TotalMinutes, toleranceSeconds);
    }

    /// <summary>
    /// Check if the current provider time is aligned to the measurement interval
    /// </summary>
    /// <param name="intervalMinutes">Measurement interval in minutes (supports fractional minutes)</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the current time is aligned within tolerance</returns>
    public bool IsCurrentTimeAligned(double intervalMinutes, int toleranceSeconds = 1)
    {
        return IsAlignedMeasurementTime(_timeProvider.GetUtcNow(), intervalMinutes, toleranceSeconds);
    }

    /// <summary>
    /// Check if the current provider time is aligned to the measurement interval (integer minutes)
    /// </summary>
    /// <param name="intervalMinutes">Measurement interval in minutes</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the current time is aligned within tolerance</returns>
    public bool IsCurrentTimeAligned(int intervalMinutes, int toleranceSeconds = 1)
    {
        return IsAlignedMeasurementTime(_timeProvider.GetUtcNow(), (double)intervalMinutes, toleranceSeconds);
    }

    /// <summary>
    /// Check if the current provider time is aligned to the measurement interval
    /// </summary>
    /// <param name="interval">Measurement interval as TimeSpan</param>
    /// <param name="toleranceSeconds">Tolerance in seconds (default: 1 second)</param>
    /// <returns>True if the current time is aligned within tolerance</returns>
    public bool IsCurrentTimeAligned(TimeSpan interval, int toleranceSeconds = 1)
    {
        return IsAlignedMeasurementTime(_timeProvider.GetUtcNow(), interval.TotalMinutes, toleranceSeconds);
    }

    /// <summary>
    /// Get the most recent aligned measurement time before the specified time
    /// </summary>
    /// <param name="currentTime">The reference time</param>
    /// <param name="intervalMinutes">Measurement interval in minutes (supports fractional minutes)</param>
    /// <returns>The most recent aligned measurement time</returns>
    public DateTimeOffset GetLastAlignedMeasurementTime(DateTimeOffset currentTime, double intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentException($"Interval must be positive. Got: {intervalMinutes}");
        }

        // Convert interval to total seconds for more precise calculations
        var intervalSeconds = intervalMinutes * 60;
        
        // Get total seconds since start of hour
        var currentSecondsInHour = currentTime.Minute * 60 + currentTime.Second;
        
        // Calculate the most recent aligned time in seconds since start of hour
        var lastAlignedSeconds = Math.Floor(currentSecondsInHour / intervalSeconds) * intervalSeconds;
        
        // Calculate aligned minute and second
        var alignedMinutes = (int)(lastAlignedSeconds / 60);
        var alignedSeconds = (int)(lastAlignedSeconds % 60);
        
        return new DateTimeOffset(
            currentTime.Year, currentTime.Month, currentTime.Day,
            currentTime.Hour, alignedMinutes, alignedSeconds, currentTime.Offset);
    }

    /// <summary>
    /// Get the most recent aligned measurement time before the specified time (integer minutes)
    /// </summary>
    /// <param name="currentTime">The reference time</param>
    /// <param name="intervalMinutes">Measurement interval in minutes</param>
    /// <returns>The most recent aligned measurement time</returns>
    public DateTimeOffset GetLastAlignedMeasurementTime(DateTimeOffset currentTime, int intervalMinutes)
    {
        return GetLastAlignedMeasurementTime(currentTime, (double)intervalMinutes);
    }

    /// <summary>
    /// Get elapsed minutes since simulation start using the TimeProvider
    /// </summary>
    /// <param name="simulationStartTime">Start time of the simulation</param>
    /// <returns>Minutes elapsed since simulation start</returns>
    public double GetElapsedMinutes(DateTimeOffset simulationStartTime)
    {
        var currentTime = _timeProvider.GetUtcNow();
        return (currentTime - simulationStartTime).TotalMinutes;
    }
}