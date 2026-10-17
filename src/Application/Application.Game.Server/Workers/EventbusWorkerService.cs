using Application.Game.Server.Services;
using Library.Commons.Eventbus.RabbitMq.EventArgs;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using ILogger = Serilog.ILogger;

namespace Application.Game.Server.Workers;

/// <summary>Listens for players who want to join and for hosts who want a room. The decisions live in the handlers.</summary>
public class EventbusWorkerService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IEventbusConsumer<PlayerJoinPayload> _joinConsumer;
    private readonly IEventbusConsumer<RoomCreatePayload> _createConsumer;
    private readonly JoinRequestHandler _joinHandler;
    private readonly RoomCreateRequestHandler _createHandler;

    public EventbusWorkerService(
        ILogger logger,
        IEventbusConsumer<PlayerJoinPayload> joinConsumer,
        IEventbusConsumer<RoomCreatePayload> createConsumer,
        JoinRequestHandler joinHandler,
        RoomCreateRequestHandler createHandler)
    {
        _logger = logger;
        _joinConsumer = joinConsumer;
        _createConsumer = createConsumer;
        _joinHandler = joinHandler;
        _createHandler = createHandler;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _joinConsumer.OnMessageReceived += OnJoinRequest;
        _createConsumer.OnMessageReceived += OnRoomCreateRequest;
        _joinConsumer.Start();
        _createConsumer.Start();
        await base.StartAsync(cancellationToken);
    }

    private void OnJoinRequest(object? sender, EventbusConsumerMessageReceivedEventArgs<PlayerJoinPayload> e)
        => Handle(e.Response, _joinHandler.HandleAsync);

    private void OnRoomCreateRequest(object? sender, EventbusConsumerMessageReceivedEventArgs<RoomCreatePayload> e)
        => Handle(e.Response, _createHandler.HandleAsync);

    private void Handle<T>(IMessageResponse<T> response, Func<T, Task<MessageDisposition>> handler) where T : class
    {
        try
        {
            Apply(response, handler(response.Value).GetAwaiter().GetResult());
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Could not handle message {MessageId}", response.MessageId);
            response.Discard(true);
        }
    }

    private static void Apply<T>(IMessageResponse<T> response, MessageDisposition disposition) where T : class
    {
        switch (disposition)
        {
            case MessageDisposition.Acknowledge:
                response.Acknowledge();
                break;
            case MessageDisposition.Requeue:
                response.Discard(true);
                break;
            default:
                response.Discard(false);
                break;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _joinConsumer.Stop();
        _createConsumer.Stop();
        await base.StopAsync(cancellationToken);
    }
}
