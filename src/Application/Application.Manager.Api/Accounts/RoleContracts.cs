using System.Collections.Concurrent;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// The roles a player can have (docs/ROLES.md). Every account is a <see cref="User"/>; an <see cref="Admin"/> can also manage the game
/// (the admin page): perks, world events, rooms and who else is an admin. A role is attached to an account, so it follows the player to
/// whatever device they sign in on.
/// </summary>
public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";

    /// <summary>Every role there is, in the order they are listed.</summary>
    public static IReadOnlyList<string> All { get; } = [User, Admin];

    /// <summary>The role as it is kept: trimmed, lower case. Null when blank.</summary>
    public static string? Normalize(string? role) => string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();

    /// <summary>What an account can do: <see cref="User"/> always, and what was granted to it. Listed in the order of <see cref="All"/>.</summary>
    public static IReadOnlyList<string> Effective(IEnumerable<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        var held = granted.ToHashSet(StringComparer.Ordinal);
        held.Add(User);
        return All.Where(held.Contains).ToList();
    }

    public static bool IsAdmin(IEnumerable<string> granted) => granted.Contains(Admin, StringComparer.Ordinal);
}

/// <summary>Where the roles granted to accounts are kept. The database (<c>EfRoleStore</c>), or memory when there is no database.</summary>
public interface IRoleStore
{
    /// <summary>The roles granted to the account (<see cref="Roles.User"/> is not among them unless it was granted: it is implied).</summary>
    IReadOnlyList<string> RolesOf(Guid accountId);

    /// <summary>Grants the role. False for a role that does not exist or an account that does not. Granting what is held changes nothing.</summary>
    bool Grant(Guid accountId, string role);

    /// <summary>Takes the role away. False when the account did not have it.</summary>
    bool Revoke(Guid accountId, string role);

    /// <summary>The accounts the role was granted to.</summary>
    IReadOnlyList<Guid> AccountsWith(string role);
}

/// <summary>Roles in memory, gone at restart: for a run without a database. With one, roles are in the table.</summary>
public sealed class MemoryRoleStore : IRoleStore
{
    private readonly ConcurrentDictionary<(Guid Account, string Role), bool> _granted = new();

    public IReadOnlyList<string> RolesOf(Guid accountId)
        => _granted.Keys.Where(key => key.Account == accountId).Select(key => key.Role).Order(StringComparer.Ordinal).ToList();

    public bool Grant(Guid accountId, string role)
    {
        var name = Roles.Normalize(role);
        if (name is null || !Roles.All.Contains(name)) return false;
        _granted[(accountId, name)] = true;
        return true;
    }

    public bool Revoke(Guid accountId, string role)
    {
        var name = Roles.Normalize(role);
        return name is not null && _granted.TryRemove((accountId, name), out _);
    }

    public IReadOnlyList<Guid> AccountsWith(string role)
    {
        var name = Roles.Normalize(role);
        return name is null ? [] : _granted.Keys.Where(key => key.Role == name).Select(key => key.Account).ToList();
    }
}
