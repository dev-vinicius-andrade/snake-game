
using Application.Game.Server.Entities.Configurartions;
using Microsoft.Extensions.Options;

namespace Application.Game.Server;

public class GameServerWorker : BackgroundService
{
    private readonly Serilog.ILogger _logger;
    private readonly IOptions<AppSettings> _appSettings;

    public GameServerWorker(Serilog.ILogger logger, IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _appSettings = appSettings;
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