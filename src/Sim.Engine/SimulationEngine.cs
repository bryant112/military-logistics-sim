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

            var baseIncrement = state.Scenario.TickRateSeconds / Math.Max(1.0, route.EstimatedTravelTimeMinutes * 60.0);
            var modeFactor = route.Mode switch
            {
                TransportMode.Ground => 1.0,
                TransportMode.Rail => 0.95,
                TransportMode.Air => 1.2,
                _ => 1.0
            };

            var increment = baseIncrement * modeFactor;
            if (route.Mode == TransportMode.Ground && state.EnrichmentByRoute.TryGetValue(route.RouteId, out var snapshot))
            {
                increment *= ComputeGroundModifier(snapshot);
            }

            movement.Progress = Math.Clamp(movement.Progress + increment, 0, 1);
            movement.Status = movement.Progress >= 1 ? "Completed" : "InTransit";

            var expectedProgress = state.Tick / Math.Max(1.0, route.EstimatedTravelTimeMinutes * 60.0 / state.Scenario.TickRateSeconds);
            movement.EtaDriftMinutes = Math.Round((expectedProgress - movement.Progress) * route.EstimatedTravelTimeMinutes, 2);
            if (movement.Status != "Completed" && movement.EtaDriftMinutes > 5)
            {
                movement.Status = "Delayed";
            }

            ConsumeFuel(route.Mode, asset, state.EnrichmentByRoute.TryGetValue(route.RouteId, out var enr) ? enr : null);

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

    private static void ConsumeFuel(TransportMode mode, AssetRuntime asset, EnrichmentSnapshot? enrichment)
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

        asset.FuelState = Math.Max(0, asset.FuelState - (baseUse + congestionPenalty));
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

                var probabilityPerTick = Math.Clamp(seed.Probability, 0.0, 1.0);
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
}
