using System.Net.Http.Headers;
using System.Text.Json;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>Talks to the providers over HTTPS. Nothing it receives is logged: access tokens are used once and forgotten.</summary>
public sealed class HttpOAuthClient(HttpClient http) : IOAuthClient
{
    public async Task<string?> ExchangeCodeAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, provider.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["code_verifier"] = codeVerifier,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String
            ? token.GetString()
            : null;
    }

    public async Task<JsonElement?> GetUserAsync(OAuthProviderInfo provider, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, provider.UserUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // GitHub refuses a call without a user agent.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("snake-game", "1.0"));
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }
}
