using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Rooms;
using Library.Commons.Api.Entities.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class AdminAuthTests
{
    private const string Key = "the-admin-key";
    private const string Ip = "203.0.113.7";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly MemoryRoleStore _roles = new();
    private readonly AccountService _accounts;

    public AdminAuthTests()
    {
        _accounts = new AccountService(new FileAccountStore(null), AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);
    }

    private (string Token, Guid Id) NewPlayer(string name = "Vini", int tag = 9291)
    {
        var registered = _accounts.Register(name, tag, null, Ip).Value!;
        return (registered.Token, registered.Account.Id);
    }

    private IActionResult? Run(string? adminKey = null, string? bearer = null, string[]? configuredKeys = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IOptions<AuthConfiguration>>(Options.Create(new AuthConfiguration { AdminApiKeys = (configuredKeys ?? [Key]).ToList() }))
            .AddSingleton(_accounts)
            .AddSingleton<IRoleStore>(_roles)
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (adminKey is not null) http.Request.Headers[AdminAuth.HeaderName] = adminKey;
        if (bearer is not null) http.Request.Headers.Authorization = $"Bearer {bearer}";
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        new AdminAuth().OnAuthorization(context);
        return context.Result;
    }

    [Fact]
    public void TheAdminKey_StillLetsARequestThrough()
    {
        Assert.Null(Run(adminKey: Key));
    }

    [Fact]
    public void AWrongAdminKey_IsRefused_EvenWithAnAdminSignedIn()
    {
        var (token, id) = NewPlayer();
        _roles.Grant(id, Roles.Admin);

        var result = Run(adminKey: "wrong", bearer: token);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void NothingAtAll_IsRefused()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(Run()).StatusCode);
    }

    [Fact]
    public void ASignedInPlayerWithTheAdminRole_IsLetThrough_WithoutAKey()
    {
        var (token, id) = NewPlayer();
        _roles.Grant(id, Roles.Admin);

        Assert.Null(Run(bearer: token, configuredKeys: []));
    }

    [Fact]
    public void ASignedInPlayerWithoutTheRole_IsForbidden_NotUnauthorized()
    {
        var (token, _) = NewPlayer();

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(Run(bearer: token)).StatusCode);
    }

    [Fact]
    public void ATokenThatIsNotKnown_IsUnauthorized()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(Run(bearer: "not-a-token")).StatusCode);
    }

    [Fact]
    public void TheRoleIsLookedAtEveryTime_SoTakingItAwayWorksAtOnce()
    {
        var (token, id) = NewPlayer();
        _roles.Grant(id, Roles.Admin);
        Assert.Null(Run(bearer: token));

        _roles.Revoke(id, Roles.Admin);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(Run(bearer: token)).StatusCode);
    }

    [Fact]
    public void NoAdminKeysConfigured_AndNoRole_MeansNobodyIsAnAdmin()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(Run(adminKey: Key, configuredKeys: [])).StatusCode);
    }
}
