using Application.Game.Server.Services;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class EmptyRoomReaperTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly TestClock _clock = new();
    private readonly IRoomsService _rooms = Substitute.For<IRoomsService>();
    private readonly IPlayerRoomService _playerRooms = Substitute.For<IPlayerRoomService>();
    private readonly IRoomCloser _closer = Substitute.For<IRoomCloser>();
    private readonly EmptyRoomReaper _reaper;
    private readonly RoomLifetimeSettings _settings = new(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 10 });
    private readonly List<IRoom> _roomList = new();

    public EmptyRoomReaperTests()
    {
        _rooms.GetRooms().Returns(_ => _roomList);
        _reaper = new EmptyRoomReaper(_rooms, _playerRooms, _closer, _clock, _settings);
    }

    private IRoom AddRoom(bool isPrivate = true, int players = 0, DateTimeOffset? createdAt = null)
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.IsPrivate.Returns(isPrivate);
        room.CreatedAt.Returns(createdAt ?? _clock.Now);
        _playerRooms.CountPlayers(room).Returns(players);
        _roomList.Add(room);
        return room;
    }

    [Fact]
    public async Task SweepAsync_PrivateRoomEmptyLongEnough_IsClosed()
    {
        var room = AddRoom();
        _clock.Advance(Ttl + TimeSpan.FromSeconds(1));

        await _reaper.SweepAsync();

        await _closer.Received(1).CloseAsync(room.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_EmptyForLessThanTheLimit_IsKept()
    {
        AddRoom();
        _clock.Advance(Ttl - TimeSpan.FromSeconds(1));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_PublicRoom_IsNeverClosed()
    {
        AddRoom(isPrivate: false);
        _clock.Advance(Ttl * 3);

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_RoomWithPlayers_IsKept()
    {
        AddRoom(players: 2);
        _clock.Advance(Ttl * 3);

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_TheLimitCountsFromTheMomentTheRoomBecameEmpty()
    {
        var room = AddRoom(players: 2);
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _reaper.SweepAsync();
        _playerRooms.CountPlayers(room).Returns(0);
        await _reaper.SweepAsync();
        _clock.Advance(Ttl - TimeSpan.FromSeconds(1));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_RoomThatFilledUpAgain_StartsOver()
    {
        var room = AddRoom();
        _clock.Advance(TimeSpan.FromMinutes(9));
        await _reaper.SweepAsync();
        _playerRooms.CountPlayers(room).Returns(1);
        await _reaper.SweepAsync();
        _playerRooms.CountPlayers(room).Returns(0);
        await _reaper.SweepAsync();
        _clock.Advance(TimeSpan.FromMinutes(9));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_RoomNeverJoined_CountsFromItsCreation()
    {
        var room = AddRoom(createdAt: _clock.Now - Ttl - TimeSpan.FromSeconds(1));

        await _reaper.SweepAsync();

        await _closer.Received(1).CloseAsync(room.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_ClosedRoom_IsNotClosedTwice()
    {
        AddRoom();
        _clock.Advance(Ttl + TimeSpan.FromSeconds(1));
        await _reaper.SweepAsync();
        _roomList.Clear();

        await _reaper.SweepAsync();

        await _closer.ReceivedWithAnyArgs(1).CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_OneRoomFailingToClose_DoesNotStopTheOthers()
    {
        var failing = AddRoom();
        var other = AddRoom();
        _closer.CloseAsync(failing.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns<Task<bool>>(_ => throw new InvalidOperationException("boom"));
        _clock.Advance(Ttl + TimeSpan.FromSeconds(1));

        await _reaper.SweepAsync();

        await _closer.Received(1).CloseAsync(other.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task SweepAsync_APrivateRoomOlderThanTheMaxAge_IsClosedEvenWithPlayers()
    {
        _settings.Reconfigure(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 10, MaxPrivateRoomMinutes = 60 });
        var room = AddRoom(players: 3);
        _clock.Advance(TimeSpan.FromMinutes(61));

        await _reaper.SweepAsync();

        await _closer.Received(1).CloseAsync(room.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_AYoungerRoomWithPlayers_IsKeptUnderAMaxAge()
    {
        _settings.Reconfigure(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 10, MaxPrivateRoomMinutes = 60 });
        AddRoom(players: 3);
        _clock.Advance(TimeSpan.FromMinutes(59));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_WithoutAMaxAge_ARoomWithPlayersLivesAsLongAsItLikes()
    {
        AddRoom(players: 3);
        _clock.Advance(TimeSpan.FromDays(30));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_APublicRoomIsNotTouchedByTheMaxAge()
    {
        _settings.Reconfigure(new RoomLifetimeOptions { MaxPrivateRoomMinutes = 5 });
        AddRoom(isPrivate: false, players: 1);
        _clock.Advance(TimeSpan.FromHours(3));

        await _reaper.SweepAsync();

        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);
    }

    [Fact]
    public async Task SweepAsync_ChangingTheLimitTakesEffectOnTheNextSweep()
    {
        var room = AddRoom();
        _clock.Advance(TimeSpan.FromMinutes(4));
        await _reaper.SweepAsync();
        await _closer.DidNotReceiveWithAnyArgs().CloseAsync(default, default, default);

        _settings.Reconfigure(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 3 });
        await _reaper.SweepAsync();

        await _closer.Received(1).CloseAsync(room.Guid, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }
}

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan span) => Now += span;
}