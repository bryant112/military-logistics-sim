using Sim.Contracts;
using Sim.Domain;

namespace Sim.Engine;

public sealed class MovementRuntime
{
    public string MovementId { get; init; } = string.Empty;
    public string RouteId { get; init; } = string.Empty;
    public TransportMode Mode { get; init; }
    public string AssetId { get; init; } = string.Empty;
    public List<string> ShipmentIds { get; init; } = new();
    public double Progress { get; set; }
    public double EtaDriftMinutes { get; set; }
    public string Status { get; set; } = "Planned";
    public bool Delivered { get; set; }
    public int CrewSize { get; set; }
    public bool AssistantDriverAssigned { get; set; }
    public double CrewFatigueHours { get; set; }
    public double CrewFatigueIndex { get; set; }
    public double ReportingConfidence { get; set; }
    public double ConfiguredLoadQuality { get; set; }
    public double SupportScore { get; set; }
    public double ThreatExposure { get; set; }
    public double Morale { get; set; } = 1.0;
    public double CargoDamageRisk { get; set; }
    public double ConcealmentScore { get; set; }
    public double RouteSeverityIndex { get; set; }
    public double SurfaceAttritionFactor { get; set; }
}

public sealed class AssetRuntime
{
    public string AssetId { get; init; } = string.Empty;
    public AssetType AssetType { get; init; }
    public int PayloadCapacity { get; init; }
    public double FuelState { get; set; }
    public double Readiness { get; set; }
    public double MaintenanceBacklog { get; set; }
}

public sealed class ShipmentRuntime
{
    public string ShipmentId { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double DeliveredQuantity { get; set; }
    public string Status { get; set; } = "Pending";
}

public sealed class IncidentRuntime
{
    public string IncidentId { get; init; } = string.Empty;
    public IncidentType IncidentType { get; init; }
    public Severity Severity { get; init; }
    public string RouteId { get; init; } = string.Empty;
    public int TickDetected { get; init; }
    public List<string> CameraRefs { get; init; } = new();
}

public sealed class TimelineEvent
{
    public int Tick { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class SimulationRuntimeState
{
    public Guid SessionId { get; set; }
    public int Seed { get; set; }
    public bool IsRunning { get; set; }
    public int Tick { get; set; }
    public DateTimeOffset SimulatedTime { get; set; }
    public ScenarioDefinition Scenario { get; init; } = new();
    public Dictionary<string, EnrichmentSnapshot> EnrichmentByRoute { get; init; } = new();
    public Dictionary<string, AssetRuntime> AssetsById { get; init; } = new();
    public Dictionary<string, ShipmentRuntime> ShipmentsById { get; init; } = new();
    public List<MovementRuntime> Movements { get; init; } = new();
    public List<IncidentRuntime> Incidents { get; init; } = new();
    public List<TimelineEvent> Timeline { get; init; } = new();
    public HashSet<string> IncidentUniqueness { get; init; } = new();
    public HashSet<string> EventUniqueness { get; init; } = new();
}
