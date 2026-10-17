using Application.Manager.Api.Rooms;
using Library.Commons.Api.Entities.Requests.Room;

namespace Application.Manager.Api.Tests;

internal sealed class TestTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan span) => Now += span;
}

public sealed class RoomRegistryTests
{
    private const string ServerA = "http://a:5000";
    private const string ServerB = "http://b:5000";
    private readonly TestTime _time = new();
    private readonly RoomRegistry _registry;

    public RoomRegistryTests() => _registry = new RoomRegistry(_time);

    private static RoomSummary Room(Guid? id = null, string? code = null, int players = 0, int max = 4)
        => new() { Id = id ?? Guid.NewGuid(), Code = code, Name = "r", IsPrivate = code is not null, Players = players, MaxPlayers = max, CreatedAt = DateTimeOffset.UnixEpoch };

    private static RoomHeartbeatRequest Beat(string server, params RoomSummary[] rooms)
        => new() { BaseUrl = server, GameServerUrl = server + "/game", Rooms = rooms.ToList() };

    private static RoomCreatedRequest Created(Guid id, string code, string server = ServerA)
        => new() { RoomId = id, Code = code, Name = "Friends", MaxPlayers = 4, BaseUrl = server, GameServerUrl = server + "/game" };

    [Fact]
    public void Update_AddsTheRoomsOfAServer()
    {
        var room = Room();

        _registry.Update(Beat(ServerA, room));

        var found = Assert.Single(_registry.List());
        Assert.Equal(room.Id, found.Summary.Id);
        Assert.Equal(ServerA, found.BaseUrl);
        Assert.Equal(ServerA + "/game", found.GameServerUrl);
    }

    [Fact]
    public void Update_RefreshesPlayerCounts()
    {
        var id = Guid.NewGuid();
        _registry.Update(Beat(ServerA, Room(id, players: 1)));

        _registry.Update(Beat(ServerA, Room(id, players: 3)));

        Assert.Equal(3, _registry.Get(id)!.Summary.Players);
    }

    [Fact]
    public void Update_ForgetsRoomsTheServerNoLongerHas()
    {
        var gone = Room();
        var kept = Room();
        _registry.Update(Beat(ServerA, gone, kept));
        _time.Advance(TimeSpan.FromMinutes(1));

        _registry.Update(Beat(ServerA, kept));

        Assert.Equal([kept.Id], _registry.List().Select(room => room.Summary.Id));
    }

    [Fact]
    public void Update_DoesNotTouchTheRoomsOfAnotherServer()
    {
        var other = Room();
        _registry.Update(Beat(ServerB, other));

        _registry.Update(Beat(ServerA));

        Assert.NotNull(_registry.Get(other.Id));
    }

    [Fact]
    public void Update_KeepsAJustCreatedRoomThatAnOlderHeartbeatDoesNotKnowYet()
    {
        var id = Guid.NewGuid();
        _registry.Register(Created(id, "ABC234"));

        _registry.Update(Beat(ServerA));

        Assert.NotNull(_registry.Get(id));
    }

    [Fact]
    public void Update_ForgetsACreatedRoomOnceItIsOldEnoughToHaveBeenInTheHeartbeat()
    {
        var id = Guid.NewGuid();
        _registry.Register(Created(id, "ABC234"));
        _time.Advance(RoomRegistry.NewRoomGrace + TimeSpan.FromSeconds(1));

        _registry.Update(Beat(ServerA));

        Assert.Null(_registry.Get(id));
    }

    [Fact]
    public void Register_MakesTheRoomFindableByCode()
    {
        var id = Guid.NewGuid();
        _registry.Register(Created(id, "ABC234"));

        var found = _registry.FindByCode("abc234");

        Assert.Equal(id, found!.Summary.Id);
        Assert.True(found.Summary.IsPrivate);
        Assert.Equal("Friends", found.Summary.Name);
        Assert.Equal(4, found.Summary.MaxPlayers);
    }

    [Fact]
    public void FindByCode_UnknownCode_ReturnsNull()
    {
        Assert.Null(_registry.FindByCode("NOPE22"));
    }

    [Fact]
    public void FindByCode_KeepsCodesAfterAHeartbeat()
    {
        var id = Guid.NewGuid();
        _registry.Register(Created(id, "ABC234"));

        _registry.Update(Beat(ServerA, Room(id, "ABC234", players: 2)));

        Assert.Equal(2, _registry.FindByCode("ABC234")!.Summary.Players);
    }

    [Fact]
    public void IsCodeTaken_IgnoresCase()
    {
        _registry.Register(Created(Guid.NewGuid(), "ABC234"));

        Assert.True(_registry.IsCodeTaken("abc234"));
        Assert.False(_registry.IsCodeTaken("ZZZ999"));
    }

    [Fact]
    public void Remove_ForgetsTheRoom()
    {
        var id = Guid.NewGuid();
        _registry.Register(Created(id, "ABC234"));

        Assert.True(_registry.Remove(id));
        Assert.False(_registry.Remove(id));
        Assert.Null(_registry.Get(id));
    }

    [Fact]
    public void List_OldestRoomFirst()
    {
        var newer = Room();
        newer.CreatedAt = DateTimeOffset.UnixEpoch.AddHours(2);
        var older = Room();
        older.CreatedAt = DateTimeOffset.UnixEpoch.AddHours(1);
        _registry.Update(Beat(ServerA, newer, older));

        Assert.Equal([older.Id, newer.Id], _registry.List().Select(room => room.Summary.Id));
    }

    [Fact]
    public void Prune_ForgetsRoomsOfServersThatWentSilent()
    {
        _registry.Update(Beat(ServerA, Room()));
        _registry.Update(Beat(ServerB, Room()));
        _time.Advance(TimeSpan.FromSeconds(20));
        _registry.Update(Beat(ServerB, _registry.List().Single(room => room.BaseUrl == ServerB).Summary));

        var removed = _registry.Prune(TimeSpan.FromSeconds(15));

        Assert.Equal(1, removed);
        Assert.All(_registry.List(), room => Assert.Equal(ServerB, room.BaseUrl));
    }
}
