using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class RoomsServiceTests
{
    private const int MaxPlayersPerRoom = 4;
    private readonly PlayerRoomService _playerRooms;
    private readonly RoomsService _rooms;

    public RoomsServiceTests()
    {
        var configuration = Options.Create(new ServerConfiguration
        {
            RoomsConfiguration = new RoomsConfiguration { MaxRooms = 3, MaxPlayersPerRoom = MaxPlayersPerRoom },
        });
        _playerRooms = new PlayerRoomService(configuration, Substitute.For<IManagementApiService>(), Substitute.For<ITrackableIdGeneratorService>());
        _rooms = new RoomsService(configuration, new(), _playerRooms);
    }

    private void Fill(IRoom room, int players)
    {
        for (var i = 0; i < players; i++) _playerRooms.ConnectPlayer(Substitute.For<IPlayer>(), room);
    }

    [Fact]
    public void CreateRoom_WithoutOptions_IsPublicWithDefaults()
    {
        var room = _rooms.CreateRoom();

        Assert.False(room.IsPrivate);
        Assert.Null(room.Code);
        Assert.Equal(0, room.MaxPlayers);
    }

    [Fact]
    public void CreateRoom_StampsTheCreationTime()
    {
        var before = DateTimeOffset.UtcNow;

        var room = _rooms.CreateRoom();

        Assert.InRange(room.CreatedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CreateRoom_WithOptions_KeepsTheseOptions()
    {
        var room = _rooms.CreateRoom(new RoomOptions { Name = "Friends", Code = "ABC234", IsPrivate = true, MaxPlayers = 3 });

        Assert.Equal("Friends", room.Name);
        Assert.Equal("ABC234", room.Code);
        Assert.True(room.IsPrivate);
        Assert.Equal(3, room.MaxPlayers);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(MaxPlayersPerRoom, MaxPlayersPerRoom)]
    [InlineData(99, MaxPlayersPerRoom)]
    public void CreateRoom_LimitsTheHostMaxPlayersToWhatTheServerAllows(int requested, int expected)
    {
        var room = _rooms.CreateRoom(new RoomOptions { MaxPlayers = requested });

        Assert.Equal(expected, room.MaxPlayers);
    }

    [Fact]
    public void CreateRoom_CodeAlreadyUsed_Throws()
    {
        _rooms.CreateRoom(new RoomOptions { Code = "ABC234" });

        Assert.Throws<UnableToCreateRoomException>(() => _rooms.CreateRoom(new RoomOptions { Code = "abc234" }));
    }

    [Fact]
    public void GetRoomByCode_IgnoresCase()
    {
        var room = _rooms.CreateRoom(new RoomOptions { Code = "ABC234", IsPrivate = true });

        Assert.Same(room, _rooms.GetRoomByCode("abc234"));
    }

    [Fact]
    public void GetRoomByCode_UnknownCode_ReturnsNull()
    {
        Assert.Null(_rooms.GetRoomByCode("NOPE22"));
    }

    [Fact]
    public void GetAvailableRooms_LeavesPrivateRoomsOut()
    {
        var open = _rooms.CreateRoom();
        _rooms.CreateRoom(new RoomOptions { IsPrivate = true, Code = "ABC234" });

        Assert.Equal([open], _rooms.GetAvailableRooms());
    }

    [Fact]
    public void GetAvailableRooms_LeavesFullRoomsOut()
    {
        var full = _rooms.CreateRoom();
        var free = _rooms.CreateRoom();
        Fill(full, MaxPlayersPerRoom);

        Assert.Equal([free], _rooms.GetAvailableRooms());
    }

    [Fact]
    public void TryGetRoom_UnknownRoom_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(_rooms.TryGetRoom(Guid.NewGuid()));
    }

    [Fact]
    public void TryGetRoom_KnownRoom_ReturnsIt()
    {
        var room = _rooms.CreateRoom();

        Assert.Same(room, _rooms.TryGetRoom(room.Guid));
    }
}

public sealed class PlayerRoomCapacityTests
{
    [Fact]
    public void RemoveRoom_ForgetsItsPlayers()
    {
        var (players, rooms) = Create();
        var room = rooms.CreateRoom();
        players.ConnectPlayer(Substitute.For<IPlayer>(), room);

        players.RemoveRoom(room.Guid);

        Assert.DoesNotContain(room.Guid, players.GetRoomsPlayers().Keys);
        Assert.Equal(0, players.CountPlayers(room));
    }

    [Fact]
    public void RemoveRoom_UnknownRoom_DoesNothing()
    {
        var (players, _) = Create();

        players.RemoveRoom(Guid.NewGuid());
    }

    private static (PlayerRoomService players, RoomsService rooms) Create(int maxPlayersPerRoom = 4)
    {
        var configuration = Options.Create(new ServerConfiguration
        {
            RoomsConfiguration = new RoomsConfiguration { MaxRooms = 3, MaxPlayersPerRoom = maxPlayersPerRoom },
        });
        var players = new PlayerRoomService(configuration, Substitute.For<IManagementApiService>(), Substitute.For<ITrackableIdGeneratorService>());
        return (players, new RoomsService(configuration, new(), players));
    }

    [Fact]
    public void ConnectPlayer_RoomWithHostLimit_StopsAtTheHostLimit()
    {
        var (players, rooms) = Create();
        var room = rooms.CreateRoom(new RoomOptions { MaxPlayers = 2 });

        var results = Enumerable.Range(0, 3).Select(_ => players.ConnectPlayer(Substitute.For<IPlayer>(), room)).ToArray();

        Assert.Equal([true, true, false], results);
        Assert.True(players.IsRoomFull(room));
    }

    [Fact]
    public void ConnectPlayer_RoomWithoutHostLimit_UsesTheServerLimit()
    {
        var (players, rooms) = Create(maxPlayersPerRoom: 2);
        var room = rooms.CreateRoom();

        var results = Enumerable.Range(0, 3).Select(_ => players.ConnectPlayer(Substitute.For<IPlayer>(), room)).ToArray();

        Assert.Equal([true, true, false], results);
    }
}
