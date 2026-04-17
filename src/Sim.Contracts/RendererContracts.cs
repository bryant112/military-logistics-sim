namespace Sim.Contracts;

public sealed class RendererSceneSnapshotDto
{
    public Guid SessionId { get; set; }
    public int Tick { get; set; }
    public List<RendererNodeDto> Nodes { get; set; } = new();
    public List<RendererMovementDto> Movements { get; set; } = new();
}

public sealed class RendererDeltaUpdateDto
{
    public Guid SessionId { get; set; }
    public int Tick { get; set; }
    public List<RendererMovementDto> ChangedMovements { get; set; } = new();
    public List<IncidentMarkerDto> IncidentMarkers { get; set; } = new();
}

public sealed class IncidentMarkerDto
{
    public string IncidentId { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public sealed class CameraSwitchRequestDto
{
    public string IncidentId { get; set; } = string.Empty;
    public string PreferredFeedRef { get; set; } = string.Empty;
}

public sealed class RendererNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public sealed class RendererMovementDto
{
    public string MovementId { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string Mode { get; set; } = string.Empty;
    public SettlementProfile SettlementProfile { get; set; } = new();
    public GeneratedStructureSummary GeneratedStructureSummary { get; set; } = new();
}
