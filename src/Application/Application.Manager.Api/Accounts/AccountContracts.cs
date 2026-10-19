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
    IReadOnlyList<DeviceToken> Devices)
{
    /// <summary>The name as players see it: Name#09291.</summary>
    public string Display => $"{Username}#{Tag:D5}";
}

/// <summary>A browser (or device) that is signed in. Only the hash of its secret is kept.</summary>
public sealed record DeviceToken(string Hash, DateTimeOffset CreatedAt, string? Label = null);

/// <summary>Where accounts are kept. A JSON file for now (FileAccountStore), Postgres later: the rest of the code only knows this.</summary>
public interface IAccountStore
{
    Account? Find(Guid id);

    /// <summary>The account with this username (case does not matter) and tag, or null.</summary>
    Account? Find(string username, int tag);

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
