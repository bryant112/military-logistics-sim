namespace Sim.Contracts;

public enum PopulationDensityBand
{
    Sparse,
    Suburban,
    Dense
}

public enum RoadSegmentSurfaceType
{
    PavedHighway,
    SecondaryPaved,
    ImprovedHybrid,
    Gravel,
    Dirt,
    MudSoftGround
}

public enum SurfaceConditionState
{
    Dry,
    Wet,
    Dusty,
    Degraded,
    ReducedTraction,
    Muddy
}

public sealed class GroundCorridorMetrics
{
    public string RouteId { get; set; } = string.Empty;
    public double AverageSpeedLimitKph { get; set; } = 70;
    public double CongestionIndex { get; set; } = 0.25;
    public double RefuelStationsPer100Km { get; set; } = 2.5;
    public double RestaurantsPer100Km { get; set; } = 4.0;
    public double CampgroundsPer100Km { get; set; } = 0.8;
    public double TrafficVehiclesPerHour { get; set; } = 450;
    public double RouteSeverityIndex { get; set; } = 0.3;
    public double SurfaceAttritionFactor { get; set; } = 0.35;
    public double MoralePressure { get; set; } = 0.2;
    public double CargoDamageRisk { get; set; } = 0.12;
    public double ConcealmentOpportunity { get; set; } = 0.18;
}

public sealed class SettlementProfile
{
    public PopulationDensityBand DensityBand { get; set; }
    public double PopulationPerSqKm { get; set; }
    public double BuiltUpCoveragePercent { get; set; }
    public double RoadsideDevelopmentIndex { get; set; }
}

public sealed class GeneratedStructureSummary
{
    public string RouteId { get; set; } = string.Empty;
    public int EstimatedStructureCount { get; set; }
    public int EstimatedHousingClusters { get; set; }
    public int EstimatedServiceStops { get; set; }
    public int EstimatedSettlementFootprints { get; set; }
}

public sealed class EnrichmentSnapshot
{
    public string RouteId { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public GroundCorridorMetrics GroundCorridorMetrics { get; set; } = new();
    public SettlementProfile SettlementProfile { get; set; } = new();
    public GeneratedStructureSummary GeneratedStructureSummary { get; set; } = new();
    public List<RouteSegmentEnrichment> Segments { get; set; } = new();
    public string SnapshotSource { get; set; } = "mock-v1";
}

public sealed class RouteSegmentEnrichment
{
    public string SegmentId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public double LengthMiles { get; set; }
    public RoadSegmentSurfaceType SurfaceType { get; set; }
    public SurfaceConditionState ConditionState { get; set; }
    public bool IsHighway { get; set; }
    public bool ConnectsHighway { get; set; }
    public double TrafficStress { get; set; }
    public double LocalHostility { get; set; }
    public double DustIndex { get; set; }
    public double RouteSeverityIndex { get; set; }
    public double SurfaceAttritionFactor { get; set; }
    public double ConcealmentFactor { get; set; }
    public double ChokepointRisk { get; set; }
}
