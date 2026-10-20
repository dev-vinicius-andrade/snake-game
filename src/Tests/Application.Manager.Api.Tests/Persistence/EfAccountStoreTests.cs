using Microsoft.EntityFrameworkCore;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Persistence;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class EfAccountStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 10, 19, 12, 34, 56, TimeSpan.FromMinutes(330)).AddTicks(1_234_560);

    private readonly PersistenceTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static DeviceToken Device(string hash, string? label = null) => new(hash, Created.AddMinutes(1), label);

    private static Account NewAccount(string username = "Vini", int tag = 9291, params DeviceToken[] devices) =>
        new(Guid.NewGuid(), username, tag, "pbkdf2-sha256$600000$c2FsdA+/==$aGFzaA+/==", Created, devices);

    private static void AssertSameAccount(Account expected, Account? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.Tag, actual.Tag);
        Assert.Equal(expected.PhraseHash, actual.PhraseHash);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.CreatedAt.UtcDateTime, actual.CreatedAt.UtcDateTime);
        Assert.Equal(expected.Devices.Select(d => (d.Hash, d.Label, d.CreatedAt.UtcDateTime)), actual.Devices.Select(d => (d.Hash, d.Label, d.CreatedAt.UtcDateTime)));
        Assert.Equal(expected.Logins.Select(l => (l.Provider, l.IdHash)), actual.Logins.Select(l => (l.Provider, l.IdHash)));
    }

    [Fact]
    public void TryAdd_ANewAccount_IsFoundByIdNameAndDeviceToken_AndComesBackExactlyInANewStore()
    {
        var account = NewAccount("Vini", 9291, Device("t1", "laptop")) with { DisplayName = "Rattler" };
        var store = _host.Accounts();

        Assert.True(store.TryAdd(account));

        AssertSameAccount(account, store.Find(account.Id));
        AssertSameAccount(account, store.Find("Vini", 9291));
        AssertSameAccount(account, store.FindByDeviceTokenHash("t1"));
        AssertSameAccount(account, _host.Accounts().Find(account.Id));
    }

    [Fact]
    public void Find_SomethingThatIsNotThere_ReturnsNull()
    {
        var store = _host.Accounts();

        Assert.Null(store.Find(Guid.NewGuid()));
        Assert.Null(store.Find("Nobody", 1));
        Assert.Null(store.FindByDeviceTokenHash("nope"));
        Assert.Null(store.FindByExternalLogin("github", "1"));
    }

    [Fact]
    public void Find_ByUsername_IgnoresCaseAndSurroundingSpaces()
    {
        var account = NewAccount("Vini", 9291, Device("t1"));
        var store = _host.Accounts();
        store.TryAdd(account);

        Assert.Equal(account.Id, store.Find("  vINI ", 9291)!.Id);
        Assert.Null(store.Find("Vini", 9292));
    }

    [Fact]
    public void TryAdd_TheSameUsernameInAnotherCaseAndTheSameTag_IsRejectedAndNothingChanges()
    {
        var store = _host.Accounts();
        var first = NewAccount("Vini", 9291, Device("t1"));
        store.TryAdd(first);

        var added = store.TryAdd(NewAccount("vini", 9291, Device("t2")));

        Assert.False(added);
        Assert.Null(store.FindByDeviceTokenHash("t2"));
        AssertSameAccount(first, store.Find("Vini", 9291));
    }

    [Fact]
    public void TryAdd_AnIdThatIsAlreadyUsed_IsRejected()
    {
        var store = _host.Accounts();
        var first = NewAccount("Vini", 1, Device("t1"));
        store.TryAdd(first);

        Assert.False(store.TryAdd(first with { Username = "Other", Devices = [Device("t2")] }));
    }

    [Fact]
    public void TryAdd_ADeviceTokenOfAnotherAccount_IsRejected_AndNoHalfAccountIsLeft()
    {
        var store = _host.Accounts();
        store.TryAdd(NewAccount("Vini", 1, Device("t1")));
        var other = NewAccount("Other", 2, Device("fresh"), Device("t1"));

        Assert.False(store.TryAdd(other));

        Assert.Null(store.Find(other.Id));
        Assert.Null(store.FindByDeviceTokenHash("fresh"));
    }

    [Fact]
    public void TryAdd_TheSameUsernameWithAnotherTag_IsAccepted_AndTagsOfListsThemInAnyCase()
    {
        var store = _host.Accounts();

        Assert.True(store.TryAdd(NewAccount("Vini", 1, Device("t1"))));
        Assert.True(store.TryAdd(NewAccount("vini", 2, Device("t2"))));

        Assert.Equal(new[] { 1, 2 }, store.TagsOf("VINI").Order());
        Assert.Empty(store.TagsOf("Nobody"));
    }

    [Fact]
    public void Update_AddingAndRevokingDevices_ChangesWhichTokensAreKnown()
    {
        var store = _host.Accounts();
        var account = NewAccount("Vini", 1, Device("t1"), Device("t2"));
        store.TryAdd(account);

        Assert.True(store.Update(account with { Devices = [Device("t2", "phone"), Device("t3")] }));

        Assert.Null(store.FindByDeviceTokenHash("t1"));
        Assert.Equal(account.Id, store.FindByDeviceTokenHash("t3")!.Id);
        Assert.Equal("phone", store.Find(account.Id)!.Devices.Single(device => device.Hash == "t2").Label);
    }

    [Fact]
    public void Update_AnAccountThatIsNotThere_ReturnsFalseAndAddsNothing()
    {
        var store = _host.Accounts();
        var ghost = NewAccount("Ghost", 1, Device("g"));

        Assert.False(store.Update(ghost));

        Assert.Null(store.Find(ghost.Id));
        Assert.Null(store.FindByDeviceTokenHash("g"));
    }

    [Fact]
    public void Update_ToANameAndTagTakenByAnotherAccount_IsRefusedAndNothingChanges()
    {
        var store = _host.Accounts();
        var mine = NewAccount("Ana", 1, Device("a1"));
        store.TryAdd(mine);
        store.TryAdd(NewAccount("Bob", 2, Device("b1")));

        Assert.False(store.Update(mine with { Username = "bob", Tag = 2, Devices = [Device("a1"), Device("a2")] }));

        AssertSameAccount(mine, store.Find(mine.Id));
        Assert.Null(store.FindByDeviceTokenHash("a2"));
    }

    [Fact]
    public void Update_WithADeviceTokenOfAnotherAccount_IsRefused()
    {
        var store = _host.Accounts();
        var mine = NewAccount("Ana", 1, Device("a1"));
        store.TryAdd(mine);
        store.TryAdd(NewAccount("Bob", 2, Device("b1")));

        Assert.False(store.Update(mine with { Devices = [Device("a1"), Device("b1")] }));

        AssertSameAccount(mine, store.Find(mine.Id));
    }

    [Fact]
    public void Update_ChangingOnlyTheCaseOfItsOwnName_OrToAFreeNameAndTag_IsAccepted()
    {
        var store = _host.Accounts();
        var account = NewAccount("Vini", 1, Device("t1"));
        store.TryAdd(account);

        Assert.True(store.Update(account with { Username = "VINI" }));
        Assert.True(store.Update(account with { Username = "Viniz", Tag = 7 }));

        Assert.Null(store.Find("Vini", 1));
        Assert.Equal(account.Id, store.Find("Viniz", 7)!.Id);
    }

    [Fact]
    public void ADisplayName_IsKept_AndClearedWhenSetToNull()
    {
        var store = _host.Accounts();
        var account = NewAccount("Vini", 1, Device("t1")) with { DisplayName = "Rattler" };
        store.TryAdd(account);

        Assert.Equal("Rattler", store.Find(account.Id)!.DisplayName);
        store.Update(account with { DisplayName = null });
        Assert.Null(store.Find(account.Id)!.DisplayName);
    }

    [Fact]
    public void AnExternalLogin_CanBeFound_BelongsToOneAccountOnly_AndCanBeLinkedLater()
    {
        var store = _host.Accounts();
        var login = new ExternalLogin("github", "583231", Created);
        var first = new Account(Guid.NewGuid(), "octocat", 1, Account.NoPhrase, Created, [Device("t1")], null, [login]);
        var second = new Account(Guid.NewGuid(), "other", 2, Account.NoPhrase, Created, [Device("t2")], null, [login]);

        Assert.True(store.TryAdd(first));
        Assert.False(store.TryAdd(second));

        Assert.Equal(first.Id, store.FindByExternalLogin("github", "583231")!.Id);
        Assert.Equal(first.Id, store.FindByExternalLogin("GitHub", "583231")!.Id);
        Assert.Null(store.FindByExternalLogin("discord", "583231"));

        var phrase = NewAccount("Vini", 3, Device("t3"));
        store.TryAdd(phrase);
        Assert.True(store.Update(phrase with { ExternalLogins = [new ExternalLogin("discord", "9", Created)] }));
        Assert.Equal(phrase.Id, store.FindByExternalLogin("discord", "9")!.Id);
        Assert.False(store.Update(phrase with { ExternalLogins = [login] }));
    }

    [Fact]
    public void TryAdd_ChangingTheCallersDeviceListAfterwards_DoesNotChangeTheStore()
    {
        var store = _host.Accounts();
        var devices = new List<DeviceToken> { Device("t1") };
        var account = NewAccount("Vini", 1) with { Devices = devices };
        store.TryAdd(account);

        devices.Add(Device("t2"));

        Assert.Single(store.Find(account.Id)!.Devices);
    }

    [Fact]
    public void ADatabaseThatCannotBeReached_ThrowsInsteadOfPretendingTheAccountIsMissing()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SnakeDbContext>().UseSqlite("Data Source=Z:/no/such/folder/snake.db;Mode=ReadOnly").Options;
        var store = new EfAccountStore(new SingleOptionsFactory(options));

        Assert.ThrowsAny<Exception>(() => store.Find(Guid.NewGuid()));
    }

    private sealed class SingleOptionsFactory(Microsoft.EntityFrameworkCore.DbContextOptions<SnakeDbContext> options) : Microsoft.EntityFrameworkCore.IDbContextFactory<SnakeDbContext>
    {
        public SnakeDbContext CreateDbContext() => new(options);
    }
}
