using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>
/// Short lived secrets that work once: the state of a sign in that is under way, and the ticket the browser trades for its token. Each is
/// found by a long random key, forgotten after its time, and gone the moment it is taken. Kept in memory: a restart only cancels sign ins
/// that were under way. At most <c>capacity</c> are kept, the oldest go first, so a flood of starts cannot fill the memory.
/// </summary>
public sealed class OneTimeStore<T> where T : class
{
    private const int KeyBytes = 32;

    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;
    private readonly int _capacity;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, T Value)> _items = new(StringComparer.Ordinal);

    public OneTimeStore(TimeProvider time, TimeSpan lifetime, int capacity = 10_000)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _lifetime = lifetime;
        _capacity = capacity;
    }

    /// <summary>Keeps the value and gives the key to find it with (43 characters of URL safe text).</summary>
    public string Put(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        MakeRoom();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _items[key] = (_time.GetUtcNow(), value);
        return key;
    }

    /// <summary>The value, once: it is forgotten as it is taken. Null for a key that is unknown, used or expired.</summary>
    public T? Take(string? key)
    {
        if (string.IsNullOrEmpty(key) || !_items.TryRemove(key, out var item))
            return null;
        return _time.GetUtcNow() - item.At <= _lifetime ? item.Value : null;
    }

    private void MakeRoom()
    {
        if (_items.Count < _capacity)
            return;
        var now = _time.GetUtcNow();
        foreach (var expired in _items.Where(item => now - item.Value.At > _lifetime).Select(item => item.Key).ToList())
            _items.TryRemove(expired, out _);
        foreach (var oldest in _items.OrderBy(item => item.Value.At).Take(Math.Max(0, _items.Count - _capacity + 1)).Select(item => item.Key).ToList())
            _items.TryRemove(oldest, out _);
    }
}
