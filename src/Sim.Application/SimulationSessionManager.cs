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
                Movements = runtime.Movements.Select(m => new MovementStateDto
                {
                    MovementId = m.MovementId,
                    RouteId = m.RouteId,
                    Mode = m.Mode.ToString(),
                    Status = m.Status,
                    Progress = Math.Round(m.Progress, 4),
                    EtaDriftMinutes = m.EtaDriftMinutes
                }).ToList(),
                Assets = runtime.AssetsById.Values.Select(a => new AssetStateDto
                {
                    AssetId = a.AssetId,
                    AssetType = a.AssetType.ToString(),
                    FuelState = Math.Round(a.FuelState, 2),
                    Readiness = a.Readiness
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
                Readiness = asset.Readiness
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
                Status = "Planned"
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

    private sealed class SessionEnvelope
    {
        public object Sync { get; } = new();
        public required SimulationEngine Engine { get; init; }
        public required SimulationRuntimeState Runtime { get; init; }
    }
}
