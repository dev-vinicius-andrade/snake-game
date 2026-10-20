using Application.Manager.Api.Accounts.OAuth;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class OneTimeStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    private OneTimeStore<string> Store(int capacity = 100) => new(_time, TimeSpan.FromMinutes(10), capacity);

    [Fact]
    public void ATakenValueIsGone_SoItWorksOnce()
    {
        var store = Store();
        var key = store.Put("secret");

        Assert.Equal("secret", store.Take(key));
        Assert.Null(store.Take(key));
    }

    [Fact]
    public void KeysAreLongAndDifferent()
    {
        var store = Store();

        var keys = Enumerable.Range(0, 50).Select(_ => store.Put("x")).ToList();

        Assert.Equal(50, keys.Distinct().Count());
        Assert.All(keys, key => Assert.True(key.Length >= 40));
        Assert.All(keys, key => Assert.Matches("^[A-Za-z0-9_-]+$", key));
    }

    [Fact]
    public void AnUnknownOrBlankKeyGivesNothing()
    {
        var store = Store();

        Assert.Null(store.Take("nope"));
        Assert.Null(store.Take(""));
        Assert.Null(store.Take(null));
    }

    [Fact]
    public void AValueExpiresAfterItsTime()
    {
        var store = Store();
        var key = store.Put("secret");

        _time.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.Null(store.Take(key));
    }

    [Fact]
    public void AValueStillCountsJustBeforeItExpires()
    {
        var store = Store();
        var key = store.Put("secret");

        _time.Advance(TimeSpan.FromMinutes(9));

        Assert.Equal("secret", store.Take(key));
    }

    [Fact]
    public void WhenFull_TheOldestGoFirst_SoAFloodCannotFillTheMemory()
    {
        var store = Store(capacity: 3);
        var first = store.Put("1");
        _time.Advance(TimeSpan.FromSeconds(1));
        var second = store.Put("2");
        _time.Advance(TimeSpan.FromSeconds(1));
        var third = store.Put("3");
        _time.Advance(TimeSpan.FromSeconds(1));

        var fourth = store.Put("4");

        Assert.Null(store.Take(first));
        Assert.Equal("2", store.Take(second));
        Assert.Equal("3", store.Take(third));
        Assert.Equal("4", store.Take(fourth));
    }
}
