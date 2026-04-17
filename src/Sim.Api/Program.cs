using Sim.Application;
using Sim.Contracts;
using Sim.Api;
using Sim.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var rootPath = ResolveWorkspaceRoot();
var starterScenarioPath = Path.Combine(rootPath, "schemas", "08_starter_scenario.json");

builder.Services.AddSingleton(new ScenarioValidationService());
builder.Services.AddSingleton<IScenarioSource>(new FileScenarioSource(starterScenarioPath));
builder.Services.AddSingleton<IEnrichmentProvider, MockEnrichmentProvider>();
builder.Services.AddSingleton<IAoiPlanningService, MockAoiPlanningService>();
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

