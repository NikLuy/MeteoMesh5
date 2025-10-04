using MeteoMesh5.Shared.Models;

namespace MeteoMesh5.CentralServer.Models;

public class LocalNodeInfo
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeUrl { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public List<StationInfo> Stations { get; set; } = new();
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class StationInfo
{
    public string StationId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StationType Type { get; set; }
    public bool IsActive { get; set; }
    public bool Suspended { get; set; }
    public DateTime LastUpdate { get; set; }
    public double? LastValue { get; set; }
    public string Quality { get; set; } = "Unknown";
}

public class MeasurementInfo
{
    public string NodeId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double AirPressure { get; set; }
    public double PrecipitationIntensity { get; set; }
    public string QualityStatus { get; set; } = "Good";
}