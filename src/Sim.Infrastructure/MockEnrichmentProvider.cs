using Sim.Application;
using Sim.Contracts;
using Sim.Domain;

namespace Sim.Infrastructure;

public sealed class MockEnrichmentProvider : IEnrichmentProvider
{
    public EnrichmentSnapshot BuildSnapshot(ScenarioDefinition scenario, RouteDefinition route)
    {
        if (route.Mode != TransportMode.Ground)
        {
            return BuildNonGroundSnapshot(route);
        }

        var hash = Math.Abs(route.RouteId.GetHashCode(StringComparison.Ordinal));
        var band = (hash % 3) switch
        {
            0 => PopulationDensityBand.Sparse,
            1 => PopulationDensityBand.Suburban,
            _ => PopulationDensityBand.Dense
        };

        var segments = BuildGroundSegments(route, band, scenario.Realism);
        var metrics = BuildGroundMetrics(route, segments);

        return new EnrichmentSnapshot
        {
            RouteId = route.RouteId,
            SnapshotSource = "mock-v2-route-severity",
            GroundCorridorMetrics = metrics,
            SettlementProfile = new SettlementProfile
            {
                DensityBand = band,
                PopulationPerSqKm = band switch
                {
                    PopulationDensityBand.Dense => 1300,
                    PopulationDensityBand.Suburban => 340,
                    _ => 45
                },
                BuiltUpCoveragePercent = band switch
                {
                    PopulationDensityBand.Dense => 70,
                    PopulationDensityBand.Suburban => 35,
                    _ => 8
                },
                RoadsideDevelopmentIndex = band switch
                {
                    PopulationDensityBand.Dense => 0.88,
                    PopulationDensityBand.Suburban => 0.54,
                    _ => 0.18
                }
            },
            GeneratedStructureSummary = new GeneratedStructureSummary
            {
                RouteId = route.RouteId,
                EstimatedStructureCount = band switch
                {
                    PopulationDensityBand.Dense => 240,
                    PopulationDensityBand.Suburban => 110,
                    _ => 35
                },
                EstimatedHousingClusters = band switch
                {
                    PopulationDensityBand.Dense => 18,
                    PopulationDensityBand.Suburban => 10,
                    _ => 3
                },
                EstimatedServiceStops = band switch
                {
                    PopulationDensityBand.Dense => 30,
                    PopulationDensityBand.Suburban => 13,
                    _ => 4
                },
                EstimatedSettlementFootprints = band switch
                {
                    PopulationDensityBand.Dense => 9,
                    PopulationDensityBand.Suburban => 5,
                    _ => 2
                }
            },
            Segments = segments
        };
    }

    private static EnrichmentSnapshot BuildNonGroundSnapshot(RouteDefinition route)
    {
        return new EnrichmentSnapshot
        {
            RouteId = route.RouteId,
            SnapshotSource = "mock-v2-route-severity",
            GroundCorridorMetrics = new GroundCorridorMetrics
            {
                RouteId = route.RouteId,
                AverageSpeedLimitKph = 120,
                CongestionIndex = 0.1,
                RefuelStationsPer100Km = 0.1,
                RestaurantsPer100Km = 0.2,
                CampgroundsPer100Km = 0.0,
                TrafficVehiclesPerHour = 80,
                RouteSeverityIndex = 0.08,
                SurfaceAttritionFactor = 0.05,
                MoralePressure = 0.04,
                CargoDamageRisk = 0.03,
                ConcealmentOpportunity = 0.05
            },
            SettlementProfile = new SettlementProfile
            {
                DensityBand = PopulationDensityBand.Sparse,
                PopulationPerSqKm = 12,
                BuiltUpCoveragePercent = 2,
                RoadsideDevelopmentIndex = 0.1
            },
            GeneratedStructureSummary = new GeneratedStructureSummary
            {
                RouteId = route.RouteId,
                EstimatedStructureCount = 20,
                EstimatedHousingClusters = 2,
                EstimatedServiceStops = 1,
                EstimatedSettlementFootprints = 1
            },
            Segments =
            [
                new RouteSegmentEnrichment
                {
                    SegmentId = $"{route.RouteId}-seg-01",
                    Sequence = 1,
                    LengthMiles = 15,
                    SurfaceType = RoadSegmentSurfaceType.PavedHighway,
                    ConditionState = SurfaceConditionState.Dry,
                    IsHighway = true,
                    ConnectsHighway = true,
                    TrafficStress = 0.1,
                    LocalHostility = 0.08,
                    DustIndex = 0.0,
                    RouteSeverityIndex = 0.08,
                    SurfaceAttritionFactor = 0.05,
                    ConcealmentFactor = 0.05,
                    ChokepointRisk = 0.08
                }
            ]
        };
    }

