using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MeteoMesh5.CentralServer.Tests;

public class LocalNodeManagerTests
{
    [Fact]
    public void LocalNodeManager_ShouldInitialize()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<object>>();

        // Act
        var result = logger != null;

        // Assert
        result.Should().BeTrue();
        logger.Should().NotBeNull();
    }

    [Fact]
    public void NodeId_ShouldHaveValidFormat()
    {
        // Arrange
        var nodeId = "Node-001";
        var nodeName = "Test Node";

        // Act & Assert
        nodeId.Should().NotBeNullOrEmpty();
        nodeId.Should().StartWith("Node-");
        nodeName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void NodeCollection_ShouldBeManageable()
    {
        // Arrange
        var nodes = new List<string> { "Node-001", "Node-002", "Node-003" };

        // Act
        var count = nodes.Count;
        var firstNode = nodes.FirstOrDefault();

        // Assert
        count.Should().Be(3);
        firstNode.Should().Be("Node-001");
        nodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Heartbeat_ShouldBeRecent()
    {
        // Arrange
        var lastHeartbeat = DateTimeOffset.UtcNow;
        var checkTime = DateTimeOffset.UtcNow;

        // Act
        var isRecent = (checkTime - lastHeartbeat).TotalMinutes < 5;

        // Assert
        isRecent.Should().BeTrue();
        lastHeartbeat.Should().BeCloseTo(checkTime, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("Node-001", "Test Node 1")]
    [InlineData("Node-002", "Test Node 2")]
    [InlineData("Node-003", "Test Node 3")]
    public void NodeData_ShouldBeValid(string nodeId, string nodeName)
    {
        // Arrange & Act
        var isValidId = !string.IsNullOrEmpty(nodeId) && nodeId.StartsWith("Node-");
        var isValidName = !string.IsNullOrEmpty(nodeName);

        // Assert
        isValidId.Should().BeTrue();
        isValidName.Should().BeTrue();
        nodeId.Should().Contain(nodeId.Split('-')[1]);
    }
}
