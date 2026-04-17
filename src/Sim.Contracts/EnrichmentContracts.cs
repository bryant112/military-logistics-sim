namespace Sim.Contracts;

public enum PopulationDensityBand
{
    Sparse,
    Suburban,
    Dense
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
    public string SnapshotSource { get; set; } = "mock-v1";
}
