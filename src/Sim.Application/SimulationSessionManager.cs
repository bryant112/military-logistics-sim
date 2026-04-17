using Sim.Contracts;
using Sim.Domain;
using Sim.Engine;

namespace Sim.Application;

public sealed class SimulationSessionManager : ISimulationSessionManager
{
    private readonly IEnrichmentProvider _enrichmentProvider;
    private readonly IRealWorldWeatherService _weatherService;
    private readonly Dictionary<Guid, SessionEnvelope> _sessions = new();
    private readonly object _gate = new();

    public SimulationSessionManager(IEnrichmentProvider enrichmentProvider, IRealWorldWeatherService? weatherService = null)
    {
        _enrichmentProvider = enrichmentProvider;
        _weatherService = weatherService ?? new FallbackWeatherService();
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
                WorldData = BuildWorldDataStatus(runtime),
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
                    ThreatExposure = Math.Round(m.ThreatExposure, 2),
                    Morale = Math.Round(m.Morale, 2),
                    CargoDamageRisk = Math.Round(m.CargoDamageRisk, 2),
                    ConcealmentScore = Math.Round(m.ConcealmentScore, 2),
                    RouteSeverityIndex = Math.Round(m.RouteSeverityIndex, 2),
                    SurfaceAttritionFactor = Math.Round(m.SurfaceAttritionFactor, 2)
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
                Summary = $"{m.Status} | ETA drift {m.EtaDriftMinutes:F1}m | RSI {m.RouteSeverityIndex:P0} | morale {m.Morale:P0} | damage {m.CargoDamageRisk:P0}"
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

    public WorldDataStatusResponse GetWorldDataStatus(Guid sessionId)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            return BuildWorldDataStatus(envelope.Runtime);
        }
    }

    public WorldDataStatusResponse UpdateDevFeatures(Guid sessionId, SessionDevFeatureFlagsDto devFeatures)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            envelope.Runtime.WorldData.DevFeatures = CloneDevFeatures(devFeatures);
            envelope.Runtime.WorldData.AutoWeatherRefreshEnabled = devFeatures.UseRealWorldWeather && devFeatures.AutoWeatherRefreshEnabled;
            envelope.Runtime.WorldData.TimelineSafeNextRefresh();
            envelope.Runtime.Timeline.Add(new TimelineEvent
            {
                Tick = envelope.Runtime.Tick,
                Timestamp = envelope.Runtime.SimulatedTime,
                EventType = "DevFeaturesUpdated",
                Message = $"Dev feature toggles updated. Live weather {(devFeatures.UseRealWorldWeather ? "enabled" : "disabled")}; auto refresh {(devFeatures.AutoWeatherRefreshEnabled ? "enabled" : "disabled")}."
            });
            return BuildWorldDataStatus(envelope.Runtime);
        }
    }

    public Task<WorldDataStatusResponse> RefreshWorldDataAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var envelope = GetEnvelope(sessionId);
        lock (envelope.Sync)
        {
            if (!envelope.Runtime.WorldData.DevFeatures.AllowManualWorldDataRefresh)
            {
                return Task.FromResult(BuildWorldDataStatus(envelope.Runtime));
            }

            RebuildEnrichments(envelope.Runtime);
            var now = DateTimeOffset.UtcNow;
            envelope.Runtime.WorldData.WorldSnapshotCapturedAt = now;
            envelope.Runtime.WorldData.LastWorldDataRefreshAt = now;
            envelope.Runtime.Timeline.Add(new TimelineEvent
            {
                Tick = envelope.Runtime.Tick,
                Timestamp = envelope.Runtime.SimulatedTime,
                EventType = "WorldDataRefreshed",
                Message = "Static RWD snapshot rebuilt from the current imported dataset."
            });

            return Task.FromResult(BuildWorldDataStatus(envelope.Runtime));
        }
    }

    public async Task<WorldDataStatusResponse> RefreshWeatherAsync(Guid sessionId, bool force = true, CancellationToken cancellationToken = default)
    {
        var envelope = GetEnvelope(sessionId);
        return await RefreshWeatherInternalAsync(envelope, force, cancellationToken);
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
            var shouldAdvance = false;
            var shouldAutoRefreshWeather = false;

            lock (envelope.Sync)
            {
                shouldAdvance = envelope.Runtime.IsRunning;
                shouldAutoRefreshWeather = shouldAdvance && ShouldAutoRefreshWeather(envelope.Runtime.WorldData);
            }

            if (!shouldAdvance)
            {
                continue;
            }

            if (shouldAutoRefreshWeather)
            {
                RefreshWeatherInternalAsync(envelope, false, CancellationToken.None).GetAwaiter().GetResult();
            }

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

    private async Task<WorldDataStatusResponse> RefreshWeatherInternalAsync(SessionEnvelope envelope, bool force, CancellationToken cancellationToken)
    {
        double lat;
        double lon;
        SessionDevFeatureFlagsDto features;
        RealWorldWeatherSnapshotDto currentSnapshot;
        WorldDataRuntime currentWorldData;
        lock (envelope.Sync)
        {
            currentWorldData = envelope.Runtime.WorldData;
            features = CloneDevFeatures(currentWorldData.DevFeatures);
            lat = currentWorldData.QueryLat;
            lon = currentWorldData.QueryLon;
            currentSnapshot = CloneWeatherSnapshot(currentWorldData.WeatherSnapshot);

            if (force && !features.AllowManualWeatherRefresh)
            {
                return BuildWorldDataStatus(envelope.Runtime);
            }

            if (!force)
            {
                if (!features.UseRealWorldWeather || !features.AutoWeatherRefreshEnabled || DateTimeOffset.UtcNow < currentWorldData.NextWeatherRefreshAt)
                {
                    return BuildWorldDataStatus(envelope.Runtime);
                }
            }
        }

        RealWorldWeatherSnapshotDto snapshot;
        try
        {
            snapshot = features.UseRealWorldWeather
                ? await _weatherService.GetCurrentWeatherAsync(lat, lon, cancellationToken)
                : BuildFallbackWeatherSnapshot(currentSnapshot, lat, lon, DateTimeOffset.UtcNow);
        }
        catch
        {
            if (!features.UseMockWeatherFallback)
            {
                throw;
            }

            snapshot = BuildFallbackWeatherSnapshot(currentSnapshot, lat, lon, DateTimeOffset.UtcNow);
        }

        lock (envelope.Sync)
        {
            ApplyWeatherSnapshot(envelope.Runtime, snapshot);
            envelope.Runtime.Timeline.Add(new TimelineEvent
            {
                Tick = envelope.Runtime.Tick,
                Timestamp = envelope.Runtime.SimulatedTime,
                EventType = force ? "WeatherRefreshed" : "WeatherAutoRefreshed",
                Message = $"Weather updated from {snapshot.Source}: {snapshot.Summary} ({snapshot.SeverityBand}, severity {snapshot.Severity:P0})."
            });
            return BuildWorldDataStatus(envelope.Runtime);
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

        var now = DateTimeOffset.UtcNow;
        var (queryLat, queryLon) = DetermineQueryPoint(scenario);

        var runtime = new SimulationRuntimeState
        {
            SessionId = sessionId,
            Seed = seed,
            IsRunning = false,
            Tick = 0,
            SimulatedTime = scenario.StartTime,
            Scenario = scenario,
            EnrichmentByRoute = enrichments,
            WorldData = new WorldDataRuntime
            {
                QueryLat = queryLat,
                QueryLon = queryLon,
                WorldSnapshotCapturedAt = now,
                LastWorldDataRefreshAt = now,
                LastWeatherRefreshAt = now,
                NextWeatherRefreshAt = now,
                WeatherRefreshIntervalMinutes = 30,
                AutoWeatherRefreshEnabled = true,
                CurrentWeatherSeverity = scenario.Realism.WeatherSeverity,
                CurrentWeatherBand = ToWeatherBand(scenario.Realism.WeatherSeverity),
                DevFeatures = new SessionDevFeatureFlagsDto(),
                WorldSnapshotSource = string.Join(", ", enrichments.Values.Select(snapshot => snapshot.SnapshotSource).Distinct()),
                WeatherSource = "Scenario baseline",
                WeatherSnapshot = new RealWorldWeatherSnapshotDto
                {
                    QueryLat = queryLat,
                    QueryLon = queryLon,
                    Summary = "Scenario baseline weather",
                    DetailedForecast = "Baseline weather severity seeded from scenario realism until a real-world refresh occurs.",
                    TemperatureF = 70,
                    TemperatureTrend = "Steady",
                    WindSpeedMph = 6,
                    WindDirection = "Variable",
                    PrecipitationChancePercent = 10,
                    Severity = Math.Round(scenario.Realism.WeatherSeverity, 2),
                    SeverityBand = ToWeatherBand(scenario.Realism.WeatherSeverity),
                    ObservedAt = now,
                    Source = "Scenario baseline",
                    GridId = string.Empty,
                    ForecastOfficeUrl = string.Empty
                }
            }
        };

        foreach (var asset in scenario.Assets)
        {
            runtime.AssetsById[asset.AssetId] = new AssetRuntime
            {
                AssetId = asset.AssetId,
                AssetType = asset.AssetType,
                PayloadCapacity = asset.PayloadCapacity,
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

    private static WorldDataStatusResponse BuildWorldDataStatus(SimulationRuntimeState runtime)
    {
        var worldData = runtime.WorldData;
        return new WorldDataStatusResponse
        {
            SessionId = runtime.SessionId,
            StaticDataPolicy = worldData.StaticDataPolicy,
            WeatherPolicy = worldData.WeatherPolicy,
            WorldSnapshotSource = worldData.WorldSnapshotSource,
            WeatherSource = worldData.WeatherSource,
            WorldSnapshotCapturedAt = worldData.WorldSnapshotCapturedAt,
            LastWorldDataRefreshAt = worldData.LastWorldDataRefreshAt,
            LastWeatherRefreshAt = worldData.LastWeatherRefreshAt,
            NextWeatherRefreshAt = worldData.NextWeatherRefreshAt,
            WeatherRefreshIntervalMinutes = worldData.WeatherRefreshIntervalMinutes,
            AutoWeatherRefreshEnabled = worldData.AutoWeatherRefreshEnabled,
            CurrentWeatherSeverity = Math.Round(worldData.CurrentWeatherSeverity, 2),
            CurrentWeatherBand = worldData.CurrentWeatherBand,
            DevFeatures = CloneDevFeatures(worldData.DevFeatures),
            Weather = CloneWeatherSnapshot(worldData.WeatherSnapshot)
        };
    }

    private void RebuildEnrichments(SimulationRuntimeState runtime)
    {
        runtime.EnrichmentByRoute.Clear();
        foreach (var route in runtime.Scenario.Routes)
        {
            runtime.EnrichmentByRoute[route.RouteId] = _enrichmentProvider.BuildSnapshot(runtime.Scenario, route);
        }

        runtime.WorldData.WorldSnapshotSource = string.Join(", ", runtime.EnrichmentByRoute.Values.Select(snapshot => snapshot.SnapshotSource).Distinct());
    }

    private void ApplyWeatherSnapshot(SimulationRuntimeState runtime, RealWorldWeatherSnapshotDto snapshot)
    {
        runtime.WorldData.WeatherSnapshot = CloneWeatherSnapshot(snapshot);
        runtime.WorldData.WeatherSource = snapshot.Source;
        runtime.WorldData.CurrentWeatherSeverity = snapshot.Severity;
        runtime.WorldData.CurrentWeatherBand = snapshot.SeverityBand;
        runtime.WorldData.LastWeatherRefreshAt = snapshot.ObservedAt;
        runtime.WorldData.NextWeatherRefreshAt = snapshot.ObservedAt.AddMinutes(runtime.WorldData.WeatherRefreshIntervalMinutes);
        runtime.WorldData.AutoWeatherRefreshEnabled = runtime.WorldData.DevFeatures.UseRealWorldWeather && runtime.WorldData.DevFeatures.AutoWeatherRefreshEnabled;
        runtime.Scenario.Realism.WeatherSeverity = snapshot.Severity;
        RebuildEnrichments(runtime);
    }

    private static (double Lat, double Lon) DetermineQueryPoint(ScenarioDefinition scenario)
    {
        if (scenario.Nodes.Count == 0)
        {
            return (36.1627, -85.5016);
        }

        return (scenario.Nodes.Average(node => node.Lat), scenario.Nodes.Average(node => node.Lon));
    }

    private static bool ShouldAutoRefreshWeather(WorldDataRuntime worldData)
    {
        return worldData.DevFeatures.UseRealWorldWeather
            && worldData.AutoWeatherRefreshEnabled
            && DateTimeOffset.UtcNow >= worldData.NextWeatherRefreshAt;
    }

    private static SessionDevFeatureFlagsDto CloneDevFeatures(SessionDevFeatureFlagsDto source)
    {
        return new SessionDevFeatureFlagsDto
        {
            UseRealWorldWeather = source.UseRealWorldWeather,
            AutoWeatherRefreshEnabled = source.AutoWeatherRefreshEnabled,
            AllowManualWorldDataRefresh = source.AllowManualWorldDataRefresh,
            AllowManualWeatherRefresh = source.AllowManualWeatherRefresh,
            FreezeStaticRwdDuringRun = source.FreezeStaticRwdDuringRun,
            UseMockWeatherFallback = source.UseMockWeatherFallback
        };
    }

    private static RealWorldWeatherSnapshotDto CloneWeatherSnapshot(RealWorldWeatherSnapshotDto source)
    {
        return new RealWorldWeatherSnapshotDto
        {
            QueryLat = source.QueryLat,
            QueryLon = source.QueryLon,
            Summary = source.Summary,
            DetailedForecast = source.DetailedForecast,
            TemperatureF = source.TemperatureF,
            TemperatureTrend = source.TemperatureTrend,
            WindSpeedMph = source.WindSpeedMph,
            WindDirection = source.WindDirection,
            PrecipitationChancePercent = source.PrecipitationChancePercent,
            Severity = source.Severity,
            SeverityBand = source.SeverityBand,
            ObservedAt = source.ObservedAt,
            Source = source.Source,
            GridId = source.GridId,
            ForecastOfficeUrl = source.ForecastOfficeUrl
        };
    }

    private static RealWorldWeatherSnapshotDto BuildFallbackWeatherSnapshot(RealWorldWeatherSnapshotDto currentSnapshot, double lat, double lon, DateTimeOffset now)
    {
        var snapshot = CloneWeatherSnapshot(currentSnapshot);
        snapshot.QueryLat = lat;
        snapshot.QueryLon = lon;
        snapshot.ObservedAt = now;
        snapshot.Source = string.IsNullOrWhiteSpace(currentSnapshot.Source) ? "Fallback weather baseline" : $"Fallback from {currentSnapshot.Source}";
        snapshot.Summary = string.IsNullOrWhiteSpace(currentSnapshot.Summary) ? "Retaining previous weather baseline" : currentSnapshot.Summary;
        snapshot.DetailedForecast = string.IsNullOrWhiteSpace(currentSnapshot.DetailedForecast)
            ? "Live weather feed unavailable, so the sim retained the previous weather baseline."
            : currentSnapshot.DetailedForecast;
        snapshot.SeverityBand = string.IsNullOrWhiteSpace(snapshot.SeverityBand) ? ToWeatherBand(snapshot.Severity) : snapshot.SeverityBand;
        return snapshot;
    }

    private static string ToWeatherBand(double severity)
    {
        return severity switch
        {
            <= 0.25 => "Nominal",
            <= 0.5 => "Watch",
            <= 0.75 => "Rough",
            _ => "Severe"
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
            AverageMaintenanceBacklog = Math.Round(runtime.AssetsById.Count == 0 ? 0 : runtime.AssetsById.Values.Average(a => a.MaintenanceBacklog), 2),
            AverageMorale = Math.Round(runtime.Movements.Count == 0 ? 0 : runtime.Movements.Average(m => m.Morale), 2),
            AverageRouteSeverityIndex = Math.Round(runtime.Movements.Count == 0 ? 0 : runtime.Movements.Average(m => m.RouteSeverityIndex), 2),
            AverageCargoDamageRisk = Math.Round(runtime.Movements.Count == 0 ? 0 : runtime.Movements.Average(m => m.CargoDamageRisk), 2)
        };
    }

    private static string DeterminePinTone(MovementRuntime movement, AssetRuntime asset)
    {
        if (movement.Status == "Delayed" || movement.ThreatExposure >= 0.7 || asset.FuelState <= 25 || movement.RouteSeverityIndex >= 0.7 || movement.CargoDamageRisk >= 0.55)
        {
            return "Red";
        }

        if (movement.CrewFatigueIndex >= 0.45 || movement.ReportingConfidence <= 0.6 || asset.MaintenanceBacklog >= 2.5 || movement.Morale <= 0.55)
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

    private sealed class FallbackWeatherService : IRealWorldWeatherService
    {
        public Task<RealWorldWeatherSnapshotDto> GetCurrentWeatherAsync(double lat, double lon, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RealWorldWeatherSnapshotDto
            {
                QueryLat = lat,
                QueryLon = lon,
                Summary = "Fallback nominal weather",
                DetailedForecast = "No live weather provider was registered, so the sim used a fallback nominal weather snapshot.",
                TemperatureF = 72,
                TemperatureTrend = "Steady",
                WindSpeedMph = 7,
                WindDirection = "Variable",
                PrecipitationChancePercent = 10,
                Severity = 0.2,
                SeverityBand = "Nominal",
                ObservedAt = DateTimeOffset.UtcNow,
                Source = "Fallback weather service",
                GridId = string.Empty,
                ForecastOfficeUrl = string.Empty
            });
        }
    }
}

file static class WorldDataRuntimeExtensions
{
    public static void TimelineSafeNextRefresh(this WorldDataRuntime worldData)
    {
        worldData.AutoWeatherRefreshEnabled = worldData.DevFeatures.UseRealWorldWeather && worldData.DevFeatures.AutoWeatherRefreshEnabled;
        worldData.NextWeatherRefreshAt = worldData.LastWeatherRefreshAt.AddMinutes(worldData.WeatherRefreshIntervalMinutes);
    }
}
