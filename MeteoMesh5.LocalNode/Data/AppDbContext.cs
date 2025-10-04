using Microsoft.EntityFrameworkCore;using MeteoMesh5.LocalNode.Models;

namespace MeteoMesh5.LocalNode.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Station>().HasIndex(s => s.StationId).IsUnique();
        modelBuilder.Entity<Measurement>().HasIndex(m => new { m.StationId, m.Timestamp });
        modelBuilder.Entity<CommandLog>().HasIndex(c => c.CommandId).IsUnique();
    }
}
