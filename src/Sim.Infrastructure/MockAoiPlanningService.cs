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

        var regionalSnapshot = TryResolveSnapshot(area.CenterLat, area.CenterLon);
        var zones = BuildSupportZones(request.CenterLat, request.CenterLon, normalizedRadius, request.Criteria, request.Seed, regionalSnapshot);
        var transportation = regionalSnapshot is not null
            ? BuildImportedRegionalSummary(area, request.Criteria, zones, regionalSnapshot, request.Seed)
            : BuildTransportationSummary(area, request.Criteria, request.Seed, zones);
        var objectives = BuildObjectives(transportation, zones, request.Criteria, request.Seed);

        var validationMessage = transportation.IsImportedRegionalSnapshot
            ? "AO accepted. Upper Cumberland regional dataset with county election weighting applied."
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

    private RegionalSnapshot? TryResolveSnapshot(double lat, double lon)
    {
        return _upperCumberlandSnapshot is not null && IsWithinSnapshot(lat, lon, _upperCumberlandSnapshot)
            ? _upperCumberlandSnapshot
            : null;
    }

    private TransportationAreaSummaryDto BuildImportedRegionalSummary(
        AoiDescriptorDto area,
        SituationCriteriaDto criteria,
        IReadOnlyCollection<SupportZoneDto> zones,
        RegionalSnapshot snapshot,
        int seed)
    {
        var hostileZoneRatio = zones.Count == 0
            ? 0
            : zones.Count(z => z.GovernmentStance is "Restricted" or "Hostile") / (double)zones.Count;

        return new TransportationAreaSummaryDto
        {
            DataSource = snapshot.Summary.DataSource,
            IsImportedRegionalSnapshot = true,
            DatasetCoverage = snapshot.DatasetCoverage,
            MajorRoadSegments = snapshot.Summary.MajorRoadSegments,
            RailConnections = snapshot.Summary.RailConnections,
            Airfields = snapshot.Summary.Airfields,
            FuelSites = snapshot.Summary.FuelSites,
            Restaurants = snapshot.Summary.Restaurants,
            Campgrounds = snapshot.Summary.Campgrounds,
            ArmyCorpsCampgrounds = snapshot.Summary.ArmyCorpsCampgrounds,
            AverageSpeedLimitKph = Math.Round(Math.Clamp(
                snapshot.Summary.BaselineSpeedLimitKph - (criteria.CivilFriction * 10) - (criteria.WeatherStress * 8) - (hostileZoneRatio * 10),
                45,
                105), 1),
            TrafficVehiclesPerHour = Math.Round(Math.Clamp(
                snapshot.Summary.BaselineTrafficVehiclesPerHour + (criteria.CivilFriction * 650) + (criteria.InfrastructurePriority * 140),
                250,
                4000), 0),
            Counties = snapshot.Counties.ToList(),
            CountyAllegiances = BuildCountyAllegiances(snapshot, criteria, seed),
            HighwayCorridors = snapshot.HighwayCorridors.ToList(),
            TransitServices = snapshot.TransitServices.ToList(),
            FeatureHighlights = snapshot.FeatureHighlights
                .Select(feature => new ImportedFeatureDto
                {
                    Category = feature.Category,
                    Name = feature.Name,
                    County = feature.County,
                    Notes = feature.Notes
                })
                .ToList(),
            TransportProfiles = snapshot.TransportProfiles
                .Select(profile => new TransportRealismProfileDto
                {
                    ProfileId = profile.ProfileId,
                    Category = profile.Category,
                    Platform = profile.Platform,
                    ReferenceVehicle = profile.ReferenceVehicle,
                    Crew = profile.Crew,
                    PassengerCapacity = profile.PassengerCapacity,
                    FuelEconomyMpg = profile.FuelEconomyMpg,
                    FuelBurnGallonsPerHourRoad = profile.FuelBurnGallonsPerHourRoad,
                    FuelBurnGallonsPerHourIdle = profile.FuelBurnGallonsPerHourIdle,
                    OperationalRangeMiles = profile.OperationalRangeMiles,
                    MaxSpeedMph = profile.MaxSpeedMph,
                    TypicalCruiseSpeedMph = profile.TypicalCruiseSpeedMph,
                    MaintenanceCostUsdPer1000Miles = profile.MaintenanceCostUsdPer1000Miles,
                    BreakdownEventsPer100kMiles = profile.BreakdownEventsPer100kMiles,
                    SurvivabilityScore = profile.SurvivabilityScore,
                    DataConfidence = profile.DataConfidence,
                    ProtectionSummary = profile.ProtectionSummary,
                    EstimateBasis = profile.EstimateBasis,
                    ImmersionFactors = profile.ImmersionFactors.ToList(),
                    SourceSummary = profile.SourceSummary
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
                Description = $"Open and secure {weakestZone.ZoneId} in {weakestZone.CountyName} to restore predictable throughput near the least supportive authorities.",
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
                Description = $"Use {strongestZone.ZoneId} in {strongestZone.CountyName} as the preferred LOGPAC handoff area to exploit the strongest BLUFOR support in the AO.",
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
                    ? $"Assess campground and support capacity near {frictionZone.ZoneId} in {frictionZone.CountyName} for emergency overnight dispersion."
                    : $"Reduce restriction risk around {frictionZone.ZoneId} in {frictionZone.CountyName}, where {frictionZone.Allegiance.ToLowerInvariant()} alignment is strongest.",
                Priority = criteria.CivilFriction >= 0.55 ? "High" : "Medium",
                AssociatedZoneId = frictionZone.ZoneId
            });
        }

        return objectives;
    }

    private List<SupportZoneDto> BuildSupportZones(
        double centerLat,
        double centerLon,
        double radiusMiles,
        SituationCriteriaDto criteria,
        int seed,
        RegionalSnapshot? snapshot)
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
                var countyProfile = snapshot is null ? null : ResolveCountyProfile(zoneLat, zoneLon, snapshot);
                var alignment = countyProfile is not null
                    ? BuildImportedPoliticalAlignment(countyProfile, criteria, rng, distance, radiusMiles)
                    : BuildSyntheticPoliticalAlignment(criteria, rng, distance, radiusMiles);

                var supportLevel = alignment.BluforSupport switch
                {
                    >= 0.72 => "High",
                    >= 0.52 => "Moderate",
                    >= 0.34 => "Low",
                    _ => "Denied"
                };

                zones.Add(new SupportZoneDto
                {
                    ZoneId = $"zone-{x:D2}-{y:D2}",
                    CountyName = countyProfile?.CountyName ?? "Synthetic",
                    GridX = x,
                    GridY = y,
                    CenterLat = Math.Round(zoneLat, 4),
                    CenterLon = Math.Round(zoneLon, 4),
                    AreaSquareMiles = ZoneAreaSquareMiles,
                    GovernmentStance = DetermineGovernmentStance(alignment, criteria),
                    Allegiance = DetermineAllegiance(alignment.BluforSupport, alignment.OpforSupport),
                    SupportLevel = supportLevel,
                    SupportScore = Math.Round(alignment.BluforSupport, 2),
                    BluforSupport = Math.Round(alignment.BluforSupport, 2),
                    OpforSupport = Math.Round(alignment.OpforSupport, 2),
                    IndependentShare = Math.Round(alignment.IndependentShare, 2),
                    ElectionLean = BuildElectionLean(alignment.BluforSupport, alignment.OpforSupport),
                    Notes = countyProfile is not null
                        ? $"{countyProfile.CountyName} weighted D {countyProfile.WeightedDemocratPct:F1}% / R {countyProfile.WeightedRepublicanPct:F1}% / I {countyProfile.WeightedIndependentPct:F1}%."
                        : $"{supportLevel} support with {DetermineAllegiance(alignment.BluforSupport, alignment.OpforSupport).ToLowerInvariant()} local posture."
                });
            }
        }

        return zones
            .OrderBy(z => z.GridY)
            .ThenBy(z => z.GridX)
            .ToList();
    }

    private static List<CountyAllegianceDto> BuildCountyAllegiances(RegionalSnapshot snapshot, SituationCriteriaDto criteria, int seed)
    {
        return snapshot.CountyPoliticalProfiles
            .OrderBy(profile => profile.CountyName)
            .Select(profile =>
            {
                var rng = new Random(seed ^ profile.CountyName.GetHashCode(StringComparison.Ordinal));
                var alignment = BuildImportedPoliticalAlignment(profile, criteria, rng, 0, MaxRadiusMiles);
                return new CountyAllegianceDto
                {
                    CountyName = profile.CountyName,
                    CountySeat = profile.CountySeat,
                    WeightedDemocratPct = profile.WeightedDemocratPct,
                    WeightedRepublicanPct = profile.WeightedRepublicanPct,
                    WeightedIndependentPct = profile.WeightedIndependentPct,
                    BluforSupport = Math.Round(alignment.BluforSupport, 2),
                    OpforSupport = Math.Round(alignment.OpforSupport, 2),
                    Allegiance = DetermineAllegiance(alignment.BluforSupport, alignment.OpforSupport),
                    Notes = $"Weighted 25-year county baseline with recent-election emphasis. 2024 D {profile.RecentDemocratPct:F1}% / R {profile.RecentRepublicanPct:F1}%."
                };
            })
            .ToList();
    }

    private static CountyPoliticalProfile? ResolveCountyProfile(double lat, double lon, RegionalSnapshot snapshot)
    {
        return snapshot.CountyPoliticalProfiles
            .OrderBy(profile => DistanceMiles(lat, lon, profile.CenterLat, profile.CenterLon))
            .FirstOrDefault();
    }

    private static PoliticalAlignment BuildImportedPoliticalAlignment(
        CountyPoliticalProfile profile,
        SituationCriteriaDto criteria,
        Random rng,
        double distanceFromCenter,
        double radiusMiles)
    {
        var blueBase = profile.WeightedDemocratPct / 100.0;
        var redBase = profile.WeightedRepublicanPct / 100.0;
        var independent = profile.WeightedIndependentPct / 100.0;
        var majorityIsBlue = blueBase >= redBase;
        var noise = (rng.NextDouble() - 0.5) * 0.08;

        var minorityShare = Math.Clamp(0.56 - (criteria.PropagandaFactor * 0.24) + noise, 0.22, 0.62);
        var majorityShare = 1.0 - minorityShare;

        if (criteria.PropagandaFactor > 0.6 && rng.NextDouble() < criteria.PropagandaFactor)
        {
            majorityShare = Math.Clamp(0.62 + (criteria.PropagandaFactor * 0.22) + Math.Abs(noise), 0.58, 0.90);
            minorityShare = 1.0 - majorityShare;
        }

        var blue = blueBase + (majorityIsBlue ? independent * majorityShare : independent * minorityShare);
        var red = redBase + (majorityIsBlue ? independent * minorityShare : independent * majorityShare);

        var centerBias = 1.0 - Math.Min(1.0, distanceFromCenter / Math.Max(radiusMiles, 1.0));
        blue += ((criteria.GovernmentFriendliness - 0.5) * 0.18) + (centerBias * 0.04) - (criteria.CivilFriction * 0.07) - (criteria.ThreatLevel * 0.05) + (noise * 0.35);
        red += ((0.5 - criteria.GovernmentFriendliness) * 0.10) + (criteria.CivilFriction * 0.05) + (criteria.ThreatLevel * 0.04) - (centerBias * 0.02) - (noise * 0.15);

        NormalizePair(ref blue, ref red);
        return new PoliticalAlignment(blue, red, independent);
    }

    private static PoliticalAlignment BuildSyntheticPoliticalAlignment(
        SituationCriteriaDto criteria,
        Random rng,
        double distanceFromCenter,
        double radiusMiles)
    {
        var centerBias = 1.0 - Math.Min(1.0, distanceFromCenter / Math.Max(radiusMiles, 1.0));
        var noise = (rng.NextDouble() - 0.5) * 0.18;
        var blue = Math.Clamp(
            criteria.GovernmentFriendliness * 0.48
            + (1.0 - criteria.CivilFriction) * 0.2
            + (1.0 - criteria.ThreatLevel) * 0.14
            + centerBias * 0.10
            + noise,
            0.05,
            0.95);
        var red = Math.Clamp((1.0 - blue) + (criteria.PropagandaFactor * 0.08) - (noise * 0.35), 0.05, 0.95);
        NormalizePair(ref blue, ref red);

        var independent = Math.Clamp(0.08 + (rng.NextDouble() * 0.06) - (criteria.PropagandaFactor * 0.03), 0.03, 0.12);
        return new PoliticalAlignment(blue, red, independent);
    }

    private static string DetermineGovernmentStance(PoliticalAlignment alignment, SituationCriteriaDto criteria)
    {
        var stanceScore = alignment.BluforSupport - alignment.OpforSupport - (criteria.CivilFriction * 0.10) - (criteria.ThreatLevel * 0.06);
        return stanceScore switch
        {
            >= 0.20 => "Friendly",
            >= 0.04 => "Neutral",
            >= -0.14 => "Restricted",
            _ => "Hostile"
        };
    }

    private static string DetermineAllegiance(double bluforSupport, double opforSupport)
    {
        var delta = bluforSupport - opforSupport;
        if (Math.Abs(delta) < 0.05)
        {
            return "Contested";
        }

        return delta > 0 ? "BLUFOR" : "OPFOR";
    }

    private static string BuildElectionLean(double bluforSupport, double opforSupport)
    {
        var allegiance = DetermineAllegiance(bluforSupport, opforSupport);
        var delta = Math.Abs(bluforSupport - opforSupport);
        return allegiance == "Contested" ? "Contested" : $"{allegiance} +{delta:P0}";
    }

    private static void NormalizePair(ref double left, ref double right)
    {
        left = Math.Clamp(left, 0.02, 0.98);
        right = Math.Clamp(right, 0.02, 0.98);
        var total = left + right;
        left /= total;
        right /= total;
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

    private sealed record PoliticalAlignment(double BluforSupport, double OpforSupport, double IndependentShare);

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
        public PoliticalModelDefinition PoliticalModel { get; set; } = new();
        public SnapshotSummary Summary { get; set; } = new();
        public List<CountyPoliticalProfile> CountyPoliticalProfiles { get; set; } = new();
        public List<RegionalFeature> FeatureHighlights { get; set; } = new();
        public List<TransportRealismProfile> TransportProfiles { get; set; } = new();
    }

    private sealed class PoliticalModelDefinition
    {
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, double> Weights { get; set; } = new();
        public string Interpretation { get; set; } = string.Empty;
    }

    private sealed class SnapshotSummary
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

    private sealed class CountyPoliticalProfile
    {
        public string CountyName { get; set; } = string.Empty;
        public string CountySeat { get; set; } = string.Empty;
        public double CenterLat { get; set; }
        public double CenterLon { get; set; }
        public double WeightedDemocratPct { get; set; }
        public double WeightedRepublicanPct { get; set; }
        public double WeightedIndependentPct { get; set; }
        public double RecentDemocratPct { get; set; }
        public double RecentRepublicanPct { get; set; }
        public double RecentIndependentPct { get; set; }
    }

    private sealed class RegionalFeature
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    private sealed class TransportRealismProfile
    {
        public string ProfileId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ReferenceVehicle { get; set; } = string.Empty;
        public int Crew { get; set; }
        public int PassengerCapacity { get; set; }
        public double? FuelEconomyMpg { get; set; }
        public double? FuelBurnGallonsPerHourRoad { get; set; }
        public double? FuelBurnGallonsPerHourIdle { get; set; }
        public double? OperationalRangeMiles { get; set; }
        public double? MaxSpeedMph { get; set; }
        public double? TypicalCruiseSpeedMph { get; set; }
        public double? MaintenanceCostUsdPer1000Miles { get; set; }
        public double? BreakdownEventsPer100kMiles { get; set; }
        public double SurvivabilityScore { get; set; }
        public string DataConfidence { get; set; } = string.Empty;
        public string ProtectionSummary { get; set; } = string.Empty;
        public string EstimateBasis { get; set; } = string.Empty;
        public List<string> ImmersionFactors { get; set; } = new();
        public string SourceSummary { get; set; } = string.Empty;
    }
}
