using System.Diagnostics;
using Application.Manager.Api.Accounts;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Application.Manager.Api.Tests;

public sealed class AccountServiceTests
{
    private const string Ip = "203.0.113.7";
    private const string StrongPhrase = "Purple Llama sings at dawn";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _store = new(null);

    private AccountService Service(IAccountStore? store = null, SecretHasher? hasher = null, Func<int, int>? nextTag = null) =>
        new(store ?? _store, AccountThrottles.Create(_time), SimpleWordFilter.Default, new RecoveryPhrase(), hasher ?? new SecretHasher(1), _time,
            nextTag: nextTag);

    private static Registration Registered(AccountService service, string username = "Vini", int? tag = null, string? phrase = null, string ip = Ip)
    {
        var result = service.Register(username, tag, phrase, ip);
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    private static IAccountStore StoreWithEveryTagTaken()
    {
        var store = Substitute.For<IAccountStore>();
        store.TagsOf(Arg.Any<string>()).Returns(new HashSet<int>(Enumerable.Range(Tags.Min, Tags.Count)));
        return store;
    }

    private static void FailLogins(AccountService service, string username, int tag, int times, Func<int, string> ipOf)
    {
        for (var attempt = 0; attempt < times; attempt++)
            Assert.Equal(AccountErrors.InvalidCredentials, service.Login(username, tag, "wrong wrong wrong", ipOf(attempt)).Error);
    }

    private static TimeSpan Fastest(Action<int> attempt) =>
        Enumerable.Range(0, 3).Select(index => Measure(() => attempt(index))).Min();

    private static TimeSpan Measure(Action action)
    {
        var watch = Stopwatch.StartNew();
        action();
        return watch.Elapsed;
    }

    [Fact]
    public void Register_WithoutTagOrPhrase_GivesARandomPhraseAFreeTagAndAWorkingToken()
    {
        var service = Service();

        var registered = Registered(service);

        Assert.Equal("Vini", registered.Account.Username);
        Assert.True(Tags.IsValid(registered.Account.Tag));
        Assert.Equal(RecoveryPhrase.WordCount, registered.RecoveryPhrase.Split('-').Length);
        Assert.Equal(registered.Account.Id, service.Authenticate(registered.Token)?.Id);
    }

    [Fact]
    public void Register_CleansTheUsername()
    {
        var registered = Registered(Service(), "  Vini    Boy ");

        Assert.Equal("Vini Boy", registered.Account.Username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("v")]
    [InlineData("Vini#09291")]
    [InlineData("TheAdmin")]
    public void Register_AnInvalidUsername_IsInvalidUsername(string? username)
    {
        var result = Service().Register(username, null, null, Ip);

        Assert.Equal(AccountErrors.InvalidUsername, result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    public void Register_ATagOutsideTheRange_IsInvalidTag(int tag)
    {
        var result = Service().Register("Vini", tag, null, Ip);

        Assert.Equal(AccountErrors.InvalidTag, result.Error);
    }

    [Theory]
    [InlineData("too short")]
    [InlineData("Password 1234")]
    [InlineData("aaaaaaaaaaaaaaa")]
    [InlineData("Vini Vini Vini")]
    public void Register_AWeakChosenPhrase_IsWeakPhrase(string phrase)
    {
        var result = Service().Register("ViniViniVini", null, phrase, Ip);

        Assert.Equal(AccountErrors.WeakPhrase, result.Error);
    }

    [Fact]
    public void Register_AStrongChosenPhrase_IsEchoedBackAndSignsIn()
    {
        var service = Service();

        var registered = Registered(service, phrase: StrongPhrase);

        Assert.Equal(StrongPhrase, registered.RecoveryPhrase);
        Assert.True(service.Login("vini", registered.Account.Tag, "purple-llama  SINGS at-dawn", "198.51.100.1").Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_ABlankPhrase_GetsARandomOne(string phrase)
    {
        var registered = Registered(Service(), phrase: phrase);

        Assert.Equal(RecoveryPhrase.WordCount, registered.RecoveryPhrase.Split('-').Length);
    }

    [Fact]
    public void Register_KeepsOnlyHashesOfThePhraseAndTheToken()
    {
        var hasher = new SecretHasher(1);
        var registered = Registered(Service(hasher: hasher));

        var stored = _store.Find(registered.Account.Id)!;

        Assert.DoesNotContain(registered.RecoveryPhrase, stored.PhraseHash);
        Assert.True(hasher.Verify(RecoveryPhrase.Normalize(registered.RecoveryPhrase), stored.PhraseHash));
        Assert.Equal(DeviceTokens.Hash(registered.Token), Assert.Single(stored.Devices).Hash);
        Assert.Equal(_time.GetUtcNow(), stored.CreatedAt);
    }

    [Fact]
    public void Register_TheSameUsernameInAnyCase_GetsDifferentTags()
    {
        var service = Service();

        var first = Registered(service, "Vini");
        var second = Registered(service, "vini");
        var third = Registered(service, "VINI");

        Assert.Equal(3, new[] { first.Account.Tag, second.Account.Tag, third.Account.Tag }.Distinct().Count());
    }

    [Fact]
    public void Register_AFreeWantedTag_IsGiven()
    {
        var registered = Registered(Service(), tag: 9291);

        Assert.Equal(9291, registered.Account.Tag);
        Assert.Equal("Vini#09291", registered.Account.Display);
    }

    [Fact]
    public void Register_AWantedTagThatIsTaken_GetsARandomFreeOne()
    {
        var service = Service(nextTag: _ => 42);
        Registered(service, "Vini", 9291);

        var second = Registered(service, "vini", 9291);

        Assert.Equal(42, second.Account.Tag);
    }

    [Fact]
    public void Register_WhenEveryTagIsTaken_IsNoTagsLeft()
    {
        var result = Service(StoreWithEveryTagTaken()).Register("Vini", null, null, Ip);

        Assert.Equal(AccountErrors.NoTagsLeft, result.Error);
    }

    [Fact]
    public void Register_LosingARaceForTheTag_TriesAnotherTag()
    {
        var store = new RacingStore();
        var service = Service(store, nextTag: _ => 42);

        var registered = Registered(service, "Vini", 9291);

        Assert.Equal(42, registered.Account.Tag);
        Assert.Equal(new[] { 42, 9291 }, store.TagsOf("vini").Order());
    }

    [Fact]
    public void Register_LosingEveryRace_IsNoTagsLeftAndAddsNothing()
    {
        var store = Substitute.For<IAccountStore>();
        store.TagsOf(Arg.Any<string>()).Returns(new HashSet<int>());
        store.TryAdd(Arg.Any<Account>()).Returns(false);

        var result = Service(store).Register("Vini", null, null, Ip);

        Assert.Equal(AccountErrors.NoTagsLeft, result.Error);
        store.Received(AccountService.AddAttempts).TryAdd(Arg.Any<Account>());
    }

    [Fact]
    public void Register_TooManyFromOneIp_IsThrottled()
    {
        var service = Service();
        for (var registration = 0; registration <= AccountThrottles.RegistrationOptions.FreeAttempts; registration++)
            Registered(service, "Player");

        var throttled = service.Register("Player", null, null, Ip);
        var otherIp = service.Register("Player", null, null, "198.51.100.1");

        Assert.Equal(AccountErrors.Throttled, throttled.Error);
        Assert.Equal((int)AccountThrottles.RegistrationOptions.BaseDelay.TotalSeconds, throttled.RetryAfterSeconds);
        Assert.True(otherIp.Succeeded);
    }

    [Fact]
    public void Register_InvalidInput_DoesNotCountTowardsTheThrottle()
    {
        var service = Service();
        for (var registration = 0; registration < 50; registration++)
            service.Register("x", null, null, Ip);

        Assert.True(service.Register("Player", null, null, Ip).Succeeded);
    }

    [Fact]
    public void Login_TheRightPhrase_SignsInAnotherDeviceAndKeepsTheFirst()
    {
        var service = Service();
        var registered = Registered(service);

        var signIn = service.Login("VINI", registered.Account.Tag, registered.RecoveryPhrase, "198.51.100.1");

        Assert.True(signIn.Succeeded);
        Assert.NotEqual(registered.Token, signIn.Value.Token);
        Assert.Equal(registered.Account.Id, service.Authenticate(signIn.Value.Token)?.Id);
        Assert.Equal(registered.Account.Id, service.Authenticate(registered.Token)?.Id);
        Assert.Equal(2, _store.Find(registered.Account.Id)!.Devices.Count);
    }

    [Fact]
    public void Login_AWrongPhrase_IsInvalidCredentials()
    {
        var service = Service();
        var registered = Registered(service);

        var result = service.Login("Vini", registered.Account.Tag, "amber-otter-nine-cable", Ip);

        Assert.Equal(AccountErrors.InvalidCredentials, result.Error);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Theory]
    [InlineData("Nobody", 1)]
    [InlineData(null, 1)]
    [InlineData("Vini", null)]
    [InlineData("Vini", -5)]
    public void Login_AnUnknownAccount_GetsTheSameAnswerAsAWrongPhrase(string? username, int? tag)
    {
        var service = Service();
        var registered = Registered(service);
        var wrongPhrase = service.Login("Vini", registered.Account.Tag, "amber-otter-nine-cable", Ip);

        var unknown = service.Login(username, tag, registered.RecoveryPhrase, Ip);

        Assert.Equal(wrongPhrase, unknown);
    }

    [Fact]
    public void Login_AnUnknownAccount_TakesAboutAsLongAsAWrongPhrase()
    {
        var service = Service(hasher: new SecretHasher(50_000));
        var registered = Registered(service);

        var wrongPhrase = Fastest(attempt => service.Login("Vini", registered.Account.Tag, "amber-otter-nine-cable", $"10.0.0.{attempt}"));
        var unknown = Fastest(attempt => service.Login("Nobody", 1, "amber-otter-nine-cable", $"10.0.1.{attempt}"));

        Assert.True(unknown >= wrongPhrase / 3, $"unknown account {unknown.TotalMilliseconds} ms, wrong phrase {wrongPhrase.TotalMilliseconds} ms");
    }

    [Fact]
    public void Login_AfterTheFreeFailures_IsThrottledEvenWithTheRightPhrase()
    {
        var service = Service();
        var registered = Registered(service);
        FailLogins(service, "Vini", registered.Account.Tag, AccountThrottles.SignInOptions.FreeAttempts + 1, attempt => $"10.0.0.{attempt}");

        var throttled = service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, "198.51.100.1");
        _time.Advance(AccountThrottles.SignInOptions.BaseDelay);
        var later = service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, "198.51.100.1");

        Assert.Equal(AccountErrors.Throttled, throttled.Error);
        Assert.Equal(2, throttled.RetryAfterSeconds);
        Assert.True(later.Succeeded);
    }

    [Fact]
    public void Login_FailuresFromOneIp_ThrottleThatIpForEveryAccount()
    {
        var service = Service();
        var other = Registered(service, "Other");
        FailLogins(service, "Nobody", 1, AccountThrottles.SignInOptions.FreeAttempts + 1, _ => Ip);

        var sameIp = service.Login("Other", other.Account.Tag, other.RecoveryPhrase, Ip);
        var otherIp = service.Login("Other", other.Account.Tag, other.RecoveryPhrase, "198.51.100.1");

        Assert.Equal(AccountErrors.Throttled, sameIp.Error);
        Assert.True(otherIp.Succeeded);
    }

    [Fact]
    public void Login_FailuresOnAnUnknownAccount_ThrottleThatAccountFromEveryIp()
    {
        var service = Service();
        FailLogins(service, "Nobody", 1, AccountThrottles.SignInOptions.FreeAttempts + 1, attempt => $"10.0.0.{attempt}");

        var result = service.Login("NOBODY", 1, "wrong wrong wrong", "198.51.100.1");

        Assert.Equal(AccountErrors.Throttled, result.Error);
    }

    [Fact]
    public void Login_WhenBothKeysWait_RetryAfterIsTheLongerWaitInWholeSecondsRoundedUp()
    {
        var service = Service();
        var registered = Registered(service);
        var free = AccountThrottles.SignInOptions.FreeAttempts;
        for (var account = 0; account <= free; account++)
            service.Login($"Nobody{account}", 1, "wrong wrong wrong", Ip);
        _time.Advance(AccountThrottles.SignInOptions.BaseDelay);
        service.Login("Someone", 1, "wrong wrong wrong", Ip);
        FailLogins(service, "Vini", registered.Account.Tag, free + 1, attempt => $"10.0.0.{attempt}");
        _time.Advance(TimeSpan.FromMilliseconds(500));

        var result = service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, Ip);

        Assert.Equal(AccountErrors.Throttled, result.Error);
        Assert.Equal(4, result.RetryAfterSeconds);
    }

    [Fact]
    public void Login_Success_ForgetsTheFailuresOfTheAccount()
    {
        var service = Service();
        var registered = Registered(service);
        var free = AccountThrottles.SignInOptions.FreeAttempts;
        FailLogins(service, "Vini", registered.Account.Tag, free, attempt => $"10.0.0.{attempt}");
        Assert.True(service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, "198.51.100.1").Succeeded);
        FailLogins(service, "Vini", registered.Account.Tag, free + 1, attempt => $"10.0.1.{attempt}");

        var result = service.Login("Vini", registered.Account.Tag, "wrong wrong wrong", "198.51.100.2");

        Assert.Equal(AccountErrors.Throttled, result.Error);
    }

    [Fact]
    public void Login_WithAnOldHash_StoresANewOne()
    {
        var registered = Registered(Service(hasher: new SecretHasher(1)));
        var stronger = new SecretHasher(2);

        Assert.True(Service(hasher: stronger).Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, Ip).Succeeded);

        var stored = _store.Find(registered.Account.Id)!;
        Assert.False(stronger.NeedsRehash(stored.PhraseHash));
        Assert.True(stronger.Verify(RecoveryPhrase.Normalize(registered.RecoveryPhrase), stored.PhraseHash));
    }

    [Fact]
    public void Login_BeyondTheDeviceCap_DropsTheOldestDevice()
    {
        var service = Service();
        var registered = Registered(service);
        var tokens = Enumerable.Range(0, AccountService.MaxDevices)
            .Select(_ => service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, Ip).Value!.Token)
            .ToList();

        Assert.Equal(AccountService.MaxDevices, _store.Find(registered.Account.Id)!.Devices.Count);
        Assert.Null(service.Authenticate(registered.Token));
        Assert.All(tokens, token => Assert.NotNull(service.Authenticate(token)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a token")]
    public void Authenticate_SomethingThatIsNotAToken_IsNull(string? token)
    {
        var service = Service();
        Registered(service);

        Assert.Null(service.Authenticate(token));
    }

    [Fact]
    public void Authenticate_AWellFormedTokenThatIsNotSignedIn_IsNull()
    {
        var service = Service();
        Registered(service);

        Assert.Null(service.Authenticate(DeviceTokens.Create().Token));
    }

    [Fact]
    public void Logout_RemovesOnlyThatDevice()
    {
        var service = Service();
        var registered = Registered(service);
        var other = service.Login("Vini", registered.Account.Tag, registered.RecoveryPhrase, Ip).Value!;

        var loggedOut = service.Logout(registered.Token);

        Assert.True(loggedOut);
        Assert.Null(service.Authenticate(registered.Token));
        Assert.Equal(registered.Account.Id, service.Authenticate(other.Token)?.Id);
    }

    [Fact]
    public void Logout_ATokenThatIsNotSignedIn_IsFalse()
    {
        var service = Service();
        Registered(service);

        Assert.False(service.Logout(null));
        Assert.False(service.Logout(DeviceTokens.Create().Token));
    }

    [Fact]
    public void Logout_Twice_IsFalseTheSecondTime()
    {
        var service = Service();
        var registered = Registered(service);
        service.Logout(registered.Token);

        Assert.False(service.Logout(registered.Token));
    }

    [Fact]
    public void Availability_AnInvalidUsername_IsInvalidUsername()
    {
        Assert.Equal(AccountErrors.InvalidUsername, Service().Availability("#", null, Ip).Error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("100000")]
    [InlineData("-1")]
    public void Availability_ATagThatCannotBe_IsInvalidTag(string tag)
    {
        Assert.Equal(AccountErrors.InvalidTag, Service().Availability("Vini", tag, Ip).Error);
    }

    [Theory]
    [InlineData("09291", false)]
    [InlineData("#9291", false)]
    [InlineData("00001", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void Availability_SaysWhetherTheTagOrAnyTagIsFree(string? tag, bool expected)
    {
        var service = Service();
        Registered(service, "Vini", 9291);

        var result = service.Availability("  VINI ", tag, Ip);

        Assert.True(result.Succeeded);
        Assert.Equal(new UsernameAvailability("VINI", expected), result.Value);
    }

    [Fact]
    public void Availability_WhenEveryTagIsTaken_IsNotAvailable()
    {
        var result = Service(StoreWithEveryTagTaken()).Availability("Vini", null, Ip);

        Assert.False(result.Value!.Available);
    }

    [Fact]
    public void Availability_TooManyFromOneIp_IsThrottled()
    {
        var service = Service();
        for (var lookup = 0; lookup <= AccountThrottles.LookupOptions.FreeAttempts; lookup++)
            Assert.True(service.Availability("Vini", null, Ip).Succeeded);

        var throttled = service.Availability("Vini", null, Ip);

        Assert.Equal(AccountErrors.Throttled, throttled.Error);
        Assert.True(throttled.RetryAfterSeconds >= 1);
        Assert.True(service.Availability("Vini", null, "198.51.100.1").Succeeded);
    }

    /// <summary>A store where someone else takes the same name and tag right before the first add.</summary>
    private sealed class RacingStore : IAccountStore
    {
        private readonly FileAccountStore _inner = new(null);
        private bool _raced;

        public Account? Find(Guid id) => _inner.Find(id);

        public Account? Find(string username, int tag) => _inner.Find(username, tag);

        public Account? FindByExternalLogin(string provider, string subject) => _inner.FindByExternalLogin(provider, subject);

        public Account? FindByDeviceTokenHash(string tokenHash) => _inner.FindByDeviceTokenHash(tokenHash);

        public IReadOnlySet<int> TagsOf(string username) => _inner.TagsOf(username);

        public bool TryAdd(Account account)
        {
            if (!_raced)
                _raced = _inner.TryAdd(account with { Id = Guid.NewGuid(), Devices = [new DeviceToken("someone-else", account.CreatedAt)] });
            return _inner.TryAdd(account);
        }

        public bool Update(Account account) => _inner.Update(account);
    }

    [Fact]
    public void Register_WithoutADisplayName_ShowsTheUsernameAndNeverTheTag()
    {
        var registered = Registered(Service(), "Vini", 9291);

        Assert.Equal("Vini", registered.Account.PublicName);
        Assert.Equal("Vini#09291", registered.Account.Display);
    }

    [Fact]
    public void Register_WithADisplayName_KeepsItCleanedAsThePublicName()
    {
        var result = Service().Register("Vini", 9291, null, Ip, "  Cool   Snake ");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Cool Snake", result.Value.Account.PublicName);
        Assert.Equal("Vini#09291", result.Value.Account.Display);
    }

    [Theory]
    [InlineData("Cool#12345")]
    [InlineData("x")]
    [InlineData("admin")]
    public void Register_WithADisplayNameThatIsNotAllowed_IsRefusedAndNothingIsMade(string displayName)
    {
        var service = Service();

        var result = service.Register("Vini", 9291, null, Ip, displayName);

        Assert.Equal(AccountErrors.InvalidDisplayName, result.Error);
        Assert.Null(_store.Find("Vini", 9291));
    }

    [Fact]
    public void ChangeDisplayName_ChangesWhatOtherPlayersSee_AndTheAccountKeepsItNextTime()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291);

        var result = service.ChangeDisplayName(registered.Token, "Rattler");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Rattler", service.Authenticate(registered.Token)!.PublicName);
        Assert.Equal("Vini#09291", service.Authenticate(registered.Token)!.Display);
    }

    [Fact]
    public void ChangeDisplayName_ABlankNameGoesBackToTheUsername()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291);
        service.ChangeDisplayName(registered.Token, "Rattler");

        var result = service.ChangeDisplayName(registered.Token, "  ");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Vini", result.Value.PublicName);
    }

    [Fact]
    public void ChangeDisplayName_ANameThatIsNotAllowed_ChangesNothing()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291);

        var result = service.ChangeDisplayName(registered.Token, "Vini#09291");

        Assert.Equal(AccountErrors.InvalidDisplayName, result.Error);
        Assert.Equal("Vini", service.Authenticate(registered.Token)!.PublicName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public void ChangeDisplayName_WithoutAnAccountToken_IsInvalidToken(string? token)
        => Assert.Equal(AccountErrors.InvalidToken, Service().ChangeDisplayName(token, "Rattler").Error);

    [Fact]
    public void SignInWithExternal_AFirstTimeMakesAnAccountWithNoPhraseFromTheProviderNames()
    {
        var service = Service();

        var result = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip);

        Assert.True(result.Succeeded, result.Error);
        var account = result.Value.Account;
        Assert.Equal("The Octocat", account.Username);
        Assert.Equal("The Octocat", account.PublicName);
        Assert.True(Tags.IsValid(account.Tag));
        Assert.Equal(Account.NoPhrase, account.PhraseHash);
        Assert.Equal(new ExternalLogin("github", LoginIdentifier.Hash("github", "583231", AccountService.DevelopmentPepper), _time.GetUtcNow()), Assert.Single(account.Logins));
        Assert.Equal(account.Id, service.Authenticate(result.Value.Token)?.Id);
    }

    [Fact]
    public void SignInWithExternal_TheSameSocialAccountAgainIsTheSameAccount_OnANewDevice_WithTheNamesAsTheyWere()
    {
        var service = Service();
        var first = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip).Value;
        service.ChangeDisplayName(first.Token, "Rattler");

        var second = service.SignInWithExternal("GitHub", "583231", "renamed-on-github", "Another Name", Ip).Value;

        Assert.Equal(first.Account.Id, second.Account.Id);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal("The Octocat", second.Account.Username);
        Assert.Equal("Rattler", service.Authenticate(second.Token)!.PublicName);
        Assert.Equal(first.Account.Id, service.Authenticate(first.Token)?.Id);
    }

