using Sim.Contracts;
using Sim.Domain;
using Sim.Engine;

namespace Sim.Application;

public sealed class SimulationSessionManager : ISimulationSessionManager
{
    private readonly IEnrichmentProvider _enrichmentProvider;
    private readonly Dictionary<Guid, SessionEnvelope> _sessions = new();
    private readonly object _gate = new();

    public SimulationSessionManager(IEnrichmentProvider enrichmentProvider)
    {
        _enrichmentProvider = enrichmentProvider;
    }

    public CreateSessionResponse CreateSession(ScenarioDefinition scenario, int seed)
    {
        var sessionId = Guid.NewGuid();
        var envelope = BuildSessionEnvelope(scenario, seed, sessionId);
        lock (_gate)
        {
            _sessions[sessionId] = envelope;
        }

        return new CreateSessionResponse
        {
            SessionId = sessionId,
            Seed = seed,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public SessionControlResponse Start(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            envelope.Runtime.IsRunning = true;
            envelope.Runtime.Timeline.Add(new TimelineEvent
            {
                Tick = envelope.Runtime.Tick,
                Timestamp = envelope.Runtime.SimulatedTime,
                EventType = "SimulationStarted",
                Message = "Simulation started."
            });
            return BuildControlResponse(envelope.Runtime);
        }
    }

    public SessionControlResponse Pause(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            envelope.Runtime.IsRunning = false;
            envelope.Runtime.Timeline.Add(new TimelineEvent
            {
                Tick = envelope.Runtime.Tick,
                Timestamp = envelope.Runtime.SimulatedTime,
                EventType = "SimulationPaused",
                Message = "Simulation paused."
            });
            return BuildControlResponse(envelope.Runtime);
        }
    }

    public SessionControlResponse Reset(Guid sessionId)
    {
        var existing = GetEnvelope(sessionId);
        ScenarioDefinition scenario;
        int seed;
        lock (existing.Sync)
        {
            scenario = existing.Runtime.Scenario;
            seed = existing.Runtime.Seed;
        }

        var replacement = BuildSessionEnvelope(scenario, seed, sessionId);
        lock (_gate)
        {
            _sessions[sessionId] = replacement;
        }

        return BuildControlResponse(replacement.Runtime);
    }