    private static List<RouteSegmentEnrichment> BuildGroundSegments(
        RouteDefinition route,
        PopulationDensityBand band,
        ScenarioRealismDefinition realism)
    {
        var baseHostility = route.RiskProfile.ToUpperInvariant() switch
        {
            "HIGH" => 0.7,
            "MEDIUM" => 0.42,
            _ => 0.18
        };

        var segmentCount = 5;
        var nominalLength = Math.Max(3.0, route.EstimatedTravelTimeMinutes / 11.0);
        var segments = new List<RouteSegmentEnrichment>(segmentCount);

        for (var i = 0; i < segmentCount; i++)
        {
            var token = Math.Abs(HashCode.Combine(route.RouteId, route.Mode, i, band));
            var isHighway = i == 0 || i == segmentCount - 1 || (band != PopulationDensityBand.Sparse && token % 5 == 0);
            var connectsHighway = isHighway || i == 1 || i == segmentCount - 2;
            var surface = DetermineBaseSurface(band, isHighway, connectsHighway, token);
            var condition = DetermineCondition(surface, realism, token);
            var trafficStress = ComputeTrafficStress(surface, band, token);
            var localHostility = Math.Clamp(baseHostility + (isHighway ? 0.12 : 0.0) + ((token % 17) / 100.0), 0.05, 0.95);
            var dustIndex = ComputeDustIndex(surface, condition, realism.DustExposure);
            var chokepointRisk = ComputeChokepointRisk(isHighway, trafficStress, localHostility, surface);
            var routeSeverity = ComputeRouteSeverity(surface, condition, trafficStress, localHostility, dustIndex, chokepointRisk);
            var attrition = ComputeSurfaceAttrition(surface, condition, realism.WeatherSeverity, dustIndex);
            var concealment = ComputeConcealment(surface, condition, dustIndex, isHighway);

            segments.Add(new RouteSegmentEnrichment
            {
                SegmentId = $"{route.RouteId}-seg-{i + 1:D2}",
                Sequence = i + 1,
                LengthMiles = Math.Round(nominalLength + ((token % 7) - 3) * 0.6, 1),
                SurfaceType = surface,
                ConditionState = condition,
                IsHighway = isHighway,
                ConnectsHighway = connectsHighway,
                TrafficStress = Math.Round(trafficStress, 2),
                LocalHostility = Math.Round(localHostility, 2),
                DustIndex = Math.Round(dustIndex, 2),
                RouteSeverityIndex = Math.Round(routeSeverity, 2),
                SurfaceAttritionFactor = Math.Round(attrition, 2),
                ConcealmentFactor = Math.Round(concealment, 2),
                ChokepointRisk = Math.Round(chokepointRisk, 2)
            });
        }

        return segments;
    }

    private static GroundCorridorMetrics BuildGroundMetrics(RouteDefinition route, IReadOnlyCollection<RouteSegmentEnrichment> segments)
    {
        var totalMiles = Math.Max(1.0, segments.Sum(segment => segment.LengthMiles));
        var weightedSpeed = segments.Sum(segment => BaseSpeedKph(segment.SurfaceType, segment.ConditionState) * segment.LengthMiles) / totalMiles;
        var weightedSeverity = segments.Sum(segment => segment.RouteSeverityIndex * segment.LengthMiles) / totalMiles;
        var weightedAttrition = segments.Sum(segment => segment.SurfaceAttritionFactor * segment.LengthMiles) / totalMiles;
        var weightedConcealment = segments.Sum(segment => segment.ConcealmentFactor * segment.LengthMiles) / totalMiles;
        var weightedHostility = segments.Sum(segment => segment.ChokepointRisk * segment.LengthMiles) / totalMiles;
        var roughShare = segments.Count(segment => segment.SurfaceType is RoadSegmentSurfaceType.Gravel or RoadSegmentSurfaceType.Dirt or RoadSegmentSurfaceType.MudSoftGround) / (double)segments.Count;

        return new GroundCorridorMetrics
        {
            RouteId = route.RouteId,
            AverageSpeedLimitKph = Math.Round(weightedSpeed, 1),
            CongestionIndex = Math.Round(Math.Clamp((segments.Average(segment => segment.TrafficStress) * 0.75) + (weightedHostility * 0.2), 0.08, 0.95), 2),
            RefuelStationsPer100Km = Math.Round(Math.Clamp(3.6 - (roughShare * 1.2) - (weightedHostility * 0.9), 0.8, 5.5), 1),
            RestaurantsPer100Km = Math.Round(Math.Clamp(4.8 - (roughShare * 1.4), 0.6, 10.5), 1),
            CampgroundsPer100Km = Math.Round(Math.Clamp(0.4 + (roughShare * 1.5), 0.1, 2.0), 1),
            TrafficVehiclesPerHour = Math.Round(Math.Clamp(450 + (segments.Average(segment => segment.TrafficStress) * 2500), 120, 3200), 0),
            RouteSeverityIndex = Math.Round(weightedSeverity, 2),
            SurfaceAttritionFactor = Math.Round(weightedAttrition, 2),
            MoralePressure = Math.Round(Math.Clamp((weightedSeverity * 0.7) + (roughShare * 0.2), 0.05, 0.95), 2),
            CargoDamageRisk = Math.Round(Math.Clamp((weightedAttrition * 0.55) + (weightedHostility * 0.2), 0.03, 0.9), 2),
            ConcealmentOpportunity = Math.Round(Math.Clamp(weightedConcealment, 0.02, 0.85), 2)
        };
    }

