using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Services;
using MeteoMesh5.LocalNode.Grpc;
using MeteoMesh5.Shared.Models;
using Grpc.Core;

namespace MeteoMesh5.LocalNode.Tests;

public class LocalNodeDataGrpcServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private async Task SeedAsync(AppDbContext db)
    {
        db.Stations.Add(new MeteoMesh5.LocalNode.Models.Station
        {
            StationId = "LID1",
            Type = StationType.Lidar,
            LastUpdated = DateTime.UtcNow,
            LastValue = 1.8,
            Suspended = false,
            IntervalMinutes = 5
        });
        db.Measurements.Add(new MeteoMesh5.LocalNode.Models.Measurement
        {
            StationId = "LID1",
            Timestamp = DateTime.UtcNow.AddMinutes(-1),
            Value = 1.8,
            Aux1 = 8.5,
            Aux2 = 0,
            Quality = "Good"
        });
        await db.SaveChangesAsync();
    }

    private LocalNodeDataGrpcService CreateService(AppDbContext db)
        => new LocalNodeDataGrpcService(db, new NullLogger<LocalNodeDataGrpcService>());

    [Fact]
    public async Task QueryMeasurements_ReturnsPrecipitationIntensity()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var svc = CreateService(db);
        var resp = await svc.QueryMeasurements(new MeasurementQueryRequest{ MaxRecords = 10 }, TestServerCallContext.Create());
        resp.Success.Should().BeTrue();
        resp.Measurements.Should().HaveCount(1);
        var rec = resp.Measurements[0];
        rec.PrecipitationIntensity.Should().BeApproximately(1.8, 0.0001);
    }

    [Fact]
    public async Task GetStations_ReturnsStation()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var svc = CreateService(db);
        var resp = await svc.GetStations(new StationQueryRequest(), TestServerCallContext.Create());
        resp.Success.Should().BeTrue();
        resp.Stations.Should().ContainSingle(s => s.StationId == "LID1");
    }
}
