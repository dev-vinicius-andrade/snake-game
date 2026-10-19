using Application.Manager.Api.Accounts;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Application.Manager.Api.Tests;

public class AttemptThrottleTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private AttemptThrottle Create(ThrottleOptions? o = null) => new(_time, o);

    private static void Fail(AttemptThrottle t, string key, int n)
    {
        for (var i = 0; i < n; i++) t.Failed(key);
    }

    [Fact]
    public void UnknownKey_MayTry() => Assert.Null(Create().RetryAfter("k"));

    [Fact]
    public void FreeAttempts_NoWait()
    {
        var t = Create();
        Fail(t, "k", 5);
        Assert.Null(t.RetryAfter("k"));
    }

    [Fact]
    public void EscalationSequence_DoublesFromBaseDelay()
    {
        var t = Create();
        Fail(t, "k", 5);
        foreach (var secs in new[] { 2, 4, 8, 16, 32 })
        {
            t.Failed("k");
            Assert.Equal(TimeSpan.FromSeconds(secs), t.RetryAfter("k"));
            _time.Advance(TimeSpan.FromSeconds(secs));
            Assert.Null(t.RetryAfter("k"));
        }
    }

    [Fact]
    public void RetryAfter_ReturnsTimeLeft()
    {
        var t = Create();
        Fail(t, "k", 6);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(TimeSpan.FromMilliseconds(1500), t.RetryAfter("k"));
    }

    [Fact]
    public void FailedDuringWait_IsNoOp()
    {
        var t = Create();
        Fail(t, "k", 6);
        t.Failed("k");
        t.Failed("k");
        Assert.Equal(TimeSpan.FromSeconds(2), t.RetryAfter("k"));
        _time.Advance(TimeSpan.FromSeconds(2));
        t.Failed("k"); // next level only (4s), not escalated by the ignored calls
        Assert.Equal(TimeSpan.FromSeconds(4), t.RetryAfter("k"));
    }

    [Fact]
    public void Delay_CappedAtMax()
    {
        var t = Create();
        Fail(t, "k", 5);
        for (var i = 0; i < 40; i++)
        {
            t.Failed("k");
            _time.Advance(TimeSpan.FromMinutes(16)); // past any wait, inside ForgetAfter of last failure
        }
        t.Failed("k");
        Assert.Equal(TimeSpan.FromMinutes(15), t.RetryAfter("k"));
    }

    [Fact]
    public void HugeFailureCount_DoesNotOverflow()
    {
        var t = Create();
        Fail(t, "k", 5);
        for (var i = 0; i < 3000; i++)
        {
            t.Failed("k");
            _time.Advance(TimeSpan.FromMinutes(16));
        }
        t.Failed("k");
        Assert.Equal(TimeSpan.FromMinutes(15), t.RetryAfter("k"));
    }

    [Fact]
    public void Succeeded_ClearsKey()
    {
        var t = Create();
        Fail(t, "k", 6);
        t.Succeeded("k");
        Assert.Null(t.RetryAfter("k"));
        Fail(t, "k", 5);
        Assert.Null(t.RetryAfter("k")); // free attempts again
    }

    [Fact]
    public void ForgetsAfterIdle()
    {
        var t = Create();
        Fail(t, "k", 6);
        _time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        Assert.Null(t.RetryAfter("k"));
        t.Failed("k"); // fresh first failure
        Assert.Null(t.RetryAfter("k"));
        Assert.Equal(1, t.TrackedKeys);
    }

    [Fact]
    public void Keys_AreIndependent()
    {
        var t = Create();
        Fail(t, "a", 6);
        Assert.NotNull(t.RetryAfter("a"));
        Assert.Null(t.RetryAfter("b"));
        t.Succeeded("b");
        Assert.NotNull(t.RetryAfter("a"));
    }

    [Fact]
    public void Memory_IsBounded_DropsOldest()
    {
        var t = Create(new ThrottleOptions { MaxKeys = 100 });
        for (var i = 0; i < 1000; i++)
        {
            t.Failed($"k{i}");
            _time.Advance(TimeSpan.FromMilliseconds(1));
        }
        Assert.True(t.TrackedKeys <= 100);
        // newest key survived with 1 failure: 5 more reach 6 -> waiting
        Fail(t, "k999", 5);
        Assert.NotNull(t.RetryAfter("k999"));
    }

    [Fact]
    public void IdleKeys_ArePurged()
    {
        var t = Create();
        for (var i = 0; i < 50; i++) t.Failed($"k{i}");
        _time.Advance(TimeSpan.FromHours(2));
        t.Failed("fresh");
        Assert.Equal(1, t.TrackedKeys);
    }

    [Fact]
    public async Task Concurrency_Smoke()
    {
        var t = Create();
        var tasks = Enumerable.Range(0, 16).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                var key = $"k{i % 50}";
                _ = t.RetryAfter(key);
                if (i % 7 == 0) t.Succeeded(key); else t.Failed(key);
            }
        }));
        await Task.WhenAll(tasks);
        Assert.True(t.TrackedKeys <= 50);
    }
}
