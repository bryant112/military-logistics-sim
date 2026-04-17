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
}
