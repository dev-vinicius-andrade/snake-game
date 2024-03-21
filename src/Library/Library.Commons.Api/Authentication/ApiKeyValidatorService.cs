using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Api.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Library.Commons.Api.Authentication;

internal class ApiKeyValidatorService : IApiKeyValidatorService
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var authOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<AuthConfiguration>>();

        if (!IsAuthApiKeyConfigured(authOptions.Value))
            return;

        if (!IsAuthHeaderValid(context, out var apiKey))
        {
            HandleApiKeyMissing(context);
            return;
        }

        if (IsValidApiKey(authOptions.Value, apiKey)) return;

        HandleInvalidApiKey(context);
        await Task.CompletedTask;

    }

    private static bool IsAuthHeaderValid(AuthorizationFilterContext context, out StringValues apiKey)
    {
        return AuthHeaderExists(context, out apiKey) && !string.IsNullOrWhiteSpace(apiKey.ToString());
    }

    private static bool IsValidApiKey(AuthConfiguration authConfiguration, StringValues apiKey)
    {
        return authConfiguration!.ApiKeys!.Contains(apiKey.ToString());
    }

    private static bool AuthHeaderExists(AuthorizationFilterContext context, out StringValues apiKey)
    {
        return context.HttpContext.Request.Headers.TryGetValue(AuthConstants.ApiKeyHeaderName, out apiKey);
    }


    private void HandleInvalidApiKey(AuthorizationFilterContext context)
    {
        context.Result = new UnauthorizedObjectResult("Invalid ApiKey");
    }

    private bool IsAuthApiKeyConfigured(AuthConfiguration? authConfiguration)
    {
        if (authConfiguration == null) return false;
        return authConfiguration is { ApiKeys: not null } && authConfiguration.ApiKeys.Any();
    }

    private static void HandleApiKeyMissing(AuthorizationFilterContext context)
    {
        context.Result = new UnauthorizedObjectResult($"Missing {AuthConstants.ApiKeyHeaderName} header");

    }
}