using System;
using FluentAssertions;
using MeteoMesh5.Shared.Services;
using Xunit;

namespace MeteoMesh5.Shared.Tests;

public class TimeAlignmentServiceTests
{
    private readonly TimeAlignmentService _timeAlignment;

    public TimeAlignmentServiceTests()
    {
        _timeAlignment = new TimeAlignmentService(SystemTimeProvider.Instance);
    }

    [Theory]
    [InlineData(15, "2024-01-01T00:07:30Z", "2024-01-01T00:15:00Z")]
    [InlineData(15, "2024-01-01T00:00:00Z", "2024-01-01T00:15:00Z")]
    [InlineData(15, "2024-01-01T00:15:00Z", "2024-01-01T00:30:00Z")]
    [InlineData(15, "2024-01-01T00:45:30Z", "2024-01-01T01:00:00Z")]
    [InlineData(30, "2024-01-01T00:15:00Z", "2024-01-01T00:30:00Z")]
    [InlineData(30, "2024-01-01T00:30:00Z", "2024-01-01T01:00:00Z")]
    [InlineData(60, "2024-01-01T00:30:00Z", "2024-01-01T01:00:00Z")]
    public void GetNextAlignedMeasurementTime_ShouldCalculateCorrectNextTime(int intervalMinutes, string currentTimeStr, string expectedTimeStr)
    {
        // Arrange
        var currentTime = DateTimeOffset.Parse(currentTimeStr);
        var expectedTime = DateTimeOffset.Parse(expectedTimeStr);

        // Act
        var result = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, intervalMinutes);

