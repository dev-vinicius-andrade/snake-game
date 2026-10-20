using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Text.Json;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Rooms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class AdminRolesControllerTests
{
    private const string Ip = "203.0.113.7";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly MemoryRoleStore _roles = new();
    private readonly AccountService _accounts;

    public AdminRolesControllerTests()
    {
        _accounts = new AccountService(new FileAccountStore(null), AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);
    }

    private Guid NewPlayer(string name, int tag) => _accounts.Register(name, tag, null, Ip).Value!.Account.Id;

    private AdminRolesController Controller(Guid? callerAccount = null)
    {
        var http = new DefaultHttpContext();
        if (callerAccount is Guid id) http.Items[AdminAuth.AccountItem] = id;
        return new AdminRolesController(_accounts, _roles) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static int Status(IActionResult result) => Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode!.Value;

    private static string ErrorOf(IActionResult result)
        => JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<ObjectResult>(result).Value, WebJson).GetProperty("error").GetString()!;

    [Fact]
    public void Grant_GivesTheRoleToTheAccountWithThatNameAndTag()
    {
        var id = NewPlayer("Vini", 9291);

        var result = Controller().Grant(Roles.Admin, "Vini", 9291);

        Assert.Equal(StatusCodes.Status204NoContent, Status(result));
        Assert.Equal(new[] { Roles.Admin }, _roles.RolesOf(id));
    }

    [Fact]
    public void Grant_TheNameIsNotCaseSensitive()
    {
        var id = NewPlayer("Vini", 9291);

        Controller().Grant("ADMIN", "vini", 9291);

        Assert.Equal(new[] { Roles.Admin }, _roles.RolesOf(id));
    }

    [Fact]
    public void Grant_AnAccountThatDoesNotExist_Is404_AndARoleThatDoesNotExist_Is400()
    {
        NewPlayer("Vini", 9291);

        var noAccount = Controller().Grant(Roles.Admin, "Nobody", 1);
        var noRole = Controller().Grant("superuser", "Vini", 9291);

        Assert.Equal(StatusCodes.Status404NotFound, Status(noAccount));
        Assert.Equal("unknownAccount", ErrorOf(noAccount));
        Assert.Equal(StatusCodes.Status400BadRequest, Status(noRole));
        Assert.Equal("unknownRole", ErrorOf(noRole));
    }

    [Fact]
    public void Revoke_TakesTheRoleAway_AndARoleThatWasNotHeldIs404()
    {
        var id = NewPlayer("Vini", 9291);
        _roles.Grant(id, Roles.Admin);

        var first = Controller().Revoke(Roles.Admin, "Vini", 9291);
        var second = Controller().Revoke(Roles.Admin, "Vini", 9291);

        Assert.Equal(StatusCodes.Status204NoContent, Status(first));
        Assert.Equal(StatusCodes.Status404NotFound, Status(second));
        Assert.Empty(_roles.RolesOf(id));
    }

    [Fact]
    public void Revoke_AnAdminCannotTakeTheAdminRoleFromThemself()
    {
        var me = NewPlayer("Vini", 9291);
        _roles.Grant(me, Roles.Admin);

        var result = Controller(callerAccount: me).Revoke(Roles.Admin, "Vini", 9291);

        Assert.Equal(StatusCodes.Status409Conflict, Status(result));
        Assert.Equal("cannotRevokeSelf", ErrorOf(result));
        Assert.Equal(new[] { Roles.Admin }, _roles.RolesOf(me));
    }

    [Fact]
    public void Revoke_AnAdminCanTakeItFromAnotherAdmin()
    {
        var me = NewPlayer("Ana", 1);
        var other = NewPlayer("Bob", 2);
        _roles.Grant(me, Roles.Admin);
        _roles.Grant(other, Roles.Admin);

        Assert.Equal(StatusCodes.Status204NoContent, Status(Controller(callerAccount: me).Revoke(Roles.Admin, "Bob", 2)));
    }

    [Fact]
    public void List_ShowsTheRolesAndWhoIsAdmin_ByTheNameOtherPlayersSee()
    {
        var id = NewPlayer("Vini", 9291);
        _roles.Grant(id, Roles.Admin);

        var ok = Assert.IsType<OkObjectResult>(Controller().List());

        var body = JsonSerializer.SerializeToElement(ok.Value, WebJson);
        Assert.Equal(new[] { Roles.User, Roles.Admin }, body.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        var admin = Assert.Single(body.GetProperty("admins").EnumerateArray());
        Assert.Equal("Vini", admin.GetProperty("displayName").GetString());
        Assert.Equal("Vini#09291", admin.GetProperty("handle").GetString());
    }
}
