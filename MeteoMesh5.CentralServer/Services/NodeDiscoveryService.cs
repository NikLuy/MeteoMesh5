using MeteoMesh5.CentralServer.Services;

namespace MeteoMesh5.CentralServer.Services;

public class NodeDiscoveryService : BackgroundService
{
    private readonly LocalNodeManager _nodeManager;
    private readonly LocalNodeDataService _dataService;
    private readonly ILogger<NodeDiscoveryService> _logger;

    public NodeDiscoveryService(
        LocalNodeManager nodeManager,
        LocalNodeDataService dataService,
        ILogger<NodeDiscoveryService> logger)
    {
        _nodeManager = nodeManager;
        _dataService = dataService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Node Discovery Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DiscoverAndUpdateNodes();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Check every minute
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in node discovery service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Node Discovery Service stopped");
    }

    private async Task DiscoverAndUpdateNodes()
    {
        var nodes = _nodeManager.GetAllNodes();
        _logger.LogDebug("Checking health of {NodeCount} nodes", nodes.Count);

        var tasks = nodes.Select(async node =>
        {
            try
            {
                // Try to fetch stations to check if node is alive
                var stations = await _dataService.FetchStationsFromNodeAsync(node.NodeId);
                
                if (stations.Any())
                {
                    _nodeManager.UpdateNodeStatus(node.NodeId, true);
                    _logger.LogDebug("Node {NodeId} is healthy with {StationCount} stations", node.NodeId, stations.Count);
                }
                else
                {
                    _nodeManager.UpdateNodeStatus(node.NodeId, false);
                    _logger.LogDebug("Node {NodeId} appears to be offline (no stations returned)", node.NodeId);
                }
            }
            catch (Exception ex)
            {
                _nodeManager.UpdateNodeStatus(node.NodeId, false);
                _logger.LogDebug("Node {NodeId} health check failed: {Error}", node.NodeId, ex.Message);
            }
        });

        await Task.WhenAll(tasks);
    }
}