        // Assert
        result.Should().Be(expectedTime);
    }

    [Theory]
    [InlineData(7.5, "2024-01-01T00:05:00Z", "2024-01-01T00:07:30Z")]    // Next 7.5min from 00:05 -> 00:07:30
    [InlineData(7.5, "2024-01-01T00:07:30Z", "2024-01-01T00:15:00Z")]    // Next 7.5min from 00:07:30 -> 00:15:00
    [InlineData(7.5, "2024-01-01T00:12:00Z", "2024-01-01T00:15:00Z")]    // Next 7.5min from 00:12 -> 00:15:00
    [InlineData(7.5, "2024-01-01T00:15:00Z", "2024-01-01T00:22:30Z")]    // Next 7.5min from 00:15 -> 00:22:30
    [InlineData(7.5, "2024-01-01T00:52:30Z", "2024-01-01T01:00:00Z")]    // Next 7.5min from 00:52:30 -> 01:00:00
    [InlineData(2.5, "2024-01-01T00:01:00Z", "2024-01-01T00:02:30Z")]    // Next 2.5min from 00:01 -> 00:02:30
    [InlineData(3.33333, "2024-01-01T00:02:00Z", "2024-01-01T00:03:20Z")] // Next 3:20min from 00:02 -> 00:03:20
    public void GetNextAlignedMeasurementTime_WithFractionalMinutes_ShouldCalculateCorrectNextTime(double intervalMinutes, string currentTimeStr, string expectedTimeStr)
    {
        // Arrange
        var currentTime = DateTimeOffset.Parse(currentTimeStr);
        var expectedTime = DateTimeOffset.Parse(expectedTimeStr);

        // Act
        var result = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, intervalMinutes);

        // Assert
        result.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1)); // Allow 1 second tolerance for rounding
    }

    [Theory]
    [InlineData(15, "2024-01-01T00:00:00Z", true)]
    [InlineData(15, "2024-01-01T00:15:00Z", true)]
    [InlineData(15, "2024-01-01T00:30:00Z", true)]
    [InlineData(15, "2024-01-01T00:45:00Z", true)]
    [InlineData(15, "2024-01-01T00:07:30Z", false)]
    [InlineData(15, "2024-01-01T00:15:01Z", false)]
    [InlineData(30, "2024-01-01T00:00:00Z", true)]
    [InlineData(30, "2024-01-01T00:30:00Z", true)]
    [InlineData(30, "2024-01-01T00:15:00Z", false)]
    public void IsAlignedMeasurementTime_ShouldDetectAlignedTimes(int intervalMinutes, string timeStr, bool expected)
    {
        // Arrange
        var time = DateTimeOffset.Parse(timeStr);

        // Act
        var result = _timeAlignment.IsAlignedMeasurementTime(time, intervalMinutes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(7.5, "2024-01-01T00:00:00Z", true)]    // 00:00:00 is aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:07:30Z", true)]    // 00:07:30 is aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:15:00Z", true)]    // 00:15:00 is aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:22:30Z", true)]    // 00:22:30 is aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:30:00Z", true)]    // 00:30:00 is aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:05:00Z", false)]   // 00:05:00 is not aligned to 7.5min
    [InlineData(7.5, "2024-01-01T00:12:00Z", false)]   // 00:12:00 is not aligned to 7.5min
    [InlineData(2.5, "2024-01-01T00:02:30Z", true)]    // 00:02:30 is aligned to 2.5min
    [InlineData(2.5, "2024-01-01T00:05:00Z", true)]    // 00:05:00 is aligned to 2.5min
    [InlineData(2.5, "2024-01-01T00:03:00Z", false)]   // 00:03:00 is not aligned to 2.5min
    public void IsAlignedMeasurementTime_WithFractionalMinutes_ShouldDetectAlignedTimes(double intervalMinutes, string timeStr, bool expected)
    {
        // Arrange
        var time = DateTimeOffset.Parse(timeStr);

        // Act
        var result = _timeAlignment.IsAlignedMeasurementTime(time, intervalMinutes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(15, "2024-01-01T00:07:30Z", "2024-01-01T00:00:00Z")]
    [InlineData(15, "2024-01-01T00:15:00Z", "2024-01-01T00:15:00Z")]
    [InlineData(15, "2024-01-01T00:22:30Z", "2024-01-01T00:15:00Z")]
    [InlineData(30, "2024-01-01T00:45:00Z", "2024-01-01T00:30:00Z")]
    [InlineData(60, "2024-01-01T00:45:00Z", "2024-01-01T00:00:00Z")]
    public void GetLastAlignedMeasurementTime_ShouldCalculateCorrectLastTime(int intervalMinutes, string currentTimeStr, string expectedTimeStr)
    {
        // Arrange
        var currentTime = DateTimeOffset.Parse(currentTimeStr);
        var expectedTime = DateTimeOffset.Parse(expectedTimeStr);

        // Act
        var result = _timeAlignment.GetLastAlignedMeasurementTime(currentTime, intervalMinutes);

        // Assert
        result.Should().Be(expectedTime);
    }

    [Theory]
    [InlineData(7.5, "2024-01-01T00:10:00Z", "2024-01-01T00:07:30Z")]    // Last 7.5min from 00:10 -> 00:07:30
    [InlineData(7.5, "2024-01-01T00:07:30Z", "2024-01-01T00:07:30Z")]    // Last 7.5min from 00:07:30 -> 00:07:30
    [InlineData(7.5, "2024-01-01T00:20:00Z", "2024-01-01T00:15:00Z")]    // Last 7.5min from 00:20 -> 00:15:00
    [InlineData(2.5, "2024-01-01T00:04:00Z", "2024-01-01T00:02:30Z")]    // Last 2.5min from 00:04 -> 00:02:30
    public void GetLastAlignedMeasurementTime_WithFractionalMinutes_ShouldCalculateCorrectLastTime(double intervalMinutes, string currentTimeStr, string expectedTimeStr)
    {
        // Arrange
        var currentTime = DateTimeOffset.Parse(currentTimeStr);
        var expectedTime = DateTimeOffset.Parse(expectedTimeStr);

        // Act
        var result = _timeAlignment.GetLastAlignedMeasurementTime(currentTime, intervalMinutes);

        // Assert
        result.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1)); // Allow 1 second tolerance for rounding
    }

    [Theory]
    [InlineData(7)]   // Not a divisor of 60 - but now allowed with fractional support
    [InlineData(13)]  // Not a divisor of 60 - but now allowed with fractional support  
    [InlineData(-15)] // Negative
    [InlineData(0)]   // Zero
    public void GetNextAlignedMeasurementTime_ShouldThrowForInvalidInterval(double intervalMinutes)
    {
        // Arrange
        var currentTime = DateTimeOffset.Parse("2024-01-01T00:07:30Z");

        // Act & Assert
        if (intervalMinutes <= 0)
        {
            _timeAlignment.Invoking(x => x.GetNextAlignedMeasurementTime(currentTime, intervalMinutes))
                .Should().Throw<ArgumentException>()
                .WithMessage($"Interval must be positive. Got: {intervalMinutes}");
        }
        else
        {
            // These should not throw anymore since we support fractional intervals
            _timeAlignment.Invoking(x => x.GetNextAlignedMeasurementTime(currentTime, intervalMinutes))
                .Should().NotThrow();
        }
    }

    [Fact]
    public void GetElapsedMinutes_ShouldCalculateCorrectElapsedTime()
    {
        // This test uses a mock time provider to ensure consistent results
        var startTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var currentTime = DateTimeOffset.Parse("2024-01-01T01:30:00Z");
        
        var mockTimeProvider = new MockTimeProvider(currentTime);
        var timeAlignment = new TimeAlignmentService(mockTimeProvider);

        // Act
        var result = timeAlignment.GetElapsedMinutes(startTime);

        // Assert
        result.Should().Be(90.0); // 1 hour 30 minutes = 90 minutes
    }

    [Fact]
    public void TemperatureStation_SevenAndHalfMinuteInterval_ShouldAlignCorrectly()
    {
        // Arrange - Simulate temperature station with 7.5 minute interval (15/2)
        var intervalMinutes = 7.5; // 7 minutes 30 seconds
        
        // Test different times to ensure proper alignment
        var testCases = new[]
        {
            ("2024-01-01T00:05:00Z", "2024-01-01T00:07:30Z"), // From 00:05 -> next should be 00:07:30
            ("2024-01-01T00:07:30Z", "2024-01-01T00:15:00Z"), // From 00:07:30 -> next should be 00:15:00
            ("2024-01-01T00:12:00Z", "2024-01-01T00:15:00Z"), // From 00:12 -> next should be 00:15:00
            ("2024-01-01T00:15:00Z", "2024-01-01T00:22:30Z"), // From 00:15 -> next should be 00:22:30
            ("2024-01-01T00:22:30Z", "2024-01-01T00:30:00Z"), // From 00:22:30 -> next should be 00:30:00
            ("2024-01-01T00:55:00Z", "2024-01-01T01:00:00Z"), // Near end of hour
        };

        foreach (var (currentTimeStr, expectedTimeStr) in testCases)
        {
            // Act
            var currentTime = DateTimeOffset.Parse(currentTimeStr);
            var expectedTime = DateTimeOffset.Parse(expectedTimeStr);
            var result = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, intervalMinutes);

            // Assert
            result.Should().Be(expectedTime, 
                $"because 7.5-minute intervals should align to 00:00, 00:07:30, 00:15:00, 00:22:30, etc. Current: {currentTimeStr}");
        }
    }

    [Fact] 
    public void TimeSpan_SevenAndHalfMinuteInterval_ShouldWorkCorrectly()
    {
        // Arrange - This test specifically verifies the TimeSpan overload works with fractional minutes
        var interval = TimeSpan.FromMinutes(7.5); // This was the source of the bug!
        
        // Test the problematic scenario from the logs
        var testCases = new[]
        {
            ("2024-01-01T02:00:00Z", "2024-01-01T02:07:30Z"), // The failing case: should go 02:00:00 -> 02:07:30, not 02:21:00
            ("2024-01-01T02:07:30Z", "2024-01-01T02:15:00Z"), // Should continue correctly
            ("2024-01-01T02:15:00Z", "2024-01-01T02:22:30Z"), // Should continue correctly
        };

        foreach (var (currentTimeStr, expectedTimeStr) in testCases)
        {
            // Act - Using TimeSpan overload that was causing the bug
            var currentTime = DateTimeOffset.Parse(currentTimeStr);
            var expectedTime = DateTimeOffset.Parse(expectedTimeStr);
            var result = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, interval);

            // Assert
            result.Should().Be(expectedTime, 
                $"because TimeSpan.FromMinutes(7.5) should work correctly. Current: {currentTimeStr}");
        }
    }

    [Theory]
    [InlineData("00:15:00")]
    [InlineData("00:30:00")]
    [InlineData("01:00:00")]
    public void GetNextAlignedMeasurementTime_WithTimeSpan_ShouldWork(string intervalStr)
    {
        // Arrange
        var interval = TimeSpan.Parse(intervalStr);
        var currentTime = DateTimeOffset.Parse("2024-01-01T00:07:30Z");

        // Act
        var result = _timeAlignment.GetNextAlignedMeasurementTime(currentTime, interval);

        // Assert
        result.Second.Should().Be(0, "because aligned times should have 0 seconds"); // Should align to exact seconds
        
        // Check that the result is properly aligned to the interval
        var intervalMinutes = (int)interval.TotalMinutes;
        result.Minute.Should().Match(m => m % intervalMinutes == 0, 
            $"because the minute should be aligned to {intervalMinutes}-minute intervals");
    }

    // Mock TimeProvider for testing
    private class MockTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime;

        public MockTimeProvider(DateTimeOffset fixedTime)
        {
            _fixedTime = fixedTime;
        }

        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }
}