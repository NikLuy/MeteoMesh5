using MeteoMesh5.Shared.Models;
using MeteoMesh5.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeteoMesh5.Shared.Extensions;

public static class TimeProviderServiceExtensions
{
    /// <summary>
    /// Registers TimeProvider based on simulation configuration
    /// </summary>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(serviceProvider =>
        {
            var simulationOptions = serviceProvider.GetService<IOptions<SimulationOptions>>();

            // If simulation options are configured and UseSimulation is true, use SimulationTimeProvider
            if (simulationOptions?.Value?.UseSimulation == true)
            {
                return new SimulationTimeProvider(simulationOptions);
            }

            // Otherwise use SystemTimeProvider
            return SystemTimeProvider.Instance;
        });

        return services;
    }

    /// <summary>
    /// Registers system time provider (production use)
    /// </summary>
    public static IServiceCollection AddSystemTimeProvider(this IServiceCollection services)
    {
        return services.AddSingleton<TimeProvider>(SystemTimeProvider.Instance);
    }

    /// <summary>
    /// Registers simulation time provider with specified parameters
    /// </summary>
    public static IServiceCollection AddSimulationTimeProvider(this IServiceCollection services,
        DateTimeOffset startTime,
        double speedMultiplier = 1.0,
        TimeZoneInfo? timeZone = null)
    {
        services.Configure<SimulationOptions>(options =>
        {
            options.UseSimulation = true;
            options.StartTime = startTime;
            options.SpeedMultiplier = speedMultiplier;
        });

        services.AddSingleton<TimeProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SimulationOptions>>();
            return new SimulationTimeProvider(options, timeZone);
        });

        return services;
    }
}