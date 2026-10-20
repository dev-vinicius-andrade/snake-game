using System.Reflection;
using System.Text.Json;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Contants;
using Library.Commons.Api.Extensions;
using Library.Commons.Game.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class AccountStatsControllerTests
{
    private const string ApiKey = "E1F7F301-B9DD-4CE0-A4FF-85DC46B3624D";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly AccountService _accounts;
    private readonly AchievementService _achievements;

    public AccountStatsControllerTests()
    {
        var store = new FileAccountStore(null);
        _accounts = new AccountService(store, AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);
        _achievements = new AchievementService(store, new FileStatsStore(null), _time);
    }

    private AccountStatsController Controller(string? authorization = null)
    {
        var http = new DefaultHttpContext();
        if (authorization is not null)
            http.Request.Headers.Authorization = authorization;
        return new AccountStatsController(_achievements, _accounts) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private Registration Register() => _accounts.Register("Vini", 9291, null, "203.0.113.7").Value!;

    private static (int Status, JsonElement Body) Read(IActionResult result)
    {
        var answer = Assert.IsAssignableFrom<ObjectResult>(result);
        return (answer.StatusCode ?? StatusCodes.Status200OK, JsonSerializer.SerializeToElement(answer.Value, answer.Value!.GetType(), WebJson));
    }

    private static void AssertError(IActionResult result, int status, string error)
    {
        var (actualStatus, body) = Read(result);
        Assert.Equal((status, error), (actualStatus, body.GetProperty("error").GetString()));
    }

    private static StatsReportRequest Request(Dictionary<string, long>? counters = null, Dictionary<string, long>? maxima = null, Guid? reportId = null)
        => new() { ReportId = reportId ?? Guid.NewGuid(), Counters = counters, Maxima = maxima };

    private static IEnumerable<string> Names(JsonElement element) => element.EnumerateObject().Select(property => property.Name);

    [Fact]
    public void Report_ForAnAccount_Is200WithTheIdsItUnlocked()
    {
        var account = Register().Account;

        var (status, body) = Read(Controller().Report(account.Id, Request(new() { [StatKeys.Kills] = 3 }, new() { [StatKeys.BestScore] = 120 })));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(new[] { "unlocked" }, Names(body));
        Assert.Equal(new[] { "first-blood" }, body.GetProperty("unlocked").EnumerateArray().Select(id => id.GetString()));
    }

    [Fact]
    public void Report_TheSameReportAgain_Is200WithNothingUnlocked()
    {
        var account = Register().Account;
        var request = Request(new() { [StatKeys.Kills] = 1 });
        Controller().Report(account.Id, request);

        var (status, body) = Read(Controller().Report(account.Id, request));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(0, body.GetProperty("unlocked").GetArrayLength());
    }

    [Fact]
    public void Report_ForAnUnknownAccount_Is404UnknownAccount()
    {
        AssertError(Controller().Report(Guid.NewGuid(), Request(new() { [StatKeys.Kills] = 1 })), StatusCodes.Status404NotFound, StatsErrors.UnknownAccount);
    }

    [Fact]
    public void Report_WithAValueOutOfRange_Is400InvalidStats()
    {
        var account = Register().Account;

        var result = Controller().Report(account.Id, Request(new() { [StatKeys.Kills] = StatsReport.MaxCounter + 1 }));

        AssertError(result, StatusCodes.Status400BadRequest, StatsErrors.InvalidStats);
    }

    [Fact]
    public void Report_WithoutAReportId_Is400InvalidStats()
    {
        var account = Register().Account;

        var result = Controller().Report(account.Id, new StatsReportRequest { Counters = new() { [StatKeys.Kills] = 1 } });

        AssertError(result, StatusCodes.Status400BadRequest, StatsErrors.InvalidStats);
    }

    [Fact]
    public void Report_TheBodyOfTheContract_IsReadAsARequest()
    {
        const string body = """{ "reportId": "6f9619ff-8b86-d011-b42d-00c04fc964ff", "counters": { "kills": 3, "jumps": 2 }, "maxima": { "bestScore": 120 } }""";

        var request = JsonSerializer.Deserialize<StatsReportRequest>(body, WebJson)!;

        Assert.Equal(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"), request.ReportId);
        Assert.Equal(3, request.Counters![StatKeys.Kills]);
        Assert.Equal(120, request.Maxima![StatKeys.BestScore]);
    }

    private static async Task<IActionResult?> AuthorizeReport(string? apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthConfiguration:ApiKeys:0"] = ApiKey })
            .Build();
        var services = new ServiceCollection().AddOptions();
        services.AddApiKeyAuth(configuration);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (apiKey is not null)
            http.Request.Headers[AuthConstants.ApiKeyHeaderName] = apiKey;
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        var filter = typeof(AccountStatsController).GetMethod(nameof(AccountStatsController.Report))!.GetCustomAttribute<ApiKeyAuth>()!;

        await filter.OnAuthorizationAsync(context);

        return context.Result;
    }

    [Fact]
    public async Task Report_WithoutTheApiKey_IsRefusedByItsFilter()
    {
        Assert.IsType<UnauthorizedObjectResult>(await AuthorizeReport(null));
        Assert.IsType<UnauthorizedObjectResult>(await AuthorizeReport("not-the-key"));
    }

    [Fact]
    public async Task Report_WithTheApiKey_IsLetThroughByItsFilter()
    {
        Assert.Null(await AuthorizeReport(ApiKey));
    }

    [Fact]
    public void Profile_WithAValidToken_Is200WithTheAccountEveryStatAndTheAchievements()
    {
        var registration = Register();
        Controller().Report(registration.Account.Id, Request(new() { [StatKeys.Kills] = 3, [StatKeys.Games] = 1 }, new() { [StatKeys.BestScore] = 120 }));

        var (status, body) = Read(Controller($"Bearer {registration.Token}").Profile());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(new[] { "account", "stats", "achievements" }, Names(body));
        var account = body.GetProperty("account");
        Assert.Equal(new[] { "id", "username", "tag", "display", "displayName", "createdAt" }, Names(account));
        Assert.Equal("Vini#09291", account.GetProperty("display").GetString());
        var stats = body.GetProperty("stats");
        Assert.Equal(StatKeys.Counters.Concat(StatKeys.Maxima), Names(stats));
        Assert.Equal((3, 120, 0), (stats.GetProperty("kills").GetInt64(), stats.GetProperty("bestScore").GetInt64(), stats.GetProperty("deaths").GetInt64()));
        var achievements = body.GetProperty("achievements").EnumerateArray().ToList();
        Assert.All(achievements, achievement => Assert.Equal(new[] { "id", "unlockedAt" }, Names(achievement)));
        Assert.Equal(new[] { "first-steps", "first-blood" }, achievements.Select(achievement => achievement.GetProperty("id").GetString()));
        Assert.Equal(_time.GetUtcNow(), achievements[0].GetProperty("unlockedAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Basic dmluaTpwYXNzd29yZA==")]
    public void Profile_WithoutAValidBearerToken_Is401InvalidToken(string? authorization)
    {
        Register();

        AssertError(Controller(authorization).Profile(), StatusCodes.Status401Unauthorized, AccountErrors.InvalidToken);
    }

    [Fact]
    public void Profile_TheSchemeInAnyCase_IsABearerToken()
    {
        var registration = Register();

        Assert.Equal(StatusCodes.Status200OK, Read(Controller($"bearer {registration.Token}").Profile()).Status);
    }

    [Fact]
    public void ListAchievements_ListsTheDefaultsWithTheContractFields()
    {
        var (status, body) = Read(Controller().ListAchievements());

        Assert.Equal(StatusCodes.Status200OK, status);
        var listed = body.EnumerateArray().ToList();
        Assert.All(listed, achievement => Assert.Equal(new[] { "id", "icon", "stat", "threshold" }, Names(achievement)));
        Assert.Equal(
            Achievements.Defaults,
            listed.Select(achievement => new AchievementDefinition(
                achievement.GetProperty("id").GetString()!,
                achievement.GetProperty("icon").GetString()!,
                achievement.GetProperty("stat").GetString()!,
                achievement.GetProperty("threshold").GetInt64())));
    }

    [Theory]
    [InlineData(nameof(AccountStatsController.Report), "POST", "accounts/{id:guid}/stats", true)]
    [InlineData(nameof(AccountStatsController.Profile), "GET", "accounts/me/profile", false)]
    [InlineData(nameof(AccountStatsController.ListAchievements), "GET", "achievements", false)]
    public void Routes_AreTheOnesOfTheContract(string action, string method, string template, bool needsApiKey)
    {
        var endpoint = typeof(AccountStatsController).GetMethod(action)!;
        var verb = endpoint.GetCustomAttribute<HttpMethodAttribute>()!;

        Assert.Null(typeof(AccountStatsController).GetCustomAttribute<RouteAttribute>());
        Assert.NotNull(typeof(AccountStatsController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal((method, template), (verb.HttpMethods.Single(), verb.Template));
        Assert.Equal(needsApiKey, endpoint.GetCustomAttribute<ApiKeyAuth>() is not null);
    }
}