    private static RoadSegmentSurfaceType DetermineBaseSurface(PopulationDensityBand band, bool isHighway, bool connectsHighway, int token)
    {
        if (isHighway)
        {
            return RoadSegmentSurfaceType.PavedHighway;
        }

        if (!connectsHighway)
        {
            return RoadSegmentSurfaceType.ImprovedHybrid;
        }

        return band switch
        {
            PopulationDensityBand.Dense => token % 4 == 0 ? RoadSegmentSurfaceType.ImprovedHybrid : RoadSegmentSurfaceType.SecondaryPaved,
            PopulationDensityBand.Suburban => (token % 5) switch
            {
                0 => RoadSegmentSurfaceType.Gravel,
                1 => RoadSegmentSurfaceType.ImprovedHybrid,
                _ => RoadSegmentSurfaceType.SecondaryPaved
            },
            _ => (token % 4) switch
            {
                0 => RoadSegmentSurfaceType.Gravel,
                1 => RoadSegmentSurfaceType.Dirt,
                _ => RoadSegmentSurfaceType.ImprovedHybrid
            }
        };
    }

    private static SurfaceConditionState DetermineCondition(RoadSegmentSurfaceType surface, ScenarioRealismDefinition realism, int token)
    {
        if (realism.WeatherSeverity >= 0.72 && surface == RoadSegmentSurfaceType.Dirt)
        {
            return SurfaceConditionState.Muddy;
        }

        if (realism.WeatherSeverity >= 0.62 && surface == RoadSegmentSurfaceType.Gravel)
        {
            return SurfaceConditionState.Degraded;
        }

        if (realism.WeatherSeverity >= 0.55 && surface is RoadSegmentSurfaceType.PavedHighway or RoadSegmentSurfaceType.SecondaryPaved)
        {
            return SurfaceConditionState.ReducedTraction;
        }

        if (realism.DustExposure >= 0.55 && surface is RoadSegmentSurfaceType.ImprovedHybrid or RoadSegmentSurfaceType.Gravel or RoadSegmentSurfaceType.Dirt && token % 2 == 0)
        {
            return SurfaceConditionState.Dusty;
        }

        if (realism.WeatherSeverity >= 0.35 && surface is RoadSegmentSurfaceType.ImprovedHybrid or RoadSegmentSurfaceType.Gravel)
        {
            return SurfaceConditionState.Wet;
        }

        return SurfaceConditionState.Dry;
    }

    private static double ComputeTrafficStress(RoadSegmentSurfaceType surface, PopulationDensityBand band, int token)
    {
        var baseStress = band switch
        {
            PopulationDensityBand.Dense => 0.55,
            PopulationDensityBand.Suburban => 0.34,
            _ => 0.16
        };

        var surfaceModifier = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.18,
            RoadSegmentSurfaceType.SecondaryPaved => 0.10,
            RoadSegmentSurfaceType.ImprovedHybrid => -0.02,
            _ => -0.08
        };

