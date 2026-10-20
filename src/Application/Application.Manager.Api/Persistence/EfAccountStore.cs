using Application.Manager.Api.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Accounts in the database. Same promises as <see cref="FileAccountStore"/>: a username (any case) and tag, a device token hash, and a social
/// account each belong to one account; a refused add or update changes nothing. The database enforces them with unique indexes, so they hold
/// across several manager instances too. A database that cannot be reached throws: it is never answered as "no such account".
/// </summary>
public sealed class EfAccountStore(IDbContextFactory<SnakeDbContext> factory) : IAccountStore
{
    public Account? Find(Guid id)
    {
        using var context = factory.CreateDbContext();
        return One(context, context.Accounts.Where(row => row.Id == id));
    }

    public Account? Find(string username, int tag)
    {
        ArgumentNullException.ThrowIfNull(username);
        var key = NameKey(username);
        using var context = factory.CreateDbContext();
        return One(context, context.Accounts.Where(row => row.UsernameKey == key && row.Tag == tag));
    }

    public Account? FindByExternalLogin(string provider, string idHash)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(idHash);
        var (name, hash) = (provider.Trim().ToLowerInvariant(), idHash.Trim().ToLowerInvariant());
        using var context = factory.CreateDbContext();
        return One(context, context.Accounts.Where(row => row.Logins.Any(login => login.Provider == name && login.IdHash == hash)));
    }

    public Account? FindByDeviceTokenHash(string tokenHash)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        using var context = factory.CreateDbContext();
        return One(context, context.Accounts.Where(row => row.Devices.Any(device => device.Hash == tokenHash)));
    }

    public IReadOnlySet<int> TagsOf(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        var key = NameKey(username);
        using var context = factory.CreateDbContext();
        return context.Accounts.Where(row => row.UsernameKey == key).Select(row => row.Tag).ToHashSet();
    }

    public bool TryAdd(Account account)
    {
        var row = ToRow(account);
        using var context = factory.CreateDbContext();
        context.Accounts.Add(row);
        try
        {
            context.SaveChanges();
            return true;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            if (Conflicts(context, row, ownId: null)) return false;
            throw;
        }
    }

    public bool Update(Account account)
    {
        var replacement = ToRow(account);
        using var context = factory.CreateDbContext();
        var current = context.Accounts.Include(row => row.Devices).Include(row => row.Logins).SingleOrDefault(row => row.Id == replacement.Id);
        if (current is null || Conflicts(context, replacement, ownId: replacement.Id)) return false;
        Replace(context, current, replacement);
        try
        {
            context.SaveChanges();
            return true;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            if (Conflicts(context, replacement, ownId: replacement.Id)) return false;
            throw;
        }
    }

    private static Account? One(SnakeDbContext context, IQueryable<AccountRow> query)
    {
        var row = query.Include(account => account.Devices).Include(account => account.Logins).AsNoTracking().AsSplitQuery().FirstOrDefault();
        return row is null ? null : ToAccount(row);
    }

    /// <summary>Whether the id, the name and tag, a device token hash or a social login of the row already belong to another account.</summary>
    private static bool Conflicts(SnakeDbContext context, AccountRow row, Guid? ownId)
    {
        if (ownId is null && context.Accounts.Any(other => other.Id == row.Id)) return true;
        if (context.Accounts.Any(other => other.Id != row.Id && other.UsernameKey == row.UsernameKey && other.Tag == row.Tag)) return true;
        var hashes = row.Devices.Select(device => device.Hash).ToList();
        if (context.Devices.Any(device => device.AccountId != row.Id && hashes.Contains(device.Hash))) return true;
        return row.Logins.Any(login => context.ExternalLogins.Any(other => other.AccountId != row.Id && other.Provider == login.Provider && other.IdHash == login.IdHash));
    }

    private static void Replace(SnakeDbContext context, AccountRow current, AccountRow replacement)
    {
        current.Username = replacement.Username;
        current.UsernameKey = replacement.UsernameKey;
        current.Tag = replacement.Tag;
        current.PhraseHash = replacement.PhraseHash;
        current.DisplayName = replacement.DisplayName;
        current.CreatedAt = replacement.CreatedAt;
        SyncDevices(context, current, replacement);
        SyncLogins(context, current, replacement);
    }

    private static void SyncDevices(SnakeDbContext context, AccountRow current, AccountRow replacement)
    {
        var kept = replacement.Devices.ToDictionary(device => device.Hash, StringComparer.Ordinal);
        foreach (var gone in current.Devices.Where(device => !kept.ContainsKey(device.Hash)).ToList())
            context.Devices.Remove(gone);
        var existing = current.Devices.ToDictionary(device => device.Hash, StringComparer.Ordinal);
        foreach (var device in replacement.Devices)
        {
            if (existing.TryGetValue(device.Hash, out var same))
            {
                same.Label = device.Label;
                same.CreatedAt = device.CreatedAt;
            }
            else current.Devices.Add(device);
        }
    }

    private static void SyncLogins(SnakeDbContext context, AccountRow current, AccountRow replacement)
    {
        var kept = replacement.Logins.Select(login => (login.Provider, login.IdHash)).ToHashSet();
        foreach (var gone in current.Logins.Where(login => !kept.Contains((login.Provider, login.IdHash))).ToList())
            context.ExternalLogins.Remove(gone);
        var existing = current.Logins.Select(login => (login.Provider, login.IdHash)).ToHashSet();
        foreach (var login in replacement.Logins.Where(login => !existing.Contains((login.Provider, login.IdHash))))
            current.Logins.Add(login);
    }

    private static string NameKey(string username) => username.Trim().ToLowerInvariant();

    private static AccountRow ToRow(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(account.Devices, nameof(account.Devices));
        if (!Tags.IsValid(account.Tag)) throw new ArgumentOutOfRangeException(nameof(account.Tag), account.Tag, "A tag is 0 to 99999.");
        if (account.Devices.Any(device => device is null || string.IsNullOrEmpty(device.Hash)))
            throw new ArgumentException("Every device needs a token hash.", nameof(account.Devices));
        return new AccountRow
        {
            Id = account.Id,
            Username = account.Username,
            UsernameKey = NameKey(account.Username),
            Tag = account.Tag,
            PhraseHash = account.PhraseHash,
            DisplayName = account.DisplayName,
            CreatedAt = account.CreatedAt,
            Devices = account.Devices.Select(device => new DeviceRow { Hash = device.Hash, AccountId = account.Id, Label = device.Label, CreatedAt = device.CreatedAt }).ToList(),
            Logins = account.Logins
                .Select(login => new ExternalLoginRow
                {
                    AccountId = account.Id,
                    Provider = login.Provider.Trim().ToLowerInvariant(),
                    IdHash = login.IdHash.Trim().ToLowerInvariant(),
                    LinkedAt = login.LinkedAt,
                })
                .ToList(),
        };
    }

    private static Account ToAccount(AccountRow row) => new(
        row.Id,
        row.Username,
        row.Tag,
        row.PhraseHash,
        row.CreatedAt,
        row.Devices.OrderBy(device => device.CreatedAt).ThenBy(device => device.Hash, StringComparer.Ordinal)
            .Select(device => new DeviceToken(device.Hash, device.CreatedAt, device.Label)).ToList().AsReadOnly(),
        row.DisplayName,
        row.Logins.OrderBy(login => login.LinkedAt).ThenBy(login => login.Provider, StringComparer.Ordinal)
            .Select(login => new ExternalLogin(login.Provider, login.IdHash, login.LinkedAt)).ToList().AsReadOnly());
}
