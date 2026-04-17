using Sim.Contracts;
using Sim.Domain;

namespace Sim.Engine;

public sealed class SimulationEngine
{
    private readonly Random _rng;

    public SimulationEngine(int seed)
    {
        _rng = new Random(seed);
    }

    public void AdvanceOneTick(SimulationRuntimeState state)
    {
        state.Tick++;
        state.SimulatedTime = state.SimulatedTime.AddSeconds(state.Scenario.TickRateSeconds);

        foreach (var movement in state.Movements)
        {
            if (movement.Status == "Completed")
            {
                continue;
            }

            var route = state.Scenario.Routes.First(r => r.RouteId == movement.RouteId);
            var asset = state.AssetsById[movement.AssetId];
            var realism = state.Scenario.Realism;
            state.EnrichmentByRoute.TryGetValue(route.RouteId, out var snapshot);

            UpdateOperationalPressure(state, route, movement, asset, snapshot, realism);

            var baseIncrement = state.Scenario.TickRateSeconds / Math.Max(1.0, route.EstimatedTravelTimeMinutes * 60.0);
            var modeFactor = route.Mode switch
            {
                TransportMode.Ground => 1.0,
                TransportMode.Rail => 0.95,
                TransportMode.Air => 1.2,
                _ => 1.0
            };

            var increment = baseIncrement * modeFactor;
            if (route.Mode == TransportMode.Ground && snapshot is not null)
            {
                increment *= ComputeGroundModifier(snapshot);
            }

            increment *= ComputeRealismModifier(movement, asset, route.Mode, realism);
            movement.Progress = Math.Clamp(movement.Progress + increment, 0, 1);
            movement.Status = movement.Progress >= 1 ? "Completed" : "InTransit";

            var expectedProgress = state.Tick / Math.Max(1.0, route.EstimatedTravelTimeMinutes * 60.0 / state.Scenario.TickRateSeconds);
            var doctrinalFriction = (1.0 - movement.ReportingConfidence) * 7.0
                + (movement.CrewFatigueIndex * 10.0)
                + (asset.MaintenanceBacklog * 1.2);
            movement.EtaDriftMinutes = Math.Round(((expectedProgress - movement.Progress) * route.EstimatedTravelTimeMinutes) + doctrinalFriction, 2);
            if (movement.Status != "Completed" && movement.EtaDriftMinutes > 5)
            {
                movement.Status = "Delayed";
            }

            ConsumeFuel(route.Mode, asset, snapshot, movement, realism);
            EmitPressureEvents(state, movement, asset);

            if (movement.Status == "Completed" && !movement.Delivered)
            {
                movement.Delivered = true;
                foreach (var shipmentId in movement.ShipmentIds)
                {
                    if (!state.ShipmentsById.TryGetValue(shipmentId, out var shipment))
                    {
                        continue;
                    }

                    shipment.DeliveredQuantity = shipment.Quantity;
                    shipment.Status = "Delivered";
                }

                state.Timeline.Add(new TimelineEvent
                {
                    Tick = state.Tick,
                    Timestamp = state.SimulatedTime,
                    EventType = "DeliveryCompleted",
                    Message = $"{movement.MovementId} completed on {movement.RouteId}."
                });
            }
        }

        GenerateIncidents(state);
    }

    private static void UpdateOperationalPressure(
        SimulationRuntimeState state,
        RouteDefinition route,
        MovementRuntime movement,
        AssetRuntime asset,
        EnrichmentSnapshot? snapshot,
        ScenarioRealismDefinition realism)
    {
        var tickHours = state.Scenario.TickRateSeconds / 3600.0;
        movement.CrewFatigueHours += tickHours;

        var fatigueRatio = movement.CrewFatigueHours / Math.Max(1.0, realism.CrewEnduranceHours);
        var assistantDriverRelief = movement.AssistantDriverAssigned ? 0.18 : 0.0;
        movement.CrewFatigueIndex = Math.Clamp((fatigueRatio - assistantDriverRelief) / 1.25, 0.0, 1.0);
        movement.SupportScore = ComputeSupportScore(snapshot);

        var reportingBaseline = realism.ReportingQuality;
        var rhythmBonus = realism.SustainmentRhythmAdherence * 0.08;
        var configuredLoadBonus = movement.ConfiguredLoadQuality * 0.06;
        movement.ReportingConfidence = Math.Clamp(
            reportingBaseline + rhythmBonus + configuredLoadBonus - (movement.CrewFatigueIndex * 0.14) - (asset.MaintenanceBacklog * 0.015),
            0.3,
            0.99);

        movement.ThreatExposure = ComputeThreatExposure(route, snapshot, realism.SecurityDiscipline);

        var maintenanceGrowth = BaseMaintenanceGrowth(route.Mode)
            + (movement.CrewFatigueIndex * 0.08)
            + (movement.ThreatExposure * 0.04)
            - (realism.MaintenanceDiscipline * 0.05)
            - (movement.SupportScore * 0.03);

        asset.MaintenanceBacklog = Math.Clamp(asset.MaintenanceBacklog + maintenanceGrowth, 0.0, 10.0);
        asset.Readiness = Math.Clamp(asset.Readiness - Math.Max(0.0, maintenanceGrowth * 0.01), 0.4, 1.0);
    }

