using System.Text.Json;
using Sim.Application;
using Sim.Contracts;

namespace Sim.Infrastructure;

public sealed class MockAoiPlanningService : IAoiPlanningService
{
    private const double MaxRadiusMiles = 30.0;
    private const double ZoneAreaSquareMiles = 10.0;
    private const double ZoneSideMiles = 3.1623;
    private readonly RegionalSnapshot? _upperCumberlandSnapshot;

    public MockAoiPlanningService(string? snapshotPath = null)
    {
        if (!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath))
        {
            var json = File.ReadAllText(snapshotPath);
            _upperCumberlandSnapshot = JsonSerializer.Deserialize<RegionalSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }

    public AoiPlanningResponse PlanArea(AoiPlanningRequest request)
    {
        var normalizedRadius = Math.Clamp(request.RadiusMiles, 1.0, MaxRadiusMiles);
        var isWithinUsa = IsWithinContiguousUsa(request.CenterLat, request.CenterLon);
        var area = new AoiDescriptorDto
        {
            CenterLat = request.CenterLat,
            CenterLon = request.CenterLon,
            RadiusMiles = normalizedRadius,
            RadiusKm = Math.Round(normalizedRadius * 1.60934, 2),
            AreaSquareMiles = Math.Round(Math.PI * normalizedRadius * normalizedRadius, 1)
        };

        if (!isWithinUsa)
        {
            return new AoiPlanningResponse
            {
                IsWithinUsa = false,
                ValidationMessage = "AO must be within the current USA-only first-look bounds.",
                Area = area
            };
        }

        var zones = BuildSupportZones(request.CenterLat, request.CenterLon, normalizedRadius, request.Criteria, request.Seed);
        var transportation = TryBuildImportedRegionalSummary(area, request.Criteria, zones)
            ?? BuildTransportationSummary(area, request.Criteria, request.Seed, zones);
        var objectives = BuildObjectives(transportation, zones, request.Criteria, request.Seed);

        var validationMessage = transportation.IsImportedRegionalSnapshot
            ? "AO accepted. Upper Cumberland regional dataset applied."
            : request.RadiusMiles > MaxRadiusMiles
                ? $"Radius normalized to {MaxRadiusMiles} miles for the current first-look limit."
                : "AO accepted.";

        return new AoiPlanningResponse
        {
            IsWithinUsa = true,
            ValidationMessage = validationMessage,
            Area = area,
            Transportation = transportation,
            Objectives = objectives,
            SupportZones = zones
        };
    }

    private TransportationAreaSummaryDto? TryBuildImportedRegionalSummary(
        AoiDescriptorDto area,
        SituationCriteriaDto criteria,
        IReadOnlyCollection<SupportZoneDto> zones)
    {
        if (_upperCumberlandSnapshot is null || !IsWithinSnapshot(area.CenterLat, area.CenterLon, _upperCumberlandSnapshot))
        {
            return null;
        }

        var hostileZoneRatio = zones.Count == 0
            ? 0
            : zones.Count(z => z.GovernmentStance is "Restricted" or "Hostile") / (double)zones.Count;
        return new TransportationAreaSummaryDto
        {
            DataSource = _upperCumberlandSnapshot.Summary.DataSource,
            IsImportedRegionalSnapshot = true,
            DatasetCoverage = _upperCumberlandSnapshot.DatasetCoverage,
            MajorRoadSegments = _upperCumberlandSnapshot.Summary.MajorRoadSegments,
            RailConnections = _upperCumberlandSnapshot.Summary.RailConnections,
            Airfields = _upperCumberlandSnapshot.Summary.Airfields,
            FuelSites = _upperCumberlandSnapshot.Summary.FuelSites,
            Restaurants = _upperCumberlandSnapshot.Summary.Restaurants,
            Campgrounds = _upperCumberlandSnapshot.Summary.Campgrounds,
            ArmyCorpsCampgrounds = _upperCumberlandSnapshot.Summary.ArmyCorpsCampgrounds,
            AverageSpeedLimitKph = Math.Round(Math.Clamp(
                _upperCumberlandSnapshot.Summary.BaselineSpeedLimitKph - (criteria.CivilFriction * 10) - (criteria.WeatherStress * 8) - (hostileZoneRatio * 10),
                45,
                105), 1),
            TrafficVehiclesPerHour = Math.Round(Math.Clamp(
                _upperCumberlandSnapshot.Summary.BaselineTrafficVehiclesPerHour + (criteria.CivilFriction * 650) + (criteria.InfrastructurePriority * 140),
                250,
                4000), 0),
            Counties = _upperCumberlandSnapshot.Counties.ToList(),
            HighwayCorridors = _upperCumberlandSnapshot.HighwayCorridors.ToList(),
            TransitServices = _upperCumberlandSnapshot.TransitServices.ToList(),
            FeatureHighlights = _upperCumberlandSnapshot.FeatureHighlights
                .Select(feature => new ImportedFeatureDto
                {
                    Category = feature.Category,
                    Name = feature.Name,
                    County = feature.County,
                    Notes = feature.Notes
                })
                .ToList()
        };
    }

