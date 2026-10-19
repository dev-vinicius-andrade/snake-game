using Application.Game.Server.Stats;

namespace Application.Game.Server.Workers;

/// <summary>Sends the stats of ended lives to the manager, away from the game loop (see <see cref="HttpStatsReporter"/>).</summary>
public sealed class StatsReportWorkerService(HttpStatsReporter reporter) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await reporter.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
