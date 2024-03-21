using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Library.Commons.Api.Entities.Requests.Player;
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

    public async Task<bool> PlayerJoinRoomAsync(string playerConnectionId,string playerName, IRoom room)
    {

        try
        {

            if (!BuildServerUri(out var rootUri)) return false;

            var request = new PlayerJoinedRequest
            {
                GameServerUrl = rootUri,
                Nickname = playerName,
                RoomId = room.Guid
            };
            var jsonContent = JsonContent.Create(request);
            var response = await _httpClient.PostAsync($"/join/{playerConnectionId}", jsonContent);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending player joined room request to management api");
            return false;
        }
    }

    private bool BuildServerUri(out string rootUri)
    {
        rootUri = string.Empty;
        var domain = _configuration[$"{ServerConfiguration.SectionName}:Domain"];
        if (string.IsNullOrWhiteSpace(domain))
            return false;
        var scheme = _configuration[$"{ServerConfiguration.SectionName}:Scheme"];
        if (string.IsNullOrWhiteSpace(scheme))
            return false;
        var port= scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase) ? _configuration["ASPNETCORE_HTTPS_PORT"] : _configuration["ASPNETCORE_HTTP_PORTS"];
        
        if (!string.IsNullOrWhiteSpace(port))
            domain = $"{domain}:{port}";
        if(scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase) && port!.Equals("80"))
            domain = domain.Replace(":80", string.Empty);
        if(scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase) && port!.Equals("443"))
            domain = domain.Replace(":443", string.Empty);

        rootUri = $"{scheme}://{domain}{HubEndpoints.Game}";
        return true;
    }
}