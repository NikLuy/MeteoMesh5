using MeteoMesh5.LocalNode.Models;
using Microsoft.EntityFrameworkCore;

namespace MeteoMesh5.LocalNode.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            // Ensure database is created
            //await context.Database.EnsureCreatedAsync();
            
            //// Check if we already have data
            //if (await context.MeteringStations.AnyAsync())
            //{
            //    logger.LogInformation("Database already contains data, skipping seed");
            //    return;
            //}
            await Task.Delay(1);

            // No test station seeding required
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while seeding database");
            throw;
        }
    }
}
