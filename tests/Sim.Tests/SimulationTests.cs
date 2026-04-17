using Sim.Application;
using Sim.Contracts;
using Sim.Domain;
using Sim.Infrastructure;

namespace Sim.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void SameSeedAndSnapshot_ProducesDeterministicState()
    {
        var scenario = BuildScenario();

        var leftManager = new SimulationSessionManager(new MockEnrichmentProvider());
        var rightManager = new SimulationSessionManager(new MockEnrichmentProvider());

        var left = leftManager.CreateSession(scenario, 1234);
        var right = rightManager.CreateSession(scenario, 1234);

        leftManager.Start(left.SessionId);
        rightManager.Start(right.SessionId);

        for (var i = 0; i < 120; i++)
        {
            leftManager.AdvanceRunningSessions();
            rightManager.AdvanceRunningSessions();
        }

        var leftState = leftManager.GetWorldState(left.SessionId);
        var rightState = rightManager.GetWorldState(right.SessionId);

        Assert.Equal(leftState.Tick, rightState.Tick);
        Assert.Equal(leftState.Movements.Select(m => (m.MovementId, m.Progress, m.Status)), rightState.Movements.Select(m => (m.MovementId, m.Progress, m.Status)));
        Assert.Equal(leftState.Incidents.Count, rightState.Incidents.Count);
    }

    [Fact]
    public void GroundRailAirModes_AllAdvanceInFirstLookSlice()
    {
        var scenario = BuildScenario();
        var manager = new SimulationSessionManager(new MockEnrichmentProvider());
        var session = manager.CreateSession(scenario, 42);

        manager.Start(session.SessionId);
        for (var i = 0; i < 80; i++)
        {
            manager.AdvanceRunningSessions();
        }

        var state = manager.GetWorldState(session.SessionId);
        Assert.Contains(state.Movements, m => m.Mode == TransportMode.Ground.ToString() && m.Progress > 0);
        Assert.Contains(state.Movements, m => m.Mode == TransportMode.Rail.ToString() && m.Progress > 0);
        Assert.Contains(state.Movements, m => m.Mode == TransportMode.Air.ToString() && m.Progress > 0);
    }

    [Fact]
    public void GroundEnrichment_ModifiesProgressPredictably()
    {
        var scenario = BuildScenario();

        var fastManager = new SimulationSessionManager(new StaticEnrichmentProvider(0.1, PopulationDensityBand.Sparse));
        var slowManager = new SimulationSessionManager(new StaticEnrichmentProvider(0.9, PopulationDensityBand.Dense));

        var fast = fastManager.CreateSession(scenario, 7);
        var slow = slowManager.CreateSession(scenario, 7);

        fastManager.Start(fast.SessionId);
        slowManager.Start(slow.SessionId);

        for (var i = 0; i < 80; i++)
        {
            fastManager.AdvanceRunningSessions();
            slowManager.AdvanceRunningSessions();
        }

        var fastGround = fastManager.GetWorldState(fast.SessionId).Movements.First(m => m.RouteId == "ground-1");
        var slowGround = slowManager.GetWorldState(slow.SessionId).Movements.First(m => m.RouteId == "ground-1");

        Assert.True(fastGround.Progress > slowGround.Progress);
    }

    [Fact]
    public void HighProbabilityIncident_EmitsTimelineEvent()
    {
        var scenario = BuildScenario();
        scenario.IncidentSeeds =
        [
            new IncidentSeedDefinition
            {
                IncidentType = IncidentType.Ambush,
                Severity = Severity.High,
                Probability = 1.0,
                RouteId = "ground-1",
                CameraRefs = ["drone-01", "helmet-alpha"]
            }
        ];

        var manager = new SimulationSessionManager(new MockEnrichmentProvider());
        var session = manager.CreateSession(scenario, 22);

        manager.Start(session.SessionId);
        for (var i = 0; i < 4; i++)
        {
            manager.AdvanceRunningSessions();
        }

        var timeline = manager.GetTimeline(session.SessionId);
        Assert.Contains(timeline.Events, e => e.EventType == "Incident");
    }

    private static ScenarioDefinition BuildScenario()
    {
        return new ScenarioDefinition
        {
            ScenarioId = "test-scenario",
            Name = "Test Scenario",
            StartTime = DateTimeOffset.Parse("2026-04-10T08:00:00Z"),
            TickRateSeconds = 5,
            DurationMinutes = 180,
            TerrainReference = "starter-map-01",
            RulesetId = "baseline-logistics-v1",
            Nodes =
            [
                new NodeDefinition { NodeId = "depot-alpha", NodeType = NodeType.Depot, Name = "Depot Alpha", Lat = 36.1, Lon = -85.6, Capacity = 10000 },
                new NodeDefinition { NodeId = "warehouse-bravo", NodeType = NodeType.Warehouse, Name = "Warehouse Bravo", Lat = 36.2, Lon = -85.45, Capacity = 6000 },
                new NodeDefinition { NodeId = "fob-charlie", NodeType = NodeType.FOB, Name = "FOB Charlie", Lat = 36.3, Lon = -85.25, Capacity = 2500 },
                new NodeDefinition { NodeId = "base-delta", NodeType = NodeType.Base, Name = "Base Delta", Lat = 36.4, Lon = -85.05, Capacity = 12000 }
            ],
            Routes =
            [
                new RouteDefinition { RouteId = "ground-1", Mode = TransportMode.Ground, StartNodeId = "depot-alpha", EndNodeId = "fob-charlie", EstimatedTravelTimeMinutes = 90, RiskProfile = "Medium" },
                new RouteDefinition { RouteId = "rail-1", Mode = TransportMode.Rail, StartNodeId = "warehouse-bravo", EndNodeId = "base-delta", EstimatedTravelTimeMinutes = 120, RiskProfile = "Low" },
                new RouteDefinition { RouteId = "air-1", Mode = TransportMode.Air, StartNodeId = "base-delta", EndNodeId = "fob-charlie", EstimatedTravelTimeMinutes = 30, RiskProfile = "Medium" }
            ],
            Assets =
            [
                new AssetDefinition { AssetId = "truck-01", AssetType = AssetType.Truck, FuelState = 100, Readiness = 0.95, PayloadCapacity = 2000 },
                new AssetDefinition { AssetId = "train-01", AssetType = AssetType.TrainLocomotive, FuelState = 100, Readiness = 0.97, PayloadCapacity = 8000 },
                new AssetDefinition { AssetId = "air-01", AssetType = AssetType.CargoAircraft, FuelState = 100, Readiness = 0.93, PayloadCapacity = 3000 }
            ],
            Shipments =
            [
                new ShipmentDefinition { ShipmentId = "ship-g", CommodityType = CommodityType.Rations, Quantity = 1000, OriginNodeId = "depot-alpha", DestinationNodeId = "fob-charlie" },
                new ShipmentDefinition { ShipmentId = "ship-r", CommodityType = CommodityType.Fuel, Quantity = 600, OriginNodeId = "warehouse-bravo", DestinationNodeId = "base-delta" },
                new ShipmentDefinition { ShipmentId = "ship-a", CommodityType = CommodityType.Medical, Quantity = 200, OriginNodeId = "base-delta", DestinationNodeId = "fob-charlie" }
            ],
            IncidentSeeds =
            [
                new IncidentSeedDefinition { IncidentType = IncidentType.Ambush, Severity = Severity.Medium, RouteId = "ground-1", Probability = 0.4, CameraRefs = ["drone-01"] }
            ]
        };
    }

    private sealed class StaticEnrichmentProvider : IEnrichmentProvider
    {
        private readonly double _congestion;
        private readonly PopulationDensityBand _band;

        public StaticEnrichmentProvider(double congestion, PopulationDensityBand band)
        {
            _congestion = congestion;
            _band = band;
        }

        public EnrichmentSnapshot BuildSnapshot(ScenarioDefinition scenario, RouteDefinition route)
        {
            return new EnrichmentSnapshot
            {
                RouteId = route.RouteId,
                GroundCorridorMetrics = new GroundCorridorMetrics
                {
                    RouteId = route.RouteId,
                    AverageSpeedLimitKph = 70,
                    CongestionIndex = _congestion,
                    RefuelStationsPer100Km = 2,
                    RestaurantsPer100Km = 2,
                    CampgroundsPer100Km = 1,
                    TrafficVehiclesPerHour = 1000
                },
                SettlementProfile = new SettlementProfile
                {
                    DensityBand = _band,
                    PopulationPerSqKm = _band == PopulationDensityBand.Dense ? 1200 : 40,
                    BuiltUpCoveragePercent = _band == PopulationDensityBand.Dense ? 70 : 10,
                    RoadsideDevelopmentIndex = _band == PopulationDensityBand.Dense ? 0.9 : 0.2
                },
                GeneratedStructureSummary = new GeneratedStructureSummary
                {
                    RouteId = route.RouteId,
                    EstimatedStructureCount = _band == PopulationDensityBand.Dense ? 250 : 35,
                    EstimatedHousingClusters = _band == PopulationDensityBand.Dense ? 15 : 3,
                    EstimatedServiceStops = _band == PopulationDensityBand.Dense ? 25 : 4,
                    EstimatedSettlementFootprints = _band == PopulationDensityBand.Dense ? 8 : 2
                }
            };
        }
    }
}