    private static TransportationAreaSummaryDto BuildTransportationSummary(
        AoiDescriptorDto area,
        SituationCriteriaDto criteria,
        int seed,
        IReadOnlyCollection<SupportZoneDto> zones)
    {
        var rng = new Random(seed ^ HashCode.Combine(area.CenterLat, area.CenterLon, area.RadiusMiles));
        var scale = Math.Max(1.0, area.AreaSquareMiles / 120.0);
        var govtPenalty = 1.0 - ((1.0 - criteria.GovernmentFriendliness) * 0.25);
        var frictionPenalty = 1.0 - (criteria.CivilFriction * 0.2);
        var hostileZoneRatio = zones.Count == 0
            ? 0
            : zones.Count(z => z.GovernmentStance is "Restricted" or "Hostile") / (double)zones.Count;

        return new TransportationAreaSummaryDto
        {
            MajorRoadSegments = (int)Math.Round((10 + (scale * 7) + rng.Next(3, 11)) * govtPenalty),
            RailConnections = Math.Max(0, (int)Math.Round((scale * 1.8) + rng.Next(0, 3) - (criteria.ThreatLevel * 2))),
            Airfields = Math.Max(0, (int)Math.Round((scale * 0.9) + rng.Next(0, 2))),
            FuelSites = Math.Max(1, (int)Math.Round((6 + (scale * 3) + rng.Next(0, 6)) * frictionPenalty)),
            Restaurants = Math.Max(2, (int)Math.Round((12 + (scale * 5) + rng.Next(2, 10)) * frictionPenalty)),
            Campgrounds = Math.Max(0, (int)Math.Round((2 + (scale * 2) + rng.Next(0, 4)))),
            ArmyCorpsCampgrounds = Math.Max(0, (int)Math.Round((rng.NextDouble() * 2) + (area.RadiusMiles / 18.0))),
            AverageSpeedLimitKph = Math.Round(Math.Clamp(92 - (criteria.CivilFriction * 18) - (criteria.WeatherStress * 12) - (hostileZoneRatio * 12), 45, 105), 1),
            TrafficVehiclesPerHour = Math.Round(Math.Clamp(650 + (criteria.CivilFriction * 1400) + (criteria.InfrastructurePriority * 300) + rng.Next(-120, 180), 250, 3200), 0)
        };
    }

    private static List<ObjectiveDto> BuildObjectives(
        TransportationAreaSummaryDto transportation,
        IReadOnlyList<SupportZoneDto> zones,
        SituationCriteriaDto criteria,
        int seed)
    {
        var rng = new Random(seed ^ 0x4F3A2D);
        var sortedZones = zones
            .OrderBy(z => z.SupportScore)
            .ThenByDescending(z => z.GovernmentStance == "Hostile")
            .ToList();

        var strongestZone = zones.OrderByDescending(z => z.SupportScore).FirstOrDefault();
        var weakestZone = sortedZones.FirstOrDefault();
        var frictionZone = zones.OrderByDescending(z => GovernmentSeverity(z.GovernmentStance)).ThenBy(z => z.SupportScore).FirstOrDefault();

        var objectives = new List<ObjectiveDto>();

        if (weakestZone is not null)
        {
            objectives.Add(new ObjectiveDto
            {
                ObjectiveId = $"obj-route-{seed}",
                Category = "Route Security",
                Title = "Stabilize the weakest corridor slice",
                Description = $"Open and secure {weakestZone.ZoneId} to restore predictable throughput near low-support local authorities.",
                Priority = criteria.ThreatLevel >= 0.6 ? "Critical" : "High",
                AssociatedZoneId = weakestZone.ZoneId
            });
        }

        if (strongestZone is not null)
        {
            objectives.Add(new ObjectiveDto
            {
                ObjectiveId = $"obj-logpac-{seed}",
                Category = "Configured Loads",
                Title = "Stand up a dependable sustainment release point",
                Description = $"Use {strongestZone.ZoneId} as the preferred LOGPAC handoff area to exploit friendly support and transport access.",
                Priority = criteria.InfrastructurePriority >= 0.65 ? "High" : "Medium",
                AssociatedZoneId = strongestZone.ZoneId
            });
        }

        if (frictionZone is not null)
        {
            var category = transportation.ArmyCorpsCampgrounds > 0 && rng.NextDouble() > 0.45
                ? "Temporary Staging"
                : "Civil Coordination";

            objectives.Add(new ObjectiveDto
            {
                ObjectiveId = $"obj-civil-{seed}",
                Category = category,
                Title = category == "Temporary Staging" ? "Survey federal staging fallback options" : "Mitigate local government friction",
                Description = category == "Temporary Staging"
                    ? $"Assess campground and support capacity near {frictionZone.ZoneId} for emergency overnight dispersion."
                    : $"Negotiate access and reduce restriction risk around {frictionZone.ZoneId} where local posture is least reliable.",
                Priority = criteria.CivilFriction >= 0.55 ? "High" : "Medium",
                AssociatedZoneId = frictionZone.ZoneId
            });
        }

        return objectives;
    }

