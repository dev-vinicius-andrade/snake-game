using Application.Game.Server.Services;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Workers;

/// <summary>Asks the manager every few seconds for the gameplay config and applies it when it changed.</summary>
public class GameplaySyncWorkerService(IManagementApiService management, GameplayApplier applier, Serilog.ILogger logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await SyncAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncAsync()
    {
        try
        {
            applier.Apply(await management.GetGameplayJsonAsync());
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Could not fetch the gameplay config");
        }
    }
}
