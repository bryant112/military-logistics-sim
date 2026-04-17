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

public interface IRealWorldWeatherService
{
    Task<RealWorldWeatherSnapshotDto> GetCurrentWeatherAsync(double lat, double lon, CancellationToken cancellationToken = default);
}

public interface IAoiPlanningService
{
    AoiPlanningResponse PlanArea(AoiPlanningRequest request);
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
    SitrepResponse GetSitrep(Guid sessionId);
    WorldDataStatusResponse GetWorldDataStatus(Guid sessionId);
    WorldDataStatusResponse UpdateDevFeatures(Guid sessionId, SessionDevFeatureFlagsDto devFeatures);
    Task<WorldDataStatusResponse> RefreshWorldDataAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<WorldDataStatusResponse> RefreshWeatherAsync(Guid sessionId, bool force = true, CancellationToken cancellationToken = default);
    void AdvanceRunningSessions();
}