    private static List<SupportZoneDto> BuildSupportZones(
        double centerLat,
        double centerLon,
        double radiusMiles,
        SituationCriteriaDto criteria,
        int seed)
    {
        var rng = new Random(seed ^ HashCode.Combine(centerLat, centerLon, radiusMiles));
        var zoneSpan = Math.Max(1, (int)Math.Ceiling((radiusMiles * 2) / ZoneSideMiles));
        var halfSpan = zoneSpan / 2.0;
        var latStep = ZoneSideMiles / 69.0;
        var lonStep = ZoneSideMiles / Math.Max(40.0, 54.6 * Math.Cos(ToRadians(centerLat)));
        var zones = new List<SupportZoneDto>();

        for (var y = 0; y < zoneSpan; y++)
        {
            for (var x = 0; x < zoneSpan; x++)
            {
                var offsetMilesX = ((x - halfSpan) + 0.5) * ZoneSideMiles;
                var offsetMilesY = ((y - halfSpan) + 0.5) * ZoneSideMiles;
                var distance = Math.Sqrt((offsetMilesX * offsetMilesX) + (offsetMilesY * offsetMilesY));
                if (distance > radiusMiles)
                {
                    continue;
                }

                var zoneLat = centerLat + ((y - halfSpan) * latStep);
                var zoneLon = centerLon + ((x - halfSpan) * lonStep);
                var localNoise = (rng.NextDouble() - 0.5) * 0.2;
                var supportScore = Math.Clamp(
                    criteria.GovernmentFriendliness * 0.45
                    + (1.0 - criteria.CivilFriction) * 0.2
                    + (1.0 - criteria.ThreatLevel) * 0.15
                    + (1.0 - Math.Min(1.0, distance / radiusMiles)) * 0.12
                    + localNoise,
                    0.05,
                    0.95);

                var stanceRoll = supportScore - (criteria.CivilFriction * 0.25) - (criteria.ThreatLevel * 0.1);
                var stance = stanceRoll switch
                {
                    >= 0.7 => "Friendly",
                    >= 0.48 => "Neutral",
                    >= 0.28 => "Restricted",
                    _ => "Hostile"
                };

                var supportLevel = supportScore switch
                {
                    >= 0.75 => "High",
                    >= 0.5 => "Moderate",
                    >= 0.3 => "Low",
                    _ => "Denied"
                };

                zones.Add(new SupportZoneDto
                {
                    ZoneId = $"zone-{x:D2}-{y:D2}",
                    GridX = x,
                    GridY = y,
                    CenterLat = Math.Round(zoneLat, 4),
                    CenterLon = Math.Round(zoneLon, 4),
                    AreaSquareMiles = ZoneAreaSquareMiles,
                    GovernmentStance = stance,
                    SupportLevel = supportLevel,
                    SupportScore = Math.Round(supportScore, 2),
                    Notes = $"{supportLevel} support with {stance.ToLowerInvariant()} local posture."
                });
            }
        }

        return zones
            .OrderBy(z => z.GridY)
            .ThenBy(z => z.GridX)
            .ToList();
    }

    private static double GovernmentSeverity(string stance)
    {
        return stance switch
        {
            "Hostile" => 3,
            "Restricted" => 2,
            "Neutral" => 1,
            _ => 0
        };
    }

    private static bool IsWithinContiguousUsa(double lat, double lon)
    {
        return lat >= 24.0 && lat <= 49.8 && lon >= -125.0 && lon <= -66.0;
    }

    private static bool IsWithinSnapshot(double lat, double lon, RegionalSnapshot snapshot)
    {
        return DistanceMiles(lat, lon, snapshot.CenterLat, snapshot.CenterLon) <= snapshot.MatchRadiusMiles;
    }

    private static double DistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMiles = 3958.8;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMiles * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private sealed class RegionalSnapshot
    {
        public string RegionName { get; set; } = string.Empty;
        public double CenterLat { get; set; }
        public double CenterLon { get; set; }
        public double MatchRadiusMiles { get; set; }
        public string DatasetCoverage { get; set; } = string.Empty;
        public List<string> Counties { get; set; } = new();
        public List<string> HighwayCorridors { get; set; } = new();
        public List<string> TransitServices { get; set; } = new();
        public RegionalSummary Summary { get; set; } = new();
        public List<RegionalFeature> FeatureHighlights { get; set; } = new();
    }

    private sealed class RegionalSummary
    {
        public string DataSource { get; set; } = string.Empty;
        public int MajorRoadSegments { get; set; }
        public int RailConnections { get; set; }
        public int Airfields { get; set; }
        public int FuelSites { get; set; }
        public int Restaurants { get; set; }
        public int Campgrounds { get; set; }
        public int ArmyCorpsCampgrounds { get; set; }
        public double BaselineSpeedLimitKph { get; set; }
        public double BaselineTrafficVehiclesPerHour { get; set; }
    }

    private sealed class RegionalFeature
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
