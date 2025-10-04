namespace MeteoMesh5.Shared.Models
{
    public class SimulationOptions
    {
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
        public double SpeedMultiplier { get; set; } = 1.0;
        public bool UseSimulation { get; set; } = false;
        public string GetStartTime() => StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
