
namespace Application.Game.Server;

public class GameServerWorker : BackgroundService
{
    private readonly Serilog.ILogger _logger;

    public GameServerWorker(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.Information("GameServerWorker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}