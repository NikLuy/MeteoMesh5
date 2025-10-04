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

namespace MeteoMesh5.LocalNode.Tests;

public class LocalNodeDataGrpcServiceEdgeTests
{
    private AppDbContext NewDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task QueryMeasurements_EmptyDb_ReturnsSuccessEmpty()
    {
        var db = NewDb();
        var svc = new LocalNodeDataGrpcService(db, new NullLogger<LocalNodeDataGrpcService>());
        var resp = await svc.QueryMeasurements(new MeasurementQueryRequest{ MaxRecords=10 }, TestServerCallContext.Create());
        resp.Success.Should().BeTrue();
        resp.Measurements.Should().BeEmpty();
        resp.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task QueryMeasurements_MaxRecordsClamp()
    {
        var db = NewDb();
        db.Stations.Add(new MeteoMesh5.LocalNode.Models.Station{ StationId="S", Type=StationType.Temperature, LastUpdated=DateTime.UtcNow });
        for(int i=0;i<1500;i++)
            db.Measurements.Add(new MeteoMesh5.LocalNode.Models.Measurement{ StationId="S", Timestamp=DateTime.UtcNow.AddSeconds(-i), Value=10 });
        await db.SaveChangesAsync();
        var svc = new LocalNodeDataGrpcService(db, new NullLogger<LocalNodeDataGrpcService>());
        var resp = await svc.QueryMeasurements(new MeasurementQueryRequest{ MaxRecords=5000 }, TestServerCallContext.Create());
        resp.TotalCount.Should().BeLessThanOrEqualTo(1000, "service clamps to 1000 max records");
    }
}