    [Fact]
    public void SignInWithExternal_TheSameIdOnAnotherProviderIsAnotherAccount()
    {
        var service = Service();

        var github = service.SignInWithExternal("github", "42", "ana", "Ana", Ip).Value;
        var discord = service.SignInWithExternal("discord", "42", "ana", "Ana", Ip).Value;

        Assert.NotEqual(github.Account.Id, discord.Account.Id);
        Assert.NotEqual(github.Account.Tag, discord.Account.Tag);
    }

    [Theory]
    [InlineData(null, "João da Silva", "João da Silva", "João da Silva")]
    [InlineData("😎😎", "Ana Souza", "Ana Souza", "Ana Souza")]
    [InlineData("admin", null, "Player", "Player")]
    [InlineData(null, null, "Player", "Player")]
    [InlineData("octo#12345", "Octo", "Octo", "Octo")]
    [InlineData("octo#12345", null, "octo12345", "octo12345")]
    [InlineData("😎😎", "😎", "Player", "Player")]
    public void SignInWithExternal_NamesThatDoNotFitAreCleanedOrReplaced(string? username, string? displayName, string expectedUsername, string expectedPublic)
    {
        var account = Service().SignInWithExternal("github", "1", username, displayName, Ip).Value.Account;

        Assert.Equal(expectedUsername, account.Username);
        Assert.Equal(expectedPublic, account.PublicName);
    }

