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
        Assert.Equal(leftState.Movements.Select(m => (m.CrewFatigueIndex, m.ReportingConfidence)), rightState.Movements.Select(m => (m.CrewFatigueIndex, m.ReportingConfidence)));
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
    public void AssistantDriverAndBetterDiscipline_ReduceFatigueDrag()
    {
        var supportedScenario = BuildScenario();
        supportedScenario.Realism = new ScenarioRealismDefinition
        {
            ReportingQuality = 0.85,
            SustainmentRhythmAdherence = 0.85,
            ConfiguredLoadQuality = 0.85,
            MaintenanceDiscipline = 0.85,
            SecurityDiscipline = 0.8,
            UmoPlanningQuality = 0.88,
            LoadingTeamChiefQuality = 0.86,
            WeatherSeverity = 0.15,
            DustExposure = 0.1,
            CrewEnduranceHours = 1.0,
            UseAssistantDrivers = true,
            UseTiedowns = true,
            UseBlockingAndBracing = true,
            UsePalletRestraint = true,
            UseCargoIsolation = true
        };

        var strainedScenario = BuildScenario();
        strainedScenario.Realism = new ScenarioRealismDefinition
        {
            ReportingQuality = 0.45,
            SustainmentRhythmAdherence = 0.45,
            ConfiguredLoadQuality = 0.4,
            MaintenanceDiscipline = 0.4,
            SecurityDiscipline = 0.45,
            UmoPlanningQuality = 0.35,
            LoadingTeamChiefQuality = 0.3,
            WeatherSeverity = 0.65,
            DustExposure = 0.7,
            CrewEnduranceHours = 1.0,
            UseAssistantDrivers = false,
            UseTiedowns = false,
            UseBlockingAndBracing = false,
            UsePalletRestraint = false,
            UseCargoIsolation = false
        };

        var supportedManager = new SimulationSessionManager(new MockEnrichmentProvider());
        var strainedManager = new SimulationSessionManager(new MockEnrichmentProvider());

        var supported = supportedManager.CreateSession(supportedScenario, 77);
        var strained = strainedManager.CreateSession(strainedScenario, 77);

        supportedManager.Start(supported.SessionId);
        strainedManager.Start(strained.SessionId);

        for (var i = 0; i < 900; i++)
        {
            supportedManager.AdvanceRunningSessions();
            strainedManager.AdvanceRunningSessions();
        }

        var supportedMovement = supportedManager.GetWorldState(supported.SessionId).Movements.First(m => m.RouteId == "ground-1");
        var strainedMovement = strainedManager.GetWorldState(strained.SessionId).Movements.First(m => m.RouteId == "ground-1");

        Assert.True(supportedMovement.CrewFatigueIndex < strainedMovement.CrewFatigueIndex);
        Assert.True(supportedMovement.Progress > strainedMovement.Progress);
    }

    [Fact]
    public void PressureSignals_SurfaceInWorldState()
    {
        var scenario = BuildScenario();
        scenario.Realism.CrewEnduranceHours = 0.5;
        scenario.Realism.ReportingQuality = 0.4;
        scenario.Realism.MaintenanceDiscipline = 0.35;

        var manager = new SimulationSessionManager(new MockEnrichmentProvider());
        var session = manager.CreateSession(scenario, 55);

        manager.Start(session.SessionId);
        for (var i = 0; i < 900; i++)
        {
            manager.AdvanceRunningSessions();
        }

        var state = manager.GetWorldState(session.SessionId);
        var timeline = manager.GetTimeline(session.SessionId);

        Assert.True(state.Overview.AverageCrewFatigueIndex > 0);
        Assert.True(state.Overview.AverageRouteSeverityIndex > 0);
        Assert.Contains(state.Assets, a => a.MaintenanceBacklog > 0);
        Assert.Contains(timeline.Events, e => e.EventType is "FatigueWarning" or "ReportingGap" or "MaintenanceDelay" or "SurfaceAttritionWarning" or "CargoDamageRisk");
    }

    [Fact]
    public void AoiPlanner_BuildsUsaOnlyObjectivesAndSupportZones()
    {
        var planner = new MockAoiPlanningService(Path.Combine(ResolveWorkspaceRoot(), "docs", "data", "upper-cumberland-realworld.json"));
        var response = planner.PlanArea(new AoiPlanningRequest
        {
            CenterLat = 36.1627,
            CenterLon = -85.5016,
            RadiusMiles = 30,
            Seed = 42,
            Criteria = new SituationCriteriaDto
            {
                InfrastructurePriority = 0.7,
                CivilFriction = 0.4,
                GovernmentFriendliness = 0.55,
                ThreatLevel = 0.5,
                WeatherStress = 0.2,
                PropagandaFactor = 0.35
            }
        });

        Assert.True(response.IsWithinUsa);
        Assert.NotEmpty(response.Objectives);
        Assert.NotEmpty(response.SupportZones);
        Assert.True(response.Transportation.IsImportedRegionalSnapshot);
        Assert.Contains("Putnam", response.Transportation.Counties);
        Assert.NotEmpty(response.Transportation.CountyAllegiances);
        Assert.NotEmpty(response.Transportation.TransportProfiles);
        Assert.Contains(response.Transportation.TransportProfiles, profile => profile.Category == "Civilian");
        Assert.Contains(response.Transportation.TransportProfiles, profile => profile.Category == "Military");
        Assert.Contains(response.SupportZones, zone => !string.IsNullOrWhiteSpace(zone.CountyName) && zone.Allegiance is "BLUFOR" or "OPFOR" or "Contested");
        Assert.Contains(response.Transportation.FeatureHighlights, feature => feature.Name.Contains("Upper Cumberland Regional Airport", StringComparison.OrdinalIgnoreCase));
        Assert.All(response.SupportZones, z => Assert.Equal(10.0, z.AreaSquareMiles));
    }

    [Fact]
    public void HigherPropagandaFactor_PushesUpperCumberlandFurtherTowardOpfor()
    {
        var planner = new MockAoiPlanningService(Path.Combine(ResolveWorkspaceRoot(), "docs", "data", "upper-cumberland-realworld.json"));
        var lowPropaganda = planner.PlanArea(new AoiPlanningRequest
        {
            CenterLat = 36.1627,
            CenterLon = -85.5016,
            RadiusMiles = 30,
            Seed = 42,
            Criteria = new SituationCriteriaDto
            {
                InfrastructurePriority = 0.7,
                CivilFriction = 0.35,
                GovernmentFriendliness = 0.55,
                ThreatLevel = 0.4,
                WeatherStress = 0.2,
                PropagandaFactor = 0.1
            }
        });

        var highPropaganda = planner.PlanArea(new AoiPlanningRequest
        {
            CenterLat = 36.1627,
            CenterLon = -85.5016,
            RadiusMiles = 30,
            Seed = 42,
            Criteria = new SituationCriteriaDto
            {
                InfrastructurePriority = 0.7,
                CivilFriction = 0.35,
                GovernmentFriendliness = 0.55,
                ThreatLevel = 0.4,
                WeatherStress = 0.2,
                PropagandaFactor = 0.9
            }
        });

        Assert.True(highPropaganda.SupportZones.Average(zone => zone.OpforSupport) > lowPropaganda.SupportZones.Average(zone => zone.OpforSupport));
        Assert.True(highPropaganda.SupportZones.Average(zone => zone.BluforSupport) < lowPropaganda.SupportZones.Average(zone => zone.BluforSupport));
    }

    [Fact]
    public void Sitrep_ReturnsPinsForTrackedMovements()
    {
        var scenario = BuildScenario();
        var manager = new SimulationSessionManager(new MockEnrichmentProvider());
        var session = manager.CreateSession(scenario, 91);

        manager.Start(session.SessionId);
        for (var i = 0; i < 120; i++)
        {
            manager.AdvanceRunningSessions();
        }

        var sitrep = manager.GetSitrep(session.SessionId);

        Assert.NotEmpty(sitrep.MovementPins);
        Assert.Contains(sitrep.MovementPins, pin => pin.PinTone is "Green" or "Amber" or "Red");
    }

    [Fact]
    public void ImportedTransportProfiles_SurfaceGroundedCivilianAndMilitaryMetrics()
    {
        var planner = new MockAoiPlanningService(Path.Combine(ResolveWorkspaceRoot(), "docs", "data", "upper-cumberland-realworld.json"));
        var response = planner.PlanArea(new AoiPlanningRequest
        {
            CenterLat = 36.1627,
            CenterLon = -85.5016,
            RadiusMiles = 30,
            Seed = 42,
            Criteria = new SituationCriteriaDto()
        });

        var civilian = Assert.Single(response.Transportation.TransportProfiles.Where(profile => profile.ProfileId == "civilian-midsize-sedan"));
        var military = Assert.Single(response.Transportation.TransportProfiles.Where(profile => profile.ProfileId == "military-m1a2-abrams"));

        Assert.True(civilian.MaintenanceCostUsdPer1000Miles > 0);
        Assert.True(civilian.FuelEconomyMpg > 0);
        Assert.True(military.FuelBurnGallonsPerHourRoad > 0);
        Assert.True(military.SurvivabilityScore > civilian.SurvivabilityScore);
    }

    [Fact]
    public void RoughSegmentsIncreaseRouteSeverityAndSlowGroundMovement()
    {
        var scenario = BuildScenario();

        var fastManager = new SimulationSessionManager(new StaticEnrichmentProvider(0.12, PopulationDensityBand.Sparse, 0.18, 0.14, 0.12));
        var roughManager = new SimulationSessionManager(new StaticEnrichmentProvider(0.7, PopulationDensityBand.Sparse, 0.74, 0.72, 0.48));

        var fast = fastManager.CreateSession(scenario, 9);
        var rough = roughManager.CreateSession(scenario, 9);

        fastManager.Start(fast.SessionId);
        roughManager.Start(rough.SessionId);

        for (var i = 0; i < 140; i++)
        {
            fastManager.AdvanceRunningSessions();
            roughManager.AdvanceRunningSessions();
        }

        var fastGround = fastManager.GetWorldState(fast.SessionId).Movements.First(m => m.RouteId == "ground-1");
        var roughGround = roughManager.GetWorldState(rough.SessionId).Movements.First(m => m.RouteId == "ground-1");

        Assert.True(roughGround.RouteSeverityIndex > fastGround.RouteSeverityIndex);
        Assert.True(roughGround.SurfaceAttritionFactor > fastGround.SurfaceAttritionFactor);
        Assert.True(fastGround.Progress > roughGround.Progress);
    }

    [Fact]
    public void BetterLoadDisciplineReducesCargoDamageRisk()
    {
        var disciplinedScenario = BuildScenario();
        disciplinedScenario.Realism.UmoPlanningQuality = 0.9;
        disciplinedScenario.Realism.LoadingTeamChiefQuality = 0.88;
        disciplinedScenario.Realism.UseTiedowns = true;
        disciplinedScenario.Realism.UseBlockingAndBracing = true;
        disciplinedScenario.Realism.UsePalletRestraint = true;
        disciplinedScenario.Realism.UseCargoIsolation = true;

        var sloppyScenario = BuildScenario();
        sloppyScenario.Realism.UmoPlanningQuality = 0.3;
        sloppyScenario.Realism.LoadingTeamChiefQuality = 0.28;
        sloppyScenario.Realism.UseTiedowns = false;
        sloppyScenario.Realism.UseBlockingAndBracing = false;
        sloppyScenario.Realism.UsePalletRestraint = false;
        sloppyScenario.Realism.UseCargoIsolation = false;
        sloppyScenario.Realism.WeatherSeverity = 0.6;
        sloppyScenario.Realism.DustExposure = 0.65;

        var provider = new StaticEnrichmentProvider(0.45, PopulationDensityBand.Suburban, 0.68, 0.7, 0.24);
        var disciplinedManager = new SimulationSessionManager(provider);
        var sloppyManager = new SimulationSessionManager(provider);

        var disciplined = disciplinedManager.CreateSession(disciplinedScenario, 71);
        var sloppy = sloppyManager.CreateSession(sloppyScenario, 71);

        disciplinedManager.Start(disciplined.SessionId);
        sloppyManager.Start(sloppy.SessionId);

        for (var i = 0; i < 90; i++)
        {
            disciplinedManager.AdvanceRunningSessions();
            sloppyManager.AdvanceRunningSessions();
        }

        var disciplinedGround = disciplinedManager.GetWorldState(disciplined.SessionId).Movements.First(m => m.RouteId == "ground-1");
        var sloppyGround = sloppyManager.GetWorldState(sloppy.SessionId).Movements.First(m => m.RouteId == "ground-1");

        Assert.True(disciplinedGround.CargoDamageRisk < sloppyGround.CargoDamageRisk);
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

    [Fact]
    public async Task LiveWeatherRefresh_UpdatesSessionWeatherState()
    {
        var scenario = BuildScenario();
        var weatherService = new SequenceWeatherService(
            BuildWeatherSnapshot(0.18, "Clear", 72, 8, 10),
            BuildWeatherSnapshot(0.68, "Strong thunderstorms", 84, 28, 80));

        var manager = new SimulationSessionManager(new MockEnrichmentProvider(), weatherService);
        var session = manager.CreateSession(scenario, 14);

        var first = await manager.RefreshWeatherAsync(session.SessionId, true);
        var second = await manager.RefreshWeatherAsync(session.SessionId, true);

        Assert.Equal("NOAA stub", first.Weather.Source);
        Assert.True(second.CurrentWeatherSeverity > first.CurrentWeatherSeverity);
        Assert.Equal("Rough", second.CurrentWeatherBand);

        var state = manager.GetWorldState(session.SessionId);
        Assert.Equal(second.CurrentWeatherSeverity, state.WorldData.CurrentWeatherSeverity);
        Assert.Equal("Strong thunderstorms", state.WorldData.Weather.Summary);
    }

    [Fact]
    public async Task DevFeatureToggle_CanDisableAutomaticLiveWeather()
    {
        var scenario = BuildScenario();
        var weatherService = new SequenceWeatherService(BuildWeatherSnapshot(0.55, "Rain showers", 64, 15, 60));
        var manager = new SimulationSessionManager(new MockEnrichmentProvider(), weatherService);
        var session = manager.CreateSession(scenario, 27);

        await manager.RefreshWeatherAsync(session.SessionId, true);
        var before = manager.GetWorldDataStatus(session.SessionId);

        manager.UpdateDevFeatures(session.SessionId, new SessionDevFeatureFlagsDto
        {
            UseRealWorldWeather = false,
            AutoWeatherRefreshEnabled = false,
            AllowManualWorldDataRefresh = true,
            AllowManualWeatherRefresh = true,
            FreezeStaticRwdDuringRun = true,
            UseMockWeatherFallback = true
        });

        var after = await manager.RefreshWeatherAsync(session.SessionId, false);

        Assert.Equal(before.LastWeatherRefreshAt, after.LastWeatherRefreshAt);
        Assert.False(after.DevFeatures.UseRealWorldWeather);
        Assert.False(after.AutoWeatherRefreshEnabled);
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
            Realism = new ScenarioRealismDefinition
            {
                ReportingQuality = 0.75,
                SustainmentRhythmAdherence = 0.8,
                ConfiguredLoadQuality = 0.78,
                MaintenanceDiscipline = 0.8,
                SecurityDiscipline = 0.75,
                UmoPlanningQuality = 0.74,
                LoadingTeamChiefQuality = 0.72,
                WeatherSeverity = 0.22,
                DustExposure = 0.25,
                CrewEnduranceHours = 10.0,
                UseAssistantDrivers = true,
                UseTiedowns = true,
                UseBlockingAndBracing = true,
                UsePalletRestraint = true,
                UseCargoIsolation = false
            },
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
                new ShipmentDefinition { ShipmentId = "ship-g", CommodityType = CommodityType.Rations, Quantity = 1000, Weight = 1200, OriginNodeId = "depot-alpha", DestinationNodeId = "fob-charlie" },
                new ShipmentDefinition { ShipmentId = "ship-r", CommodityType = CommodityType.Fuel, Quantity = 600, Weight = 1800, OriginNodeId = "warehouse-bravo", DestinationNodeId = "base-delta" },
                new ShipmentDefinition { ShipmentId = "ship-a", CommodityType = CommodityType.Medical, Quantity = 200, Weight = 240, OriginNodeId = "base-delta", DestinationNodeId = "fob-charlie" }
            ],
            IncidentSeeds =
            [
                new IncidentSeedDefinition { IncidentType = IncidentType.Ambush, Severity = Severity.Medium, RouteId = "ground-1", Probability = 0.4, CameraRefs = ["drone-01"] }
            ]
        };
    }

    private static string ResolveWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MilitaryLogisticsSim.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate workspace root.");
    }

    private sealed class StaticEnrichmentProvider : IEnrichmentProvider
    {
        private readonly double _congestion;
        private readonly PopulationDensityBand _band;
        private readonly double _routeSeverity;
        private readonly double _surfaceAttrition;
        private readonly double _concealment;

        public StaticEnrichmentProvider(double congestion, PopulationDensityBand band, double routeSeverity = 0.24, double surfaceAttrition = 0.2, double concealment = 0.18)
        {
            _congestion = congestion;
            _band = band;
            _routeSeverity = routeSeverity;
            _surfaceAttrition = surfaceAttrition;
            _concealment = concealment;
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
                    TrafficVehiclesPerHour = 1000,
                    RouteSeverityIndex = _routeSeverity,
                    SurfaceAttritionFactor = _surfaceAttrition,
                    MoralePressure = _routeSeverity * 0.65,
                    CargoDamageRisk = _surfaceAttrition * 0.55,
                    ConcealmentOpportunity = _concealment
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
                },
                Segments =
                [
                    new RouteSegmentEnrichment
                    {
                        SegmentId = $"{route.RouteId}-seg-01",
                        Sequence = 1,
                        LengthMiles = 9,
                        SurfaceType = _routeSeverity >= 0.65 ? RoadSegmentSurfaceType.Dirt : RoadSegmentSurfaceType.SecondaryPaved,
                        ConditionState = _routeSeverity >= 0.65 ? SurfaceConditionState.Muddy : SurfaceConditionState.Dry,
                        IsHighway = _routeSeverity < 0.4,
                        ConnectsHighway = true,
                        TrafficStress = _congestion,
                        LocalHostility = _routeSeverity * 0.5,
                        DustIndex = _routeSeverity >= 0.65 ? 0.35 : 0.08,
                        RouteSeverityIndex = _routeSeverity,
                        SurfaceAttritionFactor = _surfaceAttrition,
                        ConcealmentFactor = _concealment,
                        ChokepointRisk = _routeSeverity * 0.45
                    }
                ]
            };
        }
    }

    private static RealWorldWeatherSnapshotDto BuildWeatherSnapshot(double severity, string summary, int temperatureF, int windMph, int precipChancePercent)
    {
        return new RealWorldWeatherSnapshotDto
        {
            QueryLat = 36.16,
            QueryLon = -85.5,
            Summary = summary,
            DetailedForecast = summary,
            TemperatureF = temperatureF,
            TemperatureTrend = "Steady",
            WindSpeedMph = windMph,
            WindDirection = "SW",
            PrecipitationChancePercent = precipChancePercent,
            Severity = severity,
            SeverityBand = severity switch
            {
                <= 0.25 => "Nominal",
                <= 0.5 => "Watch",
                <= 0.75 => "Rough",
                _ => "Severe"
            },
            ObservedAt = DateTimeOffset.UtcNow,
            Source = "NOAA stub",
            GridId = "OHX",
            ForecastOfficeUrl = "https://api.weather.gov/offices/OHX"
        };
    }

    private sealed class SequenceWeatherService : IRealWorldWeatherService
    {
        private readonly Queue<RealWorldWeatherSnapshotDto> _snapshots;
        private RealWorldWeatherSnapshotDto _last;

        public SequenceWeatherService(params RealWorldWeatherSnapshotDto[] snapshots)
        {
            _snapshots = new Queue<RealWorldWeatherSnapshotDto>(snapshots);
            _last = snapshots.LastOrDefault() ?? BuildWeatherSnapshot(0.2, "Fallback", 70, 5, 10);
        }

        public Task<RealWorldWeatherSnapshotDto> GetCurrentWeatherAsync(double lat, double lon, CancellationToken cancellationToken = default)
        {
            if (_snapshots.Count > 0)
            {
                _last = _snapshots.Dequeue();
            }

            return Task.FromResult(new RealWorldWeatherSnapshotDto
            {
                QueryLat = lat,
                QueryLon = lon,
                Summary = _last.Summary,
                DetailedForecast = _last.DetailedForecast,
                TemperatureF = _last.TemperatureF,
                TemperatureTrend = _last.TemperatureTrend,
                WindSpeedMph = _last.WindSpeedMph,
                WindDirection = _last.WindDirection,
                PrecipitationChancePercent = _last.PrecipitationChancePercent,
                Severity = _last.Severity,
                SeverityBand = _last.SeverityBand,
                ObservedAt = DateTimeOffset.UtcNow,
                Source = _last.Source,
                GridId = _last.GridId,
                ForecastOfficeUrl = _last.ForecastOfficeUrl
            });
        }
    }
}
