namespace Sim.Domain;

public sealed class ScenarioDefinition
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public int TickRateSeconds { get; set; } = 5;
    public int DurationMinutes { get; set; } = 360;
    public string TerrainReference { get; set; } = string.Empty;
    public string RulesetId { get; set; } = string.Empty;
    public ScenarioRealismDefinition Realism { get; set; } = new();
    public List<NodeDefinition> Nodes { get; set; } = new();
    public List<RouteDefinition> Routes { get; set; } = new();
    public List<AssetDefinition> Assets { get; set; } = new();
    public List<ShipmentDefinition> Shipments { get; set; } = new();
    public List<IncidentSeedDefinition> IncidentSeeds { get; set; } = new();
}

public sealed class ScenarioRealismDefinition
{
    public double ReportingQuality { get; set; } = 0.75;
    public double SustainmentRhythmAdherence { get; set; } = 0.8;
    public double ConfiguredLoadQuality { get; set; } = 0.78;
    public double MaintenanceDiscipline { get; set; } = 0.8;
    public double SecurityDiscipline { get; set; } = 0.75;
    public double UmoPlanningQuality { get; set; } = 0.72;
    public double LoadingTeamChiefQuality { get; set; } = 0.7;
    public double WeatherSeverity { get; set; } = 0.2;
    public double DustExposure { get; set; } = 0.25;
    public double CrewEnduranceHours { get; set; } = 10.0;
    public bool UseAssistantDrivers { get; set; } = true;
    public bool UseTiedowns { get; set; } = true;
    public bool UseBlockingAndBracing { get; set; } = true;
    public bool UsePalletRestraint { get; set; } = true;
    public bool UseCargoIsolation { get; set; } = false;
}

public sealed class NodeDefinition
{
    public string NodeId { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Capacity { get; set; }
    public string SecurityPosture { get; set; } = "Medium";
}

public sealed class RouteDefinition
{
    public string RouteId { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public string RiskProfile { get; set; } = "Low";
    public int EstimatedTravelTimeMinutes { get; set; } = 60;
}

public sealed class AssetDefinition
{
    public string AssetId { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public int PayloadCapacity { get; set; }
    public double FuelState { get; set; } = 100.0;
    public double Readiness { get; set; } = 1.0;
}

public sealed class ShipmentDefinition
{
    public string ShipmentId { get; set; } = string.Empty;
    public CommodityType CommodityType { get; set; }
    public double Quantity { get; set; }
    public double Weight { get; set; }
    public double Volume { get; set; }
    public string Priority { get; set; } = "Medium";
    public string OriginNodeId { get; set; } = string.Empty;
    public string DestinationNodeId { get; set; } = string.Empty;
}

public sealed class IncidentSeedDefinition
{
    public IncidentType IncidentType { get; set; }
    public Severity Severity { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public double Probability { get; set; }
    public List<string> CameraRefs { get; set; } = new();
}
