namespace Application.Manager.Api.Accounts;

/// <summary>Backoff tuning. Never a permanent lock: delay is capped at <see cref="MaxDelay"/>.</summary>
public sealed record ThrottleOptions
{
    public int FreeAttempts { get; init; } = 5;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public double Factor { get; init; } = 2;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>Key with no failures for this long is forgotten.</summary>
    public TimeSpan ForgetAfter { get; init; } = TimeSpan.FromHours(1);
    /// <summary>Hard cap on tracked keys; oldest dropped first.</summary>
    public int MaxKeys { get; init; } = 100_000;
}

/// <summary>
/// Slows guessing with exponential backoff. Callers pass keys such as "acct:name#tag" and "ip:addr"
/// and check all of them. Protocol: call <see cref="RetryAfter"/> first; if non-null, reject without
/// checking the secret and do NOT call <see cref="Failed"/>. As a safety net, <see cref="Failed"/>
/// while a key is still waiting is a no-op (no escalation), so waits never exceed the formula.
/// </summary>
public sealed class AttemptThrottle
{
    private sealed class Entry
    {
        public int Failures;
        public DateTimeOffset LastFailure;
        public DateTimeOffset BlockedUntil;
    }

    private readonly TimeProvider _time;
    private readonly ThrottleOptions _o;
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _gate = new();
    private DateTimeOffset _lastSweep;

    public AttemptThrottle(TimeProvider time, ThrottleOptions? options = null)
    {
        _time = time;
        _o = options ?? new ThrottleOptions();
        _lastSweep = time.GetUtcNow();
    }

    /// <summary>Number of keys currently tracked (diagnostics/tests).</summary>
    public int TrackedKeys
    {
        get { lock (_gate) return _entries.Count; }
    }

    public TimeSpan? RetryAfter(string key)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var e)) return null;
            if (now - e.LastFailure >= _o.ForgetAfter)
            {
                _entries.Remove(key);
                return null;
            }
            var left = e.BlockedUntil - now;
            return left > TimeSpan.Zero ? left : null;
        }
    }

    public void Failed(string key)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            Sweep(now);
            if (!_entries.TryGetValue(key, out var e))
            {
                EnsureRoom();
                e = new Entry();
                _entries[key] = e;
            }
            else if (now - e.LastFailure >= _o.ForgetAfter)
            {
                e.Failures = 0;
                e.BlockedUntil = default;
            }
            else if (e.BlockedUntil > now)
            {
                return; // still waiting: no escalation
            }

            if (e.Failures < int.MaxValue) e.Failures++;
            e.LastFailure = now;
            if (e.Failures > _o.FreeAttempts)
                e.BlockedUntil = now + Delay(e.Failures - _o.FreeAttempts);
        }
    }

    public void Succeeded(string key)
    {
        lock (_gate) _entries.Remove(key);
    }

    private TimeSpan Delay(int level)
    {
        var ms = _o.BaseDelay.TotalMilliseconds * Math.Pow(_o.Factor, level - 1);
        var max = _o.MaxDelay.TotalMilliseconds;
        return double.IsNaN(ms) || ms >= max ? _o.MaxDelay : TimeSpan.FromMilliseconds(ms);
    }

    private void Sweep(DateTimeOffset now)
    {
        // At most once per quarter of ForgetAfter; amortised cheap.
        if (now - _lastSweep < _o.ForgetAfter / 4) return;
        _lastSweep = now;
        var stale = _entries.Where(kv => now - kv.Value.LastFailure >= _o.ForgetAfter)
                            .Select(kv => kv.Key).ToList();
        foreach (var k in stale) _entries.Remove(k);
    }

    private void EnsureRoom()
    {
        if (_entries.Count < _o.MaxKeys) return;
        // Drop oldest 10% in one pass so the O(n) scan is amortised.
        var drop = Math.Max(1, _o.MaxKeys / 10);
        foreach (var kv in _entries.OrderBy(kv => kv.Value.LastFailure).Take(drop).ToList())
            _entries.Remove(kv.Key);
    }
}
