using System.Collections.Concurrent;
using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class FileAccountStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 10, 19, 12, 34, 56, TimeSpan.FromMinutes(330)).AddTicks(1_234_567);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-accounts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string StorePath => Path.Combine(_folder, "accounts.json");

    private FileAccountStore FileStore() => new(StorePath);

    private static FileAccountStore MemoryStore() => new(null);

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
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.CreatedAt.Offset, actual.CreatedAt.Offset);
        Assert.Equal(expected.Devices, actual.Devices);
    }

    [Fact]
    public void TryAdd_ANewAccount_IsFoundByIdNameAndDeviceToken()
    {
        var store = MemoryStore();
        var account = NewAccount(devices: Device("token-hash-1"));

        var added = store.TryAdd(account);

        Assert.True(added);
        AssertSameAccount(account, store.Find(account.Id));
        AssertSameAccount(account, store.Find("Vini", 9291));
        AssertSameAccount(account, store.FindByDeviceTokenHash("token-hash-1"));
    }

    [Fact]
    public void Find_SomethingThatIsNotThere_ReturnsNull()
    {
        var store = MemoryStore();
        store.TryAdd(NewAccount(devices: Device("token-hash-1")));

        Assert.Null(store.Find(Guid.NewGuid()));
        Assert.Null(store.Find("Vini", 1));
        Assert.Null(store.Find("Other", 9291));
        Assert.Null(store.FindByDeviceTokenHash("TOKEN-HASH-1"));
    }

    [Fact]
    public void Find_ByUsername_IgnoresCaseAndSurroundingSpaces()
    {
        var store = MemoryStore();
        var account = NewAccount("Vinícius");
        store.TryAdd(account);

        AssertSameAccount(account, store.Find("  VINÍCIUS ", 9291));
        AssertSameAccount(account, store.Find("vinícius", 9291));
    }

    [Fact]
    public void TryAdd_TheSameUsernameInAnotherCaseAndTheSameTag_IsRejectedAndNothingChanges()
    {
        var store = MemoryStore();
        var first = NewAccount("Vini");
        store.TryAdd(first);
        var second = NewAccount("vINI", devices: Device("token-hash-2"));

        var added = store.TryAdd(second);

        Assert.False(added);
        Assert.Null(store.Find(second.Id));
        Assert.Null(store.FindByDeviceTokenHash("token-hash-2"));
        AssertSameAccount(first, store.Find("vini", 9291));
    }

    [Fact]
    public void TryAdd_AnIdThatIsAlreadyUsed_IsRejected()
    {
        var store = MemoryStore();
        var first = NewAccount("Vini");
        store.TryAdd(first);

        var added = store.TryAdd(NewAccount("Other", 1) with { Id = first.Id });

        Assert.False(added);
        Assert.Null(store.Find("Other", 1));
        AssertSameAccount(first, store.Find(first.Id));
    }

    [Fact]
    public void TryAdd_ADeviceTokenOfAnotherAccount_IsRejected()
    {
        var store = MemoryStore();
        var first = NewAccount("Vini", devices: Device("token-hash-1"));
        store.TryAdd(first);

        var added = store.TryAdd(NewAccount("Other", 1, Device("token-hash-1")));

        Assert.False(added);
        Assert.Null(store.Find("Other", 1));
        AssertSameAccount(first, store.FindByDeviceTokenHash("token-hash-1"));
    }

    [Fact]
    public void TryAdd_TheSameUsernameWithAnotherTag_IsAccepted()
    {
        var store = MemoryStore();
        var first = NewAccount("Vini", 1);
        var second = NewAccount("VINI", 2);

        Assert.True(store.TryAdd(first));
        Assert.True(store.TryAdd(second));

        AssertSameAccount(first, store.Find("vini", 1));
        AssertSameAccount(second, store.Find("vini", 2));
    }

    [Fact]
    public void TagsOf_ListsTheTagsOfThatUsernameInAnyCase()
    {
        var store = MemoryStore();
        store.TryAdd(NewAccount("Vini", 1));
        store.TryAdd(NewAccount("vini", 2));
        store.TryAdd(NewAccount("VINI ", 99_999));
        store.TryAdd(NewAccount("Other", 3));

        var tags = store.TagsOf(" ViNi");

        Assert.Equal(new[] { 1, 2, 99_999 }, tags.Order());
        Assert.Empty(store.TagsOf("Nobody"));
    }

    [Fact]
    public void TagsOf_ReturnsASnapshotThatLaterChangesDoNotTouch()
    {
        var store = MemoryStore();
        store.TryAdd(NewAccount("Vini", 1));
        var tags = store.TagsOf("Vini");

        store.TryAdd(NewAccount("Vini", 2));

        Assert.Equal(new[] { 1 }, tags);
    }

    [Fact]
    public void Update_AddingADevice_MakesItsTokenFindable()
    {
        var store = MemoryStore();
        var account = NewAccount(devices: Device("token-hash-1"));
        store.TryAdd(account);
        var updated = account with { Devices = [Device("token-hash-1"), Device("token-hash-2", "phone")] };

        var changed = store.Update(updated);

        Assert.True(changed);
        AssertSameAccount(updated, store.FindByDeviceTokenHash("token-hash-1"));
        AssertSameAccount(updated, store.FindByDeviceTokenHash("token-hash-2"));
        AssertSameAccount(updated, store.Find(account.Id));
    }

    [Fact]
    public void Update_RevokingADevice_MakesItsTokenUnknown()
    {
        var store = MemoryStore();
        var account = NewAccount(devices: [Device("token-hash-1"), Device("token-hash-2")]);
        store.TryAdd(account);
        var updated = account with { Devices = [Device("token-hash-2")] };

        store.Update(updated);

        Assert.Null(store.FindByDeviceTokenHash("token-hash-1"));
        AssertSameAccount(updated, store.FindByDeviceTokenHash("token-hash-2"));
    }

    [Fact]
    public void Update_AnAccountThatIsNotThere_ReturnsFalseAndAddsNothing()
    {
        var store = MemoryStore();
        var account = NewAccount();

        var changed = store.Update(account);

        Assert.False(changed);
        Assert.Null(store.Find(account.Id));
        Assert.Null(store.Find("Vini", 9291));
    }

    [Fact]
    public void Update_ToANameAndTagTakenByAnotherAccount_IsRefusedAndNothingChanges()
    {
        var store = MemoryStore();
        var taken = NewAccount("Vini", 1);
        var mine = NewAccount("Other", 2, Device("token-hash-1"));
        store.TryAdd(taken);
        store.TryAdd(mine);

        var changed = store.Update(mine with { Username = "VINI", Tag = 1, Devices = [Device("token-hash-2")] });

        Assert.False(changed);
        AssertSameAccount(taken, store.Find("vini", 1));
        AssertSameAccount(mine, store.Find(mine.Id));
        AssertSameAccount(mine, store.FindByDeviceTokenHash("token-hash-1"));
        Assert.Null(store.FindByDeviceTokenHash("token-hash-2"));
    }

    [Fact]
    public void Update_WithADeviceTokenOfAnotherAccount_IsRefused()
    {
        var store = MemoryStore();
        var other = NewAccount("Vini", 1, Device("token-hash-1"));
        var mine = NewAccount("Other", 2);
        store.TryAdd(other);
        store.TryAdd(mine);

        var changed = store.Update(mine with { Devices = [Device("token-hash-1")] });

        Assert.False(changed);
        AssertSameAccount(other, store.FindByDeviceTokenHash("token-hash-1"));
        AssertSameAccount(mine, store.Find(mine.Id));
    }

    [Fact]
    public void Update_ToAFreeNameAndTag_MovesTheAccountThere()
    {
        var store = MemoryStore();
        var account = NewAccount("Vini", 1);
        store.TryAdd(account);
        var renamed = account with { Username = "Snake", Tag = 7 };

        var changed = store.Update(renamed);

        Assert.True(changed);
        Assert.Null(store.Find("Vini", 1));
        Assert.Empty(store.TagsOf("Vini"));
        AssertSameAccount(renamed, store.Find("snake", 7));
        Assert.Equal(new[] { 7 }, store.TagsOf("SNAKE"));
        Assert.True(store.TryAdd(NewAccount("vini", 1)));
    }

    [Fact]
    public void Update_ChangingOnlyTheCaseOfItsOwnName_IsAccepted()
    {
        var store = MemoryStore();
        var account = NewAccount("vini", 1);
        store.TryAdd(account);
        var renamed = account with { Username = "Vini" };

        var changed = store.Update(renamed);

        Assert.True(changed);
        AssertSameAccount(renamed, store.Find("VINI", 1));
        Assert.Equal(new[] { 1 }, store.TagsOf("vini"));
    }

    [Fact]
    public void TryAdd_ChangingTheCallersDeviceListAfterwards_DoesNotChangeTheStore()
    {
        var store = MemoryStore();
        var devices = new List<DeviceToken> { Device("token-hash-1") };
        var account = NewAccount(devices: devices.ToArray()) with { Devices = devices };
        store.TryAdd(account);

        devices.Add(Device("token-hash-2"));

        Assert.Null(store.FindByDeviceTokenHash("token-hash-2"));
        Assert.Single(store.Find(account.Id)!.Devices);
    }

    [Fact]
    public void Constructor_WithNoPathOrABlankOne_KeepsAccountsInMemoryOnly()
    {
        var withNull = new FileAccountStore(null);
        var withBlank = new FileAccountStore("   ");
        var account = NewAccount();

        Assert.True(withNull.TryAdd(account));
        Assert.True(withBlank.TryAdd(account));

        AssertSameAccount(account, withNull.Find(account.Id));
        AssertSameAccount(account, withBlank.Find(account.Id));
        Assert.Null(new FileAccountStore(null).Find(account.Id));
    }

    [Fact]
    public void Constructor_WhenTheFileDoesNotExist_StartsEmpty()
    {
        var store = FileStore();

        Assert.Null(store.Find("Vini", 9291));
        Assert.Empty(store.TagsOf("Vini"));
        Assert.False(File.Exists(StorePath));
    }

    [Fact]
    public void TryAdd_IsKeptOnDiskAndComesBackExactlyInANewInstance()
    {
        var account = NewAccount("Vinícius", 42, Device("aGFzaA+/=", "laptop"), Device("ZGV2aWNl", null)) with
        {
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-3)).AddTicks(9_876_543)
        };
        FileStore().TryAdd(account);

        var reopened = FileStore();

        AssertSameAccount(account, reopened.Find(account.Id));
        AssertSameAccount(account, reopened.Find("vinícius", 42));
        AssertSameAccount(account, reopened.FindByDeviceTokenHash("ZGV2aWNl"));
        Assert.Equal(new[] { 42 }, reopened.TagsOf("VINÍCIUS"));
    }

    [Fact]
    public void Update_IsKeptOnDiskAndAnotherInstanceSeesItAfterAReload()
    {
        var first = FileStore();
        var second = FileStore();
        var account = NewAccount(devices: Device("token-hash-1"));
        first.TryAdd(account);
        var updated = account with { Username = "Snake", Tag = 7, Devices = [Device("token-hash-2", "phone")] };
        first.Update(updated);

        second.Reload();

        AssertSameAccount(updated, second.Find(account.Id));
        AssertSameAccount(updated, second.Find("snake", 7));
        AssertSameAccount(updated, second.FindByDeviceTokenHash("token-hash-2"));
        Assert.Null(second.FindByDeviceTokenHash("token-hash-1"));
        Assert.Null(second.Find("Vini", 9291));
    }

    [Fact]
    public void TryAdd_WritesIndentedCamelCaseJson()
    {
        var store = FileStore();

        store.TryAdd(NewAccount(devices: Device("token-hash-1")));

        var text = File.ReadAllText(StorePath);
        Assert.Contains("\"username\": \"Vini\"", text);
        Assert.Contains("\"phraseHash\"", text);
        Assert.Contains("\"devices\"", text);
        Assert.Contains('\n', text);
    }

    [Fact]
    public void Constructor_WithACorruptFile_KeepsItAsideAndStartsEmpty()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, "{ this is not json");

        var store = FileStore();

        Assert.Null(store.Find("Vini", 9291));
        Assert.False(File.Exists(StorePath));
        var quarantined = Assert.Single(Directory.GetFiles(_folder, "accounts.json.corrupt-*"));
        Assert.Equal("{ this is not json", File.ReadAllText(quarantined));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{ \"version\": 1, \"accounts\": [ null ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"id\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\", \"tag\": 1, \"phraseHash\": \"h\", \"devices\": [] } ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"id\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\", \"username\": \"Vini\", \"tag\": 1, \"phraseHash\": \"h\" } ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"id\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\", \"username\": \"Vini\", \"tag\": 100000, \"phraseHash\": \"h\", \"devices\": [] } ] }")]
    public void Constructor_WithAFileThatDoesNotHoldValidAccounts_KeepsItAsideAndStartsEmpty(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, content);

        var store = FileStore();

        Assert.Null(store.Find("Vini", 1));
        Assert.False(File.Exists(StorePath));
        var quarantined = Assert.Single(Directory.GetFiles(_folder, "accounts.json.corrupt-*"));
        Assert.Equal(content, File.ReadAllText(quarantined));
    }

    [Theory]
    [InlineData(" ", 1)]
    [InlineData("Vini", -1)]
    [InlineData("Vini", 100_000)]
    public void TryAdd_AnAccountThatCannotBeStored_ThrowsAndAddsNothing(string username, int tag)
    {
        var store = MemoryStore();
        var account = NewAccount(username, tag);

        Assert.ThrowsAny<ArgumentException>(() => store.TryAdd(account));

        Assert.Null(store.Find(account.Id));
    }

    [Fact]
    public void TryAdd_AfterACorruptFileWasKeptAside_WritesANewFileAndLeavesTheBadOneAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, "[1, 2,");
        var store = FileStore();
        var account = NewAccount();

        store.TryAdd(account);

        var quarantined = Assert.Single(Directory.GetFiles(_folder, "accounts.json.corrupt-*"));
        Assert.Equal("[1, 2,", File.ReadAllText(quarantined));
        AssertSameAccount(account, FileStore().Find(account.Id));
    }

    [Fact]
    public void Constructor_WithAFileThatHoldsTheSameNameAndTagTwice_KeepsItAsideAndStartsEmpty()
    {
        var store = FileStore();
        store.TryAdd(NewAccount("Vini", 1));
        store.TryAdd(NewAccount("Other", 2));
        var text = File.ReadAllText(StorePath);
        File.WriteAllText(StorePath, text.Replace("\"username\": \"Other\"", "\"username\": \"VINI\"").Replace("\"tag\": 2", "\"tag\": 1"));

        var reopened = FileStore();

        Assert.Null(reopened.Find("Vini", 1));
        Assert.Single(Directory.GetFiles(_folder, "accounts.json.corrupt-*"));
    }

    [Fact]
    public void TryAdd_WhenWritingIsInterrupted_ThrowsAndLeavesTheOldFileAndMemoryAsTheyWere()
    {
        var store = FileStore();
        var kept = NewAccount("Vini", 1);
        store.TryAdd(kept);
        var before = File.ReadAllBytes(StorePath);
        store.BeforeReplacingFile = _ => throw new IOException("the disk went away");
        var lost = NewAccount("Other", 2, Device("token-hash-2"));

        Assert.Throws<IOException>(() => store.TryAdd(lost));

        Assert.Equal(before, File.ReadAllBytes(StorePath));
        Assert.Null(store.Find(lost.Id));
        Assert.Null(store.Find("Other", 2));
        Assert.Null(store.FindByDeviceTokenHash("token-hash-2"));
        Assert.Empty(store.TagsOf("Other"));
        Assert.Single(Directory.GetFiles(_folder));
        AssertSameAccount(kept, FileStore().Find(kept.Id));
    }

    [Fact]
    public void Update_WhenWritingIsInterrupted_ThrowsAndKeepsTheOldAccount()
    {
        var store = FileStore();
        var account = NewAccount("Vini", 1, Device("token-hash-1"));
        store.TryAdd(account);
        store.BeforeReplacingFile = _ => throw new IOException("the disk went away");

        Assert.Throws<IOException>(() => store.Update(account with { Username = "Snake", Devices = [Device("token-hash-2")] }));

        AssertSameAccount(account, store.Find(account.Id));
        AssertSameAccount(account, store.Find("vini", 1));
        AssertSameAccount(account, store.FindByDeviceTokenHash("token-hash-1"));
        Assert.Null(store.FindByDeviceTokenHash("token-hash-2"));
        Assert.Null(store.Find("Snake", 1));
        AssertSameAccount(account, FileStore().Find(account.Id));
    }

    [Fact]
    public void Constructor_WithAHalfWrittenTempFileLeftByACrash_LoadsTheLastCompleteFile()
    {
        var account = NewAccount();
        FileStore().TryAdd(account);
        File.WriteAllText(StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp", "{ \"version\": 1, \"accounts\": [ { \"id\"");

        var reopened = FileStore();

        AssertSameAccount(account, reopened.Find(account.Id));
        Assert.Empty(Directory.GetFiles(_folder, "accounts.json.corrupt-*"));
    }

    [Fact]
    public void TryAdd_WhenTheFileCannotBeWritten_ThrowsAndKeepsNothingInMemory()
    {
        Directory.CreateDirectory(StorePath);
        var store = FileStore();
        var account = NewAccount();

        var failure = Record.Exception(() => store.TryAdd(account));

        Assert.True(failure is IOException or UnauthorizedAccessException, $"unexpected {failure?.GetType()}");
        Assert.Null(store.Find(account.Id));
        Assert.Empty(store.TagsOf("Vini"));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    private sealed record ConcurrentRun(IReadOnlyCollection<Account> Added, IReadOnlyCollection<Guid> SharedNameWinners);

    private static ConcurrentRun AddFromEightThreads(FileAccountStore store, int perThread)
    {
        const int threads = 8;
        var added = new ConcurrentBag<Account>();
        var winners = new ConcurrentBag<Guid>();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, thread =>
        {
            for (var index = 0; index < perThread; index++)
            {
                var number = thread * perThread + index;
                var account = NewAccount($"Player{number % 40}", number, Device($"token-{number}"));
                Assert.True(store.TryAdd(account));
                added.Add(account);
            }

            var contender = NewAccount("Shared", 1);
            if (store.TryAdd(contender)) winners.Add(contender.Id);
        });
        return new ConcurrentRun(added, winners);
    }

    private static void AssertAllIndexed(FileAccountStore store, ConcurrentRun run)
    {
        Assert.Single(run.SharedNameWinners);
        Assert.Equal(run.SharedNameWinners.Single(), store.Find("shared", 1)!.Id);
        Assert.Equal(run.Added.Count / 40, store.TagsOf("player0").Count);
        foreach (var account in run.Added)
        {
            AssertSameAccount(account, store.Find(account.Id));
            AssertSameAccount(account, store.Find(account.Username, account.Tag));
            AssertSameAccount(account, store.FindByDeviceTokenHash(account.Devices[0].Hash));
        }
    }

    [Fact]
    public void TryAdd_TwoThousandAccountsFromEightThreadsAtOnce_EndsWithEveryAccountIndexed()
    {
        var store = MemoryStore();

        var run = AddFromEightThreads(store, perThread: 250);

        Assert.Equal(2000, run.Added.Count);
        AssertAllIndexed(store, run);
    }

    [Fact]
    public void TryAdd_FromEightThreadsAtOnceWithAFile_EndsWithEveryAccountSaved()
    {
        var store = FileStore();

        var run = AddFromEightThreads(store, perThread: 25);

        AssertAllIndexed(store, run);
        AssertAllIndexed(FileStore(), run);
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void ADisplayName_SurvivesAReload_AndAnOlderFileWithoutOneLoadsAsNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            var first = new FileAccountStore(path);
            var account = new Account(Guid.NewGuid(), "Vini", 9291, "hash", DateTimeOffset.UnixEpoch, [new DeviceToken("t", DateTimeOffset.UnixEpoch)], "Rattler");
            Assert.True(first.TryAdd(account));

            Assert.Equal("Rattler", new FileAccountStore(path).Find(account.Id)!.DisplayName);

            var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            document["accounts"]![0]!.AsObject().Remove("displayName");
            File.WriteAllText(path, document.ToJsonString());
            Assert.Null(new FileAccountStore(path).Find(account.Id)!.DisplayName);
            Assert.Equal("Vini", new FileAccountStore(path).Find(account.Id)!.PublicName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnExternalLogin_CanBeFound_SurvivesAReload_AndBelongsToOneAccountOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            var login = new ExternalLogin("github", "583231", DateTimeOffset.UnixEpoch);
            var first = new Account(Guid.NewGuid(), "octocat", 1, Account.NoPhrase, DateTimeOffset.UnixEpoch, [new DeviceToken("t1", DateTimeOffset.UnixEpoch)], null, [login]);
            var second = new Account(Guid.NewGuid(), "other", 2, Account.NoPhrase, DateTimeOffset.UnixEpoch, [new DeviceToken("t2", DateTimeOffset.UnixEpoch)], null, [login]);
            var store = new FileAccountStore(path);

            Assert.True(store.TryAdd(first));
            Assert.False(store.TryAdd(second));

            Assert.Equal(first.Id, store.FindByExternalLogin("github", "583231")!.Id);
            Assert.Equal(first.Id, store.FindByExternalLogin("GitHub", "583231")!.Id);
            Assert.Null(store.FindByExternalLogin("discord", "583231"));
            Assert.Equal(first.Id, new FileAccountStore(path).FindByExternalLogin("github", "583231")!.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASocialAccountHasNoPhrase_ButIsStorable()
    {
        var store = new FileAccountStore(null);
        var account = new Account(Guid.NewGuid(), "octocat", 1, Account.NoPhrase, DateTimeOffset.UnixEpoch, [new DeviceToken("t1", DateTimeOffset.UnixEpoch)]);

        Assert.True(store.TryAdd(account));
    }
}
