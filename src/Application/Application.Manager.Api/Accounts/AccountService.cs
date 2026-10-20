using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Registers players, signs them in with their recovery phrase, and recognises their devices (docs/ACCOUNTS.md).
/// Expected failures are results with an <see cref="AccountErrors"/> code, never exceptions. Only the store throws (when it cannot save).
/// </summary>
public sealed class AccountService
{
    /// <summary>
    /// The secret that keys the hash of a social account's id when none is configured. Fine for tests and a local game; a real deployment sets
    /// <c>AccountsConfiguration:LoginPepper</c>, because with this one anybody could rebuild the hashes.
    /// </summary>
    public const string DevelopmentPepper = "snake-game-development-pepper-set-AccountsConfiguration-LoginPepper";

    /// <summary>Devices signed in at once; signing in one more drops the oldest.</summary>
    public const int MaxDevices = 10;

    /// <summary>Times a registration tries to add the account when another one takes its tag first.</summary>
    public const int AddAttempts = 5;

    private const string UnknownIp = "unknown";

    private readonly IAccountStore _store;
    private readonly AccountThrottles _throttles;
    private readonly IWordFilter _filter;
    private readonly RecoveryPhrase _phrases;
    private readonly SecretHasher _hasher;
    private readonly TimeProvider _time;
    private readonly ILogger<AccountService>? _logger;
    private readonly Func<int, int>? _nextTag;
    private readonly Lazy<string> _unknownAccountHash;
    private readonly object _accountChanges = new();
    private readonly string _loginPepper;

    /// <param name="filter">Blocked usernames; <see cref="SimpleWordFilter.Default"/> when null.</param>
    /// <param name="nextTag">Picks random tags like <see cref="Random.Next(int)"/>; <see cref="Random.Shared"/> when null.</param>
    public AccountService(IAccountStore store, AccountThrottles throttles, IWordFilter? filter, RecoveryPhrase phrases, SecretHasher hasher,
        TimeProvider time, ILogger<AccountService>? logger = null, Func<int, int>? nextTag = null, string? loginPepper = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(throttles);
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(time);
        _store = store;
        _throttles = throttles;
        _filter = filter ?? SimpleWordFilter.Default;
        _phrases = phrases;
        _hasher = hasher;
        _time = time;
        _logger = logger;
        _nextTag = nextTag;
        _loginPepper = string.IsNullOrEmpty(loginPepper) ? DevelopmentPepper : loginPepper;
        if (string.IsNullOrEmpty(loginPepper))
            logger?.LogWarning("AccountsConfiguration:LoginPepper is not set: social logins are hashed with the built in development secret");
        _unknownAccountHash = new Lazy<string>(() => hasher.Hash(phrases.Generate()));
    }

    /// <summary>
    /// A new account with the cleaned <paramref name="username"/>, the wanted tag when free (else a random free one), and the chosen phrase
    /// when strong (a random one when none or a blank one is given). Every valid registration counts towards the per IP throttle.
    /// </summary>
    public AccountResult<Registration> Register(string? username, int? wantedTag, string? chosenPhrase, string? clientIp, string? displayName = null)
    {
        var throttleKey = RegistrationKey(clientIp);
        if (_throttles.Registration.RetryAfter(throttleKey) is TimeSpan wait)
            return AccountResult<Registration>.Throttled(wait);
        var request = ValidateRegistration(username, wantedTag, chosenPhrase, displayName);
        if (!request.Succeeded)
            return AccountResult<Registration>.Failure(request.Error);
        _throttles.Registration.Failed(throttleKey);
        return AddAccount(request.Value);
    }

