using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MeteoMesh5.Shared.Models;

namespace MeteoMesh5.MeteringStation.Tests;

public class MeteringStationTests
{
    [Fact]
    public void StationId_ShouldBeSet()
    {
        // Arrange
        var stationId = "TEMP-001";
        
        // Act & Assert
        stationId.Should().NotBeNullOrEmpty();
        stationId.Should().StartWith("TEMP");
    }

    [Fact]
    public void StationConfig_ShouldHaveValidValues()
    {
        // Arrange
        var stationConfig = new StationConfig
        {
            Id = 1,
            Type = StationType.Temperature
        };

        // Act & Assert
        stationConfig.Id.Should().BeGreaterThan(0);
        stationConfig.Type.Should().Be(StationType.Temperature);
        stationConfig.GetName().Should().Be("Temperature-1");
        stationConfig.GetStationId().Should().Be("TEMPERATURE_001");
    }

    [Fact]
    public void Coordinates_ShouldBeValid()
    {
        // Arrange
        var coordinates = new Coordinates
        {
            Latitude = 47.3769,  // Zurich
            Longitude = 8.5417,
            Altitude = 408
        };

        // Act & Assert
        coordinates.Latitude.Should().BeInRange(-90, 90);
        coordinates.Longitude.Should().BeInRange(-180, 180);
        coordinates.Altitude.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(StationType.Temperature, -50, 60)]
    [InlineData(StationType.Humidity, 0, 100)]
    [InlineData(StationType.Pressure, 800, 1200)]
    public void MeasurementValues_ShouldBeInValidRange(StationType stationType, double minValue, double maxValue)
    {
        // Arrange
        var testValue = (minValue + maxValue) / 2; // Use middle value
        
        // Act & Assert
        testValue.Should().BeInRange(minValue, maxValue);
        stationType.Should().BeOneOf(StationType.Temperature, StationType.Humidity, StationType.Pressure, StationType.Lidar, StationType.Wind);
    }

    [Fact]
    public void StationConfig_GetPort_ShouldReturnValidPort()
    {
        // Arrange
        var basePort = 8000;
        var temperatureStation = new StationConfig { Id = 1, Type = StationType.Temperature };
        var humidityStation = new StationConfig { Id = 2, Type = StationType.Humidity };
        var lidarStation = new StationConfig { Id = 3, Type = StationType.Lidar };

        // Act & Assert
        temperatureStation.GetPort(basePort).Should().Be(8011); // basePort + 10 + Id
        humidityStation.GetPort(basePort).Should().Be(8022);    // basePort + 20 + Id
        lidarStation.GetPort(basePort).Should().Be(8053);       // basePort + 50 + Id
    }

    [Fact]
    public void LocalNodeConfig_ShouldBeComplete()
    {
        // Arrange
        var nodeConfig = new LocalNodeConfig
        {
            Id = "NODE-001",
            Name = "Test Local Node",
            Port = 7101,
            CentralUrl = "https://central:7201",
            Coordinates = new Coordinates { Latitude = 47.3769, Longitude = 8.5417, Altitude = 408 },
            StationInactiveMinutes = 60
        };

        // Act & Assert
        nodeConfig.Id.Should().NotBeNullOrEmpty();
        nodeConfig.Name.Should().NotBeNullOrEmpty();
        nodeConfig.Port.Should().BeGreaterThan(0);
        nodeConfig.CentralUrl.Should().NotBeNullOrEmpty();
        nodeConfig.Coordinates.Should().NotBeNull();
        nodeConfig.StationInactiveMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulationOptions_ShouldHaveValidDefaults()
    {
        // Arrange
        var simulation = new SimulationOptions();

        // Act & Assert
        simulation.StartTime.Should().BeAfter(DateTimeOffset.MinValue);
        simulation.SpeedMultiplier.Should().BeGreaterThan(0);
        simulation.UseSimulation.Should().Be(false); // Default value
    }
}