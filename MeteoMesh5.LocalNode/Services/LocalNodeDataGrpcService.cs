using Grpc.Core;
using MeteoMesh5.LocalNode.Grpc;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MeteoMesh5.LocalNode.Services;

public class LocalNodeDataGrpcService : LocalNodeDataService.LocalNodeDataServiceBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<LocalNodeDataGrpcService> _logger;

    public LocalNodeDataGrpcService(AppDbContext db, ILogger<LocalNodeDataGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<MeasurementQueryResponse> QueryMeasurements(MeasurementQueryRequest request, ServerCallContext context)
    {
        try
        {
            var query = _db.Measurements.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(request.StationId)) query = query.Where(m => m.StationId == request.StationId);
            if (request.FromTimestamp > 0)
            {
                var fromTime = DateTimeOffset.FromUnixTimeSeconds(request.FromTimestamp).UtcDateTime;
                query = query.Where(m => m.Timestamp >= fromTime);
            }
            if (request.ToTimestamp > 0)
            {
                var toTime = DateTimeOffset.FromUnixTimeSeconds(request.ToTimestamp).UtcDateTime;
                query = query.Where(m => m.Timestamp <= toTime);
            }
            var maxRecords = request.MaxRecords > 0 ? Math.Min(request.MaxRecords, 1000) : 500;
            var measurements = await query.OrderByDescending(m => m.Timestamp).Take(maxRecords).ToListAsync();

            var response = new MeasurementQueryResponse
            {
                Success = true,
                Message = "Data retrieved successfully via gRPC",
                TotalCount = measurements.Count
            };

            foreach (var m in measurements)
            {
                var station = await _db.Stations.AsNoTracking().FirstOrDefaultAsync(s => s.StationId == m.StationId);
                response.Measurements.Add(new MeasurementRecord
                {
                    Timestamp = ((DateTimeOffset)m.Timestamp).ToUnixTimeSeconds(),
                    StationId = m.StationId,
                    StationType = station?.Type.ToString() ?? "Unknown",
                    Value = m.Value,
                    Aux1 = m.Aux1, // visibility etc
                    Aux2 = m.Aux2,
                    Quality = m.Quality,
                    PrecipitationIntensity = station?.Type == StationType.Lidar ? m.Value : 0 // interpret value as mm/h for lidar
                });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying measurements via gRPC");
            return new MeasurementQueryResponse { Success = false, Message = ex.Message, TotalCount = 0 };
        }
    }

    public override async Task<StationQueryResponse> GetStations(StationQueryRequest request, ServerCallContext context)
    {
        try
        {
            var stations = await _db.Stations.AsNoTracking().ToListAsync();
            var resp = new StationQueryResponse { Success = true, Message = "Stations retrieved successfully" };
            foreach (var s in stations)
            {
                resp.Stations.Add(new StationRecord
                {
                    StationId = s.StationId,
                    StationType = s.Type.ToString(),
                    IsActive = !s.Suspended,
                    LastUpdate = ((DateTimeOffset)s.LastUpdated).ToUnixTimeSeconds(),
                    LastValue = s.LastValue ?? 0,
                    Quality = "Good"
                });
            }
            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stations via gRPC");
            return new StationQueryResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<HealthStatusResponse> GetHealthStatus(HealthStatusRequest request, ServerCallContext context)
    {
        try
        {
            var stationCount = await _db.Stations.CountAsync();
            var measurementCount = await _db.Measurements.CountAsync();
            var last = await _db.Measurements.OrderByDescending(m => m.Timestamp).FirstOrDefaultAsync();
            return new HealthStatusResponse
            {
                IsHealthy = true,
                StationCount = stationCount,
                MeasurementCount = measurementCount,
                LastMeasurementTime = last != null ? ((DateTimeOffset)last.Timestamp).ToUnixTimeSeconds() : 0,
                StatusMessage = "LocalNode is healthy and operational"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status via gRPC");
            return new HealthStatusResponse { IsHealthy = false, StatusMessage = ex.Message };
        }
    }
}