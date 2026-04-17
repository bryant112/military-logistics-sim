namespace Sim.Contracts;

public sealed class AoiPlanningRequest
{
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public double RadiusMiles { get; set; } = 30;
    public int Seed { get; set; } = 42;
    public SituationCriteriaDto Criteria { get; set; } = new();
}

public sealed class SituationCriteriaDto
{
    public double InfrastructurePriority { get; set; } = 0.7;
    public double CivilFriction { get; set; } = 0.35;
    public double GovernmentFriendliness { get; set; } = 0.65;
    public double ThreatLevel { get; set; } = 0.4;
    public double WeatherStress { get; set; } = 0.2;
    public double PropagandaFactor { get; set; } = 0.35;
}

public sealed class AoiPlanningResponse
{
    public bool IsWithinUsa { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public AoiDescriptorDto Area { get; set; } = new();
    public TransportationAreaSummaryDto Transportation { get; set; } = new();
    public List<ObjectiveDto> Objectives { get; set; } = new();
    public List<SupportZoneDto> SupportZones { get; set; } = new();
}

public sealed class AoiDescriptorDto
{
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public double RadiusMiles { get; set; }
    public double RadiusKm { get; set; }
    public double AreaSquareMiles { get; set; }
}

public sealed class TransportationAreaSummaryDto
{
    public string DataSource { get; set; } = "mock-us-firstlook-v1";
    public bool IsImportedRegionalSnapshot { get; set; }
    public string DatasetCoverage { get; set; } = string.Empty;
    public int MajorRoadSegments { get; set; }
    public int RailConnections { get; set; }
    public int Airfields { get; set; }
    public int FuelSites { get; set; }
    public int Restaurants { get; set; }
    public int Campgrounds { get; set; }
    public int ArmyCorpsCampgrounds { get; set; }
    public double AverageSpeedLimitKph { get; set; }
    public double TrafficVehiclesPerHour { get; set; }
    public List<string> Counties { get; set; } = new();
    public List<CountyAllegianceDto> CountyAllegiances { get; set; } = new();
    public List<string> HighwayCorridors { get; set; } = new();
    public List<string> TransitServices { get; set; } = new();
    public List<ImportedFeatureDto> FeatureHighlights { get; set; } = new();
    public List<TransportRealismProfileDto> TransportProfiles { get; set; } = new();
}

public sealed class CountyAllegianceDto
{
    public string CountyName { get; set; } = string.Empty;
    public string CountySeat { get; set; } = string.Empty;
    public double WeightedDemocratPct { get; set; }
    public double WeightedRepublicanPct { get; set; }
    public double WeightedIndependentPct { get; set; }
    public double BluforSupport { get; set; }
    public double OpforSupport { get; set; }
    public string Allegiance { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ImportedFeatureDto
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class TransportRealismProfileDto
{
    public string ProfileId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ReferenceVehicle { get; set; } = string.Empty;
    public int Crew { get; set; }
    public int PassengerCapacity { get; set; }
    public double? FuelEconomyMpg { get; set; }
    public double? FuelBurnGallonsPerHourRoad { get; set; }
    public double? FuelBurnGallonsPerHourIdle { get; set; }
    public double? OperationalRangeMiles { get; set; }
    public double? MaxSpeedMph { get; set; }
    public double? TypicalCruiseSpeedMph { get; set; }
    public double? MaintenanceCostUsdPer1000Miles { get; set; }
    public double? BreakdownEventsPer100kMiles { get; set; }
    public double SurvivabilityScore { get; set; }
    public string DataConfidence { get; set; } = string.Empty;
    public string ProtectionSummary { get; set; } = string.Empty;
    public string EstimateBasis { get; set; } = string.Empty;
    public List<string> ImmersionFactors { get; set; } = new();
    public string SourceSummary { get; set; } = string.Empty;
}

public sealed class ObjectiveDto
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string AssociatedZoneId { get; set; } = string.Empty;
}

public sealed class SupportZoneDto
{
    public string ZoneId { get; set; } = string.Empty;
    public string CountyName { get; set; } = string.Empty;
    public int GridX { get; set; }
    public int GridY { get; set; }
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public double AreaSquareMiles { get; set; }
    public string GovernmentStance { get; set; } = string.Empty;
    public string Allegiance { get; set; } = string.Empty;
    public string SupportLevel { get; set; } = string.Empty;
    public double SupportScore { get; set; }
    public double BluforSupport { get; set; }
    public double OpforSupport { get; set; }
    public double IndependentShare { get; set; }
    public string ElectionLean { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class SitrepResponse
{
    public Guid SessionId { get; set; }
    public string OverallStatus { get; set; } = string.Empty;
    public int DelayedMovements { get; set; }
    public int ActiveIncidents { get; set; }
    public int CriticalAssets { get; set; }
    public List<MovementPinDto> MovementPins { get; set; } = new();
}

public sealed class MovementPinDto
{
    public string MovementId { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public double ThreatExposure { get; set; }
    public double CrewFatigueIndex { get; set; }
    public double ReportingConfidence { get; set; }
    public string PinTone { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
