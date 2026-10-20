using System.Net;
using System.Reflection;
using System.Text.Json;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Application.Manager.Api.Tests;

public sealed class AccountsControllerTests
{
    private const string Ip = "203.0.113.7";
    private const string OtherIp = "198.51.100.1";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly AccountService _service;

    public AccountsControllerTests() => _service = Service(new FileAccountStore(null));

    private AccountService Service(IAccountStore store) =>
        new(store, AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);

    private AccountsController Controller(string? ip = Ip, string? authorization = null, string? forwardedFor = null, AccountService? service = null)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = ip is null ? null : IPAddress.Parse(ip);
        if (authorization is not null)
            http.Request.Headers.Authorization = authorization;
        if (forwardedFor is not null)
            http.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return new AccountsController(service ?? _service) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static (int Status, JsonElement Body) Read(IActionResult result)
    {
        var answer = Assert.IsAssignableFrom<ObjectResult>(result);
        return (answer.StatusCode ?? StatusCodes.Status200OK, JsonSerializer.SerializeToElement(answer.Value, answer.Value!.GetType(), WebJson));
    }

    private static void AssertError(IActionResult result, int status, string error)
    {
        var (actualStatus, body) = Read(result);
        Assert.Equal((status, error), (actualStatus, body.GetProperty("error").GetString()));
        Assert.False(body.TryGetProperty("retryAfterSeconds", out _));
    }

