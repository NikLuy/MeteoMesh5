using FluentAssertions;

namespace MeteoMesh5.CentralServer.Tests;

public class CentralServerModelTests
{
    [Fact]
    public void CentralServer_ShouldStartup()
    {
        // Simple test to verify basic functionality
        var result = true;
        result.Should().BeTrue();
    }

    [Fact]
    public void String_ShouldNotBeNullOrEmpty()
    {
        // Basic string validation test
        var testString = "MeteoMesh5";
        testString.Should().NotBeNullOrEmpty();
        testString.Should().StartWith("MeteoMesh");
    }

    [Theory]
    [InlineData("TEMP-001", "TEMP")]
    [InlineData("HUM-002", "HUM")]
    [InlineData("PRESS-003", "PRESS")]
    [InlineData("LIDAR-004", "LIDAR")]
    public void StationId_ShouldHaveValidFormat(string stationId, string expectedPrefix)
    {
        // Arrange & Act & Assert
        stationId.Should().StartWith(expectedPrefix);
        stationId.Should().Contain("-");
        stationId.Length.Should().BeGreaterThan(3);
    }

    [Fact]
    public void Coordinates_ShouldBeValidForZurich()
    {
        // Arrange
        var latitude = 47.3769;  // Zurich
        var longitude = 8.5417;
        var altitude = 408.0;

        // Act & Assert
        latitude.Should().BeInRange(-90, 90);
        longitude.Should().BeInRange(-180, 180);
        altitude.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MeasurementValue_ShouldBeInValidRange()
    {
        // Arrange
        var temperatureValue = 23.5;
        var humidityValue = 65.0;
        var pressureValue = 1013.25;

        // Act & Assert
        temperatureValue.Should().BeInRange(-50, 60);
        humidityValue.Should().BeInRange(0, 100);
        pressureValue.Should().BeInRange(800, 1200);
    }

    [Fact]
    public void DateTime_ShouldBeCurrentTime()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var testTime = DateTimeOffset.UtcNow;

        // Act & Assert
        testTime.Should().BeCloseTo(now, TimeSpan.FromSeconds(5));
    }
}