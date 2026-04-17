namespace Sim.Contracts;

public sealed class ScenarioValidationRequest
{
    public string? Json { get; set; }
}

public sealed class ScenarioValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class CreateSessionRequest
{
    public string? ScenarioPath { get; set; }
    public int Seed { get; set; } = 42;
}

public sealed class CreateSessionResponse
{
    public Guid SessionId { get; set; }
    public int Seed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SessionControlResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Tick { get; set; }
    public DateTimeOffset SimulatedTime { get; set; }
}

public sealed class WorldStateResponse
{
    public Guid SessionId { get; set; }
    public int Tick { get; set; }
    public DateTimeOffset SimulatedTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public LogisticsOverviewDto Overview { get; set; } = new();
    public WorldDataStatusResponse WorldData { get; set; } = new();
    public List<MovementStateDto> Movements { get; set; } = new();
    public List<AssetStateDto> Assets { get; set; } = new();
    public List<ShipmentStateDto> Shipments { get; set; } = new();
    public List<IncidentDto> Incidents { get; set; } = new();
}

public sealed class LogisticsOverviewDto
{
    public double ReportingQuality { get; set; }
    public double SustainmentRhythmAdherence { get; set; }
    public double ConfiguredLoadQuality { get; set; }
    public double SecurityDiscipline { get; set; }
    public double AverageCrewFatigueIndex { get; set; }
    public double AverageMaintenanceBacklog { get; set; }
    public double AverageMorale { get; set; }
    public double AverageRouteSeverityIndex { get; set; }
    public double AverageCargoDamageRisk { get; set; }
}

public sealed class MovementStateDto
{
    public string MovementId { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public double EtaDriftMinutes { get; set; }
    public int CrewSize { get; set; }
    public bool AssistantDriverAssigned { get; set; }
    public double CrewFatigueHours { get; set; }
    public double CrewFatigueIndex { get; set; }
    public double ReportingConfidence { get; set; }
    public double SupportScore { get; set; }
    public double ThreatExposure { get; set; }
    public double Morale { get; set; }
    public double CargoDamageRisk { get; set; }
    public double ConcealmentScore { get; set; }
    public double RouteSeverityIndex { get; set; }
    public double SurfaceAttritionFactor { get; set; }
}

public sealed class AssetStateDto
{
    public string AssetId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public double FuelState { get; set; }
    public double Readiness { get; set; }
    public double MaintenanceBacklog { get; set; }
}

public sealed class ShipmentStateDto
{
    public string ShipmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double DeliveredQuantity { get; set; }
}

public sealed class IncidentDto
{
    public string IncidentId { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public int TickDetected { get; set; }
    public List<string> CameraRefs { get; set; } = new();
}

public sealed class TimelineResponse
{
    public Guid SessionId { get; set; }
    public List<TimelineEventDto> Events { get; set; } = new();
}

public sealed class TimelineEventDto
{
    public int Tick { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class EnrichmentResponse
{
    public Guid SessionId { get; set; }
    public List<EnrichmentSnapshot> Routes { get; set; } = new();
}

public sealed class WorldDataStatusResponse
{
    public Guid SessionId { get; set; }
    public string StaticDataPolicy { get; set; } = "Initial setup or Update Real World Data";
    public string WeatherPolicy { get; set; } = "Game start and every 30 minutes";
    public string WorldSnapshotSource { get; set; } = string.Empty;
    public string WeatherSource { get; set; } = string.Empty;
    public DateTimeOffset WorldSnapshotCapturedAt { get; set; }
    public DateTimeOffset LastWorldDataRefreshAt { get; set; }
    public DateTimeOffset LastWeatherRefreshAt { get; set; }
    public DateTimeOffset NextWeatherRefreshAt { get; set; }
    public int WeatherRefreshIntervalMinutes { get; set; } = 30;
    public bool AutoWeatherRefreshEnabled { get; set; } = true;
    public double CurrentWeatherSeverity { get; set; }
    public string CurrentWeatherBand { get; set; } = string.Empty;
    public SessionDevFeatureFlagsDto DevFeatures { get; set; } = new();
    public RealWorldWeatherSnapshotDto Weather { get; set; } = new();
}

public sealed class SessionDevFeatureFlagsDto
{
    public bool UseRealWorldWeather { get; set; } = true;
    public bool AutoWeatherRefreshEnabled { get; set; } = true;
    public bool AllowManualWorldDataRefresh { get; set; } = true;
    public bool AllowManualWeatherRefresh { get; set; } = true;
    public bool FreezeStaticRwdDuringRun { get; set; } = true;
    public bool UseMockWeatherFallback { get; set; } = true;
}

public sealed class RealWorldWeatherSnapshotDto
{
    public double QueryLat { get; set; }
    public double QueryLon { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DetailedForecast { get; set; } = string.Empty;
    public int TemperatureF { get; set; }
    public string TemperatureTrend { get; set; } = string.Empty;
    public int? WindSpeedMph { get; set; }
    public string WindDirection { get; set; } = string.Empty;
    public int? PrecipitationChancePercent { get; set; }
    public double Severity { get; set; }
    public string SeverityBand { get; set; } = string.Empty;
    public DateTimeOffset ObservedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string GridId { get; set; } = string.Empty;
    public string ForecastOfficeUrl { get; set; } = string.Empty;
}
