using System.Text.Json;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>The two calls to a provider's servers. Apart so the rest can be tested without a network.</summary>
public interface IOAuthClient
{
    /// <summary>Trades the code the provider gave the browser for an access token. Null when the provider refuses.</summary>
    Task<string?> ExchangeCodeAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken);

    /// <summary>Asks who the access token belongs to. Null when the provider does not answer with a profile.</summary>
    Task<JsonElement?> GetUserAsync(OAuthProviderInfo provider, string accessToken, CancellationToken cancellationToken);
}
