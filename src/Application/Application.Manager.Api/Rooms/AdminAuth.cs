using Application.Manager.Api.Accounts;
using Library.Commons.Api.Entities.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Application.Manager.Api.Rooms;

/// <summary>
/// Lets a request through to the admin api when it comes from an administrator: a player who is signed in (<c>Authorization: Bearer</c>, the
/// device token) and has the <see cref="Roles.Admin"/> role, or a request with one of the configured admin keys in <see cref="HeaderName"/>.
/// The key is the way in before anyone has the role (and for scripts); the role is looked at on every request, so taking it away works at once.
/// A key that is sent and is wrong is refused whatever else comes with it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminAuth : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    /// <summary>Where the filter leaves the id of the admin account (a <see cref="Guid"/>) when the request was let in by its role, not by a key.</summary>
    public const string AccountItem = "snake.adminAccountId";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(key))
        {
            var options = http.RequestServices.GetRequiredService<IOptions<AuthConfiguration>>();
            if (!AdminKeyValidator.IsValid(options.Value.AdminApiKeys, key))
                context.Result = new UnauthorizedObjectResult("Invalid admin key");
            return;
        }
        context.Result = CheckRole(http);
    }

    private static IActionResult? CheckRole(HttpContext http)
    {
        var accounts = http.RequestServices.GetService<AccountService>();
        var roles = http.RequestServices.GetService<IRoleStore>();
        var account = accounts?.Authenticate(BearerToken(http));
        if (account is null || roles is null)
            return new UnauthorizedObjectResult("An admin key or an admin sign in is needed");
        if (!Roles.IsAdmin(roles.RolesOf(account.Id)))
            return new ObjectResult("Not an admin") { StatusCode = StatusCodes.Status403Forbidden };
        http.Items[AccountItem] = account.Id;
        return null;
    }

    private static string? BearerToken(HttpContext http)
        => System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization.ToString(), out var value)
           && string.Equals(value.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? value.Parameter
            : null;
}
