using Sim.Application;
using Sim.Contracts;
using Sim.Api;
using Sim.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var rootPath = ResolveWorkspaceRoot();
var starterScenarioPath = Path.Combine(rootPath, "schemas", "08_starter_scenario.json");
var upperCumberlandSnapshotPath = Path.Combine(rootPath, "docs", "data", "upper-cumberland-realworld.json");

builder.Services.AddSingleton(new ScenarioValidationService());
builder.Services.AddSingleton<IScenarioSource>(new FileScenarioSource(starterScenarioPath));
builder.Services.AddSingleton<IEnrichmentProvider, MockEnrichmentProvider>();
builder.Services.AddSingleton<IAoiPlanningService>(new MockAoiPlanningService(upperCumberlandSnapshotPath));
builder.Services.AddHttpClient<IRealWorldWeatherService, NoaaWeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddSingleton<ISimulationSessionManager, SimulationSessionManager>();
builder.Services.AddHostedService<SimulationTickerHostedService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "Military Logistics Sim API",
    StarterScenarioPath = starterScenarioPath,
    UtcNow = DateTimeOffset.UtcNow
}));

app.MapPost("/scenarios/validate", async (ScenarioValidationRequest request, ScenarioValidationService validator, IScenarioSource scenarioSource, CancellationToken cancellationToken) =>
{
    var json = request.Json;
    if (string.IsNullOrWhiteSpace(json))
    {
        json = await scenarioSource.LoadScenarioJsonAsync(null, cancellationToken);
    }

    var result = validator.Validate(json);
    return Results.Ok(result);
});

app.MapPost("/sessions", async (CreateSessionRequest request, IScenarioSource scenarioSource, ScenarioValidationService validator, ISimulationSessionManager sessionManager, CancellationToken cancellationToken) =>
{
    var json = await scenarioSource.LoadScenarioJsonAsync(request.ScenarioPath, cancellationToken);
    if (!validator.TryDeserialize(json, out var scenario, out var error) || scenario is null)
    {
        return Results.BadRequest(new ScenarioValidationResponse
        {
            IsValid = false,
            Errors = { error ?? "Scenario parse failed." }
        });
    }

    var validation = validator.Validate(json);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var response = sessionManager.CreateSession(scenario, request.Seed);
    try
    {
        await sessionManager.RefreshWeatherAsync(response.SessionId, true, cancellationToken);
    }
    catch
    {
        // First pass: keep session creation resilient even if the live weather feed is unavailable.
    }
    return Results.Ok(response);
});

app.MapPost("/planning/ao", (AoiPlanningRequest request, IAoiPlanningService planningService) =>
{
    var response = planningService.PlanArea(request);
    return response.IsWithinUsa ? Results.Ok(response) : Results.BadRequest(response);
});

app.MapPost("/sessions/{sessionId:guid}/start", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.Start(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapPost("/sessions/{sessionId:guid}/pause", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.Pause(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapPost("/sessions/{sessionId:guid}/reset", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.Reset(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapGet("/sessions/{sessionId:guid}/state", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.GetWorldState(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapGet("/sessions/{sessionId:guid}/timeline", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.GetTimeline(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapGet("/sessions/{sessionId:guid}/enrichment", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.GetEnrichment(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapGet("/sessions/{sessionId:guid}/sitrep", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.GetSitrep(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapGet("/sessions/{sessionId:guid}/world-data", (Guid sessionId, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.GetWorldDataStatus(sessionId));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapPut("/sessions/{sessionId:guid}/dev-features", (Guid sessionId, SessionDevFeatureFlagsDto request, ISimulationSessionManager sessionManager) =>
{
    try
    {
        return Results.Ok(sessionManager.UpdateDevFeatures(sessionId, request));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapPost("/sessions/{sessionId:guid}/world-data/refresh", async (Guid sessionId, ISimulationSessionManager sessionManager, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sessionManager.RefreshWorldDataAsync(sessionId, cancellationToken));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.MapPost("/sessions/{sessionId:guid}/weather/refresh", async (Guid sessionId, ISimulationSessionManager sessionManager, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sessionManager.RefreshWeatherAsync(sessionId, true, cancellationToken));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

app.Run("http://localhost:5080");

static string ResolveWorkspaceRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "MilitaryLogisticsSim.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}

