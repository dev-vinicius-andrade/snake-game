namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>The keys of one provider, made by registering the game with it (see docs/ACCOUNTS.md). Never sent to a browser.</summary>
public sealed class OAuthCredentials
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool IsComplete => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>Bound from the <c>OAuth</c> section. A provider is offered to players only when its keys are there.</summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    /// <summary>
    /// Where the manager is reached from outside, without the end slash (for example <c>https://snake.web.games.andradev.io/api</c>). The
    /// address a provider sends the player back to is made from it, and has to be registered with the provider exactly.
    /// </summary>
    public string PublicApiUrl { get; set; } = string.Empty;

    /// <summary>Where the site is, for sending the player back to it. Empty: the same host as the manager, by a path.</summary>
    public string PublicUiUrl { get; set; } = string.Empty;

    /// <summary>The keys by provider id (github, discord, google).</summary>
    public Dictionary<string, OAuthCredentials> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
