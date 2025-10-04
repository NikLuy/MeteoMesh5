using MeteoMesh5.LocalNode.Components;
using MeteoMesh5.LocalNode.Data;
using MeteoMesh5.LocalNode.Services;
using MeteoMesh5.Shared.Extensions;
using MeteoMesh5.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// API Controllers for CentralServer data queries
builder.Services.AddControllers();

// Logging
builder.Services.AddSerilog(cfg => cfg.ReadFrom.Configuration(builder.Configuration));

// Bind LocalNodeConfig (Node section)
builder.Services.Configure<LocalNodeConfig>(builder.Configuration.GetSection("Node"));

// Bind SimulationOptions (Simulation section)
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));

// Add TimeProvider - automatically chooses SimulationTimeProvider or SystemTimeProvider based on configuration
builder.Services.AddTimeProvider();

// EF Core (dynamic DB filename per Node Id)
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var nodeOpts = sp.GetRequiredService<IOptions<LocalNodeConfig>>();
    var rnd = new Random();
    var nodeId = nodeOpts.Value?.Id ?? $"Node_{rnd.Next(10000,99999)}";
    var invalid = Path.GetInvalidFileNameChars();
    var safeNodeId = new string(nodeId.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    options.UseSqlite($"Data Source={safeNodeId}.db");
});

// gRPC
builder.Services.AddGrpc();

// Domain services
builder.Services.AddSingleton<StationRegistry>();
builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddHostedService<RuleEngineBackgroundService>();

// Node registration with CentralServer
builder.Services.AddHostedService<NodeRegistrationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Map API controllers for CentralServer data queries
app.MapControllers();

app.MapGrpcService<StationIngressGrpcService>();
app.MapGrpcService<StationControlGrpcService>();
app.MapGrpcService<LocalNodeDataGrpcService>();

InitializeDatabase(app);

app.Run();

static void InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();
    var simulationOptions = services.GetRequiredService<IOptions<SimulationOptions>>();

    var useSimulation = simulationOptions.Value.UseSimulation;
    

    logger.LogInformation("Simulation Mode: {UseSimulation}, StartTime: {StartTime}, Speed: {Speed}x", 
        useSimulation, simulationOptions.Value.StartTime, simulationOptions.Value.SpeedMultiplier);
    
    if (useSimulation)
    {
        logger.LogWarning("Simulation mode detected - Dropping and recreating database");
        
        try
        {
            // Drop the database completely
            db.Database.EnsureDeleted();
            logger.LogInformation("Database dropped successfully");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during simulation database reset");
            throw;
        }
    }
    
    db.Database.Migrate();
}


