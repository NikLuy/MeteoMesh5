using MeteoMesh5.CentralServer.Models;
using MeteoMesh5.Shared.Models;
using MeteoMesh5.LocalNode.Grpc;
using Grpc.Net.Client;
using Grpc.Core;
using System.Collections.Concurrent;

using LocalNodeDataGrpc = MeteoMesh5.LocalNode.Grpc.LocalNodeDataService;

namespace MeteoMesh5.CentralServer.Services;

public class LocalNodeDataService : IDisposable
{
    private readonly LocalNodeManager _nodeManager;
    private readonly ILogger<LocalNodeDataService> _logger;
    private readonly ConcurrentDictionary<string, GrpcChannel> _grpcChannels = new();

    public LocalNodeDataService(
        LocalNodeManager nodeManager,
        ILogger<LocalNodeDataService> logger)
    {
        _nodeManager = nodeManager;
        _logger = logger;
    }

    public async Task<List<MeasurementInfo>> FetchDataFromAllNodesAsync(
        DateTime? from = null,
        DateTime? to = null,
        int maxRecords = 500)
    {
        var allMeasurements = new List<MeasurementInfo>();
        var nodes = _nodeManager.GetAllNodes().Where(n => n.IsOnline && !string.IsNullOrEmpty(n.NodeUrl)).ToList();

        if (!nodes.Any()) return allMeasurements;

        var perNodeLimit = Math.Max(1, maxRecords / nodes.Count);

        var tasks = nodes.Select(n => FetchDataFromNodeViaGrpcAsync(n.NodeId, null, from, to, perNodeLimit));
        var results = await Task.WhenAll(tasks);
        allMeasurements.AddRange(results.SelectMany(r => r));
        return allMeasurements.OrderByDescending(m => m.Timestamp).Take(maxRecords).ToList();
    }

    public Task<List<MeasurementInfo>> FetchDataFromNodeAsync(
        string nodeId,
        string? stationId = null,
        DateTime? from = null,
        DateTime? to = null,
        int maxRecords = 500) =>
        FetchDataFromNodeViaGrpcAsync(nodeId, stationId, from, to, maxRecords);

    private async Task<List<MeasurementInfo>> FetchDataFromNodeViaGrpcAsync(
        string nodeId,
        string? stationId,
        DateTime? from,
        DateTime? to,
        int maxRecords)
    {
        var node = _nodeManager.GetNode(nodeId);
        if (node == null || string.IsNullOrEmpty(node.NodeUrl)) return new List<MeasurementInfo>();

        try
        {
            var channel = _grpcChannels.GetOrAdd(nodeId, _ =>
                GrpcChannel.ForAddress(node.NodeUrl, new GrpcChannelOptions
                {
                    HttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }
                }));

            var client = new LocalNodeDataGrpc.LocalNodeDataServiceClient(channel);
            var request = new MeasurementQueryRequest
            {
                StationId = stationId ?? string.Empty,
                FromTimestamp = from.HasValue ? ((DateTimeOffset)from.Value).ToUnixTimeSeconds() : 0,
                ToTimestamp = to.HasValue ? ((DateTimeOffset)to.Value).ToUnixTimeSeconds() : 0,
                MaxRecords = maxRecords
            };

            var resp = await client.QueryMeasurementsAsync(request, deadline: DateTime.UtcNow.AddSeconds(30));
            if (!resp.Success) return new List<MeasurementInfo>();

            _nodeManager.UpdateNodeStatus(nodeId, true);

            return resp.Measurements.Select(m => new MeasurementInfo
            {
                NodeId = nodeId,
                StationId = m.StationId,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(m.Timestamp).UtcDateTime,
                Temperature = m.StationType == "Temperature" ? m.Value : 0,
                Humidity = m.StationType == "Humidity" ? m.Value : 0,
                WindSpeed = 0,
                WindDirection = 0,
                AirPressure = m.StationType == "Pressure" ? m.Value : 0,
                PrecipitationIntensity = m.StationType == "Lidar" ? (m.PrecipitationIntensity > 0 ? m.PrecipitationIntensity : m.Value) : 0,
                QualityStatus = m.Quality
            }).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC unavailable for node {NodeId}", nodeId);
            _grpcChannels.TryRemove(nodeId, out var ch); ch?.Dispose();
            return new List<MeasurementInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC error querying measurements from node {NodeId}", nodeId);
            _grpcChannels.TryRemove(nodeId, out var ch); ch?.Dispose();
            return new List<MeasurementInfo>();
        }
    }

    public async Task<List<StationInfo>> FetchStationsFromNodeAsync(string nodeId)
    {
        var node = _nodeManager.GetNode(nodeId);
        if (node == null || string.IsNullOrEmpty(node.NodeUrl)) return new List<StationInfo>();

        try
        {
            var channel = _grpcChannels.GetOrAdd(nodeId, _ =>
                GrpcChannel.ForAddress(node.NodeUrl, new GrpcChannelOptions
                {
                    HttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }
                }));

            var client = new LocalNodeDataGrpc.LocalNodeDataServiceClient(channel);
            var resp = await client.GetStationsAsync(new StationQueryRequest(), deadline: DateTime.UtcNow.AddSeconds(15));
            if (!resp.Success) return new List<StationInfo>();

            _nodeManager.UpdateNodeStatus(nodeId, true);

            var stations = resp.Stations.Select(s => new StationInfo
            {
                NodeId = nodeId,
                StationId = s.StationId,
                Name = s.StationId,
                Type = Enum.TryParse<StationType>(s.StationType, out var st) ? st : StationType.Temperature,
                IsActive = s.IsActive,
                Suspended = !s.IsActive,
                LastUpdate = DateTimeOffset.FromUnixTimeSeconds(s.LastUpdate).UtcDateTime,
                LastValue = s.LastValue,
                Quality = s.Quality
            }).ToList();

            _nodeManager.UpdateNodeStations(nodeId, stations);
            return stations;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC station query unavailable for node {NodeId}", nodeId);
            _grpcChannels.TryRemove(nodeId, out var ch); ch?.Dispose();
            return new List<StationInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC station query error for node {NodeId}", nodeId);
            _grpcChannels.TryRemove(nodeId, out var ch); ch?.Dispose();
            return new List<StationInfo>();
        }
    }

    public void Dispose()
    {
        foreach (var ch in _grpcChannels.Values)
        {
            try { ch.Dispose(); } catch { }
        }
        _grpcChannels.Clear();
    }
}