    private static double ComputeGroundModifier(EnrichmentSnapshot snapshot)
    {
        var metrics = snapshot.GroundCorridorMetrics;
        var settlement = snapshot.SettlementProfile;

        var speedFactor = Math.Clamp(metrics.AverageSpeedLimitKph / 70.0, 0.5, 1.4);
        var congestionFactor = Math.Clamp(1.0 - (metrics.CongestionIndex * 0.35), 0.6, 1.0);
        var densityFactor = settlement.DensityBand switch
        {
            PopulationDensityBand.Dense => 0.85,
            PopulationDensityBand.Suburban => 0.93,
            _ => 1.0
        };
        var supportBonus = Math.Min(0.1, (metrics.RefuelStationsPer100Km * 0.01) + (metrics.RestaurantsPer100Km * 0.004) + (metrics.CampgroundsPer100Km * 0.005));

        return Math.Clamp((speedFactor * congestionFactor * densityFactor) + supportBonus, 0.5, 1.5);
    }

    private static double ComputeRealismModifier(
        MovementRuntime movement,
        AssetRuntime asset,
        TransportMode mode,
        ScenarioRealismDefinition realism)
    {
        var fatiguePenalty = 1.0 - (movement.CrewFatigueIndex * 0.22);
        var maintenancePenalty = 1.0 - Math.Min(0.22, asset.MaintenanceBacklog * 0.025);
        var reportingPenalty = 0.9 + (movement.ReportingConfidence * 0.12);
        var rhythmBonus = 0.94 + (realism.SustainmentRhythmAdherence * 0.08);
        var securityPenalty = 1.0 - (movement.ThreatExposure * 0.08);
        var assistantDriverBonus = movement.AssistantDriverAssigned && mode == TransportMode.Ground ? 0.03 : 0.0;

        return Math.Clamp(
            (fatiguePenalty * maintenancePenalty * reportingPenalty * rhythmBonus * securityPenalty) + assistantDriverBonus,
            0.45,
            1.1);
    }

    private static void ConsumeFuel(
        TransportMode mode,
        AssetRuntime asset,
        EnrichmentSnapshot? enrichment,
        MovementRuntime movement,
        ScenarioRealismDefinition realism)
    {
        var baseUse = mode switch
        {
            TransportMode.Ground => 0.35,
            TransportMode.Rail => 0.25,
            TransportMode.Air => 0.8,
            _ => 0.3
        };

        var congestionPenalty = 0.0;
        if (mode == TransportMode.Ground && enrichment is not null)
        {
            congestionPenalty = enrichment.GroundCorridorMetrics.CongestionIndex * 0.2;
        }

        var fatiguePenalty = movement.CrewFatigueIndex * 0.07;
        var maintenancePenalty = asset.MaintenanceBacklog * 0.015;
        var supportRelief = movement.SupportScore * 0.03;
        var disciplineRelief = realism.MaintenanceDiscipline * 0.02;

        asset.FuelState = Math.Max(0, asset.FuelState - (baseUse + congestionPenalty + fatiguePenalty + maintenancePenalty - supportRelief - disciplineRelief));
    }

