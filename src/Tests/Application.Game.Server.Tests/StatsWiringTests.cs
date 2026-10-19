using Application.Game.Server.Services;
using Application.Game.Server.Stats;
using Application.Game.Server.Workers;
using Domain.Game.Extensions;
using Library.Commons.Api.Contants;
using Library.Commons.Entities.Configurations;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class StatsWiringTests
{
    private readonly IAccountStatsClient _client = Substitute.For<IAccountStatsClient>();

    private ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddOptions();
        services.AddSingleton(TimeProvider.System);
        services.AddGameServices();
        services.AddGameState();
        services.AddRooms();
        services.AddPlayers();
        services.AddPlayerRooms();
        services.AddSingleton(Substitute.For<IManagementApiService>());
        services.AddJoinTickets();
        services.AddAccountStats(new ApiIntegrationConfiguration { BaseUrl = "http://manager:5000/", ApiKey = "the-key" });
        services.AddSingleton(_client);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddAccountStats_TheGameAndTheWorkerShareOneReporter()
    {
        using var provider = Provider();

        Assert.Same(provider.GetRequiredService<HttpStatsReporter>(), provider.GetRequiredService<IStatsReporter>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is StatsReportWorkerService);
        Assert.IsType<HubAchievementsNotifier>(provider.GetRequiredService<IAchievementsNotifier>());
    }

    [Fact]
    public void AddAccountStats_TalksToTheManagerWithItsApiKey()
    {
        using var provider = Provider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ManagerAccountStatsClient.HttpClientName);

        Assert.Equal(new Uri("http://manager:5000/"), client.BaseAddress);
        Assert.Equal("the-key", Assert.Single(client.DefaultRequestHeaders.GetValues(AuthConstants.ApiKeyHeaderName)));
    }

    [Fact]
    public async Task GameState_RemovingASignedInPlayer_QueuesItsReportForTheManager()
    {
        using var provider = Provider();
        var games = provider.GetRequiredService<IGameStateService>();
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        var state = games.CreateGameState(room);
        var player = Substitute.For<IPlayer>();
        player.Guid.Returns(Guid.NewGuid());
        state.Players.Add(player.Guid, player);
        var accountId = Guid.NewGuid();
        games.AttachAccount(player, accountId);
        _client.SendAsync(default, default!, default).ReturnsForAnyArgs(StatsDelivery.Accepted([]));

        await games.DisconnectPlayer(room, player);
        await provider.GetRequiredService<HttpStatsReporter>().DrainAsync();

        await _client.Received(1).SendAsync(accountId, Arg.Is<LifeStatsReport>(report => report.Counters[StatKeys.Games] == 1), Arg.Any<CancellationToken>());
    }
}