    private static void AssertThrottled(AccountsController controller, IActionResult result, int seconds)
    {
        var (status, body) = Read(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, status);
        Assert.Equal(AccountErrors.Throttled, body.GetProperty("error").GetString());
        Assert.Equal(seconds, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal(seconds.ToString(), controller.Response.Headers.RetryAfter.ToString());
    }

    private (string Token, int Tag, string Phrase) Register(string username = "Vini", int? tag = null, string ip = Ip)
    {
        var (status, body) = Read(Controller(ip).Register(new RegisterAccountRequest { Username = username, Tag = tag }));
        Assert.Equal(StatusCodes.Status201Created, status);
        return (body.GetProperty("token").GetString()!, body.GetProperty("tag").GetInt32(), body.GetProperty("recoveryPhrase").GetString()!);
    }

    private IActionResult Login(string? username, int? tag, string? phrase, string ip = Ip, string? forwardedFor = null)
        => Controller(ip, forwardedFor: forwardedFor).Login(new LoginRequest { Username = username, Tag = tag, Phrase = phrase });

    [Fact]
    public void Accounts_RegisterLoginElsewhereMeLogout_WorksEndToEnd()
    {
        var (status, registered) = Read(Controller().Register(new RegisterAccountRequest { Username = "  Vini ", Tag = 9291 }));
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.Equal("Vini", registered.GetProperty("username").GetString());
        Assert.Equal(9291, registered.GetProperty("tag").GetInt32());
        Assert.Equal("Vini#09291", registered.GetProperty("display").GetString());
        var firstToken = registered.GetProperty("token").GetString()!;
        var phrase = registered.GetProperty("recoveryPhrase").GetString()!;

        var (loginStatus, signedIn) = Read(Login("vini", 9291, phrase, OtherIp));
        Assert.Equal(StatusCodes.Status200OK, loginStatus);
        Assert.Equal(new[] { "username", "tag", "display", "displayName", "linked", "token" }, signedIn.EnumerateObject().Select(property => property.Name));
        var secondToken = signedIn.GetProperty("token").GetString()!;
        Assert.NotEqual(firstToken, secondToken);

        var (meStatus, me) = Read(Controller(authorization: $"Bearer {secondToken}").Me());
        Assert.Equal(StatusCodes.Status200OK, meStatus);
        Assert.Equal(new[] { "id", "username", "tag", "display", "displayName", "linked", "roles", "createdAt" }, me.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Vini#09291", me.GetProperty("display").GetString());
        Assert.Equal(_time.GetUtcNow(), me.GetProperty("createdAt").GetDateTimeOffset());

        Assert.IsType<NoContentResult>(Controller(authorization: $"Bearer {secondToken}").Logout());

        AssertError(Controller(authorization: $"Bearer {secondToken}").Me(), StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
        Assert.Equal(StatusCodes.Status200OK, Read(Controller(authorization: $"Bearer {firstToken}").Me()).Status);
    }

    [Fact]
    public void Register_Answers_WithTheContractFields()
    {
        var (_, body) = Read(Controller().Register(new RegisterAccountRequest { Username = "Vini" }));

        Assert.Equal(new[] { "username", "tag", "display", "displayName", "linked", "token", "recoveryPhrase" }, body.EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData("v", null, null, AccountErrors.InvalidUsername)]
    [InlineData("Vini", 100_000, null, AccountErrors.InvalidTag)]
    [InlineData("Vini", null, "short", AccountErrors.WeakPhrase)]
    public void Register_InvalidInput_Is400WithTheCode(string username, int? tag, string? phrase, string error)
    {
        var result = Controller().Register(new RegisterAccountRequest { Username = username, Tag = tag, Phrase = phrase });

        AssertError(result, StatusCodes.Status400BadRequest, error);
    }

    [Fact]
    public void Register_WhenEveryTagIsTaken_Is409NoTagsLeft()
    {
        var store = Substitute.For<IAccountStore>();
        store.TagsOf(Arg.Any<string>()).Returns(new HashSet<int>(Enumerable.Range(Tags.Min, Tags.Count)));

        var result = Controller(service: Service(store)).Register(new RegisterAccountRequest { Username = "Vini" });

        AssertError(result, StatusCodes.Status409Conflict, AccountErrors.NoTagsLeft);
    }

    [Fact]
    public void Register_TooManyFromOneIp_Is429WithRetryAfter()
    {
        for (var registration = 0; registration <= AccountThrottles.RegistrationOptions.FreeAttempts; registration++)
            Register("Player");
        var controller = Controller();

        var result = controller.Register(new RegisterAccountRequest { Username = "Player" });

        AssertThrottled(controller, result, (int)AccountThrottles.RegistrationOptions.BaseDelay.TotalSeconds);
    }

    [Fact]
    public void Login_AWrongPhraseAndAnUnknownAccount_AreTheSame401()
    {
        var (_, tag, _) = Register();

        var wrongPhrase = Read(Login("Vini", tag, "amber-otter-nine-cable"));
        var unknown = Read(Login("Nobody", tag, "amber-otter-nine-cable"));

        Assert.Equal(StatusCodes.Status401Unauthorized, wrongPhrase.Status);
        Assert.Equal(AccountErrors.InvalidCredentials, wrongPhrase.Body.GetProperty("error").GetString());
        Assert.Equal((wrongPhrase.Status, wrongPhrase.Body.GetRawText()), (unknown.Status, unknown.Body.GetRawText()));
    }

    [Fact]
    public void Login_AfterTheFreeFailures_Is429WithRetryAfter()
    {
        var (_, tag, phrase) = Register();
        for (var attempt = 0; attempt <= AccountThrottles.SignInOptions.FreeAttempts; attempt++)
            AssertError(Login("Vini", tag, "wrong wrong wrong", $"10.0.0.{attempt}"), StatusCodes.Status401Unauthorized, AccountErrors.InvalidCredentials);
        var controller = Controller(OtherIp);

        var result = controller.Login(new LoginRequest { Username = "Vini", Tag = tag, Phrase = phrase });

        AssertThrottled(controller, result, 2);
    }

    [Fact]
    public void Login_ForwardedForHeader_DoesNotChangeTheIpThatIsThrottled()
    {
        var (_, tag, phrase) = Register();
        for (var attempt = 0; attempt <= AccountThrottles.SignInOptions.FreeAttempts; attempt++)
            Login($"Nobody{attempt}", 1, "wrong wrong wrong", Ip, forwardedFor: $"10.0.0.{attempt}");

        var result = Login("Vini", tag, phrase, Ip, forwardedFor: "10.9.9.9");

        Assert.Equal(StatusCodes.Status429TooManyRequests, Read(result).Status);
    }

    [Fact]
    public void Login_AnIpv4MappedAddress_IsTheSameIpAsThePlainOne()
    {
        var (_, tag, phrase) = Register();
        for (var attempt = 0; attempt <= AccountThrottles.SignInOptions.FreeAttempts; attempt++)
            Login($"Nobody{attempt}", 1, "wrong wrong wrong", "::ffff:" + Ip);

        var result = Login("Vini", tag, phrase, Ip);

        Assert.Equal(StatusCodes.Status429TooManyRequests, Read(result).Status);
    }

    [Fact]
    public void Login_WithoutARemoteAddress_StillWorks()
    {
        var (_, tag, phrase) = Register();

        var result = Controller(ip: null).Login(new LoginRequest { Username = "Vini", Tag = tag, Phrase = phrase });

        Assert.Equal(StatusCodes.Status200OK, Read(result).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Basic dmluaTpwYXNzd29yZA==")]
    public void Me_WithoutAValidBearerToken_Is401InvalidToken(string? authorization)
    {
        Register();

        AssertError(Controller(authorization: authorization).Me(), StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Fact]
    public void Me_TheSchemeInAnyCase_IsABearerToken()
    {
        var (token, _, _) = Register();

        Assert.Equal(StatusCodes.Status200OK, Read(Controller(authorization: $"bearer {token}").Me()).Status);
    }

    [Fact]
    public void Me_ABasicHeaderCarryingAToken_IsNotABearerToken()
    {
        var (token, _, _) = Register();

        AssertError(Controller(authorization: $"Basic {token}").Me(), StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Fact]
    public void Logout_WithoutAValidToken_Is401InvalidToken()
    {
        AssertError(Controller(authorization: $"Bearer {DeviceTokens.Create().Token}").Logout(), StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Fact]
    public void Availability_AnswersWithTheCleanedUsername()
    {
        Register("Vini", 9291);

        var (status, body) = Read(Controller().Availability("  vini ", "09291"));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(new[] { "available", "username" }, body.EnumerateObject().Select(property => property.Name));
        Assert.False(body.GetProperty("available").GetBoolean());
        Assert.Equal("vini", body.GetProperty("username").GetString());
    }

    [Theory]
    [InlineData("#", null, AccountErrors.InvalidUsername)]
    [InlineData("Vini", "abc", AccountErrors.InvalidTag)]
    public void Availability_InvalidInput_Is400WithTheCode(string username, string? tag, string error)
    {
        AssertError(Controller().Availability(username, tag), StatusCodes.Status400BadRequest, error);
    }

    [Fact]
    public void Availability_TooManyFromOneIp_Is429WithRetryAfter()
    {
        for (var lookup = 0; lookup <= AccountThrottles.LookupOptions.FreeAttempts; lookup++)
            Controller().Availability("Vini", null);
        var controller = Controller();

        var result = controller.Availability("Vini", null);

        AssertThrottled(controller, result, 1);
    }

    [Theory]
    [InlineData(nameof(AccountsController.Register), "POST", "")]
    [InlineData(nameof(AccountsController.Login), "POST", "login")]
    [InlineData(nameof(AccountsController.Me), "GET", "me")]
    [InlineData(nameof(AccountsController.Logout), "POST", "logout")]
    [InlineData(nameof(AccountsController.Availability), "GET", "availability")]
    public void Routes_AreTheOnesOfTheContract(string action, string method, string template)
    {
        var route = typeof(AccountsController).GetCustomAttribute<RouteAttribute>();
        var verb = typeof(AccountsController).GetMethod(action)!.GetCustomAttribute<HttpMethodAttribute>()!;

        Assert.Equal("accounts", route?.Template);
        Assert.NotNull(typeof(AccountsController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal((method, template), (verb.HttpMethods.Single(), verb.Template ?? string.Empty));
    }

    [Fact]
    public void ChangeDisplayName_ChangesTheName_AndMeAnswersWithIt()
    {
        var token = Register("Vini", 9291).Token;

        var result = Controller(authorization: $"Bearer {token}").ChangeDisplayName(new ChangeDisplayNameRequest { DisplayName = "Rattler" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(ok.Value, ok.Value!.GetType(), WebJson);
        Assert.Equal("Rattler", body.GetProperty("displayName").GetString());
        Assert.Equal("Vini#09291", body.GetProperty("display").GetString());
    }

    [Fact]
    public void ChangeDisplayName_ANameThatIsNotAllowed_Is400()
    {
        var token = Register("Vini", 9291).Token;

        var result = Controller(authorization: $"Bearer {token}").ChangeDisplayName(new ChangeDisplayNameRequest { DisplayName = "Rattler#12345" });

        var answer = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, answer.StatusCode);
    }

    [Fact]
    public void ChangeDisplayName_WithoutAToken_Is401()
    {
        var result = Controller().ChangeDisplayName(new ChangeDisplayNameRequest { DisplayName = "Rattler" });

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void Register_WithADisplayName_AnswersWithItAndKeepsTheTagOutOfIt()
    {
        var result = Controller().Register(new RegisterAccountRequest { Username = "Vini", Tag = 9291, DisplayName = "Rattler" });

        var created = Assert.IsAssignableFrom<ObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(created.Value, created.Value!.GetType(), WebJson);
        Assert.Equal("Rattler", body.GetProperty("displayName").GetString());
    }

    [Fact]
    public void Me_ListsTheLinkedSocialAccountsByProvider_NeverAnythingOfTheirIds()
    {
        var registered = _service.Register("Vini", 9291, null, Ip).Value;
        _service.LinkExternal(registered.Account.Id, "github", "583231");
        _service.LinkExternal(registered.Account.Id, "discord", "80351110224678912");

        var (status, body) = Read(Controller(authorization: $"Bearer {registered.Token}").Me());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(new[] { "discord", "github" }, body.GetProperty("linked").EnumerateArray().Select(entry => entry.GetString()));
        var text = body.GetRawText();
        Assert.DoesNotContain("583231", text);
        Assert.DoesNotContain("idHash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Me_AnAccountWithNoLinksHasAnEmptyList()
    {
        var token = Register("Vini", 9291).Token;

        var (_, body) = Read(Controller(authorization: $"Bearer {token}").Me());

        Assert.Empty(body.GetProperty("linked").EnumerateArray());
    }

    [Fact]
    public void Me_ListsTheRolesOfTheAccount_AlwaysWithUser_AndAdminWhenGranted()
    {
        var registered = _service.Register("Vini", 9291, null, Ip).Value;
        var roles = new MemoryRoleStore();
        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = $"Bearer {registered.Token}";
        var controller = new AccountsController(_service, roles) { ControllerContext = new ControllerContext { HttpContext = http } };

        var plain = Read(controller.Me()).Body.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();
        roles.Grant(registered.Account.Id, Roles.Admin);
        var admin = Read(controller.Me()).Body.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();

        Assert.Equal(new[] { "user" }, plain);
        Assert.Equal(new[] { "user", "admin" }, admin);
    }

    [Fact]
    public void ChangeHandle_MovesTheAccount_AndAnswersWithTheNewHandle()
    {
        var token = Register("Vini", 9291).Token;

        var result = Controller(authorization: $"Bearer {token}").ChangeHandle(new ChangeHandleRequest { Username = "Rattler", Tag = 42 });

        var (status, body) = Read(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Rattler#00042", body.GetProperty("display").GetString());
        Assert.Equal("Rattler", body.GetProperty("username").GetString());
    }

    [Fact]
    public void ChangeHandle_ATakenHandle_Is409_AndANameThatIsNotAllowed_Is400_AndNoToken_Is401()
    {
        var token = Register("Vini", 9291).Token;
        Register("Bob", 2);

        var taken = Controller(authorization: $"Bearer {token}").ChangeHandle(new ChangeHandleRequest { Username = "Bob", Tag = 2 });
        var bad = Controller(authorization: $"Bearer {token}").ChangeHandle(new ChangeHandleRequest { Username = "Vini#123" });
        var anonymous = Controller().ChangeHandle(new ChangeHandleRequest { Username = "Rattler", Tag = 1 });

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<ObjectResult>(taken).StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(bad).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(anonymous).StatusCode);
    }

    [Fact]
    public void ChangeHandle_AndChangeDisplayName_KeepTheRolesInTheAnswer_SoAnAdminStaysAnAdminInThePage()
    {
        var registered = _service.Register("Vini", 9291, null, Ip).Value;
        var roles = new MemoryRoleStore();
        roles.Grant(registered.Account.Id, Roles.Admin);
        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = $"Bearer {registered.Token}";
        var controller = new AccountsController(_service, roles) { ControllerContext = new ControllerContext { HttpContext = http } };

        var handle = Read(controller.ChangeHandle(new ChangeHandleRequest { Username = "Rattler", Tag = 5 })).Body.GetProperty("roles").EnumerateArray().Select(role => role.GetString());
        var display = Read(controller.ChangeDisplayName(new ChangeDisplayNameRequest { DisplayName = "Nick" })).Body.GetProperty("roles").EnumerateArray().Select(role => role.GetString());

        Assert.Equal(new[] { "user", "admin" }, handle);
        Assert.Equal(new[] { "user", "admin" }, display);
    }
}
