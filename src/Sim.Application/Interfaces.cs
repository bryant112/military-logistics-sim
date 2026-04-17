using Sim.Contracts;
using Sim.Domain;

namespace Sim.Application;

public interface IScenarioSource
{
    Task<string> LoadScenarioJsonAsync(string? scenarioPath, CancellationToken cancellationToken = default);
}

public interface IEnrichmentProvider
{
    EnrichmentSnapshot BuildSnapshot(ScenarioDefinition scenario, RouteDefinition route);
}

public interface ISimulationSessionManager
{
    CreateSessionResponse CreateSession(ScenarioDefinition scenario, int seed);
    SessionControlResponse Start(Guid sessionId);
    SessionControlResponse Pause(Guid sessionId);
    SessionControlResponse Reset(Guid sessionId);
    WorldStateResponse GetWorldState(Guid sessionId);
    TimelineResponse GetTimeline(Guid sessionId);
    EnrichmentResponse GetEnrichment(Guid sessionId);
    void AdvanceRunningSessions();
}
