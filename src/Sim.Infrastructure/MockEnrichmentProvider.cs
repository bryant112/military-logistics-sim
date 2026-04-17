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
            return new EnrichmentSnapshot
            {
                RouteId = route.RouteId,
                SnapshotSource = "mock-v1",
                GroundCorridorMetrics = new GroundCorridorMetrics
                {
                    RouteId = route.RouteId,
                    AverageSpeedLimitKph = 120,
                    CongestionIndex = 0.1,
                    RefuelStationsPer100Km = 0.1,
                    RestaurantsPer100Km = 0.2,
                    CampgroundsPer100Km = 0.0,
                    TrafficVehiclesPerHour = 80
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
                }
            };
        }

        var hash = Math.Abs(route.RouteId.GetHashCode(StringComparison.Ordinal));
        var band = (hash % 3) switch
        {
            0 => PopulationDensityBand.Sparse,
            1 => PopulationDensityBand.Suburban,
            _ => PopulationDensityBand.Dense
        };

        var congestion = band switch
        {
            PopulationDensityBand.Dense => 0.65,
            PopulationDensityBand.Suburban => 0.38,
            _ => 0.2
        };

        return new EnrichmentSnapshot
        {
            RouteId = route.RouteId,
            SnapshotSource = "mock-v1",
            GroundCorridorMetrics = new GroundCorridorMetrics
            {
                RouteId = route.RouteId,
                AverageSpeedLimitKph = band == PopulationDensityBand.Dense ? 55 : 75,
                CongestionIndex = congestion,
                RefuelStationsPer100Km = band == PopulationDensityBand.Dense ? 5.5 : 2.2,
                RestaurantsPer100Km = band == PopulationDensityBand.Dense ? 11 : 4,
                CampgroundsPer100Km = band == PopulationDensityBand.Sparse ? 1.7 : 0.5,
                TrafficVehiclesPerHour = band == PopulationDensityBand.Dense ? 2800 : 780
            },
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
            }
        };
    }
}
