using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Application.Manager.Api.Rooms;

public sealed class EventbusRoomCreatePublisher(IEventbusPublisher<IBasicProperties> publisher) : IRoomCreatePublisher
{
    public void Publish(RoomCreatePayload payload) => publisher.Publish(payload, EventNames.RoomCreateRequest);
}

/// <summary>Talks to the game servers. They accept the same API key they use toward the manager.</summary>
public sealed class HttpGameServerClient(HttpClient http, IOptions<AuthConfiguration> auth, ILogger<HttpGameServerClient> logger) : IGameServerClient
{
    public async Task<CloseResult> CloseRoomAsync(string baseUrl, Guid roomId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl.TrimEnd('/')}/rooms/{roomId}");
            request.Headers.Add(AuthConstants.ApiKeyHeaderName, auth.Value.ApiKeys?.FirstOrDefault() ?? string.Empty);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return CloseResult.NotFound;
            return response.IsSuccessStatusCode ? CloseResult.Closed : CloseResult.Unreachable;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Could not reach the game server at {BaseUrl}", baseUrl);
            return CloseResult.Unreachable;
        }
    }
}

/// <summary>Forgets rooms of game servers that stopped sending heartbeats.</summary>
public sealed class RoomRegistryPruneService(RoomRegistry registry) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ServerTtl = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            registry.Prune(ServerTtl);
    }
}
