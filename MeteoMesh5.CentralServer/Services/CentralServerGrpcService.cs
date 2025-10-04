using Grpc.Core;
using MeteoMesh5.CentralServer.Grpc;
using MeteoMesh5.CentralServer.Services;
using MeteoMesh5.CentralServer.Models;

namespace MeteoMesh5.CentralServer.Services;

public class CentralServerGrpcService : CentralServerService.CentralServerServiceBase
{
    private readonly LocalNodeManager _nodeManager;
    private readonly LocalNodeDataService _dataService;
    private readonly ILogger<CentralServerGrpcService> _logger;

    public CentralServerGrpcService(
        LocalNodeManager nodeManager, 
        LocalNodeDataService dataService, 
        ILogger<CentralServerGrpcService> logger)
    {
        _nodeManager = nodeManager;
        _dataService = dataService;
        _logger = logger;
    }

    public override async Task<LocalNodeRegistrationResponse> RegisterLocalNode(
        LocalNodeRegistrationRequest request, 
        ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Node registration request from {NodeId} ({Name}) at {Url}", 
                request.NodeId, request.NodeName, request.NodeUrl);

            _nodeManager.RegisterNode(
                request.NodeId,
                request.NodeName,
                request.NodeUrl,
                request.Location,
                request.Latitude,
                request.Longitude);

            _logger.LogInformation("Node {NodeId} registered successfully", request.NodeId);

            return new LocalNodeRegistrationResponse
            {
                Success = true,
                Message = "Node registered successfully",
                AssignedNodeId = request.NodeId,
                HeartbeatIntervalSeconds = 120 // Suggest 120 seconds heartbeat interval
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering node {NodeId}", request.NodeId);
            
            return new LocalNodeRegistrationResponse
            {
                Success = false,
                Message = $"Registration failed: {ex.Message}",
                AssignedNodeId = "",
                HeartbeatIntervalSeconds = 120 // Default heartbeat interval
            };
        }
    }

    public override async Task<HeartbeatResponse> SendHeartbeat(
        HeartbeatRequest request, 
        ServerCallContext context)
    {
        try
        {
            // Convert gRPC station statuses to internal model
            var stations = request.Stations.Select(s => new StationInfo
            {
                NodeId = request.NodeId,
                StationId = s.StationId,
                Name = s.StationName,
                Type = Enum.TryParse<MeteoMesh5.Shared.Models.StationType>(s.StationType, out var type) ? type : MeteoMesh5.Shared.Models.StationType.Temperature,
                IsActive = s.IsActive,
                // In LocalNode: IsActive = !station.Suspended
                // So if IsActive=false, the station is suspended (not just inactive)
                Suspended = !s.IsActive,
                LastUpdate = DateTimeOffset.FromUnixTimeSeconds(s.LastMeasurement).DateTime,
                LastValue = s.LastValue,
                Quality = s.Quality
            }).ToList();

            // Update node status
            _nodeManager.UpdateNodeStations(request.NodeId, stations);
            _nodeManager.UpdateNodeStatus(request.NodeId, true);

            _logger.LogDebug("Heartbeat from {NodeId} with {StationCount} stations ({ActiveCount} active, {SuspendedCount} suspended)", 
                request.NodeId, stations.Count, stations.Count(s => s.IsActive), stations.Count(s => s.Suspended));

            return new HeartbeatResponse
            {
                Acknowledged = true,
                NextHeartbeatSeconds = 120 // Suggest next heartbeat in 120 seconds 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat from {NodeId}", request.NodeId);
            
            return new HeartbeatResponse
            {
                Acknowledged = false,
                NextHeartbeatSeconds = 120 // Default next heartbeat interval
            };
        }
    }

    public override async Task<NodeDataResponse> GetNodeData(
        DataRequest request, 
        ServerCallContext context)
    {
        try
        {
            var fromTime = request.FromTimestamp > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(request.FromTimestamp).DateTime 
                : (DateTime?)null;
            var toTime = request.ToTimestamp > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(request.ToTimestamp).DateTime 
                : (DateTime?)null;
            var maxRecords = request.MaxRecords > 0 ? request.MaxRecords : 500;

            List<MeasurementInfo> measurements = string.IsNullOrEmpty(request.NodeId)
                ? await _dataService.FetchDataFromAllNodesAsync(fromTime, toTime, maxRecords)
                : await _dataService.FetchDataFromNodeAsync(request.NodeId, request.StationId, fromTime, toTime, maxRecords);

            // Convert to gRPC response format
            var stationGroups = measurements.GroupBy(m => m.StationId);
            var stationDataList = new List<StationData>();

            foreach (var group in stationGroups)
            {
                var stationData = new StationData
                {
                    StationId = group.Key,
                    StationName = group.Key,
                    StationType = "Mixed" // Could be enhanced
                };

                foreach (var measurement in group.Take(maxRecords))
                {
                    stationData.Measurements.Add(new MeasurementData
                    {
                        Timestamp = ((DateTimeOffset)measurement.Timestamp).ToUnixTimeSeconds(),
                        Temperature = measurement.Temperature,
                        Humidity = measurement.Humidity,
                        Pressure = measurement.AirPressure,
                        RainIntensity = measurement.PrecipitationIntensity,
                        RainFlag = measurement.PrecipitationIntensity > 0,
                        Quality = measurement.QualityStatus
                    });
                }

                stationDataList.Add(stationData);
            }

            return new NodeDataResponse
            {
                Success = true,
                Message = "Data retrieved successfully",
                TotalRecords = measurements.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data for request");
            
            return new NodeDataResponse
            {
                Success = false,
                Message = $"Data retrieval failed: {ex.Message}",
                TotalRecords = 0
            };
        }
    }
}