    /// <summary>
    /// Signs in a new device with username, tag and recovery phrase. An unknown account and a wrong phrase give the same
    /// <see cref="AccountErrors.InvalidCredentials"/>, take the same time and count a failure for the account and for the IP.
    /// </summary>
    public AccountResult<SignIn> Login(string? username, int? tag, string? phrase, string? clientIp)
    {
        var keys = SignInKeys.For(username, tag, clientIp);
        if (LongestWait(keys) is TimeSpan wait)
            return AccountResult<SignIn>.Throttled(wait);
        var account = FindForSignIn(username, tag);
        var normalizedPhrase = RecoveryPhrase.Normalize(phrase ?? string.Empty);
        if (!PhraseMatches(account, normalizedPhrase))
            return Refuse(keys);
        _throttles.SignIn.Succeeded(keys.Account);
        return SignInDevice(account, normalizedPhrase);
    }

    /// <summary>
    /// Signs in a player who proved who they are to a social provider (GitHub, Discord, Google, Steam). Only a keyed hash of the provider's id is
    /// used and kept (<see cref="LoginIdentifier"/>): it finds the account, which gets a new device. A first time makes one with a single name,
    /// the provider's display name cleaned to fit (else its username, else "Player"): that is both the username and what other players see. Nothing
    /// else the provider said is kept. Such an account has no recovery phrase, so a phrase never signs in to it. Only a new account counts towards
    /// the per IP registration throttle.
    /// </summary>
    public AccountResult<SignIn> SignInWithExternal(string? provider, string? subject, string? usernameSuggestion, string? displayNameSuggestion, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
            return AccountResult<SignIn>.Failure(AccountErrors.InvalidCredentials);
        var idHash = LoginIdentifier.Hash(provider, subject, _loginPepper);
        for (var attempt = 0; attempt < AddAttempts; attempt++)
        {
            if (_store.FindByExternalLogin(provider, idHash) is { } existing)
                return AddDevice(existing);
            var created = CreateExternalAccount(provider.Trim().ToLowerInvariant(), idHash, usernameSuggestion, displayNameSuggestion, clientIp);
            if (created is not null)
                return created;
        }
        return AccountResult<SignIn>.Failure(AccountErrors.NoTagsLeft);
    }

