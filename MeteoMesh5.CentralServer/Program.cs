using MeteoMesh5.CentralServer.Components;
using MeteoMesh5.CentralServer.Services;
using MeteoMesh5.Shared.Extensions;
using MeteoMesh5.Shared.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Logging
builder.Services.AddSerilog(cfg => cfg.ReadFrom.Configuration(builder.Configuration));

// Bind SimulationOptions to match LocalNode time settings
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));

// Add TimeProvider for simulation time consistency
builder.Services.AddTimeProvider();

// HTTP Client for calling LocalNodes (may still be useful for diagnostics)
builder.Services.AddHttpClient();

// CentralServer services
builder.Services.AddSingleton<LocalNodeManager>();
// LocalNodeDataService must be singleton because it's injected into singleton hosted service (NodeDiscoveryService)
// and it maintains persistent gRPC channel cache.
builder.Services.AddSingleton<LocalNodeDataService>();

// Background service for node health monitoring (not discovery)
builder.Services.AddHostedService<NodeDiscoveryService>();

// gRPC Server for LocalNode registration
builder.Services.AddGrpc();

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

// Map gRPC service for LocalNode registration
app.MapGrpcService<CentralServerGrpcService>();

app.Run();