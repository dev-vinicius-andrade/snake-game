using System.Text.Json;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Hubs;
using Application.Manager.Api.Rooms;
using Library.Commons.Api.Entities.Requests.Join;
using Library.Commons.Api.Entities.Requests.Room;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RabbitMQ.Client;

namespace Application.Manager.Api.Tests;

public sealed class JoinControllerTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly IEventbusPublisher<IBasicProperties> _publisher = Substitute.For<IEventbusPublisher<IBasicProperties>>();
    private readonly AccountService _accounts;
    private readonly RoomRegistry _registry;
    private readonly RoomSummary _room = new() { Id = Guid.NewGuid(), Code = "ABC234", Name = "Friends", IsPrivate = true, MaxPlayers = 4 };

    public JoinControllerTests()
    {
        _accounts = new AccountService(new FileAccountStore(null), AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);
        _registry = new RoomRegistry(_time);
        _registry.Update(new RoomHeartbeatRequest { BaseUrl = "http://a:5000", GameServerUrl = "http://a:5000/game", Rooms = [_room] });
    }

    private JoinController Controller(string? authorization = null)
    {
        var http = new DefaultHttpContext();
        if (authorization is not null)
            http.Request.Headers.Authorization = authorization;
        return new JoinController(new JsonSerializerOptions(), _publisher, Substitute.For<IHubContext<ManagerHub>>(), _registry, _accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private (Guid AccountId, string Token) Register()
    {
        var registration = _accounts.Register("Vini", 9291, null, "203.0.113.7").Value!;
        return (registration.Account.Id, registration.Token);
    }

    private static JoinRequest Request(string nickname) => new() { Id = "manager-connection", Nickname = nickname };

    private IReadOnlyList<PlayerJoinPayload> Published()
        => _publisher.ReceivedCalls().SelectMany(call => call.GetArguments()).OfType<PlayerJoinPayload>().ToList();

    private static void AssertError(IActionResult result, int status, string error)
    {
        var answer = Assert.IsAssignableFrom<ObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(answer.Value, answer.Value!.GetType(), WebJson);
        Assert.Equal((status, error), (answer.StatusCode, body.GetProperty("error").GetString()));
    }

    [Fact]
    public void Join_WithoutAToken_PublishesAGuestAsBefore()
    {
        var result = Controller().Join(Request("ana"));

        Assert.IsType<OkResult>(result);
        var payload = Assert.Single(Published());
        Assert.Equal(("manager-connection", "ana", (Guid?)null, (Guid?)null), (payload.Id, payload.Nickname, payload.AccountId, payload.RoomId));
    }

    [Fact]
    public void Join_AValidToken_PublishesTheAccountWithItsPublicNameAndNeverItsTag()
    {
        var (accountId, token) = Register();

        var result = Controller($"Bearer {token}").Join(Request("whatever"));

        Assert.IsType<OkResult>(result);
        var payload = Assert.Single(Published());
        Assert.Equal((accountId, "Vini"), (payload.AccountId, payload.Nickname));
        Assert.DoesNotContain("#", payload.Nickname);
    }

    [Fact]
    public void Join_AnAccount_MayHaveSentANicknameThatLooksLikeAnAccount()
    {
        var (accountId, token) = Register();

        var result = Controller($"Bearer {token}").Join(Request("Other#12345"));

        Assert.IsType<OkResult>(result);
        Assert.Equal(accountId, Assert.Single(Published()).AccountId);
    }

    [Theory]
    [InlineData("Bearer not-a-token")]
    [InlineData("Bearer")]
    [InlineData("Basic dmluaTpwYXNzd29yZA==")]
    [InlineData("rubbish")]
    public void Join_AnAuthorizationThatIsNotAValidToken_Is401InvalidToken_AndPublishesNothing(string authorization)
    {
        Register();

        var result = Controller(authorization).Join(Request("ana"));

        AssertError(result, StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
        Assert.Empty(Published());
    }

    [Fact]
    public void Join_ATokenOfNoAccount_Is401InvalidToken()
    {
        var result = Controller($"Bearer {DeviceTokens.Create().Token}").Join(Request("ana"));

        AssertError(result, StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Theory]
    [InlineData("Vini#09291")]
    [InlineData("  ana#1 ")]
    public void Join_AGuestNicknameThatLooksLikeAnAccount_Is400InvalidNickname_AndPublishesNothing(string nickname)
    {
        var result = Controller().Join(Request(nickname));

        AssertError(result, StatusCodes.Status400BadRequest, GuestNicknames.InvalidNickname);
        Assert.Empty(Published());
    }

    [Fact]
    public void JoinRoom_AValidToken_PublishesTheAccountForThatRoom()
    {
        var (accountId, token) = Register();

        var result = Controller($"Bearer {token}").Join(_room.Id, Request("ana"));

        Assert.IsType<OkResult>(result);
        var payload = Assert.Single(Published());
        Assert.Equal((accountId, "Vini", (Guid?)_room.Id), (payload.AccountId, payload.Nickname, payload.RoomId));
    }

    [Fact]
    public void JoinRoom_AnInvalidToken_Is401()
    {
        var result = Controller("Bearer not-a-token").Join(_room.Id, Request("ana"));

        AssertError(result, StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Fact]
    public void JoinRoom_AnUnknownRoom_IsStill404()
    {
        Assert.IsType<NotFoundResult>(Controller().Join(Guid.NewGuid(), Request("ana")));
        Assert.Empty(Published());
    }

    [Fact]
    public void JoinByCode_AValidToken_PublishesTheAccountAndAnswersTheRoom()
    {
        var (accountId, token) = Register();

        var result = Controller($"Bearer {token}").JoinByCode("ABC234", Request("ana"));

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result).StatusCode);
        Assert.Equal((accountId, (Guid?)_room.Id), (Assert.Single(Published()).AccountId, Assert.Single(Published()).RoomId));
    }

    [Fact]
    public void JoinByCode_AGuestNicknameThatLooksLikeAnAccount_Is400()
    {
        AssertError(Controller().JoinByCode("ABC234", Request("Vini#09291")), StatusCodes.Status400BadRequest, GuestNicknames.InvalidNickname);
    }
}

public sealed class GuestNicknamesTests
{
    [Theory]
    [InlineData("ana", true)]
    [InlineData("Vini#", true)]
    [InlineData("#1 fan", true)]
    [InlineData("Vi#ni", true)]
    [InlineData("Vini#١٢٣", true)]
    [InlineData("Vini#09291", false)]
    [InlineData("Vini#1", false)]
    [InlineData("  Vini#123  ", false)]
    [InlineData("#42", false)]
    public void IsAllowed_MirrorsTheClient_NoNicknameEndingInHashAndDigits(string nickname, bool allowed)
    {
        Assert.Equal(allowed, GuestNicknames.IsAllowed(nickname));
    }
}
