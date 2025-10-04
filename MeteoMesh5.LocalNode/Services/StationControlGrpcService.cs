using Grpc.Core;using MeteoMesh5.Grpc;

namespace MeteoMesh5.LocalNode.Services;

public class StationControlGrpcService : StationControlService.StationControlServiceBase
{
    private readonly RuleEngine _engine;
    private readonly StationRegistry _registry;
    private readonly ILogger<StationControlGrpcService> _logger;

    public StationControlGrpcService(RuleEngine engine, StationRegistry registry, ILogger<StationControlGrpcService> logger)
    {
        _engine = engine; _registry = registry; _logger = logger;
    }

    public override async Task StreamCommands(CommandStreamRequest request, IServerStreamWriter<ControlCommand> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Command stream opened for {StationId} ({Type})", request.StationId, request.StationType);
        
        // Send current state as initial command if station should be suspended
        if (_registry.TryGet(request.StationId, out var currentState))
        {
            if (currentState.Suspended)
            {
                _logger.LogInformation("Station {StationId} reconnected while suspended - sending initial Suspend command", request.StationId);
                var initialCmd = new ControlCommand
                {
                    CommandId = Guid.NewGuid().ToString(),
                    TargetStationId = request.StationId,
                    Action = "Suspend",
                    IssuedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                await responseStream.WriteAsync(initialCmd);
            }
        }
        
        while (!context.CancellationToken.IsCancellationRequested)
        {
            foreach (var cmd in _engine.DequeueCommands().Where(c =>
                (!string.IsNullOrEmpty(c.TargetStationId) && c.TargetStationId == request.StationId) ||
                (!string.IsNullOrEmpty(c.TargetType) && c.TargetType.Equals(request.StationType, StringComparison.OrdinalIgnoreCase))))
            {
                _logger.LogInformation("Streaming command {Action} to {StationId} (CommandId: {CommandId})", 
                    cmd.Action, request.StationId, cmd.CommandId);
                await responseStream.WriteAsync(cmd);
            }
            await Task.Delay(1000, context.CancellationToken);
        }
        _logger.LogInformation("Command stream closed for {StationId}", request.StationId);
    }
}
