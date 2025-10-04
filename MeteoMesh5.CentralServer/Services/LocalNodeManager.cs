using MeteoMesh5.CentralServer.Models;
using System.Collections.Concurrent;

namespace MeteoMesh5.CentralServer.Services;

public class LocalNodeManager
{
    private readonly ConcurrentDictionary<string, LocalNodeInfo> _nodes = new();
    private readonly ILogger<LocalNodeManager> _logger;
    private readonly TimeProvider _timeProvider;

    public LocalNodeManager(ILogger<LocalNodeManager> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _logger.LogInformation("LocalNodeManager initialized - waiting for node registrations");
    }

    public void RegisterNode(string nodeId, string name, string nodeUrl, string? location = null, double? lat = null, double? lng = null)
    {
        var nodeInfo = _nodes.GetOrAdd(nodeId, _ => new LocalNodeInfo { NodeId = nodeId });
        
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;
        nodeInfo.Name = name;
        nodeInfo.NodeUrl = nodeUrl;
        nodeInfo.Location = location ?? nodeInfo.Location;
        nodeInfo.Latitude = lat ?? nodeInfo.Latitude;
        nodeInfo.Longitude = lng ?? nodeInfo.Longitude;
        nodeInfo.IsOnline = true;
        nodeInfo.LastSeen = currentTime;
        
        _logger.LogInformation("Node registered: {NodeId} ({Name}) at {Url} from {Location} at {Time}", 
            nodeId, name, nodeUrl, location ?? "Unknown location", currentTime.ToString("HH:mm:ss"));
    }

    public void UpdateNodeStatus(string nodeId, bool isOnline)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            var wasOnline = node.IsOnline;
            node.IsOnline = isOnline;
            if (isOnline)
            {
                node.LastSeen = _timeProvider.GetUtcNow().UtcDateTime;
            }
            
            if (wasOnline != isOnline)
            {
                _logger.LogInformation("Node {NodeId} status changed: {Status} at {Time}", 
                    nodeId, isOnline ? "ONLINE" : "OFFLINE", _timeProvider.GetUtcNow().ToString("HH:mm:ss"));
            }
        }
        else if (isOnline)
        {
            _logger.LogWarning("Received status update for unknown node {NodeId}", nodeId);
        }
    }

    public void UpdateNodeStations(string nodeId, List<StationInfo> stations)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            // Update station info with NodeId
            foreach (var station in stations)
            {
                station.NodeId = nodeId;
            }
            
            node.Stations = stations;
            node.IsOnline = true;
            node.LastSeen = _timeProvider.GetUtcNow().UtcDateTime;
            
            _logger.LogDebug("Updated {StationCount} stations for node {NodeId} at {Time}", 
                stations.Count, nodeId, _timeProvider.GetUtcNow().ToString("HH:mm:ss"));
        }
        else
        {
            _logger.LogWarning("Received station update for unknown node {NodeId}", nodeId);
        }
    }

    public List<LocalNodeInfo> GetAllNodes()
    {
        CheckNodeHealth();
        return _nodes.Values.OrderBy(n => n.NodeId).ToList();
    }

    public LocalNodeInfo? GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    public List<StationInfo> GetAllStations()
    {
        return _nodes.Values
            .SelectMany(n => n.Stations)
            .OrderBy(s => s.NodeId)
            .ThenBy(s => s.StationId)
            .ToList();
    }

    public List<StationInfo> GetStationsForNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node.Stations : new List<StationInfo>();
    }

    public int GetTotalNodeCount() => _nodes.Count;
    public int GetOnlineNodeCount() => _nodes.Values.Count(n => n.IsOnline);
    public int GetTotalStationCount() => _nodes.Values.SelectMany(n => n.Stations).Count();
    public int GetActiveStationCount() => _nodes.Values.SelectMany(n => n.Stations).Count(s => s.IsActive);

    private void CheckNodeHealth()
    {
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = currentTime.AddMinutes(-5); // Consider offline after 5 minutes without heartbeat (2.5x heartbeat interval)
        
        foreach (var node in _nodes.Values)
        {
            if (node.IsOnline && node.LastSeen < cutoff)
            {
                node.IsOnline = false;
                _logger.LogWarning("Node {NodeId} marked as offline (last seen: {LastSeen}, current: {Current})", 
                    node.NodeId, node.LastSeen.ToString("HH:mm:ss"), currentTime.ToString("HH:mm:ss"));
            }
        }
    }
}

public class NodeConfig
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}