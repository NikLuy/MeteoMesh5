using MeteoMesh5.Shared.Models;

namespace MeteoMesh5.LocalNode.Models;

public class Station
{
    public int Id { get; set; }
    public string StationId { get; set; } = string.Empty;
    public StationType Type { get; set; }
    public double IntervalMinutes { get; set; } = 15;
    public bool Suspended { get; set; }
    public double? LastValue { get; set; }
    public bool LastFlag { get; set; } // e.g. rain > 0 converted to true
    public DateTime LastUpdated { get; set; }
}

public class Measurement
{
    public int Id { get; set; }
    public string StationId { get; set; } = string.Empty; // FK key
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public double Aux1 { get; set; }
    public double Aux2 { get; set; }
    public bool Flag { get; set; }
    public string Quality { get; set; } = "Good";
}

public class CommandLog
{
    public int Id { get; set; }
    public string CommandId { get; set; } = string.Empty;
    public string TargetStationId { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public double NumericValue { get; set; }
    public DateTime Issued { get; set; }
}
