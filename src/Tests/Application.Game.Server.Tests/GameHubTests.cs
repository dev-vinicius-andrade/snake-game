using Application.Game.Server.Hubs;
using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Entities.Hub.Requests;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class GameHubTests
{
    private const string ConnectionId = "c1";

    private readonly IPlayerService _players = Substitute.For<IPlayerService>();
    private readonly IPlayerRoomService _playerRooms = Substitute.For<IPlayerRoomService>();
    private readonly IRoomsService _rooms = Substitute.For<IRoomsService>();
    private readonly IGameStateService _gameStates = Substitute.For<IGameStateService>();
    private readonly IRoomInputQueue _queue = Substitute.For<IRoomInputQueue>();
    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
    private readonly IGameHubSendEvents _group = Substitute.For<IGameHubSendEvents>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly List<(Guid RoomId, IPlayerCommand Command)> _enqueued = new();
    private readonly Dictionary<Guid, IPlayer> _playersInRoom = new();
    private readonly IGameState _state = Substitute.For<IGameState>();
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly IPlayer _player = Substitute.For<IPlayer>();
    private readonly GameHub _hub;
    private readonly Guid _roomGuid = Guid.NewGuid();

    public GameHubTests()
    {
        _player.Guid.Returns(Guid.NewGuid());
        _player.Id.Returns(ConnectionId);
        _room.Guid.Returns(_roomGuid);
        _state.Room.Returns(_room);
        _state.Players.Returns(_playersInRoom);
        _players.Get(ConnectionId).Returns((IPlayer?)null);
        _context.ConnectionId.Returns(ConnectionId);
        _context.ConnectionAborted.Returns(CancellationToken.None);

        var clients = Substitute.For<IHubCallerClients<IGameHubSendEvents>>();
        clients.Group(_roomGuid.ToString()).Returns(_group);

        _queue.When(queue => queue.Enqueue(Arg.Any<Guid>(), Arg.Any<IPlayerCommand>()))
            .Do(call => _enqueued.Add((call.ArgAt<Guid>(0), call.ArgAt<IPlayerCommand>(1))));

        _hub = new GameHub(_players, _playerRooms, _rooms, Substitute.For<ITrackableIdGeneratorService>(), _gameStates, _queue, _logger)
        {
            Context = _context,
            Clients = clients,
            Groups = Substitute.For<IGroupManager>()
        };
    }

    private void PlayerIsInRoom()
    {
        _playersInRoom[_player.Guid] = _player;
        var roomId = Substitute.For<ITrackableId>();
        roomId.Guid.Returns(_roomGuid);
        _players.Get(ConnectionId).Returns(_player);
        _playerRooms.GetRoomTrackableId(_player).Returns(roomId);
        _rooms.GetRoom(roomId).Returns(_room);
        _gameStates.GetGameState(_room).Returns(_state);
    }

    /// <summary>Plays the part of the room loop: runs commands as soon as they are queued.</summary>
    private void RoomLoopRunsCommandsImmediately()
        => _queue.When(queue => queue.Enqueue(Arg.Any<Guid>(), Arg.Any<IPlayerCommand>()))
            .Do(call => _ = call.ArgAt<IPlayerCommand>(1).ExecuteAsync(new CommandContext(_state, _gameStates), CancellationToken.None));

    [Fact]
    public async Task OnDisconnectedAsync_PlayerInRoom_RemovesThePlayerAndTellsTheRoom()
    {
        PlayerIsInRoom();
        RoomLoopRunsCommandsImmediately();

        await _hub.OnDisconnectedAsync(null);

        await _gameStates.Received(1).DisconnectPlayer(_room, _player);
        await _group.Received(1).PlayerLefted(Arg.Is<PlayerLeftedEvent>(left =>
            left.PlayerId == ConnectionId && left.PlayerTrackableId == _player.Guid && left.RoomId == _roomGuid));
        await _group.Received(1).ConnectedPlayersUpdated(Arg.Any<ConnectedPlayersUpdatedEvent>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_QueuesALeaveCommandForTheRoomOfThePlayer()
    {
        PlayerIsInRoom();
        RoomLoopRunsCommandsImmediately();

        await _hub.OnDisconnectedAsync(null);

        var (roomId, command) = Assert.Single(_enqueued);
        Assert.Equal(_roomGuid, roomId);
        Assert.IsType<LeaveCommand>(command);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ConnectionAlreadyAborted_StillCompletesTheLeave()
    {
        PlayerIsInRoom();
        RoomLoopRunsCommandsImmediately();
        _context.ConnectionAborted.Returns(new CancellationToken(canceled: true));

        await _hub.OnDisconnectedAsync(null);

        await _gameStates.Received(1).DisconnectPlayer(_room, _player);
        await _group.Received(1).PlayerLefted(Arg.Any<PlayerLeftedEvent>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_UnknownConnection_DoesNothing()
    {
        await _hub.OnDisconnectedAsync(null);

        Assert.Empty(_enqueued);
        await _group.DidNotReceive().PlayerLefted(Arg.Any<PlayerLeftedEvent>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_PlayerAlreadyRemoved_SaysNothing()
    {
        PlayerIsInRoom();
        _playersInRoom.Clear();
        RoomLoopRunsCommandsImmediately();

        await _hub.OnDisconnectedAsync(null);

        await _group.DidNotReceive().PlayerLefted(Arg.Any<PlayerLeftedEvent>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_QueueFails_DoesNotThrowAndLogs()
    {
        PlayerIsInRoom();
        _queue.When(queue => queue.Enqueue(Arg.Any<Guid>(), Arg.Any<IPlayerCommand>())).Do(_ => throw new InvalidOperationException("boom"));

        await _hub.OnDisconnectedAsync(null);

        Assert.Contains(_logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Error");
    }

    [Fact]
    public async Task LeaveRoom_PlayerInRoom_RemovesThePlayerAndTellsTheRoom()
    {
        PlayerIsInRoom();
        RoomLoopRunsCommandsImmediately();

        await _hub.LeaveRoom(new LeaveRoomRequest());

        await _gameStates.Received(1).DisconnectPlayer(_room, _player);
        await _group.Received(1).PlayerLefted(Arg.Any<PlayerLeftedEvent>());
    }

    [Fact]
    public async Task ChangeDirection_PlayerInRoom_QueuesTheCommand()
    {
        PlayerIsInRoom();

        await _hub.ChangeDirection(new ChangeDirectionRequest { Direction = Library.Commons.Game.Domain.Enums.Direction.Up });

        var (roomId, command) = Assert.Single(_enqueued);
        Assert.Equal(_roomGuid, roomId);
        Assert.IsType<ChangeDirectionCommand>(command);
    }

    [Fact]
    public async Task Shoot_PlayerInRoom_QueuesTheCommand()
    {
        PlayerIsInRoom();

        await _hub.Shoot(new ShootRequest());

        Assert.IsType<ShootCommand>(Assert.Single(_enqueued).Command);
    }

    [Fact]
    public async Task Steer_PlayerInRoom_QueuesTheCommand()
    {
        PlayerIsInRoom();

        await _hub.Steer(new SteerRequest(1.5));

        Assert.IsType<SteerCommand>(Assert.Single(_enqueued).Command);
    }

    [Fact]
    public async Task Boost_PlayerInRoom_QueuesTheCommand()
    {
        PlayerIsInRoom();

        await _hub.Boost(new BoostRequest(true));

        Assert.IsType<SetBoostCommand>(Assert.Single(_enqueued).Command);
    }

    [Fact]
    public async Task Controls_WithoutAPlayer_QueueNothing()
    {
        await _hub.ChangeDirection(new ChangeDirectionRequest());
        await _hub.Shoot(new ShootRequest());
        await _hub.Boost(new BoostRequest(true));
        await _hub.Steer(new SteerRequest(1.5));

        Assert.Empty(_enqueued);
    }
}
