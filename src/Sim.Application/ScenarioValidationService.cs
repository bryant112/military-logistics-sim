using System.Text.Json;
using System.Text.Json.Serialization;
using Sim.Contracts;
using Sim.Domain;

namespace Sim.Application;

public sealed class ScenarioValidationService
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ScenarioValidationResponse Validate(string json)
    {
        var response = new ScenarioValidationResponse();
        if (!TryDeserialize(json, out var scenario, out var deserializeError) || scenario is null)
        {
            response.IsValid = false;
            response.Errors.Add(deserializeError ?? "Scenario JSON is invalid.");
            return response;
        }

        response.Errors.AddRange(ValidateScenario(scenario));
        response.IsValid = response.Errors.Count == 0;
        return response;
    }

    public bool TryDeserialize(string json, out ScenarioDefinition? scenario, out string? error)
    {
        try
        {
            scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, _options);
            if (scenario is null)
            {
                error = "Scenario payload is empty.";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            scenario = null;
            error = ex.Message;
            return false;
        }
    }

    private static List<string> ValidateScenario(ScenarioDefinition scenario)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(scenario.ScenarioId))
        {
            errors.Add("ScenarioId is required.");
        }

        if (scenario.TickRateSeconds <= 0)
        {
            errors.Add("TickRateSeconds must be > 0.");
        }

        if (scenario.DurationMinutes <= 0)
        {
            errors.Add("DurationMinutes must be > 0.");
        }

        if (scenario.Realism.CrewEnduranceHours <= 0)
        {
            errors.Add("Realism.CrewEnduranceHours must be > 0.");
        }

        ValidateUnitInterval(errors, scenario.Realism.ReportingQuality, "Realism.ReportingQuality");
        ValidateUnitInterval(errors, scenario.Realism.SustainmentRhythmAdherence, "Realism.SustainmentRhythmAdherence");
        ValidateUnitInterval(errors, scenario.Realism.ConfiguredLoadQuality, "Realism.ConfiguredLoadQuality");
        ValidateUnitInterval(errors, scenario.Realism.MaintenanceDiscipline, "Realism.MaintenanceDiscipline");
        ValidateUnitInterval(errors, scenario.Realism.SecurityDiscipline, "Realism.SecurityDiscipline");
        ValidateUnitInterval(errors, scenario.Realism.UmoPlanningQuality, "Realism.UmoPlanningQuality");
        ValidateUnitInterval(errors, scenario.Realism.LoadingTeamChiefQuality, "Realism.LoadingTeamChiefQuality");
        ValidateUnitInterval(errors, scenario.Realism.WeatherSeverity, "Realism.WeatherSeverity");
        ValidateUnitInterval(errors, scenario.Realism.DustExposure, "Realism.DustExposure");

        if (scenario.Nodes.Count == 0)
        {
            errors.Add("At least one node is required.");
        }

        if (scenario.Routes.Count == 0)
        {
            errors.Add("At least one route is required.");
        }

        var nodeIds = scenario.Nodes.Select(n => n.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var route in scenario.Routes)
        {
            if (!nodeIds.Contains(route.StartNodeId) || !nodeIds.Contains(route.EndNodeId))
            {
                errors.Add($"Route {route.RouteId} references missing node(s).");
            }

            if (route.EstimatedTravelTimeMinutes <= 0)
            {
                errors.Add($"Route {route.RouteId} must have positive travel time.");
            }
        }

        var routeIds = scenario.Routes.Select(r => r.RouteId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var incidentSeed in scenario.IncidentSeeds)
        {
            if (!routeIds.Contains(incidentSeed.RouteId))
            {
                errors.Add($"Incident seed {incidentSeed.IncidentType} references unknown route {incidentSeed.RouteId}.");
            }

            if (incidentSeed.Probability < 0 || incidentSeed.Probability > 1)
            {
                errors.Add($"Incident probability on route {incidentSeed.RouteId} must be between 0 and 1.");
            }
        }

        return errors;
    }

    private static void ValidateUnitInterval(List<string> errors, double value, string fieldName)
    {
        if (value < 0 || value > 1)
        {
            errors.Add($"{fieldName} must be between 0 and 1.");
        }
    }
}
