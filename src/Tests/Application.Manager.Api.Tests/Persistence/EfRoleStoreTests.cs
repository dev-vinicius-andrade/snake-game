using Application.Manager.Api.Accounts;
using Application.Manager.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class EfRoleStoreTests : IDisposable
{
    private readonly PersistenceTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private Guid NewAccount(string username = "Vini", int tag = 1)
    {
        var account = new Account(Guid.NewGuid(), username, tag, Account.NoPhrase, DateTimeOffset.UnixEpoch, [new DeviceToken($"t-{Guid.NewGuid():N}", DateTimeOffset.UnixEpoch)]);
        Assert.True(_host.Accounts().TryAdd(account));
        return account.Id;
    }

    [Fact]
    public void TheRolesTable_HasUserAndAdmin()
    {
        using var context = _host.Factory.CreateDbContext();

        Assert.Equal(new[] { Roles.Admin, Roles.User }, context.Roles.Select(role => role.Name).OrderBy(name => name));
    }

    [Fact]
    public void RolesOf_AnAccountWithoutAnyGrant_HasNone_AndOnlyTheUserRoleIsImplied()
    {
        var store = _host.Roles();
        var id = NewAccount();

        Assert.Empty(store.RolesOf(id));
        Assert.Equal(new[] { Roles.User }, Roles.Effective(store.RolesOf(id)));
    }

    [Fact]
    public void Grant_GivesTheRole_ToThatAccountOnly_AndGrantingAgainChangesNothing()
    {
        var store = _host.Roles();
        var mine = NewAccount("Ana", 1);
        var yours = NewAccount("Bob", 2);

        Assert.True(store.Grant(mine, Roles.Admin));
        Assert.True(store.Grant(mine, Roles.Admin));

        Assert.Equal(new[] { Roles.Admin }, store.RolesOf(mine));
        Assert.Empty(store.RolesOf(yours));
        Assert.Equal(1, _host.Factory.CreateDbContext().AccountRoles.Count());
    }

    [Fact]
    public void Grant_ARoleThatDoesNotExist_OrAnAccountThatDoesNot_IsRefused()
    {
        var store = _host.Roles();

        Assert.False(store.Grant(NewAccount(), "superuser"));
        Assert.False(store.Grant(Guid.NewGuid(), Roles.Admin));
        Assert.False(store.Grant(NewAccount("Zed", 9), " "));
    }

    [Fact]
    public void Grant_TheRoleNameIsNotCaseSensitive()
    {
        var store = _host.Roles();
        var id = NewAccount();

        Assert.True(store.Grant(id, "ADMIN"));

        Assert.Equal(new[] { Roles.Admin }, store.RolesOf(id));
    }

    [Fact]
    public void Revoke_TakesTheRoleAway_AndSaysWhetherItWasThere()
    {
        var store = _host.Roles();
        var id = NewAccount();
        store.Grant(id, Roles.Admin);

        Assert.True(store.Revoke(id, Roles.Admin));
        Assert.False(store.Revoke(id, Roles.Admin));

        Assert.Empty(store.RolesOf(id));
    }

    [Fact]
    public void AccountsWith_ListsWhoHasTheRole()
    {
        var store = _host.Roles();
        var admin = NewAccount("Ana", 1);
        NewAccount("Bob", 2);
        store.Grant(admin, Roles.Admin);

        Assert.Equal(new[] { admin }, store.AccountsWith(Roles.Admin));
        Assert.Empty(store.AccountsWith("nothing"));
    }

    [Fact]
    public void TheGrantsAreKeptAndAnotherStoreSeesThem()
    {
        var id = NewAccount();
        _host.Roles().Grant(id, Roles.Admin);

        Assert.Equal(new[] { Roles.Admin }, _host.Roles().RolesOf(id));
    }
}

public sealed class RolesTests
{
    [Fact]
    public void Effective_AlwaysIncludesUser_OnceAndInOrder()
    {
        Assert.Equal(new[] { Roles.User, Roles.Admin }, Roles.Effective([Roles.Admin]));
        Assert.Equal(new[] { Roles.User }, Roles.Effective([Roles.User, Roles.User]));
        Assert.Equal(new[] { Roles.User }, Roles.Effective([]));
    }

    [Fact]
    public void IsAdmin_NeedsTheAdminRole()
    {
        Assert.True(Roles.IsAdmin([Roles.Admin]));
        Assert.False(Roles.IsAdmin([]));
        Assert.False(Roles.IsAdmin([Roles.User]));
    }

    [Fact]
    public void TheMemoryStore_WorksLikeTheDatabaseOne_ForARunWithoutADatabase()
    {
        var store = new MemoryRoleStore();
        var id = Guid.NewGuid();

        Assert.True(store.Grant(id, Roles.Admin));
        Assert.False(store.Grant(id, "superuser"));
        Assert.Equal(new[] { Roles.Admin }, store.RolesOf(id));
        Assert.Equal(new[] { id }, store.AccountsWith(Roles.Admin));
        Assert.True(store.Revoke(id, Roles.Admin));
        Assert.Empty(store.RolesOf(id));
    }
}
