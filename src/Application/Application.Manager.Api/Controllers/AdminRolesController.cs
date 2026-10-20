using Application.Manager.Api.Accounts;
using Application.Manager.Api.Rooms;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

/// <summary>Who has which role (docs/ROLES.md). For administrators only.</summary>
[Route("admin/roles")]
[ApiController]
[AdminAuth]
[Produces("application/json")]
public sealed class AdminRolesController(AccountService accounts, IRoleStore roles) : Controller
{
    /// <summary>The roles there are, and the administrators.</summary>
    [HttpGet]
    public IActionResult List() => Ok(new
    {
        roles = Roles.All,
        admins = roles.AccountsWith(Roles.Admin)
            .Select(accounts.Find)
            .OfType<Account>()
            .OrderBy(account => account.PublicName, StringComparer.OrdinalIgnoreCase)
            .Select(account => new { displayName = account.PublicName, handle = account.Display }),
    });

    /// <summary>Gives the role to the account with this name and tag (as the owner signs in: <c>Name#09291</c>).</summary>
    [HttpPut("{role}/{username}/{tag:int}")]
    public IActionResult Grant(string role, string username, int tag)
    {
        if (Roles.Normalize(role) is not { } name || !Roles.All.Contains(name)) return Refusal(StatusCodes.Status400BadRequest, "unknownRole");
        var account = accounts.FindByHandle(username, tag);
        if (account is null) return Refusal(StatusCodes.Status404NotFound, "unknownAccount");
        return roles.Grant(account.Id, name) ? NoContent() : Refusal(StatusCodes.Status404NotFound, "unknownAccount");
    }

    /// <summary>Takes the role away. An administrator cannot take the admin role from themself, so the game is never left without one by accident.</summary>
    [HttpDelete("{role}/{username}/{tag:int}")]
    public IActionResult Revoke(string role, string username, int tag)
    {
        if (Roles.Normalize(role) is not { } name || !Roles.All.Contains(name)) return Refusal(StatusCodes.Status400BadRequest, "unknownRole");
        var account = accounts.FindByHandle(username, tag);
        if (account is null) return Refusal(StatusCodes.Status404NotFound, "unknownAccount");
        if (name == Roles.Admin && HttpContext.Items[AdminAuth.AccountItem] is Guid caller && caller == account.Id)
            return Refusal(StatusCodes.Status409Conflict, "cannotRevokeSelf");
        return roles.Revoke(account.Id, name) ? NoContent() : Refusal(StatusCodes.Status404NotFound, "roleNotHeld");
    }

    private ObjectResult Refusal(int status, string error) => StatusCode(status, new { error });
}