    public WorldStateResponse GetWorldState(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            var runtime = envelope.Runtime;
            return new WorldStateResponse
            {
                SessionId = runtime.SessionId,
                Tick = runtime.Tick,
                SimulatedTime = runtime.SimulatedTime,
                Status = runtime.IsRunning ? "Running" : "Paused",
                Overview = BuildOverview(runtime),
                Movements = runtime.Movements.Select(m => new MovementStateDto
                {
                    MovementId = m.MovementId,
                    RouteId = m.RouteId,
                    Mode = m.Mode.ToString(),
                    Status = m.Status,
                    Progress = Math.Round(m.Progress, 4),
                    EtaDriftMinutes = m.EtaDriftMinutes,
                    CrewSize = m.CrewSize,
                    AssistantDriverAssigned = m.AssistantDriverAssigned,
                    CrewFatigueHours = Math.Round(m.CrewFatigueHours, 2),
                    CrewFatigueIndex = Math.Round(m.CrewFatigueIndex, 2),
                    ReportingConfidence = Math.Round(m.ReportingConfidence, 2),
                    SupportScore = Math.Round(m.SupportScore, 2),
                    ThreatExposure = Math.Round(m.ThreatExposure, 2)
                }).ToList(),
                Assets = runtime.AssetsById.Values.Select(a => new AssetStateDto
                {
                    AssetId = a.AssetId,
                    AssetType = a.AssetType.ToString(),
                    FuelState = Math.Round(a.FuelState, 2),
                    Readiness = Math.Round(a.Readiness, 2),
                    MaintenanceBacklog = Math.Round(a.MaintenanceBacklog, 2)
                }).ToList(),
                Shipments = runtime.ShipmentsById.Values.Select(s => new ShipmentStateDto
                {
                    ShipmentId = s.ShipmentId,
                    Status = s.Status,
                    DeliveredQuantity = s.DeliveredQuantity
                }).ToList(),
                Incidents = runtime.Incidents.Select(i => new IncidentDto
                {
                    IncidentId = i.IncidentId,
                    IncidentType = i.IncidentType.ToString(),
                    Severity = i.Severity.ToString(),
                    RouteId = i.RouteId,
                    TickDetected = i.TickDetected,
                    CameraRefs = i.CameraRefs
                }).ToList()
            };
        }
    }

    public TimelineResponse GetTimeline(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            return new TimelineResponse
            {
                SessionId = sessionId,
                Events = envelope.Runtime.Timeline.Select(t => new TimelineEventDto
                {
                    Tick = t.Tick,
                    Timestamp = t.Timestamp,
                    EventType = t.EventType,
                    Message = t.Message
                }).ToList()
            };
        }
    }

    public EnrichmentResponse GetEnrichment(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            return new EnrichmentResponse
            {
                SessionId = sessionId,
                Routes = envelope.Runtime.EnrichmentByRoute.Values.OrderBy(v => v.RouteId).ToList()
            };
        }
    }

    public SitrepResponse GetSitrep(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            var runtime = envelope.Runtime;
            var pins = runtime.Movements.Select(m => new MovementPinDto
            {
                MovementId = m.MovementId,
                RouteId = m.RouteId,
                Status = m.Status,
                Progress = Math.Round(m.Progress, 2),
                ThreatExposure = Math.Round(m.ThreatExposure, 2),
                CrewFatigueIndex = Math.Round(m.CrewFatigueIndex, 2),
                ReportingConfidence = Math.Round(m.ReportingConfidence, 2),
                PinTone = DeterminePinTone(m, runtime.AssetsById[m.AssetId]),
                Summary = $"{m.Status} | ETA drift {m.EtaDriftMinutes:F1}m | fatigue {m.CrewFatigueIndex:P0} | LOGSTAT {m.ReportingConfidence:P0}"
            }).ToList();

            return new SitrepResponse
            {
                SessionId = sessionId,
                OverallStatus = runtime.IsRunning ? "Tracking" : "StandingBy",
                DelayedMovements = runtime.Movements.Count(m => m.Status == "Delayed"),
                ActiveIncidents = runtime.Incidents.Count,
                CriticalAssets = runtime.AssetsById.Values.Count(a => a.FuelState <= 25 || a.MaintenanceBacklog >= 2.5 || a.Readiness <= 0.7),
                MovementPins = pins
            };
        }
    }

    public void AdvanceRunningSessions()
    {
        List<SessionEnvelope> snapshots;
        lock (_gate)
        {
            snapshots = _sessions.Values.ToList();
        }

        foreach (var envelope in snapshots)
        {
            lock (envelope.Sync)
            {
                if (!envelope.Runtime.IsRunning)
                {
                    continue;
                }

                envelope.Engine.AdvanceOneTick(envelope.Runtime);

                var maxTicks = Math.Max(1, (envelope.Runtime.Scenario.DurationMinutes * 60) / envelope.Runtime.Scenario.TickRateSeconds);
                if (envelope.Runtime.Tick >= maxTicks)
                {
                    envelope.Runtime.IsRunning = false;
                    envelope.Runtime.Timeline.Add(new TimelineEvent
                    {
                        Tick = envelope.Runtime.Tick,
                        Timestamp = envelope.Runtime.SimulatedTime,
                        EventType = "SimulationEnded",
                        Message = "Scenario duration reached."
                    });
                }
            }
        }
    }

    private SessionEnvelope GetEnvelope(Guid sessionId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var envelope))
            {
                return envelope;
            }
        }

        throw new KeyNotFoundException($"Session {sessionId} was not found.");
    }

    private SessionEnvelope BuildSessionEnvelope(ScenarioDefinition scenario, int seed, Guid sessionId)
    {
        var enrichments = scenario.Routes.ToDictionary(
            route => route.RouteId,
            route => _enrichmentProvider.BuildSnapshot(scenario, route));

        var runtime = new SimulationRuntimeState
        {
            SessionId = sessionId,
            Seed = seed,
            IsRunning = false,
            Tick = 0,
            SimulatedTime = scenario.StartTime,
            Scenario = scenario,
            EnrichmentByRoute = enrichments
        };

        foreach (var asset in scenario.Assets)
        {
            runtime.AssetsById[asset.AssetId] = new AssetRuntime
            {
                AssetId = asset.AssetId,
                AssetType = asset.AssetType,
                FuelState = asset.FuelState,
                Readiness = asset.Readiness,
                MaintenanceBacklog = Math.Round((1.0 - asset.Readiness) * 4.0, 2)
            };
        }

        foreach (var shipment in scenario.Shipments)
        {
            runtime.ShipmentsById[shipment.ShipmentId] = new ShipmentRuntime
            {
                ShipmentId = shipment.ShipmentId,
                Quantity = shipment.Quantity,
                DeliveredQuantity = 0,
                Status = "Pending"
            };
        }

        var assetByMode = runtime.AssetsById.Values
            .GroupBy(asset => ModeForAsset(asset.AssetType))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var route in scenario.Routes)
        {
            if (!assetByMode.TryGetValue(route.Mode, out var assets) || assets.Count == 0)
            {
                continue;
            }

            var selectedAsset = assets[0];
            assets.RemoveAt(0);
            assets.Add(selectedAsset);

            var shipmentIds = scenario.Shipments
                .Where(s => s.OriginNodeId == route.StartNodeId && s.DestinationNodeId == route.EndNodeId)
                .Select(s => s.ShipmentId)
                .ToList();

            runtime.Movements.Add(new MovementRuntime
            {
                MovementId = $"mov-{route.RouteId}",
                RouteId = route.RouteId,
                Mode = route.Mode,
                AssetId = selectedAsset.AssetId,
                ShipmentIds = shipmentIds,
                Progress = 0,
                Status = "Planned",
                CrewSize = DetermineCrewSize(route.Mode, selectedAsset.AssetType, scenario.Realism.UseAssistantDrivers, route.EstimatedTravelTimeMinutes),
                AssistantDriverAssigned = ShouldAssignAssistantDriver(route.Mode, selectedAsset.AssetType, scenario.Realism.UseAssistantDrivers, route.EstimatedTravelTimeMinutes),
                ReportingConfidence = scenario.Realism.ReportingQuality,
                ConfiguredLoadQuality = scenario.Realism.ConfiguredLoadQuality
            });
        }

        runtime.Timeline.Add(new TimelineEvent
        {
            Tick = 0,
            Timestamp = runtime.SimulatedTime,
            EventType = "SessionCreated",
            Message = $"Session {sessionId} created for scenario {scenario.ScenarioId}."
        });

        return new SessionEnvelope
        {
            Runtime = runtime,
            Engine = new SimulationEngine(seed)
        };
    }

    private static SessionControlResponse BuildControlResponse(SimulationRuntimeState runtime)
    {
        return new SessionControlResponse
        {
            SessionId = runtime.SessionId,
            Status = runtime.IsRunning ? "Running" : "Paused",
            Tick = runtime.Tick,
            SimulatedTime = runtime.SimulatedTime
        };
    }

    private static TransportMode ModeForAsset(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.TrainLocomotive or AssetType.RailCar => TransportMode.Rail,
            AssetType.CargoAircraft or AssetType.Helicopter => TransportMode.Air,
            _ => TransportMode.Ground
        };
    }

    private static int DetermineCrewSize(TransportMode mode, AssetType assetType, bool useAssistantDrivers, int estimatedTravelTimeMinutes)
    {
        return mode switch
        {
            TransportMode.Ground when assetType == AssetType.ArmoredEscort => 3,
            TransportMode.Ground => useAssistantDrivers || estimatedTravelTimeMinutes >= 240 ? 2 : 1,
            TransportMode.Rail => 2,
            TransportMode.Air when assetType == AssetType.Helicopter => 3,
            TransportMode.Air => 4,
            _ => 1
        };
    }

    private static bool ShouldAssignAssistantDriver(TransportMode mode, AssetType assetType, bool useAssistantDrivers, int estimatedTravelTimeMinutes)
    {
        if (mode != TransportMode.Ground)
        {
            return false;
        }

        if (assetType == AssetType.ArmoredEscort)
        {
            return true;
        }

        return useAssistantDrivers || estimatedTravelTimeMinutes >= 240;
    }

    private static LogisticsOverviewDto BuildOverview(SimulationRuntimeState runtime)
    {
        var realism = runtime.Scenario.Realism;
        return new LogisticsOverviewDto
        {
            ReportingQuality = Math.Round(realism.ReportingQuality, 2),
            SustainmentRhythmAdherence = Math.Round(realism.SustainmentRhythmAdherence, 2),
            ConfiguredLoadQuality = Math.Round(realism.ConfiguredLoadQuality, 2),
            SecurityDiscipline = Math.Round(realism.SecurityDiscipline, 2),
            AverageCrewFatigueIndex = Math.Round(runtime.Movements.Count == 0 ? 0 : runtime.Movements.Average(m => m.CrewFatigueIndex), 2),
            AverageMaintenanceBacklog = Math.Round(runtime.AssetsById.Count == 0 ? 0 : runtime.AssetsById.Values.Average(a => a.MaintenanceBacklog), 2)
        };
    }

    private static string DeterminePinTone(MovementRuntime movement, AssetRuntime asset)
    {
        if (movement.Status == "Delayed" || movement.ThreatExposure >= 0.7 || asset.FuelState <= 25)
        {
            return "Red";
        }

        if (movement.CrewFatigueIndex >= 0.45 || movement.ReportingConfidence <= 0.6 || asset.MaintenanceBacklog >= 2.5)
        {
            return "Amber";
        }

        return "Green";
    }

    private sealed class SessionEnvelope
    {
        public object Sync { get; } = new();
        public required SimulationEngine Engine { get; init; }
        public required SimulationRuntimeState Runtime { get; init; }
    }
}
