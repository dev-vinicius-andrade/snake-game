using Application.Game.Server.Hubs;
using Application.Game.Server.Services;
using Application.Game.Server.Stats;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class HubAchievementsNotifierTests
{
    private readonly IHubContext<GameHub, IGameHubSendEvents> _hub = Substitute.For<IHubContext<GameHub, IGameHubSendEvents>>();
    private readonly IGameHubSendEvents _targets = Substitute.For<IGameHubSendEvents>();
    private readonly AccountConnections _connections = new();
    private readonly HubAchievementsNotifier _notifier;
    private readonly Guid _account = Guid.NewGuid();
    private IReadOnlyList<string>? _sentTo;

    public HubAchievementsNotifierTests()
    {
        _hub.Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(call =>
        {
            _sentTo = call.ArgAt<IReadOnlyList<string>>(0);
            return _targets;
        });
        _notifier = new HubAchievementsNotifier(_hub, _connections);
    }

    [Fact]
    public async Task NotifyAsync_SendsTheIdsToEveryConnectionOfTheAccount()
    {
        _connections.Add("c1", _account);
        _connections.Add("c2", _account);
        _connections.Add("someone-else", Guid.NewGuid());

        await _notifier.NotifyAsync(_account, ["first-blood", "hunter"]);

        await _targets.Received(1).AchievementsUnlocked(Arg.Is<AchievementsUnlockedEvent>(sent => sent.Ids.SequenceEqual(new[] { "first-blood", "hunter" })));
        Assert.Equal(new[] { "c1", "c2" }, _sentTo!.Order());
    }

    [Fact]
    public async Task NotifyAsync_APlayerAlreadyGone_IsSkipped()
    {
        _connections.Add("c1", _account);
        _connections.Remove("c1");

        await _notifier.NotifyAsync(_account, ["first-blood"]);

        await _targets.DidNotReceiveWithAnyArgs().AchievementsUnlocked(default!);
    }

    [Fact]
    public async Task NotifyAsync_NothingUnlocked_SendsNothing()
    {
        _connections.Add("c1", _account);

        await _notifier.NotifyAsync(_account, []);

        await _targets.DidNotReceiveWithAnyArgs().AchievementsUnlocked(default!);
    }
}
