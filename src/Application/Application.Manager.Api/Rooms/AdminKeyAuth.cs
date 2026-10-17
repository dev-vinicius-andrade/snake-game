using Library.Commons.Api.Entities.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Application.Manager.Api.Rooms;

/// <summary>Lets a request through only when it carries one of the configured admin keys in <see cref="HeaderName"/>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminKeyAuth : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<AuthConfiguration>>();
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!AdminKeyValidator.IsValid(options.Value.AdminApiKeys, provided))
            context.Result = new UnauthorizedObjectResult("Invalid admin key");
    }
}
