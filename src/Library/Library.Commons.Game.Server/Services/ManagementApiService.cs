using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Library.Commons.Api.Entities.Requests.Player;
using Library.Commons.Api.Entities.Requests.Room;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Library.Commons.Game.Server.Services;

internal class ManagementApiService: IManagementApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagementApiService> _logger;
    private readonly IConfiguration _configuration;

    public ManagementApiService(HttpClient httpClient, ILogger<ManagementApiService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> PlayerJoinRoomAsync(string playerConnectionId,string playerName, IRoom room, string? joinTicket = null)
    {

        try
        {

            if (!BuildServerUri(out var rootUri)) return false;

            var request = new PlayerJoinedRequest
            {
                GameServerUrl = JoinTicketQuery.AppendTo(rootUri, joinTicket),
                Nickname = playerName,
                RoomId = room.Guid
            };
            var jsonContent = JsonContent.Create(request);
            var response = await _httpClient.PostAsync($"player/{playerConnectionId}/joined", jsonContent);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending player joined room request to management api");
            return false;
        }
    }

    public Task<bool> PlayerJoinFailedAsync(string playerConnectionId, string reason)
        => PostAsync($"player/{playerConnectionId}/join-failed", new FailureRequest { Reason = reason });

    public async Task<bool> RoomCreatedAsync(string requesterId, IRoom room)
    {
        if (!BuildServerUri(out var hubUrl, out var baseUrl) || room.Code is null) return false;
        return await PostAsync($"rooms/{requesterId}/created", new RoomCreatedRequest
        {
            RoomId = room.Guid,
            Code = room.Code,
            Name = room.Name,
            MaxPlayers = room.MaxPlayers,
            Gameplay = room.GameplayOf().ToMap().ToDictionary(pair => pair.Key, pair => pair.Value),
            GameServerUrl = hubUrl,
            BaseUrl = baseUrl,
        });
    }

    public Task<bool> RoomCreateFailedAsync(string requesterId, string reason)
        => PostAsync($"rooms/{requesterId}/failed", new FailureRequest { Reason = reason });

    public async Task<bool> SendHeartbeatAsync(IReadOnlyCollection<RoomSnapshot> rooms)
    {
        if (!BuildServerUri(out var hubUrl, out var baseUrl)) return false;
        return await PostAsync("rooms/heartbeat", new RoomHeartbeatRequest
        {
            GameServerUrl = hubUrl,
            BaseUrl = baseUrl,
            Rooms = rooms.Select(snapshot => new RoomSummary
            {
                Id = snapshot.Room.Guid,
                Code = snapshot.Room.Code,
                Name = snapshot.Room.Name,
                IsPrivate = snapshot.Room.IsPrivate,
                Players = snapshot.Players,
                MaxPlayers = snapshot.Room.MaxPlayers,
                CreatedAt = snapshot.Room.CreatedAt,
                Gameplay = snapshot.Room.GameplayOf().ToMap().ToDictionary(pair => pair.Key, pair => pair.Value),
            }).ToList(),
        });
    }

    public async Task<string?> GetGameplayJsonAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("gameplay");
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while fetching the gameplay config from the management api");
            return null;
        }
    }

    private async Task<bool> PostAsync<T>(string path, T body)
    {
        try
        {
            var response = await _httpClient.PostAsync(path, JsonContent.Create(body));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling the management api at {Path}", path);
            return false;
        }
    }

    private bool BuildServerUri(out string rootUri) => BuildServerUri(out rootUri, out _);

    private bool BuildServerUri(out string rootUri, out string baseUrl)
    {
        rootUri = string.Empty;
        baseUrl = string.Empty;
        var domain = _configuration[$"{ServerConfiguration.SectionName}:Domain"];
        if (string.IsNullOrWhiteSpace(domain))
            return false;
        var scheme = _configuration[$"{ServerConfiguration.SectionName}:Scheme"];
        if (string.IsNullOrWhiteSpace(scheme))
            return false;
        var port= BuildPort(scheme);
        
        if (!string.IsNullOrWhiteSpace(port))
            domain = $"{domain}:{port}";
        if(scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase) && port!.Equals("80"))
            domain = domain.Replace(":80", string.Empty);
        if(scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase) && port!.Equals("443"))
            domain = domain.Replace(":443", string.Empty);

        var path = _configuration[$"{ServerConfiguration.SectionName}:Path"];
        if (!string.IsNullOrWhiteSpace(path))
            path = path!.StartsWith("/") ? path : $"/{path}";
        var publicBaseUrl = $"{scheme}://{domain}{path}";
        rootUri = $"{publicBaseUrl}{HubEndpoints.Game}";
        // Players use the public address; the manager can use a shorter in-cluster one to reach this server.
        var internalUrl = _configuration[$"{ServerConfiguration.SectionName}:InternalUrl"];
        baseUrl = string.IsNullOrWhiteSpace(internalUrl) ? publicBaseUrl : internalUrl.TrimEnd('/');
        return true;
    }

    private string? BuildPort(string scheme)
    {
        var configurationPort= _configuration.GetSection($"{ServerConfiguration.SectionName}:Port");
        if (string.IsNullOrWhiteSpace(configurationPort.Value))
            configurationPort = scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase)
                ? _configuration.GetSection("ASPNETCORE_HTTPS_PORT")
                : _configuration.GetSection("ASPNETCORE_HTTP_PORTS");
        return configurationPort.Value is "80" or "443" ? string.Empty : configurationPort.Value;
    }
}