using Application.Game.Server.Services;
using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Application.Game.Server.Tests;

public sealed class JoinRequestHandlerTests
{
    private readonly IRoomJoinRouter _router = Substitute.For<IRoomJoinRouter>();
    private readonly IManagementApiService _management = Substitute.For<IManagementApiService>();
    private readonly JoinRequestHandler _handler;
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly PlayerJoinPayload _payload = new() { Id = "c1", Nickname = "Ann" };

    public JoinRequestHandlerTests() => _handler = new JoinRequestHandler(_router, _management);

    [Fact]
    public async Task HandleAsync_RoomFound_TellsTheManagerAndAcknowledges()
    {
        _router.Resolve(null).Returns(new JoinRoute(JoinRouteOutcome.Joined, _room));

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Acknowledge, result);
        await _management.Received(1).PlayerJoinRoomAsync("c1", "Ann", _room);
    }

    [Fact]
    public async Task HandleAsync_AsksForTheRoomTheClientWants()
    {
        var roomId = Guid.NewGuid();
        _payload.RoomId = roomId;
        _router.Resolve(roomId).Returns(new JoinRoute(JoinRouteOutcome.Joined, _room));

        await _handler.HandleAsync(_payload);

        _router.Received(1).Resolve(roomId);
    }

    [Theory]
    [InlineData(JoinRouteOutcome.NotHere)]
    [InlineData(JoinRouteOutcome.NoCapacity)]
    public async Task HandleAsync_NotForThisServer_PutsTheMessageBackForAnotherOne(JoinRouteOutcome outcome)
    {
        _router.Resolve(null).Returns(new JoinRoute(outcome));

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Requeue, result);
        await _management.DidNotReceiveWithAnyArgs().PlayerJoinRoomAsync(default!, default!, default!);
    }

    [Fact]
    public async Task HandleAsync_RoomFull_TellsThePlayerAndDropsTheMessage()
    {
        _router.Resolve(null).Returns(new JoinRoute(JoinRouteOutcome.Full));

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Discard, result);
        await _management.Received(1).PlayerJoinFailedAsync("c1", JoinFailureReasons.RoomFull);
    }
}

public sealed class RoomCreateRequestHandlerTests
{
    private readonly IRoomsService _rooms = Substitute.For<IRoomsService>();
    private readonly IManagementApiService _management = Substitute.For<IManagementApiService>();
    private readonly RoomCreateRequestHandler _handler;
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly RoomCreatePayload _payload = new() { RequesterId = "c1", Name = "Friends", Code = "ABC234", MaxPlayers = 4 };

    public RoomCreateRequestHandlerTests() => _handler = new RoomCreateRequestHandler(_rooms, _management);

    [Fact]
    public async Task HandleAsync_ServerHasCapacity_CreatesAPrivateRoomAndTellsTheManager()
    {
        _rooms.IsAvailable().Returns(true);
        _rooms.CreateRoom(Arg.Any<RoomOptions>()).Returns(_room);

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Acknowledge, result);
        _rooms.Received(1).CreateRoom(Arg.Is<RoomOptions>(o => o.IsPrivate && o.Name == "Friends" && o.Code == "ABC234" && o.MaxPlayers == 4));
        await _management.Received(1).RoomCreatedAsync("c1", _room);
    }

    [Fact]
    public async Task HandleAsync_NoCapacity_PutsTheMessageBackForAnotherServer()
    {
        _rooms.IsAvailable().Returns(false);

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Requeue, result);
        _rooms.DidNotReceiveWithAnyArgs().CreateRoom(default);
    }

    [Fact]
    public async Task HandleAsync_CodeAlreadyTaken_TellsTheManagerItFailed()
    {
        _rooms.IsAvailable().Returns(true);
        _rooms.CreateRoom(Arg.Any<RoomOptions>()).Throws(new UnableToCreateRoomException());

        var result = await _handler.HandleAsync(_payload);

        Assert.Equal(MessageDisposition.Discard, result);
        await _management.Received(1).RoomCreateFailedAsync("c1", Arg.Any<string>());
    }
}

file sealed record FakeTrackableId(Guid Guid) : ITrackableId;

public sealed class RoomCloserTests
{
    private readonly IRoomsService _rooms = Substitute.For<IRoomsService>();
    private readonly IPlayerRoomService _playerRooms = Substitute.For<IPlayerRoomService>();
    private readonly IGameStateService _gameStates = Substitute.For<IGameStateService>();
    private readonly IRoomInputQueue _queue = Substitute.For<IRoomInputQueue>();
    private readonly IRoomClosedNotifier _notifier = Substitute.For<IRoomClosedNotifier>();
    private readonly ITrackableIdGeneratorService _ids = Substitute.For<ITrackableIdGeneratorService>();
    private readonly RoomCloser _closer;
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly List<IPlayerCommand> _commands = new();

    public RoomCloserTests()
    {
        _room.Guid.Returns(_roomId);
        _ids.Generate(Arg.Any<Guid?>()).Returns(call => new FakeTrackableId(call.Arg<Guid?>()!.Value));
        _closer = new RoomCloser(_rooms, _playerRooms, _gameStates, _queue, _notifier, _ids);
        _queue.When(queue => queue.Enqueue(_roomId, Arg.Any<IPlayerCommand>())).Do(call => _commands.Add(call.ArgAt<IPlayerCommand>(1)));
    }

    private void RoomHasPlayers(params string[] connectionIds)
    {
        var players = new List<IPlayer>();
        foreach (var connectionId in connectionIds)
        {
            var player = Substitute.For<IPlayer>();
            player.Id.Returns(connectionId);
            players.Add(player);
        }
        var inRooms = new Dictionary<Guid, IList<IPlayer>> { [_roomId] = players };
        _playerRooms.GetRoomsPlayers().Returns(inRooms);
    }

    [Fact]
    public async Task CloseAsync_UnknownRoom_ReturnsFalse()
    {
        _rooms.TryGetRoom(_roomId).Returns((IRoom?)null);

        Assert.False(await _closer.CloseAsync(_roomId));
    }

    [Fact]
    public async Task CloseAsync_TellsThePlayersFirstAndThenRemovesTheRoom()
    {
        _rooms.TryGetRoom(_roomId).Returns(_room);
        RoomHasPlayers("a", "b");

        var closed = await _closer.CloseAsync(_roomId);

        Assert.True(closed);
        Received.InOrder(() =>
        {
            _notifier.NotifyClosedAsync(_roomId, Arg.Any<CancellationToken>());
            _playerRooms.RemoveRoom(_roomId);
            _gameStates.RemoveGameState(Arg.Is<ITrackableId>(id => id.Guid == _roomId));
            _rooms.DeleteRoom(Arg.Is<ITrackableId>(id => id.Guid == _roomId));
        });
    }

    [Fact]
    public async Task CloseAsync_MakesEveryPlayerLeaveThroughTheRoomQueue()
    {
        _rooms.TryGetRoom(_roomId).Returns(_room);
        RoomHasPlayers("a", "b");

        await _closer.CloseAsync(_roomId, TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, _commands.OfType<LeaveCommand>().Count());
    }

    [Fact]
    public async Task CloseAsync_PlayersThatNeverLeave_DoNotBlockTheClose()
    {
        _rooms.TryGetRoom(_roomId).Returns(_room);
        RoomHasPlayers("a");

        var closed = await _closer.CloseAsync(_roomId, TimeSpan.FromMilliseconds(50));

        Assert.True(closed);
        _rooms.Received(1).DeleteRoom(Arg.Any<ITrackableId>());
    }
}
