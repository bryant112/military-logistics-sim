using Sim.Application;

namespace Sim.Infrastructure;

public sealed class FileScenarioSource : IScenarioSource
{
    private readonly string _defaultScenarioPath;

    public FileScenarioSource(string defaultScenarioPath)
    {
        _defaultScenarioPath = defaultScenarioPath;
    }

    public Task<string> LoadScenarioJsonAsync(string? scenarioPath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(scenarioPath) ? _defaultScenarioPath : scenarioPath;
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Scenario file not found: {resolvedPath}");
        }

        return File.ReadAllTextAsync(resolvedPath, cancellationToken);
    }
}