    [Fact]
    public void SignInWithExternal_ADisplayNameThatIsTheUsernameIsNotKeptTwice()
    {
        var account = Service().SignInWithExternal("github", "1", "octocat", "octocat", Ip).Value.Account;

        Assert.Null(account.DisplayName);
    }

    [Fact]
    public void SignInWithExternal_APhraseNeverSignsInASocialAccount()
    {
        var service = Service();
        var account = service.SignInWithExternal("github", "1", "octocat", "Octo", Ip).Value.Account;

        foreach (var phrase in new[] { Account.NoPhrase, "", "none", StrongPhrase })
            Assert.Equal(AccountErrors.InvalidCredentials, service.Login("octocat", account.Tag, phrase, Ip).Error);
    }

    [Fact]
    public void SignInWithExternal_ANewAccountCountsTowardsTheRegistrationThrottle_AReturningOneDoesNot()
    {
        var service = Service();
        for (var index = 0; index < 11; index++)
            Assert.True(service.SignInWithExternal("github", index.ToString(), "ana", "Ana", Ip).Succeeded);

        Assert.Equal(AccountErrors.Throttled, service.SignInWithExternal("github", "new", "ana", "Ana", Ip).Error);
        Assert.True(service.SignInWithExternal("github", "3", "ana", "Ana", Ip).Succeeded);
    }

