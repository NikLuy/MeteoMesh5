using System.Collections.Concurrent;
using MeteoMesh5.Grpc;

namespace MeteoMesh5.LocalNode.Services;

public record StationState
(
    string StationId,
    string Type,
    double? LastValue,
    bool Flag,
    DateTimeOffset LastTimestamp,
    double IntervalMinutes,
    bool Suspended
);

public class StationRegistry
{
    private readonly ConcurrentDictionary<string, StationState> _stations = new();

    public StationState Upsert(SensorMeasurement m, double defaultInterval)
    {
        return _stations.AddOrUpdate(m.StationId,
            _ => new StationState(m.StationId, m.StationType, m.Value, m.Flag, DateTimeOffset.FromUnixTimeSeconds(m.TimestampUnix), defaultInterval, false),
            (_, existing) => existing with { LastValue = m.Value, Flag = m.Flag, LastTimestamp = DateTimeOffset.FromUnixTimeSeconds(m.TimestampUnix) });
    }

    public IEnumerable<StationState> All => _stations.Values;

    public bool TryGet(string id, out StationState state) => _stations.TryGetValue(id, out state!);

    public void ApplyCommand(ControlCommand cmd)
    {
        if (!string.IsNullOrEmpty(cmd.TargetStationId) && _stations.TryGetValue(cmd.TargetStationId, out var st))
        {
            var upd = st;
            switch (cmd.Action)
            {
                case "Suspend": upd = st with { Suspended = true }; break;
                case "Resume": upd = st with { Suspended = false }; break;
                case "SetInterval": upd = st with { IntervalMinutes = cmd.NumericValue }; break;
            }
            _stations[st.StationId] = upd;
        }
        else if (!string.IsNullOrEmpty(cmd.TargetType))
        {
            foreach (var kv in _stations.Where(s => s.Value.Type.Equals(cmd.TargetType, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var sta = kv.Value;
                var upd = sta;
                switch (cmd.Action)
                {
                    case "Suspend": upd = sta with { Suspended = true }; break;
                    case "Resume": upd = sta with { Suspended = false }; break;
                    case "SetInterval": upd = sta with { IntervalMinutes = cmd.NumericValue }; break;
                }
                _stations[sta.StationId] = upd;
            }
        }
    }
}
