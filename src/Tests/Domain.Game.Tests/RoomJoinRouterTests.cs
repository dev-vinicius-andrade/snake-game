using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class RoomJoinRouterTests
{
    private readonly PlayerRoomService _playerRooms;
    private readonly RoomsService _rooms;
    private readonly RoomJoinRouter _router;

    public RoomJoinRouterTests()
    {
        var configuration = Options.Create(new ServerConfiguration
        {
            RoomsConfiguration = new RoomsConfiguration { MaxRooms = 2, MaxPlayersPerRoom = 2 },
        });
        _playerRooms = new PlayerRoomService(configuration, Substitute.For<IManagementApiService>(), Substitute.For<ITrackableIdGeneratorService>());
        _rooms = new RoomsService(configuration, new(), _playerRooms);
        _router = new RoomJoinRouter(_rooms, _playerRooms);
    }

    private void Fill(IRoom room)
    {
        _playerRooms.ConnectPlayer(Substitute.For<IPlayer>(), room);
        _playerRooms.ConnectPlayer(Substitute.For<IPlayer>(), room);
    }

    [Fact]
    public void Resolve_AnyRoom_NoRoomsYet_CreatesOne()
    {
        var route = _router.Resolve(null);

        Assert.Equal(JoinRouteOutcome.Joined, route.Outcome);
        Assert.NotNull(route.Room);
        Assert.Single(_rooms.GetRooms());
    }

    [Fact]
    public void Resolve_AnyRoom_ReusesAnOpenRoom()
    {
        var open = _rooms.CreateRoom();

        var route = _router.Resolve(null);

        Assert.Same(open, route.Room);
        Assert.Single(_rooms.GetRooms());
    }

    [Fact]
    public void Resolve_AnyRoom_NeverPicksAPrivateRoom()
    {
        var privateRoom = _rooms.CreateRoom(new RoomOptions { IsPrivate = true, Code = "ABC234" });

        var route = _router.Resolve(null);

        Assert.NotSame(privateRoom, route.Room);
        Assert.Equal(JoinRouteOutcome.Joined, route.Outcome);
    }

    [Fact]
    public void Resolve_AnyRoom_NoOpenRoomAndServerAtItsLimit_HasNoCapacity()
    {
        Fill(_rooms.CreateRoom());
        Fill(_rooms.CreateRoom());

        var route = _router.Resolve(null);

        Assert.Equal(JoinRouteOutcome.NoCapacity, route.Outcome);
        Assert.Null(route.Room);
    }

    [Fact]
    public void Resolve_SpecificRoom_OnThisServer_Joins()
    {
        var room = _rooms.CreateRoom(new RoomOptions { IsPrivate = true, Code = "ABC234" });

        var route = _router.Resolve(room.Guid);

        Assert.Equal(JoinRouteOutcome.Joined, route.Outcome);
        Assert.Same(room, route.Room);
    }

    [Fact]
    public void Resolve_SpecificRoom_OnAnotherServer_IsNotHere()
    {
        var route = _router.Resolve(Guid.NewGuid());

        Assert.Equal(JoinRouteOutcome.NotHere, route.Outcome);
        Assert.Null(route.Room);
    }

    [Fact]
    public void Resolve_SpecificRoom_Full_IsFull()
    {
        var room = _rooms.CreateRoom();
        Fill(room);

        var route = _router.Resolve(room.Guid);

        Assert.Equal(JoinRouteOutcome.Full, route.Outcome);
    }
}
