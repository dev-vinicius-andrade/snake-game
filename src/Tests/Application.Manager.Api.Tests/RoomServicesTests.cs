using Application.Manager.Api.Rooms;
using Library.Commons.Api.Entities.Requests.Room;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using NSubstitute;

namespace Application.Manager.Api.Tests;

public sealed class InviteCodeGeneratorTests
{
    [Fact]
    public void Generate_SixCharactersWithoutLookAlikes()
    {
        var code = new InviteCodeGenerator().Generate(_ => false);

        Assert.Equal(6, code.Length);
        Assert.All(code, character => Assert.DoesNotContain(character, "0O1IL"));
        Assert.Equal(code.ToUpperInvariant(), code);
    }

    [Fact]
    public void Generate_SkipsCodesAlreadyTaken()
    {
        var taken = new HashSet<string>();
        var generator = new InviteCodeGenerator(new Random(1));
        var first = generator.Generate(_ => false);
        taken.Add(first);

        var second = new InviteCodeGenerator(new Random(1)).Generate(taken.Contains);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_EverythingTaken_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new InviteCodeGenerator().Generate(_ => true));
    }
}

public sealed class AdminKeyValidatorTests
{
    [Theory]
    [InlineData("secret", true)]
    [InlineData("other", true)]
    [InlineData("Secret", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("secre", false)]
    public void IsValid_ComparesWithTheConfiguredKeys(string? provided, bool expected)
    {
        Assert.Equal(expected, AdminKeyValidator.IsValid(["secret", "other"], provided));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_NoKeysConfigured_NobodyGetsIn(string? configured)
    {
        var keys = configured is null ? null : new[] { configured };

        Assert.False(AdminKeyValidator.IsValid(keys, ""));
        Assert.False(AdminKeyValidator.IsValid(keys, "anything"));
    }
}

public sealed class RoomCreationServiceTests
{
    private readonly RoomRegistry _registry = new(TimeProvider.System);
    private readonly IRoomCreatePublisher _publisher = Substitute.For<IRoomCreatePublisher>();
    private readonly RoomCreationService _service;

    public RoomCreationServiceTests() => _service = new RoomCreationService(_registry, new InviteCodeGenerator(), _publisher);

    [Fact]
    public void Request_PublishesTheRoomWithAFreshCode()
    {
        var result = _service.Request("c1", "  Friends  ", 6);

        Assert.True(result.Success);
        Assert.Equal(6, result.Code!.Length);
        _publisher.Received(1).Publish(Arg.Is<RoomCreatePayload>(p => p.RequesterId == "c1" && p.Name == "Friends" && p.MaxPlayers == 6 && p.Code == result.Code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_NoConnection_Fails(string requester)
    {
        var result = _service.Request(requester, "Friends", 4);

        Assert.False(result.Success);
        _publisher.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    public void Request_BlankName_SendsNoName()
    {
        _service.Request("c1", "   ", 4);

        _publisher.Received(1).Publish(Arg.Is<RoomCreatePayload>(p => p.Name == null));
    }

    [Fact]
    public void Request_LongName_IsCut()
    {
        _service.Request("c1", new string('x', 100), 4);

        _publisher.Received(1).Publish(Arg.Is<RoomCreatePayload>(p => p.Name!.Length == RoomCreationService.MaxNameLength));
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(10, 10)]
    [InlineData(500, RoomCreationService.MaxPlayersLimit)]
    public void Request_KeepsMaxPlayersInRange(int requested, int expected)
    {
        _service.Request("c1", "r", requested);

        _publisher.Received(1).Publish(Arg.Is<RoomCreatePayload>(p => p.MaxPlayers == expected));
    }

    [Fact]
    public void Request_PublishFails_ReportsIt()
    {
        _publisher.When(publisher => publisher.Publish(Arg.Any<RoomCreatePayload>())).Do(_ => throw new InvalidOperationException("down"));

        var result = _service.Request("c1", "r", 4);

        Assert.False(result.Success);
    }
}

public sealed class RoomAdminServiceTests
{
    private readonly TestTime _time = new();
    private readonly RoomRegistry _registry;
    private readonly IGameServerClient _servers = Substitute.For<IGameServerClient>();
    private readonly RoomAdminService _service;
    private readonly Guid _id = Guid.NewGuid();

    public RoomAdminServiceTests()
    {
        _registry = new RoomRegistry(_time);
        _service = new RoomAdminService(_registry, _servers);
        _registry.Register(new RoomCreatedRequest { RoomId = _id, Code = "ABC234", BaseUrl = "http://a:5000", GameServerUrl = "http://a:5000/game", MaxPlayers = 4 });
    }

    [Fact]
    public async Task DeleteAsync_AsksTheServerToCloseItAndForgetsIt()
    {
        _servers.CloseRoomAsync("http://a:5000", _id, Arg.Any<CancellationToken>()).Returns(CloseResult.Closed);

        var result = await _service.DeleteAsync(_id);

        Assert.Equal(DeleteResult.Deleted, result);
        Assert.Null(_registry.Get(_id));
    }

    [Fact]
    public async Task DeleteAsync_ServerDoesNotKnowTheRoom_StillForgetsIt()
    {
        _servers.CloseRoomAsync(Arg.Any<string>(), _id, Arg.Any<CancellationToken>()).Returns(CloseResult.NotFound);

        Assert.Equal(DeleteResult.Deleted, await _service.DeleteAsync(_id));
        Assert.Null(_registry.Get(_id));
    }

    [Fact]
    public async Task DeleteAsync_ServerUnreachable_KeepsTheRoomListed()
    {
        _servers.CloseRoomAsync(Arg.Any<string>(), _id, Arg.Any<CancellationToken>()).Returns(CloseResult.Unreachable);

        Assert.Equal(DeleteResult.ServerUnreachable, await _service.DeleteAsync(_id));
        Assert.NotNull(_registry.Get(_id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownRoom_IsNotFound()
    {
        Assert.Equal(DeleteResult.NotFound, await _service.DeleteAsync(Guid.NewGuid()));
        await _servers.DidNotReceiveWithAnyArgs().CloseRoomAsync(default!, default, default);
    }
}
