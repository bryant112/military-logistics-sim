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
            var activeSegment = GetActiveSegment(snapshot, movement.Progress);
            var loadFactor = ComputeLoadFactor(state, movement, asset);
            var terrainFamily = DetermineTerrainFamily(asset.AssetType);

            UpdateOperationalPressure(state, route, movement, asset, snapshot, activeSegment, realism, loadFactor, terrainFamily);

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
                increment *= ComputeGroundModifier(snapshot, activeSegment, movement, terrainFamily, loadFactor);
            }

            increment *= ComputeRealismModifier(movement, asset, route.Mode, realism);
            movement.Progress = Math.Clamp(movement.Progress + increment, 0, 1);
            movement.Status = movement.Progress >= 1 ? "Completed" : "InTransit";

            var expectedProgress = state.Tick / Math.Max(1.0, route.EstimatedTravelTimeMinutes * 60.0 / state.Scenario.TickRateSeconds);
            var doctrinalFriction = (1.0 - movement.ReportingConfidence) * 7.0
                + (movement.CrewFatigueIndex * 10.0)
                + (asset.MaintenanceBacklog * 1.2)
                + (movement.RouteSeverityIndex * 6.0)
                + ((1.0 - movement.Morale) * 5.0);
            movement.EtaDriftMinutes = Math.Round(((expectedProgress - movement.Progress) * route.EstimatedTravelTimeMinutes) + doctrinalFriction, 2);
            if (movement.Status != "Completed" && movement.EtaDriftMinutes > 5)
            {
                movement.Status = "Delayed";
            }

            ConsumeFuel(route.Mode, asset, snapshot, activeSegment, movement, realism, loadFactor, terrainFamily);
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
        RouteSegmentEnrichment? activeSegment,
        ScenarioRealismDefinition realism,
        double loadFactor,
        VehicleTerrainFamily terrainFamily)
    {
        var tickHours = state.Scenario.TickRateSeconds / 3600.0;
        movement.CrewFatigueHours += tickHours;

        movement.SupportScore = ComputeSupportScore(snapshot);
        movement.RouteSeverityIndex = activeSegment?.RouteSeverityIndex ?? snapshot?.GroundCorridorMetrics.RouteSeverityIndex ?? 0.1;
        movement.SurfaceAttritionFactor = activeSegment?.SurfaceAttritionFactor ?? snapshot?.GroundCorridorMetrics.SurfaceAttritionFactor ?? 0.1;
        movement.ConcealmentScore = activeSegment?.ConcealmentFactor ?? snapshot?.GroundCorridorMetrics.ConcealmentOpportunity ?? 0.1;

        var fatigueRatio = movement.CrewFatigueHours / Math.Max(1.0, realism.CrewEnduranceHours);
        var assistantDriverRelief = movement.AssistantDriverAssigned ? 0.18 : 0.0;
        movement.CrewFatigueIndex = Math.Clamp((fatigueRatio - assistantDriverRelief + (movement.RouteSeverityIndex * 0.22) + (movement.SurfaceAttritionFactor * 0.16)) / 1.25, 0.0, 1.0);

        var moralePressure = (movement.RouteSeverityIndex * 0.18)
            + (movement.CrewFatigueIndex * 0.12)
            + ((activeSegment?.DustIndex ?? realism.DustExposure) * 0.06)
            + ((activeSegment?.LocalHostility ?? 0.1) * 0.06)
            - (movement.SupportScore * 0.05)
            - (realism.SecurityDiscipline * 0.04);
        movement.Morale = Math.Clamp((movement.Morale == 0 ? 0.92 : movement.Morale) - moralePressure + 0.02, 0.2, 1.0);

        movement.CargoDamageRisk = ComputeCargoDamageRisk(state, movement, asset, realism, activeSegment, loadFactor, terrainFamily);

        var reportingBaseline = realism.ReportingQuality;
        var rhythmBonus = realism.SustainmentRhythmAdherence * 0.08;
        var configuredLoadBonus = movement.ConfiguredLoadQuality * 0.04;
        var cargoPenalty = movement.CargoDamageRisk * 0.08;
        movement.ReportingConfidence = Math.Clamp(
            reportingBaseline + rhythmBonus + configuredLoadBonus - (movement.CrewFatigueIndex * 0.14) - (asset.MaintenanceBacklog * 0.015) - ((1.0 - movement.Morale) * 0.12) - cargoPenalty,
            0.22,
            0.99);

        movement.ThreatExposure = ComputeThreatExposure(route, snapshot, activeSegment, realism.SecurityDiscipline, movement.ConcealmentScore);

        var maintenanceGrowth = BaseMaintenanceGrowth(route.Mode)
            + (movement.SurfaceAttritionFactor * TerrainMaintenanceSensitivity(terrainFamily))
            + ((loadFactor - 1.0) * 0.025)
            + (movement.CrewFatigueIndex * 0.08)
            + (movement.ThreatExposure * 0.04)
            - (realism.MaintenanceDiscipline * 0.05)
            - (movement.SupportScore * 0.03);

        asset.MaintenanceBacklog = Math.Clamp(asset.MaintenanceBacklog + maintenanceGrowth, 0.0, 10.0);
        asset.Readiness = Math.Clamp(asset.Readiness - Math.Max(0.0, maintenanceGrowth * 0.01), 0.35, 1.0);
    }

    private static double ComputeGroundModifier(
        EnrichmentSnapshot snapshot,
        RouteSegmentEnrichment? activeSegment,
        MovementRuntime movement,
        VehicleTerrainFamily terrainFamily,
        double loadFactor)
    {
        var metrics = snapshot.GroundCorridorMetrics;
        var settlement = snapshot.SettlementProfile;
        var segment = activeSegment ?? snapshot.Segments.FirstOrDefault();

        var speedFactor = Math.Clamp((segment is null ? metrics.AverageSpeedLimitKph : BaseSpeedKph(segment.SurfaceType, segment.ConditionState)) / 70.0, 0.3, 1.4);
        var congestionFactor = Math.Clamp(1.0 - ((segment?.TrafficStress ?? metrics.CongestionIndex) * 0.35), 0.55, 1.0);
        var densityFactor = settlement.DensityBand switch
        {
            PopulationDensityBand.Dense => 0.85,
            PopulationDensityBand.Suburban => 0.93,
            _ => 1.0
        };
        var supportBonus = Math.Min(0.1, (metrics.RefuelStationsPer100Km * 0.01) + (metrics.RestaurantsPer100Km * 0.004) + (metrics.CampgroundsPer100Km * 0.005));
        var terrainAdjustment = TerrainSpeedAdjustment(terrainFamily, segment?.SurfaceType ?? RoadSegmentSurfaceType.SecondaryPaved);
        var moralePenalty = 1.0 - ((1.0 - movement.Morale) * 0.08);
        var loadPenalty = 1.0 - Math.Max(0.0, loadFactor - 1.0) * 0.05;

        return Math.Clamp(((speedFactor * congestionFactor * densityFactor * terrainAdjustment * moralePenalty * loadPenalty) + supportBonus), 0.28, 1.4);
    }

    private static double ComputeRealismModifier(
        MovementRuntime movement,
        AssetRuntime asset,
        TransportMode mode,
        ScenarioRealismDefinition realism)
    {
        var fatiguePenalty = 1.0 - (movement.CrewFatigueIndex * 0.22);
        var moralePenalty = 0.88 + (movement.Morale * 0.16);
        var maintenancePenalty = 1.0 - Math.Min(0.25, asset.MaintenanceBacklog * 0.025);
        var reportingPenalty = 0.9 + (movement.ReportingConfidence * 0.12);
        var rhythmBonus = 0.94 + (realism.SustainmentRhythmAdherence * 0.08);
        var securityPenalty = 1.0 - (movement.ThreatExposure * 0.08);
        var assistantDriverBonus = movement.AssistantDriverAssigned && mode == TransportMode.Ground ? 0.03 : 0.0;

        return Math.Clamp(
            (fatiguePenalty * moralePenalty * maintenancePenalty * reportingPenalty * rhythmBonus * securityPenalty) + assistantDriverBonus,
            0.35,
            1.12);
    }

    private static void ConsumeFuel(
        TransportMode mode,
        AssetRuntime asset,
        EnrichmentSnapshot? enrichment,
        RouteSegmentEnrichment? activeSegment,
        MovementRuntime movement,
        ScenarioRealismDefinition realism,
        double loadFactor,
        VehicleTerrainFamily terrainFamily)
    {
        var baseUse = mode switch
        {
            TransportMode.Ground => 0.35,
            TransportMode.Rail => 0.25,
            TransportMode.Air => 0.8,
            _ => 0.3
        };

        var congestionPenalty = 0.0;
        var surfacePenalty = 0.0;
        if (mode == TransportMode.Ground && enrichment is not null)
        {
            congestionPenalty = (activeSegment?.TrafficStress ?? enrichment.GroundCorridorMetrics.CongestionIndex) * 0.2;
            surfacePenalty = movement.RouteSeverityIndex * 0.16;
            if (terrainFamily == VehicleTerrainFamily.TrackedMilitary && activeSegment?.SurfaceType == RoadSegmentSurfaceType.PavedHighway)
            {
                surfacePenalty += 0.18;
            }
        }

        var fatiguePenalty = movement.CrewFatigueIndex * 0.07;
        var maintenancePenalty = asset.MaintenanceBacklog * 0.015;
        var loadPenalty = Math.Max(0.0, loadFactor - 1.0) * 0.08;
        var supportRelief = movement.SupportScore * 0.03;
        var disciplineRelief = realism.MaintenanceDiscipline * 0.02;

        asset.FuelState = Math.Max(0, asset.FuelState - (baseUse + congestionPenalty + surfacePenalty + fatiguePenalty + maintenancePenalty + loadPenalty - supportRelief - disciplineRelief));
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
                state.EnrichmentByRoute.TryGetValue(route.RouteId, out var snapshot);
                var activeSegment = GetActiveSegment(snapshot, movement.Progress);
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
                    * (1.0 + (movement.RouteSeverityIndex * 0.18))
                    * (1.0 + ((activeSegment?.ChokepointRisk ?? 0.1) * 0.16))
                    * (movement.AssistantDriverAssigned ? 0.96 : 1.0),
                    0.0,
                    0.97);

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

    private static double ComputeThreatExposure(
        RouteDefinition route,
        EnrichmentSnapshot? snapshot,
        RouteSegmentEnrichment? activeSegment,
        double securityDiscipline,
        double concealmentScore)
    {
        var riskBaseline = route.RiskProfile.ToUpperInvariant() switch
        {
            "HIGH" => 0.7,
            "MEDIUM" => 0.45,
            _ => 0.2
        };

        var terrainPressure = snapshot is null
            ? 0.05
            : ((activeSegment?.TrafficStress ?? snapshot.GroundCorridorMetrics.CongestionIndex) * 0.15)
              + (snapshot.SettlementProfile.RoadsideDevelopmentIndex * 0.18)
              + ((activeSegment?.ChokepointRisk ?? 0.1) * 0.1);

        var concealmentRelief = concealmentScore * 0.12;
        var disciplineRelief = securityDiscipline * 0.25;
        return Math.Clamp(riskBaseline + terrainPressure - disciplineRelief - concealmentRelief, 0.05, 0.95);
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
        EmitUniqueEvent(state, $"{movement.MovementId}:surface", movement.RouteSeverityIndex >= 0.68 || movement.SurfaceAttritionFactor >= 0.65, "SurfaceAttritionWarning", $"{movement.MovementId} is traversing high-severity ground that is accelerating wear.");
        EmitUniqueEvent(state, $"{movement.MovementId}:morale", movement.Morale <= 0.5, "MoraleDrop", $"{movement.MovementId} morale is eroding under route severity, fatigue, and local friction.");
        EmitUniqueEvent(state, $"{movement.MovementId}:cargo", movement.CargoDamageRisk >= 0.45, "CargoDamageRisk", $"{movement.MovementId} cargo damage risk is elevated due to route severity and load stress.");
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

    private static RouteSegmentEnrichment? GetActiveSegment(EnrichmentSnapshot? snapshot, double progress)
    {
        if (snapshot is null || snapshot.Segments.Count == 0)
        {
            return null;
        }

        var index = Math.Clamp((int)Math.Floor(progress * snapshot.Segments.Count), 0, snapshot.Segments.Count - 1);
        return snapshot.Segments[index];
    }

    private static double ComputeLoadFactor(SimulationRuntimeState state, MovementRuntime movement, AssetRuntime asset)
    {
        if (movement.ShipmentIds.Count == 0 || asset.PayloadCapacity <= 0)
        {
            return 1.0;
        }

        var totalLoad = 0.0;
        foreach (var shipmentId in movement.ShipmentIds)
        {
            var definition = state.Scenario.Shipments.FirstOrDefault(s => s.ShipmentId == shipmentId);
            if (definition is null)
            {
                continue;
            }

            totalLoad += definition.Weight > 0 ? definition.Weight : Math.Max(1.0, definition.Quantity * CommodityDensityFactor(definition.CommodityType));
        }

        return Math.Clamp(totalLoad / asset.PayloadCapacity, 0.35, 1.75);
    }

    private static double ComputeCargoDamageRisk(
        SimulationRuntimeState state,
        MovementRuntime movement,
        AssetRuntime asset,
        ScenarioRealismDefinition realism,
        RouteSegmentEnrichment? activeSegment,
        double loadFactor,
        VehicleTerrainFamily terrainFamily)
    {
        var commoditySensitivity = movement.ShipmentIds.Count == 0
            ? 0.25
            : movement.ShipmentIds
                .Select(id => state.Scenario.Shipments.FirstOrDefault(s => s.ShipmentId == id)?.CommodityType ?? CommodityType.General)
                .Average(CommodityShockSensitivity);

        var securementBonus = (realism.UseTiedowns ? 0.08 : 0.0)
            + (realism.UseBlockingAndBracing ? 0.08 : 0.0)
            + (realism.UsePalletRestraint ? 0.07 : 0.0)
            + (realism.UseCargoIsolation ? 0.06 : 0.0);

        var planningRelief = realism.UmoPlanningQuality * 0.18;
        var executionRelief = realism.LoadingTeamChiefQuality * 0.18;
        var terrainPenalty = TerrainCargoSensitivity(terrainFamily);
        var segmentSeverity = activeSegment?.RouteSeverityIndex ?? movement.RouteSeverityIndex;
        var segmentAttrition = activeSegment?.SurfaceAttritionFactor ?? movement.SurfaceAttritionFactor;

        return Math.Clamp(
            (segmentSeverity * 0.28)
            + (segmentAttrition * terrainPenalty * 0.28)
            + (Math.Max(0.0, loadFactor - 0.9) * 0.18)
            + (commoditySensitivity * 0.16)
            - planningRelief
            - executionRelief
            - securementBonus,
            0.02,
            0.95);
    }

    private static VehicleTerrainFamily DetermineTerrainFamily(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.ArmoredEscort => VehicleTerrainFamily.TrackedMilitary,
            AssetType.Truck or AssetType.SecurityVehicle => VehicleTerrainFamily.TacticalWheeledMilitary,
            _ => VehicleTerrainFamily.TacticalWheeledMilitary
        };
    }

    private static double TerrainSpeedAdjustment(VehicleTerrainFamily family, RoadSegmentSurfaceType surface)
    {
        return family switch
        {
            VehicleTerrainFamily.TrackedMilitary => surface switch
            {
                RoadSegmentSurfaceType.PavedHighway => 0.84,
                RoadSegmentSurfaceType.SecondaryPaved => 0.9,
                RoadSegmentSurfaceType.ImprovedHybrid => 0.98,
                RoadSegmentSurfaceType.Gravel => 1.04,
                RoadSegmentSurfaceType.Dirt => 1.08,
                RoadSegmentSurfaceType.MudSoftGround => 1.12,
                _ => 1.0
            },
            VehicleTerrainFamily.TacticalWheeledMilitary => surface switch
            {
                RoadSegmentSurfaceType.PavedHighway => 1.0,
                RoadSegmentSurfaceType.SecondaryPaved => 0.96,
                RoadSegmentSurfaceType.ImprovedHybrid => 0.9,
                RoadSegmentSurfaceType.Gravel => 0.82,
                RoadSegmentSurfaceType.Dirt => 0.74,
                RoadSegmentSurfaceType.MudSoftGround => 0.58,
                _ => 1.0
            },
            VehicleTerrainFamily.CivilianFreight => surface switch
            {
                RoadSegmentSurfaceType.PavedHighway => 1.0,
                RoadSegmentSurfaceType.SecondaryPaved => 0.92,
                RoadSegmentSurfaceType.ImprovedHybrid => 0.78,
                RoadSegmentSurfaceType.Gravel => 0.65,
                RoadSegmentSurfaceType.Dirt => 0.52,
                RoadSegmentSurfaceType.MudSoftGround => 0.3,
                _ => 1.0
            },
            _ => surface switch
            {
                RoadSegmentSurfaceType.PavedHighway => 1.02,
                RoadSegmentSurfaceType.SecondaryPaved => 0.95,
                RoadSegmentSurfaceType.ImprovedHybrid => 0.82,
                RoadSegmentSurfaceType.Gravel => 0.7,
                RoadSegmentSurfaceType.Dirt => 0.56,
                RoadSegmentSurfaceType.MudSoftGround => 0.32,
                _ => 1.0
            }
        };
    }

    private static double TerrainMaintenanceSensitivity(VehicleTerrainFamily family)
    {
        return family switch
        {
            VehicleTerrainFamily.TrackedMilitary => 0.16,
            VehicleTerrainFamily.TacticalWheeledMilitary => 0.13,
            VehicleTerrainFamily.CivilianFreight => 0.15,
            _ => 0.18
        };
    }

    private static double TerrainCargoSensitivity(VehicleTerrainFamily family)
    {
        return family switch
        {
            VehicleTerrainFamily.TrackedMilitary => 1.12,
            VehicleTerrainFamily.TacticalWheeledMilitary => 0.95,
            VehicleTerrainFamily.CivilianFreight => 1.0,
            _ => 1.08
        };
    }

    private static double CommodityDensityFactor(CommodityType commodityType)
    {
        return commodityType switch
        {
            CommodityType.Fuel => 1.1,
            CommodityType.Ammo => 1.25,
            CommodityType.Medical => 0.55,
            CommodityType.Rations => 0.8,
            _ => 0.9
        };
    }

    private static double CommodityShockSensitivity(CommodityType commodityType)
    {
        return commodityType switch
        {
            CommodityType.Medical => 0.8,
            CommodityType.Ammo => 0.62,
            CommodityType.Fuel => 0.48,
            CommodityType.Rations => 0.35,
            _ => 0.4
        };
    }

    private static double BaseSpeedKph(RoadSegmentSurfaceType surface, SurfaceConditionState condition)
    {
        var baseSpeed = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 95.0,
            RoadSegmentSurfaceType.SecondaryPaved => 78.0,
            RoadSegmentSurfaceType.ImprovedHybrid => 58.0,
            RoadSegmentSurfaceType.Gravel => 46.0,
            RoadSegmentSurfaceType.Dirt => 38.0,
            _ => 24.0
        };

        var modifier = condition switch
        {
            SurfaceConditionState.Dry => 1.0,
            SurfaceConditionState.Wet => 0.9,
            SurfaceConditionState.Dusty => 0.88,
            SurfaceConditionState.Degraded => 0.82,
            SurfaceConditionState.ReducedTraction => 0.84,
            SurfaceConditionState.Muddy => 0.62,
            _ => 1.0
        };

        return baseSpeed * modifier;
    }

    private enum VehicleTerrainFamily
    {
        CivilianLight,
        CivilianFreight,
        TacticalWheeledMilitary,
        TrackedMilitary
    }
}
