using Application.Game.Server.Entities.Configurations;
using Library.Commons.Eventbus.RabbitMq.EventArgs;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using ILogger = Serilog.ILogger;

namespace Application.Game.Server.Workers;

public class EventbusWorkerService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IEventbusConsumer<PlayerJoinPayload> _newPlayerEventbusConsumer;
    private readonly IOptions<AppSettings> _appSettings;

    private readonly IRoomsService _roomsService;
    private readonly IManagementApiService _managementApiService;
    public EventbusWorkerService(IOptions<AppSettings> appSettings, ILogger logger, IEventbusConsumer<PlayerJoinPayload> newPlayerEventbusConsumer, IPlayerService playerService, IRoomsService roomsService, IManagementApiService managementApiService)
    {
        _appSettings = appSettings;
        _logger = logger;
        _newPlayerEventbusConsumer = newPlayerEventbusConsumer;
        _roomsService = roomsService;
        _managementApiService = managementApiService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        StartEventbusConsumers();
        SetupEventBusHandlers();
        await base.StartAsync(cancellationToken);
    }

    private void StartEventbusConsumers()
    {
        _newPlayerEventbusConsumer.Start();
    }

    private void SetupEventBusHandlers()
    {
        _newPlayerEventbusConsumer.OnMessageReceived += NewPlayerEventbusConsumerOnOnMessageReceived;
    }

    private void NewPlayerEventbusConsumerOnOnMessageReceived(object? sender, EventbusConsumerMessageReceivedEventArgs<PlayerJoinPayload> e)
    {
        var payload = e.Response.Value;
        if (TryGetOrCreateRoom(out var room)) return;
        if (room is null) e.Response.Discard(true);
        _managementApiService.PlayerJoinRoomAsync(payload.Id, payload.Nickname, room!).Wait();
        e.Response.Acknowledge();
    }

    private bool TryGetOrCreateRoom(out IRoom? room)
    {
        var availableRooms = _roomsService.GetAvailableRooms().ToList();
        room = null;
        if (!availableRooms.Any())
        {
            if (!_roomsService.CanCreate())
                return false;
            room = _roomsService.CreateRoom();
        }
        else
        {
            room = availableRooms.First();

        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _newPlayerEventbusConsumer.Start();
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.Information("EventbusWorkerService running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
        await StopAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopEventbusConsumers();
        await base.StopAsync(cancellationToken);
    }

    private void StopEventbusConsumers()
    {
        _newPlayerEventbusConsumer.Stop();
    }
}