    /// <summary>
    /// Links a social account to an existing account, so the player can sign in with it too (their phrase keeps working). Only the keyed hash of the
    /// provider's id is kept. Linking what is already linked to this account changes nothing. <see cref="AccountErrors.AlreadyLinked"/> when
    /// another player's account has it, <see cref="AccountErrors.ProviderInUse"/> when this account already has another account of that provider.
    /// </summary>
    public AccountResult<Account> LinkExternal(Guid accountId, string? provider, string? subject)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
            return AccountResult<Account>.Failure(AccountErrors.InvalidCredentials);
        var idHash = LoginIdentifier.Hash(provider, subject, _loginPepper);
        var name = provider.Trim().ToLowerInvariant();
        lock (_accountChanges)
        {
            var account = _store.Find(accountId);
            if (account is null)
                return AccountResult<Account>.Failure(AccountErrors.InvalidToken);
            if (_store.FindByExternalLogin(name, idHash) is { } owner)
                return owner.Id == account.Id ? AccountResult<Account>.Success(account) : AccountResult<Account>.Failure(AccountErrors.AlreadyLinked);
            if (account.Logins.Any(login => login.Provider == name))
                return AccountResult<Account>.Failure(AccountErrors.ProviderInUse);
            var linked = account with { ExternalLogins = account.Logins.Append(new ExternalLogin(name, idHash, _time.GetUtcNow())).ToList().AsReadOnly() };
            return _store.Update(linked)
                ? AccountResult<Account>.Success(linked)
                : AccountResult<Account>.Failure(AccountErrors.AlreadyLinked);
        }
    }

    /// <summary>The new account, a refusal, or null when the add lost a race and the caller should look again.</summary>
    private AccountResult<SignIn>? CreateExternalAccount(string provider, string idHash, string? usernameSuggestion, string? displayNameSuggestion, string? clientIp)
    {
        var throttleKey = RegistrationKey(clientIp);
        if (_throttles.Registration.RetryAfter(throttleKey) is TimeSpan wait)
            return AccountResult<SignIn>.Throttled(wait);
        // One name: what the provider calls the player, else its username. It is both the username and the name others see.
        var username = UsernameSanitizer.Clean(displayNameSuggestion, _filter) ?? UsernameSanitizer.Clean(usernameSuggestion, _filter) ?? FallbackUsername;
        var login = new ExternalLogin(provider, idHash, _time.GetUtcNow());
        var (token, tokenHash) = DeviceTokens.Create();
        var device = new DeviceToken(tokenHash, login.LinkedAt);
        var request = new NewAccount(username, null, string.Empty, null, login);
        var attempt = TryAddWithFreeTag(request, Account.NoPhrase, device);
        if (attempt.Added is null)
            return attempt.IsFinal ? AccountResult<SignIn>.Failure(AccountErrors.NoTagsLeft) : null;
        _throttles.Registration.Failed(throttleKey);
        _logger?.LogInformation("An account was made with {Provider}", provider);
        return AccountResult<SignIn>.Success(new SignIn(attempt.Added, token));
    }

    private AccountResult<SignIn> AddDevice(Account account)
    {
        var (token, tokenHash) = DeviceTokens.Create();
        var device = new DeviceToken(tokenHash, _time.GetUtcNow());
        var signedIn = ChangeAccount(account.Id, current => current with { Devices = WithDevice(current.Devices, device) });
        return signedIn is null ? AccountResult<SignIn>.Failure(AccountErrors.InvalidCredentials) : AccountResult<SignIn>.Success(new SignIn(signedIn, token));
    }

    /// <summary>The username of an account made from a social login that gave no name that fits.</summary>
    private const string FallbackUsername = "Player";

    /// <summary>The account a device token belongs to, or null.</summary>
    /// <summary>The account with this id, or null.</summary>
    public Account? Find(Guid id) => _store.Find(id);

    /// <summary>The account with this username (any case) and tag, or null. For an administrator who names the player.</summary>
    public Account? FindByHandle(string? username, int tag)
        => string.IsNullOrWhiteSpace(username) || !Tags.IsValid(tag) ? null : _store.Find(UsernameRules.Normalize(username), tag);

    public Account? Authenticate(string? token)
        => DeviceTokens.LooksLikeToken(token) ? _store.FindByDeviceTokenHash(DeviceTokens.Hash(token)) : null;

    /// <summary>Signs out the device of <paramref name="token"/>. False when the token is not signed in.</summary>
    public bool Logout(string? token)
    {
        if (!DeviceTokens.LooksLikeToken(token))
            return false;
        var tokenHash = DeviceTokens.Hash(token);
        var account = _store.FindByDeviceTokenHash(tokenHash);
        if (account is null)
            return false;
        return ChangeAccount(account.Id, current => current with { Devices = WithoutDevice(current.Devices, tokenHash) }) is not null;
    }

    /// <summary>
    /// The username as it would be kept, and whether <paramref name="tag"/> ("09291", "9291" or "#09291") is free for it, or any tag when
    /// none is given. Every check counts towards the per IP throttle.
    /// </summary>
    public AccountResult<UsernameAvailability> Availability(string? username, string? tag, string? clientIp)
    {
        var throttleKey = LookupKey(clientIp);
        if (_throttles.Lookup.RetryAfter(throttleKey) is TimeSpan wait)
            return AccountResult<UsernameAvailability>.Throttled(wait);
        _throttles.Lookup.Failed(throttleKey);
        var name = UsernameRules.Validate(username, _filter);
        if (!name.IsValid)
            return AccountResult<UsernameAvailability>.Failure(name.Error);
        if (!TryReadTag(tag, out var wanted))
            return AccountResult<UsernameAvailability>.Failure(AccountErrors.InvalidTag);
        return AccountResult<UsernameAvailability>.Success(new UsernameAvailability(name.Username, IsFree(wanted, _store.TagsOf(name.Username))));
    }

    /// <summary>
    /// Changes the name other players see for the account of <paramref name="token"/>. A blank name goes back to the username. The rules are
    /// the username's, so the name cannot carry a tag.
    /// </summary>
    public AccountResult<Account> ChangeDisplayName(string? token, string? displayName)
    {
        var account = Authenticate(token);
        if (account is null)
            return AccountResult<Account>.Failure(AccountErrors.InvalidToken);
        var name = ValidateDisplayName(displayName);
        if (!name.Succeeded)
            return AccountResult<Account>.Failure(name.Error);
        var changed = ChangeAccount(account.Id, current => current with { DisplayName = name.Value.Name });
        return changed is null ? AccountResult<Account>.Failure(AccountErrors.InvalidToken) : AccountResult<Account>.Success(changed);
    }

    private sealed record CleanName(string? Name);

    /// <summary>Blank or missing: no display name (the username is shown). Otherwise it must pass the username rules.</summary>
    private AccountResult<CleanName> ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return AccountResult<CleanName>.Success(new CleanName(null));
        var name = UsernameRules.Validate(displayName, _filter);
        return name.IsValid
            ? AccountResult<CleanName>.Success(new CleanName(name.Username))
            : AccountResult<CleanName>.Failure(AccountErrors.InvalidDisplayName);
    }

    private AccountResult<NewAccount> ValidateRegistration(string? username, int? wantedTag, string? chosenPhrase, string? displayName)
    {
        var name = UsernameRules.Validate(username, _filter);
        if (!name.IsValid)
            return AccountResult<NewAccount>.Failure(name.Error);
        var shown = ValidateDisplayName(displayName);
        if (!shown.Succeeded)
            return AccountResult<NewAccount>.Failure(shown.Error);
        if (wantedTag is int tag && !Tags.IsValid(tag))
            return AccountResult<NewAccount>.Failure(AccountErrors.InvalidTag);
        if (string.IsNullOrWhiteSpace(chosenPhrase))
            return AccountResult<NewAccount>.Success(new NewAccount(name.Username, wantedTag, _phrases.Generate(), shown.Value.Name));
        return RecoveryPhrase.Validate(chosenPhrase, name.Username) is string weak
            ? AccountResult<NewAccount>.Failure(weak)
            : AccountResult<NewAccount>.Success(new NewAccount(name.Username, wantedTag, chosenPhrase, shown.Value.Name));
    }

    private AccountResult<Registration> AddAccount(NewAccount request)
    {
        var phraseHash = _hasher.Hash(RecoveryPhrase.Normalize(request.Phrase));
        var (token, tokenHash) = DeviceTokens.Create();
        var device = new DeviceToken(tokenHash, _time.GetUtcNow());
        var account = Enumerable.Range(0, AddAttempts)
            .Select(_ => TryAddWithFreeTag(request, phraseHash, device))
            .FirstOrDefault(attempt => attempt.IsFinal)?.Added;
        if (account is null)
            return AccountResult<Registration>.Failure(AccountErrors.NoTagsLeft);
        _logger?.LogInformation("Account {Display} registered", account.Display);
        return AccountResult<Registration>.Success(new Registration(account, token, request.Phrase));
    }

    private AddAttempt TryAddWithFreeTag(NewAccount request, string phraseHash, DeviceToken device)
    {
        var tag = TagAllocator.Allocate(request.Username, request.WantedTag, _store.TagsOf(request.Username), _nextTag);
        if (tag is not int free)
            return AddAttempt.NoTagsLeft;
        var account = new Account(Guid.NewGuid(), request.Username, free, phraseHash, device.CreatedAt, [device], request.DisplayName,
            request.Login is null ? null : [request.Login]);
        return _store.TryAdd(account) ? AddAttempt.Done(account) : AddAttempt.LostRace;
    }

    private TimeSpan? LongestWait(SignInKeys keys)
        => new[] { _throttles.SignIn.RetryAfter(keys.Account), _throttles.SignIn.RetryAfter(keys.Ip) }.Max();

    private Account? FindForSignIn(string? username, int? tag)
        => tag is int wanted && Tags.IsValid(wanted) && !string.IsNullOrWhiteSpace(username)
            ? _store.Find(UsernameRules.Normalize(username), wanted)
            : null;

    private bool PhraseMatches([NotNullWhen(true)] Account? account, string normalizedPhrase)
    {
        var matches = _hasher.Verify(normalizedPhrase, account?.PhraseHash ?? _unknownAccountHash.Value);
        return matches && account is not null;
    }

    private AccountResult<SignIn> Refuse(SignInKeys keys)
    {
        _throttles.SignIn.Failed(keys.Account);
        _throttles.SignIn.Failed(keys.Ip);
        return AccountResult<SignIn>.Failure(AccountErrors.InvalidCredentials);
    }

    private AccountResult<SignIn> SignInDevice(Account account, string normalizedPhrase)
    {
        var newHash = _hasher.NeedsRehash(account.PhraseHash) ? _hasher.Hash(normalizedPhrase) : null;
        var (token, tokenHash) = DeviceTokens.Create();
        var device = new DeviceToken(tokenHash, _time.GetUtcNow());
        var signedIn = ChangeAccount(account.Id, current => current with
        {
            PhraseHash = newHash ?? current.PhraseHash,
            Devices = WithDevice(current.Devices, device),
        });
        return signedIn is null
            ? AccountResult<SignIn>.Failure(AccountErrors.InvalidCredentials)
            : AccountResult<SignIn>.Success(new SignIn(signedIn, token));
    }

    /// <summary>Reads the account again and saves the change, one change at a time, so two sign-ins never lose each other's device.</summary>
    private Account? ChangeAccount(Guid id, Func<Account, Account> change)
    {
        lock (_accountChanges)
            return ApplyChange(id, change);
    }

    private Account? ApplyChange(Guid id, Func<Account, Account> change)
    {
        var current = _store.Find(id);
        if (current is null)
            return null;
        var changed = change(current);
        return _store.Update(changed) ? changed : null;
    }

    private static IReadOnlyList<DeviceToken> WithDevice(IReadOnlyList<DeviceToken> devices, DeviceToken device)
        => devices.Skip(Math.Max(0, devices.Count - (MaxDevices - 1))).Append(device).ToList().AsReadOnly();

    private static IReadOnlyList<DeviceToken> WithoutDevice(IReadOnlyList<DeviceToken> devices, string tokenHash)
        => devices.Where(device => device.Hash != tokenHash).ToList().AsReadOnly();

    private static bool TryReadTag(string? text, out int? tag)
    {
        tag = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        var parsed = TagAllocator.TryParseTag(text, out var value);
        tag = value;
        return parsed;
    }

    private static bool IsFree(int? tag, IReadOnlySet<int> taken)
        => tag is int wanted ? !taken.Contains(wanted) : taken.Count < Tags.Count;

    private static string RegistrationKey(string? clientIp) => "reg:" + (clientIp ?? UnknownIp);

    private static string LookupKey(string? clientIp) => "avail:" + (clientIp ?? UnknownIp);

    private sealed record NewAccount(string Username, int? WantedTag, string Phrase, string? DisplayName = null, ExternalLogin? Login = null);

    private sealed record AddAttempt(Account? Added, bool IsFinal)
    {
        public static AddAttempt NoTagsLeft { get; } = new(null, true);

        public static AddAttempt LostRace { get; } = new(null, false);

        public static AddAttempt Done(Account account) => new(account, true);
    }

    private sealed record SignInKeys(string Account, string Ip)
    {
        public static SignInKeys For(string? username, int? tag, string? clientIp)
            => new(string.Create(CultureInfo.InvariantCulture, $"acct:{UsernameRules.Key(username ?? string.Empty)}#{tag}"), "ip:" + (clientIp ?? UnknownIp));
    }
}
