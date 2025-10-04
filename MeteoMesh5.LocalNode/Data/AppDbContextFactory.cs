using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MeteoMesh5.LocalNode.Data;

namespace MeteoMesh5.LocalNode;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_meteomesh5.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}