    [Theory]
    [InlineData(null, "1")]
    [InlineData("", "1")]
    [InlineData("github", null)]
    [InlineData("github", " ")]
    public void SignInWithExternal_WithoutAProviderAndAnId_IsRefused(string? provider, string? subject)
        => Assert.Equal(AccountErrors.InvalidCredentials, Service().SignInWithExternal(provider, subject, "ana", "Ana", Ip).Error);

    [Fact]
    public void SignInWithExternal_KeepsOnlyAHashOfTheProvidersId_NeverTheIdItself()
    {
        var service = Service();

        var account = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip).Value.Account;

        var login = Assert.Single(account.Logins);
        Assert.Equal("github", login.Provider);
        Assert.NotEqual("583231", login.IdHash);
        Assert.Equal(LoginIdentifier.Hash("github", "583231", AccountService.DevelopmentPepper), login.IdHash);
    }

    [Fact]
    public void SignInWithExternal_KeepsOneNameOnly_NotTheProvidersUsernameToo()
    {
        var account = Service().SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip).Value.Account;

        Assert.Equal("The Octocat", account.Username);
        Assert.Null(account.DisplayName);
        Assert.Equal("The Octocat", account.PublicName);
    }

    [Fact]
    public void SignInWithExternal_TheFileHoldsNeitherTheIdNorTheProvidersUsername()
    {
        var path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            var service = new AccountService(new FileAccountStore(path), AccountThrottles.Create(_time), SimpleWordFilter.Default, new RecoveryPhrase(), new SecretHasher(1), _time);

            service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip);

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("583231", text);
            Assert.DoesNotContain("\"octocat\"", text);
            Assert.Contains("The Octocat", text);
            Assert.DoesNotContain("@", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SignInWithExternal_AnotherSecretMeansAnotherAccountForTheSameSocialAccount_SoALeakedFileCannotBeMatchedToIds()
    {
        var store = new FileAccountStore(null);
        AccountService WithPepper(string pepper) => new(store, AccountThrottles.Create(_time), SimpleWordFilter.Default, new RecoveryPhrase(), new SecretHasher(1), _time, loginPepper: pepper);

        var first = WithPepper("one").SignInWithExternal("github", "1", null, "Ana", Ip).Value.Account;
        var again = WithPepper("one").SignInWithExternal("github", "1", null, "Ana", Ip).Value.Account;
        var other = WithPepper("two").SignInWithExternal("github", "1", null, "Ana", Ip).Value.Account;

        Assert.Equal(first.Id, again.Id);
        Assert.NotEqual(first.Id, other.Id);
    }

    [Fact]
    public void LinkExternal_AddsASocialLoginToAnAccountWithAPhrase_WhichThenSignsInWithIt()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291);

        var linked = service.LinkExternal(registered.Account.Id, "github", "583231");
        var signedIn = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip);

        Assert.True(linked.Succeeded, linked.Error);
        Assert.Equal(registered.Account.Id, signedIn.Value.Account.Id);
        Assert.Equal("Vini", signedIn.Value.Account.Username);
        Assert.Equal(new[] { "github" }, linked.Value.Logins.Select(login => login.Provider));
    }

    [Fact]
    public void LinkExternal_KeepsTheRecoveryPhraseWorking_AndOnlyAHashOfTheId()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291, StrongPhrase);

        var linked = service.LinkExternal(registered.Account.Id, "discord", "80351110224678912").Value;

        Assert.Equal(registered.Account.PhraseHash, linked.PhraseHash);
        Assert.True(service.Login("Vini", 9291, StrongPhrase, Ip).Succeeded);
        Assert.Equal(LoginIdentifier.Hash("discord", "80351110224678912", AccountService.DevelopmentPepper), Assert.Single(linked.Logins).IdHash);
    }

    [Fact]
    public void LinkExternal_TheSameLoginAgainChangesNothing()
    {
        var service = Service();
        var registered = Registered(service);
        service.LinkExternal(registered.Account.Id, "github", "1");

        var again = service.LinkExternal(registered.Account.Id, "GitHub", "1");

        Assert.True(again.Succeeded);
        Assert.Single(again.Value.Logins);
    }

    [Fact]
    public void LinkExternal_ASocialAccountThatBelongsToAnotherPlayerIsRefused()
    {
        var service = Service();
        var mine = Registered(service, "Ana", 1);
        var yours = Registered(service, "Bob", 2);
        service.LinkExternal(yours.Account.Id, "github", "1");

        var result = service.LinkExternal(mine.Account.Id, "github", "1");

        Assert.Equal(AccountErrors.AlreadyLinked, result.Error);
        Assert.Empty(service.Authenticate(mine.Token)!.Logins);
    }

    [Fact]
    public void LinkExternal_AnAccountOnlyHasOneLoginPerProvider()
    {
        var service = Service();
        var registered = Registered(service);
        service.LinkExternal(registered.Account.Id, "github", "1");

        var result = service.LinkExternal(registered.Account.Id, "github", "2");

        Assert.Equal(AccountErrors.ProviderInUse, result.Error);
        Assert.Single(service.Authenticate(registered.Token)!.Logins);
    }

    [Fact]
    public void LinkExternal_AccountsCanHaveSeveralProviders()
    {
        var service = Service();
        var registered = Registered(service);

        service.LinkExternal(registered.Account.Id, "github", "1");
        var result = service.LinkExternal(registered.Account.Id, "discord", "1");

        Assert.Equal(new[] { "discord", "github" }, result.Value.Logins.Select(login => login.Provider).Order());
    }

    [Fact]
    public void LinkExternal_AnAccountThatIsGoneOrAnEmptyLoginIsRefused()
    {
        var service = Service();
        var registered = Registered(service);

        Assert.Equal(AccountErrors.InvalidToken, service.LinkExternal(Guid.NewGuid(), "github", "1").Error);
        Assert.Equal(AccountErrors.InvalidCredentials, service.LinkExternal(registered.Account.Id, "", "1").Error);
        Assert.Equal(AccountErrors.InvalidCredentials, service.LinkExternal(registered.Account.Id, "github", " ").Error);
    }

    [Fact]
    public void ChangeHandle_ToAFreeNameAndTag_MovesTheAccountThere_AndTheOldHandleIsFreeAgain()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291);

        var result = service.ChangeHandle(registered.Token, "Rattler", 42);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Rattler#00042", service.Authenticate(registered.Token)!.Display);
        Assert.Null(_store.Find("Vini", 9291));
        Assert.Equal(registered.Account.Id, _store.Find("Rattler", 42)!.Id);
    }

    [Fact]
    public void ChangeHandle_TheRecoveryPhraseSignsInToTheNewHandle_AndTheDeviceStaysSignedIn()
    {
        var service = Service();
        var registered = Registered(service, "Vini", 9291, phrase: StrongPhrase);

        service.ChangeHandle(registered.Token, "Rattler", 42);

        Assert.True(service.Login("Rattler", 42, StrongPhrase, Ip).Succeeded);
        Assert.False(service.Login("Vini", 9291, StrongPhrase, Ip).Succeeded);
        Assert.NotNull(service.Authenticate(registered.Token));
    }

    [Fact]
    public void ChangeHandle_ATakenNameAndTag_IsRefused_AndNothingChanges()
    {
        var service = Service();
        var mine = Registered(service, "Ana", 1);
        Registered(service, "Bob", 2);

        var result = service.ChangeHandle(mine.Token, "bob", 2);

        Assert.Equal(AccountErrors.NameTaken, result.Error);
        Assert.Equal("Ana#00001", service.Authenticate(mine.Token)!.Display);
    }

    [Fact]
    public void ChangeHandle_TheSameNameWithAnotherTagThatIsFree_IsAccepted()
    {
        var service = Service();
        var mine = Registered(service, "Vini", 1);
        Registered(service, "Vini", 2);

        Assert.True(service.ChangeHandle(mine.Token, "Vini", 3).Succeeded);
        Assert.Equal(AccountErrors.NameTaken, service.ChangeHandle(mine.Token, "Vini", 2).Error);
    }

    [Fact]
    public void ChangeHandle_WithoutATag_PicksAFreeOne_AndKeepsTheOwnTagWhenOnlyTheNameChanges()
    {
        var service = Service(nextTag: _ => 777);
        var mine = Registered(service, "Vini", 9291);

        var renamed = service.ChangeHandle(mine.Token, "Rattler", null);
        var sameName = service.ChangeHandle(mine.Token, "Rattler", null);

        Assert.Equal(777, renamed.Value.Tag);
        Assert.Equal(777, sameName.Value.Tag);
    }

    [Fact]
    public void ChangeHandle_OnlyTheCaseOfTheOwnName_IsAccepted_AndSoIsChangingNothing()
    {
        var service = Service();
        var mine = Registered(service, "Vini", 9291);

        Assert.True(service.ChangeHandle(mine.Token, "VINI", 9291).Succeeded);
        Assert.True(service.ChangeHandle(mine.Token, "VINI", 9291).Succeeded);
        Assert.Equal("VINI", service.Authenticate(mine.Token)!.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Vini#12345")]
    [InlineData("admin")]
    public void ChangeHandle_ANameThatIsNotAllowed_IsRefused(string name)
    {
        var service = Service();
        var mine = Registered(service, "Vini", 9291);

        Assert.Equal(AccountErrors.InvalidUsername, service.ChangeHandle(mine.Token, name, 1).Error);
        Assert.Equal("Vini#09291", service.Authenticate(mine.Token)!.Display);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    public void ChangeHandle_ATagOutOfRange_IsRefused(int tag)
    {
        var service = Service();
        var mine = Registered(service, "Vini", 9291);

        Assert.Equal(AccountErrors.InvalidTag, service.ChangeHandle(mine.Token, "Rattler", tag).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public void ChangeHandle_WithoutAnAccountToken_IsInvalidToken(string? token)
        => Assert.Equal(AccountErrors.InvalidToken, Service().ChangeHandle(token, "Rattler", 1).Error);

    [Fact]
    public void ChangeHandle_ASocialAccount_CanChangeItsHandleToo_AndKeepsSigningInWithTheProvider()
    {
        var service = Service();
        var first = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip).Value;

        var result = service.ChangeHandle(first.Token, "Rattler", 5);

        Assert.True(result.Succeeded, result.Error);
        var again = service.SignInWithExternal("github", "583231", "octocat", "The Octocat", Ip).Value;
        Assert.Equal(first.Account.Id, again.Account.Id);
        Assert.Equal("Rattler", again.Account.Username);
    }

    [Fact]
    public void ChangeHandle_TooManyTries_AreSlowedDown_PerAccount()
    {
        var service = Service();
        var mine = Registered(service, "Vini", 9291);
        var taken = Registered(service, "Taken", 1);
        AccountResult<Account>? last = null;

        for (var attempt = 0; attempt <= AccountThrottles.LookupOptions.FreeAttempts + 1; attempt++)
            last = service.ChangeHandle(mine.Token, "Taken", 1);

        Assert.Equal(AccountErrors.Throttled, last!.Error);
        Assert.NotNull(taken);
        Assert.True(service.ChangeHandle(taken.Token, "Other", 2).Succeeded);
    }
}
