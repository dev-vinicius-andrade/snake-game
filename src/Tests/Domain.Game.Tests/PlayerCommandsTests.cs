using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class PlayerCommandsTests
{
    private const string ConnectionId = "conn-1";

    private readonly IGameStateService _service = Substitute.For<IGameStateService>();
    private readonly IGameState _state = Substitute.For<IGameState>();
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly Dictionary<Guid, IPlayer> _players = new();
    private readonly CommandContext _context;

    public PlayerCommandsTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _state.Room.Returns(_room);
        _state.Players.Returns(_players);
        _context = new CommandContext(_state, _service);
    }

    private IPlayer AddPlayer(string connectionId = ConnectionId, bool isAlive = true)
    {
        var player = Substitute.For<IPlayer>();
        player.Id.Returns(connectionId);
        player.IsAlive = isAlive;
        _players[Guid.NewGuid()] = player;
        return player;
    }

    [Fact]
    public async Task ChangeDirection_Executed_ForwardsToServiceAndReportsResult()
    {
        _service.ChangeDirectionByPlayerId(ConnectionId, Direction.Left).Returns(true);
        var command = new ChangeDirectionCommand(ConnectionId, Direction.Left);

        await command.ExecuteAsync(_context, CancellationToken.None);

        Assert.True(await command.Completion);
    }

    [Fact]
    public async Task Shoot_AlivePlayer_Shoots()
    {
        var player = AddPlayer();
        var command = new ShootCommand(ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        await _service.Received(1).Shoot(_state, _room, player);
        Assert.True(await command.Completion);
    }

    [Fact]
    public async Task Shoot_UnknownConnection_DoesNothing()
    {
        AddPlayer("someone else");
        var command = new ShootCommand(ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        await _service.DidNotReceiveWithAnyArgs().Shoot(default!, default!, default!);
        Assert.False(await command.Completion);
    }

    [Fact]
    public async Task Shoot_DeadPlayer_DoesNothing()
    {
        AddPlayer(isAlive: false);
        var command = new ShootCommand(ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        await _service.DidNotReceiveWithAnyArgs().Shoot(default!, default!, default!);
        Assert.False(await command.Completion);
    }

    [Fact]
    public async Task Leave_KnownPlayer_DisconnectsAndReturnsPlayer()
    {
        var player = AddPlayer();
        var command = new LeaveCommand(ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        await _service.Received(1).DisconnectPlayer(_room, player);
        Assert.Same(player, await command.Completion);
    }

    [Fact]
    public async Task Leave_UnknownConnection_ReturnsNull()
    {
        var command = new LeaveCommand(ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        Assert.Null(await command.Completion);
        await _service.DidNotReceiveWithAnyArgs().DisconnectPlayer(default!, default!);
    }

    [Fact]
    public async Task JoinNewPlayer_Executed_ReturnsCreatedPlayer()
    {
        var roomId = Guid.NewGuid();
        var created = Substitute.For<IPlayer>();
        _service.CreatePlayerInRoom(ConnectionId, "ana", roomId, Arg.Any<CancellationToken>()).Returns(created);
        var command = new JoinNewPlayerCommand(ConnectionId, "ana", roomId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        Assert.Same(created, await command.Completion);
    }

    [Fact]
    public async Task JoinNewPlayer_RoomRefuses_ReturnsNull()
    {
        _service.CreatePlayerInRoom(default!, default!, default, default).ReturnsForAnyArgs(Task.FromResult<IPlayer?>(null));
        var command = new JoinNewPlayerCommand(ConnectionId, "ana", Guid.NewGuid());

        await command.ExecuteAsync(_context, CancellationToken.None);

        Assert.Null(await command.Completion);
    }

    [Fact]
    public async Task Reconnect_Succeeds_ReturnsPlayerAndOldConnection()
    {
        var player = Substitute.For<IPlayer>();
        var roomId = Substitute.For<ITrackableId>();
        var playerId = Substitute.For<ITrackableId>();
        _service.ReconnectPlayer(roomId, playerId, ConnectionId).Returns((player, "old-conn"));
        var command = new ReconnectPlayerCommand(roomId, playerId, ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        var result = await command.Completion;
        Assert.Same(player, result!.Player);
        Assert.Equal("old-conn", result.OldConnectionId);
    }

    [Fact]
    public async Task Reconnect_UnknownPlayer_ReturnsNull()
    {
        _service.ReconnectPlayer(default!, default!, default!)
            .ReturnsForAnyArgs(Task.FromResult<(IPlayer player, string oldPlayerConnectionId)?>(null));
        var command = new ReconnectPlayerCommand(
            Substitute.For<ITrackableId>(), Substitute.For<ITrackableId>(), ConnectionId);

        await command.ExecuteAsync(_context, CancellationToken.None);

        Assert.Null(await command.Completion);
    }

    [Fact]
    public async Task Abandon_BeforeExecution_CancelsCompletion()
    {
        var command = new ShootCommand(ConnectionId);

        command.Abandon();

        await Assert.ThrowsAsync<TaskCanceledException>(() => command.Completion);
    }
}
