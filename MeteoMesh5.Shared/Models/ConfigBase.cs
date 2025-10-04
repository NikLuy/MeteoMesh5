using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeteoMesh5.Shared.Models;

public class AppConfig
{
    public List<CentralServerConfig> CentralServers { get; set; } = new();
    public List<LocalNodeConfig> LocalNodes { get; set; } = new();
    public SimulationOptions Simulation { get; set; } = new();
}

public class CentralServerConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Port { get; set; } = 7201;
}

public class LocalNodeConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Port { get; set; } = 7201;
    public string CentralUrl { get; set; } = string.Empty;
    public Coordinates Coordinates { get; set; } = new();
    public int StationInactiveMinutes { get; set; } = 60;
    public List<StationConfig> Stations { get; set; } = new();
}


public class StationConfig
{
    public int Id { get; set; }
    public StationType Type { get; set; }

    public string GetName() => $"{Type}-{Id}";
    public int GetPort(int BasePort) => Type switch
    {
        StationType.Temperature => BasePort + 10 + Id,
        StationType.Humidity => BasePort + 20 + Id,
        StationType.Wind => BasePort + 30 + Id,
        StationType.Pressure => BasePort + 40 + Id,
        StationType.Lidar => BasePort + 50 + Id,
        _ => BasePort + 60 + Id
    };

    public string GetStationId() => $"{Type.ToString().ToUpper()}_{Id:D3}";
}

public enum StationType
{
    Temperature = 0,
    Humidity = 1,
    Pressure = 2,
    Lidar = 3,
    Wind = 4
}

public class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
}




