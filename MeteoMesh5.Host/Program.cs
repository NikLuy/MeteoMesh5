using MeteoMesh5.Host.Models;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var AppOptions = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();

if (AppOptions == null)
{
    throw new InvalidOperationException("AppConfig section not found in configuration");
}

Console.WriteLine($"Loaded AppConfig with {AppOptions.LocalNodes.Count} LocalNodes");
foreach (var node in AppOptions.LocalNodes)
{
    Console.WriteLine($"  Node: {node.Id} - {node.Name} with {node.Stations.Count} stations");
}

// Add CentralServer
if (AppOptions.CentralServers.Any())
{
    foreach (var central in AppOptions.CentralServers)
    {
        builder.AddProject<Projects.MeteoMesh5_CentralServer>($"centralserver-{central.Id.Replace("_", "-")}")
            .WithHttpsEndpoint(central.Port)
            .WithEnvironment("Server__Id", central.Id)
            .WithEnvironment("Server__Name", central.Name)
            .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
            .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
            .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());
    }
}
else
{
    // Default central server if none configured
    builder.AddProject<Projects.MeteoMesh5_CentralServer>("meteomesh5-centralserver")
        .WithHttpsEndpoint(7501);
}

// Add LocalNodes and their Stations
foreach (var node in AppOptions.LocalNodes)
{
    var nodeServiceName = $"NODE-{node.Id.ToString("000")}";
    var nodeUrl = $"https://localhost:{node.Port}";
    
    // Add LocalNode
    builder.AddProject<Projects.MeteoMesh5_LocalNode>(nodeServiceName)
        .WithHttpsEndpoint(node.Port)
        .WithEnvironment("Node__Id", $"NODE-{node.Id.ToString("000")}")
        .WithEnvironment("Node__Name", node.Name)
        .WithEnvironment("Node__Port", node.Port.ToString())
        .WithEnvironment("Node__CentralUrl", node.CentralUrl)
        .WithEnvironment("Node__StationInactiveMinutes", node.StationInactiveMinutes.ToString())
        .WithEnvironment("Node__Coordinates__Latitude", node.Coordinates.Latitude.ToString())
        .WithEnvironment("Node__Coordinates__Longitude", node.Coordinates.Longitude.ToString())
        .WithEnvironment("Node__Coordinates__Altitude", node.Coordinates.Altitude.ToString())
        .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
        .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
        .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());

    // Add Stations for this LocalNode
    foreach (var station in node.Stations)
    {
        var stationName = station.GetName().ToLower().Replace("_", "-");
        var stationPort = station.GetPort(node.Port); // Base port 8000 for stations
        var stationId = station.GetStationId();

        Console.WriteLine($"    Creating station: {stationId} ({station.Type}) on port {stationPort}");

        switch (station.Type)
        {
            case StationType.Temperature:
                builder.AddProject<Projects.MeteoMesh5_TemperatureStation>($"{nodeServiceName}-{stationName}")
                    .WithHttpsEndpoint(stationPort)
                    .WithEnvironment("Station__Id", stationId)
                    .WithEnvironment("Station__NodeUrl", nodeUrl)
                    .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
                    .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
                    .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());
                break;

            case StationType.Humidity:
                builder.AddProject<Projects.MeteoMesh5_HumidityStation>($"{nodeServiceName}-{stationName}")
                    .WithHttpsEndpoint(stationPort)
                    .WithEnvironment("Station__Id", stationId)
                    .WithEnvironment("Station__NodeUrl", nodeUrl)
                    .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
                    .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
                    .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());
                break;

            case StationType.Pressure:
                builder.AddProject<Projects.MeteoMesh5_PressureStation>($"{nodeServiceName}-{stationName}")
                    .WithHttpsEndpoint(stationPort)
                    .WithEnvironment("Station__Id", stationId)
                    .WithEnvironment("Station__NodeUrl", nodeUrl)
                    .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
                    .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
                    .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());
                break;

            case StationType.Lidar:
                builder.AddProject<Projects.MeteoMesh5_LidarStation>($"{nodeServiceName}-{stationName}")
                    .WithHttpsEndpoint(stationPort)
                    .WithEnvironment("Station__Id", stationId)
                    .WithEnvironment("Station__NodeUrl", nodeUrl)
                    .WithEnvironment("Simulation__StartTime", AppOptions.Simulation.GetStartTime())
                    .WithEnvironment("Simulation__SpeedMultiplier", AppOptions.Simulation.SpeedMultiplier.ToString())
                    .WithEnvironment("Simulation__UseSimulation", AppOptions.Simulation.UseSimulation.ToString());
                break;

            case StationType.Wind:
                // Wind station not implemented yet, skip or add placeholder
                Console.WriteLine($"Wind station {stationId} skipped - not implemented");
                break;
        }
    }
}

builder.Build().Run();
