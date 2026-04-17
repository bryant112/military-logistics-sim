using Sim.Application;

namespace Sim.Api;

public sealed class SimulationTickerHostedService : BackgroundService
{
    private readonly ISimulationSessionManager _sessionManager;
    private readonly ILogger<SimulationTickerHostedService> _logger;

    public SimulationTickerHostedService(ISimulationSessionManager sessionManager, ILogger<SimulationTickerHostedService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Simulation ticker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            _sessionManager.AdvanceRunningSessions();
            await Task.Delay(250, stoppingToken);
        }
    }
}
