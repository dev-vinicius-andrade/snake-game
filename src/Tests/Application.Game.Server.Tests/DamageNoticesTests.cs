using Application.Game.Server.Services;
using System.Collections.Concurrent;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class DamageNoticesTests
{
    private readonly IGameState _state = Substitute.For<IGameState>();

    public DamageNoticesTests()
    {
        _state.Players.Returns(new Dictionary<Guid, IPlayer>());
        _state.Hits.Returns(new ConcurrentQueue<DamageDealt>());
    }

    private IPlayer AddPlayer(string connectionId)
    {
        var player = Substitute.For<IPlayer>();
        player.Guid.Returns(Guid.NewGuid());
        player.Id.Returns(connectionId);
        _state.Players.Add(player.Guid, player);
        return player;
    }

    [Fact]
    public void Drain_TellsTheShooterAndTheTargetOnce()
    {
        var shooter = AddPlayer("shooter-conn");
        var target = AddPlayer("target-conn");
        _state.Hits.Enqueue(new DamageDealt(shooter.Guid, target.Guid, 10, 20, 33.5, DamageKind.Hit));

        var notices = DamageNotices.Drain(_state);

        Assert.Equal(new[] { "shooter-conn", "target-conn" }, notices.Select(notice => notice.ConnectionId).OrderByDescending(id => id).Reverse());
        Assert.All(notices, notice =>
        {
            Assert.Equal(shooter.Guid, notice.Event.ShooterGuid);
            Assert.Equal(target.Guid, notice.Event.TargetGuid);
            Assert.Equal(10, notice.Event.X);
            Assert.Equal(20, notice.Event.Y);
            Assert.Equal(33.5, notice.Event.Damage);
            Assert.Equal(DamageKind.Hit, notice.Event.Kind);
        });
    }

    [Fact]
    public void Drain_EmptiesTheQueue()
    {
        var shooter = AddPlayer("a");
        var target = AddPlayer("b");
        _state.Hits.Enqueue(new DamageDealt(shooter.Guid, target.Guid, 0, 0, 1, DamageKind.Hit));

        DamageNotices.Drain(_state);

        Assert.Empty(_state.Hits);
        Assert.Empty(DamageNotices.Drain(_state));
    }

    [Fact]
    public void Drain_ShooterAlreadyGone_OnlyTheTargetIsTold()
    {
        var target = AddPlayer("target-conn");
        _state.Hits.Enqueue(new DamageDealt(Guid.NewGuid(), target.Guid, 0, 0, 5, DamageKind.Hit));

        var notice = Assert.Single(DamageNotices.Drain(_state));

        Assert.Equal("target-conn", notice.ConnectionId);
    }

    [Fact]
    public void Drain_ManyHits_KeepsTheirOrder()
    {
        var shooter = AddPlayer("a");
        var target = AddPlayer("b");
        _state.Hits.Enqueue(new DamageDealt(shooter.Guid, target.Guid, 0, 0, 1, DamageKind.Hit));
        _state.Hits.Enqueue(new DamageDealt(shooter.Guid, target.Guid, 0, 0, 2, DamageKind.Broke));

        var damages = DamageNotices.Drain(_state).Where(notice => notice.ConnectionId == "a").Select(notice => notice.Event.Damage);

        Assert.Equal(new[] { 1.0, 2.0 }, damages);
    }
}