        return Math.Clamp(baseStress + surfaceModifier + ((token % 11) - 5) * 0.015, 0.05, 0.95);
    }

    private static double ComputeDustIndex(RoadSegmentSurfaceType surface, SurfaceConditionState condition, double dustExposure)
    {
        if (condition == SurfaceConditionState.Muddy || condition == SurfaceConditionState.Wet)
        {
            return 0.02;
        }

        var surfaceBase = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.01,
            RoadSegmentSurfaceType.SecondaryPaved => 0.04,
            RoadSegmentSurfaceType.ImprovedHybrid => 0.2,
            RoadSegmentSurfaceType.Gravel => 0.34,
            RoadSegmentSurfaceType.Dirt => 0.45,
            _ => 0.12
        };

        if (condition == SurfaceConditionState.Dusty)
        {
            surfaceBase += 0.18;
        }

        return Math.Clamp(surfaceBase + (dustExposure * 0.35), 0.0, 1.0);
    }

    private static double ComputeChokepointRisk(bool isHighway, double trafficStress, double hostility, RoadSegmentSurfaceType surface)
    {
        var surfaceBonus = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.16,
            RoadSegmentSurfaceType.SecondaryPaved => 0.08,
            _ => 0.02
        };

        var baseline = isHighway
            ? (hostility * 0.52) + (trafficStress * 0.28) + surfaceBonus
            : (hostility * 0.2) + (trafficStress * 0.1) + surfaceBonus;

        return Math.Clamp(baseline, 0.03, 0.95);
    }

    private static double ComputeRouteSeverity(
        RoadSegmentSurfaceType surface,
        SurfaceConditionState condition,
        double trafficStress,
        double hostility,
        double dust,
        double chokepointRisk)
    {
        var surfaceWeight = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.12,
            RoadSegmentSurfaceType.SecondaryPaved => 0.22,
            RoadSegmentSurfaceType.ImprovedHybrid => 0.38,
            RoadSegmentSurfaceType.Gravel => 0.48,
            RoadSegmentSurfaceType.Dirt => 0.58,
            _ => 0.74
        };

        var conditionModifier = condition switch
        {
            SurfaceConditionState.Dry => 0.0,
            SurfaceConditionState.Wet => 0.08,
            SurfaceConditionState.Dusty => 0.06,
            SurfaceConditionState.Degraded => 0.12,
            SurfaceConditionState.ReducedTraction => 0.1,
            SurfaceConditionState.Muddy => 0.2,
            _ => 0.0
        };

        return Math.Clamp(surfaceWeight + conditionModifier + (trafficStress * 0.14) + (hostility * 0.08) + (dust * 0.05) + (chokepointRisk * 0.12), 0.05, 1.0);
    }

    private static double ComputeSurfaceAttrition(RoadSegmentSurfaceType surface, SurfaceConditionState condition, double weatherSeverity, double dust)
    {
        var baseAttrition = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.08,
            RoadSegmentSurfaceType.SecondaryPaved => 0.14,
            RoadSegmentSurfaceType.ImprovedHybrid => 0.28,
            RoadSegmentSurfaceType.Gravel => 0.4,
            RoadSegmentSurfaceType.Dirt => 0.5,
            _ => 0.7
        };

        var conditionModifier = condition switch
        {
            SurfaceConditionState.Dry => 0.0,
            SurfaceConditionState.Wet => 0.08,
            SurfaceConditionState.Dusty => 0.04,
            SurfaceConditionState.Degraded => 0.12,
            SurfaceConditionState.ReducedTraction => 0.06,
            SurfaceConditionState.Muddy => 0.18,
            _ => 0.0
        };

        return Math.Clamp(baseAttrition + conditionModifier + (weatherSeverity * 0.08) + (dust * 0.04), 0.03, 1.0);
    }

    private static double ComputeConcealment(RoadSegmentSurfaceType surface, SurfaceConditionState condition, double dust, bool isHighway)
    {
        var baseConcealment = surface switch
        {
            RoadSegmentSurfaceType.PavedHighway => 0.05,
            RoadSegmentSurfaceType.SecondaryPaved => 0.12,
            RoadSegmentSurfaceType.ImprovedHybrid => 0.26,
            RoadSegmentSurfaceType.Gravel => 0.34,
            RoadSegmentSurfaceType.Dirt => 0.42,
            _ => 0.36
        };

        if (isHighway)
        {
            baseConcealment -= 0.06;
        }

        if (condition == SurfaceConditionState.Dusty)
        {
            baseConcealment -= 0.2;
        }

        return Math.Clamp(baseConcealment - (dust * 0.18), 0.02, 0.85);
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
}
