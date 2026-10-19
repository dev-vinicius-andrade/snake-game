using System.Net.Http.Json;
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
        var call = await SendAsync(HttpMethod.Delete, baseUrl, $"rooms/{roomId}", null, cancellationToken);
        return call switch { ServerCall.NotFound => CloseResult.NotFound, ServerCall.Unreachable => CloseResult.Unreachable, ServerCall.Refused => CloseResult.Unreachable, _ => CloseResult.Closed };
    }

    public Task<ServerCall> SetGameplayAsync(string baseUrl, Guid roomId, IReadOnlyDictionary<string, string> choices, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Put, baseUrl, $"rooms/{roomId}/gameplay", JsonContent.Create(choices), cancellationToken);

    public Task<ServerCall> TriggerEventAsync(string baseUrl, Guid roomId, string kind, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, baseUrl, $"rooms/{roomId}/events/{Uri.EscapeDataString(kind)}", null, cancellationToken);

    private async Task<ServerCall> SendAsync(HttpMethod method, string baseUrl, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{baseUrl.TrimEnd('/')}/{path}") { Content = content };
            request.Headers.Add(AuthConstants.ApiKeyHeaderName, auth.Value.ApiKeys?.FirstOrDefault() ?? string.Empty);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return ServerCall.NotFound;
            if (response.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest) return ServerCall.Refused;
            return response.IsSuccessStatusCode ? ServerCall.Done : ServerCall.Unreachable;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Could not reach the game server at {BaseUrl}", baseUrl);
            return ServerCall.Unreachable;
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
