using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>A provider the player may use, as the site shows it.</summary>
public sealed record OAuthProviderSummary(string Id, string Title);

/// <summary>Where to send the browser, and the state it has to bring back (the controller keeps it in a cookie).</summary>
public sealed record OAuthStart(string Url, string State);

/// <summary>
/// Signing in with a social account (docs/ACCOUNTS.md): the authorization code flow with PKCE. The game asks the provider only who the
/// player is (an id, a username, a name; never the email), makes or finds the account linked to that id, and forgets the provider's token.
/// The browser then trades a one time ticket for the game's own device token, so the token is never in an address.
/// </summary>
public sealed class OAuthService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);

    private sealed record Pending(string Provider, string Verifier);

    private readonly OAuthOptions _options;
    private readonly IOAuthClient _client;
    private readonly AccountService _accounts;
    private readonly ILogger<OAuthService>? _logger;
    private readonly OneTimeStore<Pending> _pending;
    private readonly OneTimeStore<SignIn> _tickets;

    public OAuthService(OAuthOptions options, IOAuthClient client, AccountService accounts, TimeProvider time, ILogger<OAuthService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(time);
        _options = options;
        _client = client;
        _accounts = accounts;
        _logger = logger;
        _pending = new OneTimeStore<Pending>(time, StateLifetime);
        _tickets = new OneTimeStore<SignIn>(time, TicketLifetime);
    }

    /// <summary>The providers with keys, in the order the game knows them. None while the public address is not set.</summary>
    public IReadOnlyList<OAuthProviderSummary> Providers()
        => string.IsNullOrWhiteSpace(_options.PublicApiUrl)
            ? []
            : OAuthProviders.All.Where(IsConfigured).Select(provider => new OAuthProviderSummary(provider.Id, provider.Title)).ToList();

    /// <summary>How a provider signs players in, or null for one that is unknown or has no keys.</summary>
    public OAuthKind? KindOf(string? providerId) => Configured(providerId) is var (provider, _) ? provider.Kind : null;

    /// <summary>The address to send the player to. <see cref="AccountErrors.UnknownProvider"/> for a provider that is unknown or has no keys.</summary>
    public AccountResult<OAuthStart> Start(string? providerId)
    {
        if (Configured(providerId) is not var (provider, credentials))
            return AccountResult<OAuthStart>.Failure(AccountErrors.UnknownProvider);
        var verifier = Random(48);
        var state = _pending.Put(new Pending(provider.Id, verifier));
        if (provider.Kind == OAuthKind.OpenId)
            return AccountResult<OAuthStart>.Success(new OAuthStart(SteamOpenId.StartUrl(provider.AuthorizeUrl, RedirectUri(provider), Realm(), state), state));
        var query = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", credentials.ClientId),
            new("redirect_uri", RedirectUri(provider)),
            new("state", state),
            new("code_challenge", Challenge(verifier)),
            new("code_challenge_method", "S256"),
        };
        if (provider.Scope.Length > 0)
            query.Insert(3, new("scope", provider.Scope));
        var url = provider.AuthorizeUrl + "?" + string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return AccountResult<OAuthStart>.Success(new OAuthStart(url, state));
    }

    /// <summary>
    /// The player came back from the provider with a code. The state has to be the one the browser was given and still be waiting, and for
    /// the same provider. Gives a one time ticket for <see cref="Redeem"/>.
    /// </summary>
    public async Task<AccountResult<string>> CompleteAsync(string? providerId, string? code, string? state, string? browserState, string? clientIp, CancellationToken cancellationToken)
    {
        if (Configured(providerId) is not var (provider, credentials) || provider.Kind != OAuthKind.OAuth)
            return AccountResult<string>.Failure(AccountErrors.UnknownProvider);
        if (!SameState(state, browserState))
            return AccountResult<string>.Failure(AccountErrors.InvalidState);
        var pending = _pending.Take(state);
        if (pending is null || pending.Provider != provider.Id)
            return AccountResult<string>.Failure(AccountErrors.InvalidState);
        if (string.IsNullOrWhiteSpace(code))
            return AccountResult<string>.Failure(AccountErrors.ProviderFailed);
        return await SignInAsync(provider, credentials, code, pending.Verifier, clientIp, cancellationToken);
    }

    /// <summary>
    /// The same, for a provider that answers with a signed OpenID answer instead of a code (Steam). <paramref name="answer"/> is everything in the
    /// address the browser came back with. The state has to be this browser's, the answer has to be for the address the game asked for, the
    /// identity has to be Steam's own address for a player, and Steam itself has to say the answer is its own. Only then is the player signed in.
    /// </summary>
    public async Task<AccountResult<string>> CompleteOpenIdAsync(string? providerId, IReadOnlyDictionary<string, string> answer, string? browserState, string? clientIp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (Configured(providerId) is not var (provider, credentials) || provider.Kind != OAuthKind.OpenId)
            return AccountResult<string>.Failure(AccountErrors.UnknownProvider);
        var state = Field(answer, "state");
        if (!SameState(state, browserState) || _pending.Take(state) is not { } pending || pending.Provider != provider.Id)
            return AccountResult<string>.Failure(AccountErrors.InvalidState);
        if (Field(answer, "openid.mode") == "cancel")
            return AccountResult<string>.Failure(AccountErrors.Cancelled);
        var steamId = AnswerIsForUs(provider, answer, state!);
        if (steamId is null)
            return AccountResult<string>.Failure(AccountErrors.ProviderFailed);
        return await SignInSteamAsync(provider, credentials, answer, steamId, clientIp, cancellationToken);
    }

    /// <summary>The Steam id the answer is about, or null when it is not an answer to the request the game made.</summary>
    private string? AnswerIsForUs(OAuthProviderInfo provider, IReadOnlyDictionary<string, string> answer, string state)
    {
        var claimed = Field(answer, "openid.claimed_id");
        var ok = Field(answer, "openid.mode") == "id_res"
                 && Field(answer, "openid.op_endpoint") == SteamOpenId.Endpoint
                 && Field(answer, "openid.return_to") == SteamOpenId.ReturnTo(RedirectUri(provider), state)
                 && Field(answer, "openid.identity") == claimed;
        return ok ? SteamOpenId.ReadSteamId(claimed) : null;
    }

    private async Task<AccountResult<string>> SignInSteamAsync(OAuthProviderInfo provider, OAuthCredentials credentials, IReadOnlyDictionary<string, string> answer, string steamId, string? clientIp, CancellationToken cancellationToken)
    {
        string? persona;
        try
        {
            if (!await _client.VerifyOpenIdAsync(provider, answer, cancellationToken))
                return AccountResult<string>.Failure(AccountErrors.ProviderFailed);
            persona = await _client.GetSteamPersonaAsync(provider, credentials.ClientSecret, steamId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger?.LogWarning("Signing in with {Provider} failed: {Reason}", provider.Id, exception.GetType().Name);
            return AccountResult<string>.Failure(AccountErrors.ProviderFailed);
        }
        var signIn = _accounts.SignInWithExternal(provider.Id, steamId, persona, persona, clientIp);
        return signIn.Succeeded
            ? AccountResult<string>.Success(_tickets.Put(signIn.Value))
            : signIn.Error == AccountErrors.Throttled ? AccountResult<string>.Throttled(TimeSpan.FromSeconds(signIn.RetryAfterSeconds ?? 60)) : AccountResult<string>.Failure(signIn.Error);
    }

    private static string? Field(IReadOnlyDictionary<string, string> answer, string name) => answer.TryGetValue(name, out var value) ? value : null;

    /// <summary>Where the game is on the web, with a slash: what Steam is told the request is for, and has to match the return address.</summary>
    private string Realm() => new Uri(_options.PublicApiUrl.TrimEnd('/') + "/").GetLeftPart(UriPartial.Authority) + "/";

    /// <summary>Trades a ticket for the account and the device token made for it. Once only, and only for a couple of minutes.</summary>
    public AccountResult<SignIn> Redeem(string? ticket)
    {
        var signIn = _tickets.Take(ticket);
        return signIn is null ? AccountResult<SignIn>.Failure(AccountErrors.InvalidTicket) : AccountResult<SignIn>.Success(signIn);
    }

    private async Task<AccountResult<string>> SignInAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string verifier, string? clientIp, CancellationToken cancellationToken)
    {
        var profile = await ReadProfileAsync(provider, credentials, code, verifier, cancellationToken);
        if (profile is null)
            return AccountResult<string>.Failure(AccountErrors.ProviderFailed);
        var signIn = _accounts.SignInWithExternal(provider.Id, profile.Subject, profile.Username, profile.DisplayName, clientIp);
        return signIn.Succeeded
            ? AccountResult<string>.Success(_tickets.Put(signIn.Value))
            : signIn.Error == AccountErrors.Throttled ? AccountResult<string>.Throttled(TimeSpan.FromSeconds(signIn.RetryAfterSeconds ?? 60)) : AccountResult<string>.Failure(signIn.Error);
    }

    private async Task<OAuthProfile?> ReadProfileAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string verifier, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await _client.ExchangeCodeAsync(provider, credentials, code, RedirectUri(provider), verifier, cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
                return null;
            var user = await _client.GetUserAsync(provider, accessToken, cancellationToken);
            return user is JsonElement element ? provider.ReadProfile(element) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger?.LogWarning("Signing in with {Provider} failed: {Reason}", provider.Id, exception.GetType().Name);
            return null;
        }
    }

    private (OAuthProviderInfo Provider, OAuthCredentials Credentials)? Configured(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicApiUrl) || OAuthProviders.Get(providerId) is not { } provider)
            return null;
        return _options.Providers.TryGetValue(provider.Id, out var credentials) && credentials.IsCompleteFor(provider) ? (WithOverrides(provider, credentials), credentials) : null;
    }

    private static OAuthProviderInfo WithOverrides(OAuthProviderInfo provider, OAuthCredentials credentials) => provider with
    {
        AuthorizeUrl = Other(credentials.AuthorizeUrl) ?? provider.AuthorizeUrl,
        TokenUrl = Other(credentials.TokenUrl) ?? provider.TokenUrl,
        UserUrl = Other(credentials.UserUrl) ?? provider.UserUrl,
    };

    private static string? Other(string address) => string.IsNullOrWhiteSpace(address) ? null : address.Trim();

    private bool IsConfigured(OAuthProviderInfo provider) => _options.Providers.TryGetValue(provider.Id, out var credentials) && credentials.IsCompleteFor(provider);

    /// <summary>The address the provider sends the player back to. It has to be registered with the provider exactly as it is here.</summary>
    private string RedirectUri(OAuthProviderInfo provider) => $"{_options.PublicApiUrl.TrimEnd('/')}/accounts/oauth/{provider.Id}/callback";

    private static bool SameState(string? state, string? browserState)
        => !string.IsNullOrEmpty(state) && !string.IsNullOrEmpty(browserState)
           && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(browserState));

    private static string Random(int bytes) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Challenge(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
