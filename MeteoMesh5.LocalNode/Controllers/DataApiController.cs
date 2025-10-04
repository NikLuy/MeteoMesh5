using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Models;
using MeteoMesh5.Shared.Models;

namespace MeteoMesh5.LocalNode.Controllers;

[ApiController]
[Route("api")]
public class DataApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataApiController> _logger;

    public DataApiController(AppDbContext db, ILogger<DataApiController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("measurements")]
    public async Task<ActionResult<List<MeasurementDto>>> GetMeasurements(
        [FromQuery] string? stationId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int maxRecords = 500)
    {
        try
        {
            var query = _db.Measurements.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(stationId))
                query = query.Where(m => m.StationId == stationId);

            if (from.HasValue)
                query = query.Where(m => m.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(m => m.Timestamp <= to.Value);

            var measurements = await query
                .OrderByDescending(m => m.Timestamp)
                .Take(Math.Min(maxRecords, 1000)) // Cap at 1000 for safety
                .Select(m => new MeasurementDto
                {
                    StationId = m.StationId,
                    StationType = GetStationType(m.StationId),
                    Timestamp = m.Timestamp,
                    Value = m.Value,
                    Aux1 = m.Aux1,
                    Aux2 = m.Aux2,
                    Flag = m.Flag,
                    Quality = m.Quality
                })
                .ToListAsync();

            _logger.LogInformation("API: Returned {Count} measurements (station: {StationId}, from: {From}, to: {To})",
                measurements.Count, stationId ?? "all", from, to);

            return Ok(measurements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving measurements from API");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stations")]
    public async Task<ActionResult<List<StationDto>>> GetStations()
    {
        try
        {
            var stations = await _db.Stations
                .AsNoTracking()
                .Select(s => new StationDto
                {
                    StationId = s.StationId,
                    Type = s.Type,
                    IsActive = !s.Suspended,
                    LastUpdate = s.LastUpdated,
                    LastValue = s.LastValue,
                    Quality = "Good" // Simplified
                })
                .ToListAsync();

            _logger.LogInformation("API: Returned {Count} stations", stations.Count);
            return Ok(stations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stations from API");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public ActionResult<object> GetHealth()
    {
        var stationCount = _db.Stations.Count();
        var measurementCount = _db.Measurements.Count();
        var lastMeasurement = _db.Measurements
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefault()?.Timestamp;

        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            StationCount = stationCount,
            MeasurementCount = measurementCount,
            LastMeasurement = lastMeasurement
        });
    }

    private StationType GetStationType(string stationId)
    {
        var station = _db.Stations.FirstOrDefault(s => s.StationId == stationId);
        return station?.Type ?? StationType.Temperature;
    }
}

public class MeasurementDto
{
    public string StationId { get; set; } = string.Empty;
    public StationType StationType { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public double Aux1 { get; set; }
    public double Aux2 { get; set; }
    public bool Flag { get; set; }
    public string Quality { get; set; } = string.Empty;
}

public class StationDto
{
    public string StationId { get; set; } = string.Empty;
    public StationType Type { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUpdate { get; set; }
    public double? LastValue { get; set; }
    public string? Quality { get; set; }
}