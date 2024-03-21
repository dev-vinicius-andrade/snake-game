using Library.Commons.Api.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Commons.Api.Attributes.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuth : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<IApiKeyValidatorService>();
        await validator.OnAuthorizationAsync(context);
    }
}