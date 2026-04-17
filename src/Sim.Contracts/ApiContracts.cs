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
    public List<MovementStateDto> Movements { get; set; } = new();
    public List<AssetStateDto> Assets { get; set; } = new();
    public List<ShipmentStateDto> Shipments { get; set; } = new();
    public List<IncidentDto> Incidents { get; set; } = new();
}

public sealed class MovementStateDto
{
    public string MovementId { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public double EtaDriftMinutes { get; set; }
}

public sealed class AssetStateDto
{
    public string AssetId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public double FuelState { get; set; }
    public double Readiness { get; set; }
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