    private void GenerateIncidents(SimulationRuntimeState state)
    {
        foreach (var movement in state.Movements.Where(m => m.Status is "InTransit" or "Delayed"))
        {
            var seeds = state.Scenario.IncidentSeeds.Where(s => s.RouteId == movement.RouteId);
            foreach (var seed in seeds)
            {
                var uniqueKey = $"{movement.MovementId}:{seed.IncidentType}";
                if (state.IncidentUniqueness.Contains(uniqueKey))
                {
                    continue;
                }

                var route = state.Scenario.Routes.First(r => r.RouteId == movement.RouteId);
                var routeRisk = route.RiskProfile.ToUpperInvariant() switch
                {
                    "HIGH" => 1.35,
                    "MEDIUM" => 1.0,
                    _ => 0.8
                };
                var probabilityPerTick = Math.Clamp(
                    seed.Probability
                    * routeRisk
                    * (1.0 + (movement.CrewFatigueIndex * 0.3))
                    * (1.0 + ((1.0 - movement.ReportingConfidence) * 0.2))
                    * (1.0 + (movement.ThreatExposure * 0.25))
                    * (movement.AssistantDriverAssigned ? 0.96 : 1.0),
                    0.0,
                    0.95);

                if (_rng.NextDouble() > probabilityPerTick)
                {
                    continue;
                }

                state.IncidentUniqueness.Add(uniqueKey);
                var incident = new IncidentRuntime
                {
                    IncidentId = $"inc-{state.Tick}-{movement.MovementId}",
                    IncidentType = seed.IncidentType,
                    Severity = seed.Severity,
                    RouteId = movement.RouteId,
                    TickDetected = state.Tick,
                    CameraRefs = seed.CameraRefs.ToList()
                };

                state.Incidents.Add(incident);
                state.Timeline.Add(new TimelineEvent
                {
                    Tick = state.Tick,
                    Timestamp = state.SimulatedTime,
                    EventType = "Incident",
                    Message = $"{seed.IncidentType} on {movement.RouteId} with severity {seed.Severity}."
                });
            }
        }
    }

    private static double ComputeSupportScore(EnrichmentSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return 0.2;
        }

        var metrics = snapshot.GroundCorridorMetrics;
        var raw = (metrics.RefuelStationsPer100Km * 0.08)
            + (metrics.RestaurantsPer100Km * 0.03)
            + (metrics.CampgroundsPer100Km * 0.1);

        return Math.Clamp(raw, 0.1, 1.0);
    }

    private static double ComputeThreatExposure(RouteDefinition route, EnrichmentSnapshot? snapshot, double securityDiscipline)
    {
        var riskBaseline = route.RiskProfile.ToUpperInvariant() switch
        {
            "HIGH" => 0.7,
            "MEDIUM" => 0.45,
            _ => 0.2
        };

        var terrainPressure = snapshot is null
            ? 0.05
            : (snapshot.GroundCorridorMetrics.CongestionIndex * 0.15) + (snapshot.SettlementProfile.RoadsideDevelopmentIndex * 0.18);

        var disciplineRelief = securityDiscipline * 0.25;
        return Math.Clamp(riskBaseline + terrainPressure - disciplineRelief, 0.05, 0.95);
    }

    private static double BaseMaintenanceGrowth(TransportMode mode)
    {
        return mode switch
        {
            TransportMode.Air => 0.03,
            TransportMode.Rail => 0.015,
            _ => 0.022
        };
    }

    private static void EmitPressureEvents(SimulationRuntimeState state, MovementRuntime movement, AssetRuntime asset)
    {
        EmitUniqueEvent(state, $"{movement.MovementId}:fatigue", movement.CrewFatigueIndex >= 0.65, "FatigueWarning", $"{movement.MovementId} crew fatigue is degrading convoy rhythm.");
        EmitUniqueEvent(state, $"{movement.MovementId}:reporting", movement.ReportingConfidence <= 0.55, "ReportingGap", $"{movement.MovementId} LOGSTAT confidence is low and ETA certainty is slipping.");
        EmitUniqueEvent(state, $"{asset.AssetId}:maintenance", asset.MaintenanceBacklog >= 2.5, "MaintenanceDelay", $"{asset.AssetId} is accumulating maintenance backlog that may require recovery or a repair stop.");
        EmitUniqueEvent(state, $"{asset.AssetId}:fuel", asset.FuelState <= 25, "FuelWarning", $"{asset.AssetId} fuel state is low and resupply timing is becoming critical.");
    }

    private static void EmitUniqueEvent(SimulationRuntimeState state, string key, bool condition, string eventType, string message)
    {
        if (!condition || !state.EventUniqueness.Add(key))
        {
            return;
        }

        state.Timeline.Add(new TimelineEvent
        {
            Tick = state.Tick,
            Timestamp = state.SimulatedTime,
            EventType = eventType,
            Message = message
        });
    }
}
