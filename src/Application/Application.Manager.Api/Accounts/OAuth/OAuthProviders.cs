using System.Text.Json;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>Who a social provider says the player is. Only what the game keeps: the provider's id, a username, a display name. Never an email.</summary>
public sealed record OAuthProfile(string Subject, string? Username, string? DisplayName);

/// <summary>How a provider signs a player in: OAuth 2 (a code, traded for a token) or OpenID 2 (Steam: a signed answer, checked with the provider).</summary>
public enum OAuthKind
{
    OAuth,
    OpenId,
}

/// <summary>What is known about a provider in advance: where to send the player, where to ask, and how to read its answer.</summary>
public sealed record OAuthProviderInfo(
    string Id,
    string Title,
    string AuthorizeUrl,
    string TokenUrl,
    string UserUrl,
    /// <summary>The least that gives a name and an id. No provider is asked for the email.</summary>
    string Scope,
    Func<JsonElement, OAuthProfile?> ReadProfile,
    OAuthKind Kind = OAuthKind.OAuth);

/// <summary>GitHub, Discord, Google and Steam. Adding another provider is one more entry: its addresses and how its profile is read.</summary>
public static class OAuthProviders
{
    public static IReadOnlyList<OAuthProviderInfo> All { get; } =
    [
        new("github", "GitHub", "https://github.com/login/oauth/authorize", "https://github.com/login/oauth/access_token", "https://api.github.com/user",
            string.Empty, ReadGitHub),
        new("discord", "Discord", "https://discord.com/oauth2/authorize", "https://discord.com/api/oauth2/token", "https://discord.com/api/users/@me",
            "identify", ReadDiscord),
        new("google", "Google", "https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token",
            "https://openidconnect.googleapis.com/v1/userinfo", "openid profile", ReadGoogle),
        // Steam: OpenID, not OAuth. The Web API key (the "client secret") only asks Steam for the player's name; the user address is where that is asked.
        new("steam", "Steam", SteamOpenId.Endpoint, string.Empty, "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/", string.Empty, _ => null, OAuthKind.OpenId),
    ];

    public static OAuthProviderInfo? Get(string? id)
        => string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(provider => string.Equals(provider.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    private static OAuthProfile? ReadGitHub(JsonElement user)
    {
        var login = Text(user, "login");
        return Profile(user, "id", login, Text(user, "name") ?? login);
    }

    private static OAuthProfile? ReadDiscord(JsonElement user)
    {
        var username = Text(user, "username");
        return Profile(user, "id", username, Text(user, "global_name") ?? username);
    }

    private static OAuthProfile? ReadGoogle(JsonElement user)
    {
        var name = Text(user, "name");
        return Profile(user, "sub", Text(user, "given_name") ?? name, name);
    }

    private static OAuthProfile? Profile(JsonElement user, string idField, string? username, string? displayName)
        => Text(user, idField) is { Length: > 0 } subject ? new OAuthProfile(subject, username, displayName) : null;

    /// <summary>A field as text, whatever it is (GitHub's id is a number, Discord's a string). Null when missing, null or not a value.</summary>
    private static string? Text(JsonElement user, string name)
    {
        if (user.ValueKind != JsonValueKind.Object || !user.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
