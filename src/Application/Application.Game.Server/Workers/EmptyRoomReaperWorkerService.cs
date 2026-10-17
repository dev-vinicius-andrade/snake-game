using Application.Game.Server.Services;

namespace Application.Game.Server.Workers;

/// <summary>Runs <see cref="EmptyRoomReaper"/> twice a minute.</summary>
public class EmptyRoomReaperWorkerService(EmptyRoomReaper reaper, Serilog.ILogger logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SweepAsync(stoppingToken);
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await reaper.SweepAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Warning(exception, "Could not sweep empty rooms");
        }
    }
}
