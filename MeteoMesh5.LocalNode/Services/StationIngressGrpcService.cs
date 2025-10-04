using Grpc.Core;
using MeteoMesh5.Grpc;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Models;
using MeteoMesh5.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MeteoMesh5.LocalNode.Services;

public class StationIngressGrpcService : StationIngressService.StationIngressServiceBase
{
    private readonly StationRegistry _registry;
    private readonly ILogger<StationIngressGrpcService> _logger;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public StationIngressGrpcService(
        StationRegistry registry,
        ILogger<StationIngressGrpcService> logger,
        AppDbContext db,
        TimeProvider timeProvider)
    {
        _registry = registry;
        _logger = logger;
        _db = db;
        _timeProvider = timeProvider;
    }

    public override async Task<SubmitMeasurementResponse> SubmitMeasurement(SubmitMeasurementRequest request, ServerCallContext context)
    {
        var m = request.Measurement;
        if (string.IsNullOrWhiteSpace(m.StationId) || string.IsNullOrWhiteSpace(m.StationType))
            return new SubmitMeasurementResponse { Success = false, Message = "Invalid" };

        if (!Enum.TryParse<StationType>(m.StationType, true, out var stType))
            return new SubmitMeasurementResponse { Success = false, Message = "Unknown type" };

        var station = await _db.Stations.FirstOrDefaultAsync(s => s.StationId == m.StationId);
        if (station == null)
        {
            station = new Station { StationId = m.StationId, Type = stType, IntervalMinutes = 15 };
            _db.Stations.Add(station);
        }

        // Use TimeProvider instead of DateTime.UtcNow for simulation synchronization
        station.LastUpdated = _timeProvider.GetUtcNow().UtcDateTime;
        station.LastValue = m.Value;
        station.LastFlag = m.Flag || m.Value > 0 && stType == StationType.Lidar; // project rule

        var meas = new Measurement
        {
            StationId = station.StationId,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(m.TimestampUnix).UtcDateTime,
            Value = m.Value,
            Aux1 = m.Aux1,
            Aux2 = m.Aux2,
            Flag = m.Flag,
            Quality = m.Quality
        };
        _db.Measurements.Add(meas);
        await _db.SaveChangesAsync();

        // update in-memory registry snapshot
        _registry.Upsert(m, station.IntervalMinutes);

        _logger.LogDebug("Stored measurement {Station} {Type} v={Val} at {Time}",
            m.StationId, m.StationType, m.Value, station.LastUpdated);
        return new SubmitMeasurementResponse { Success = true, Message = "OK" };
    }
}
