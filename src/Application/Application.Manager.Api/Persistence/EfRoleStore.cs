using Application.Manager.Api.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>The roles of accounts in the database (<c>roles</c> and <c>account_roles</c>). The role is looked at on every request, so a change is felt at once.</summary>
public sealed class EfRoleStore(IDbContextFactory<SnakeDbContext> factory) : IRoleStore
{
    public IReadOnlyList<string> RolesOf(Guid accountId)
    {
        using var context = factory.CreateDbContext();
        return context.AccountRoles.AsNoTracking().Where(row => row.AccountId == accountId).Select(row => row.Role).OrderBy(role => role).ToList();
    }

    public bool Grant(Guid accountId, string role)
    {
        var name = Roles.Normalize(role);
        if (name is null) return false;
        using var context = factory.CreateDbContext();
        if (!context.Roles.Any(row => row.Name == name) || !context.Accounts.Any(row => row.Id == accountId)) return false;
        if (context.AccountRoles.Any(row => row.AccountId == accountId && row.Role == name)) return true;
        context.AccountRoles.Add(new AccountRoleRow { AccountId = accountId, Role = name });
        try
        {
            context.SaveChanges();
            return true;
        }
        catch (DbUpdateException)
        {
            // Granted at the same moment from somewhere else: it is held now, which is what was asked.
            context.ChangeTracker.Clear();
            return context.AccountRoles.Any(row => row.AccountId == accountId && row.Role == name);
        }
    }

    public bool Revoke(Guid accountId, string role)
    {
        var name = Roles.Normalize(role);
        if (name is null) return false;
        using var context = factory.CreateDbContext();
        return context.AccountRoles.Where(row => row.AccountId == accountId && row.Role == name).ExecuteDelete() > 0;
    }

    public IReadOnlyList<Guid> AccountsWith(string role)
    {
        var name = Roles.Normalize(role);
        if (name is null) return [];
        using var context = factory.CreateDbContext();
        return context.AccountRoles.AsNoTracking().Where(row => row.Role == name).Select(row => row.AccountId).ToList();
    }
}
