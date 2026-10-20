namespace Application.Manager.Api.Accounts;

/// <summary>
/// A player account: a username that anyone may share, a 5 digit tag that makes the pair unique (Name#09291), and the secrets that
/// prove ownership. There is no password: see docs/ACCOUNTS.md. Nothing in here is ever sent to a client as it is stored.
/// </summary>
public sealed record Account(
    Guid Id,
    string Username,
    int Tag,
    string PhraseHash,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DeviceToken> Devices,
    string? DisplayName = null,
    IReadOnlyList<ExternalLogin>? ExternalLogins = null)
{
    /// <summary>The "phrase hash" of an account that signs in with a social account and has no recovery phrase: nothing can match it.</summary>
    public const string NoPhrase = "none";

    /// <summary>The social accounts (GitHub, Discord, Google) that sign in to this account. Empty for an account with a phrase.</summary>
    public IReadOnlyList<ExternalLogin> Logins => ExternalLogins ?? [];

    /// <summary>
    /// The name with its tag, Name#09291: how the owner signs in. It is **private**: it is never shown to other players, because with the
    /// tag anyone knows exactly which account to go after. Other players see <see cref="PublicName"/>.
    /// </summary>
    public string Display => $"{Username}#{Tag:D5}";

    /// <summary>The name other players see in the game and on the ranking. Not unique, and says nothing about the tag. Defaults to the username.</summary>
    public string PublicName => string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName;
}

/// <summary>
/// A social account linked to a game account: which provider, and a keyed hash of the id the provider gave (see <see cref="LoginIdentifier"/>).
/// The id itself, the provider's username, and anything else the provider knows are never kept.
/// </summary>
public sealed record ExternalLogin(string Provider, string IdHash, DateTimeOffset LinkedAt);

/// <summary>A browser (or device) that is signed in. Only the hash of its secret is kept.</summary>
public sealed record DeviceToken(string Hash, DateTimeOffset CreatedAt, string? Label = null);

/// <summary>Where accounts are kept. A JSON file for now (FileAccountStore), Postgres later: the rest of the code only knows this.</summary>
public interface IAccountStore
{
    Account? Find(Guid id);

    /// <summary>The account with this username (case does not matter) and tag, or null.</summary>
    Account? Find(string username, int tag);

    /// <summary>The account linked to this social account (the provider, and the hash of its id from <see cref="LoginIdentifier"/>), or null.</summary>
    Account? FindByExternalLogin(string provider, string idHash);

    /// <summary>The account that has a device with this token hash, or null.</summary>
    Account? FindByDeviceTokenHash(string tokenHash);

    /// <summary>Tags already used with this username (case does not matter).</summary>
    IReadOnlySet<int> TagsOf(string username);

    /// <summary>Adds the account. False, and nothing changes, when the username (any case) and tag are already taken.</summary>
    bool TryAdd(Account account);

    /// <summary>Replaces the account with the same id. False when there is none.</summary>
    bool Update(Account account);
}

public static class Tags
{
    public const int Min = 0;
    public const int Max = 99_999;
    public const int Count = Max - Min + 1;

    public static bool IsValid(int tag) => tag is >= Min and <= Max;
}
