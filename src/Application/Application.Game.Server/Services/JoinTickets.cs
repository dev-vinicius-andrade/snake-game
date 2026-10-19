using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Application.Game.Server.Services;

/// <summary>A signed-in player this server accepted into a room, that has not connected to the game hub yet.</summary>
public sealed record PendingAccount(Guid AccountId, string DisplayName, Guid RoomId);

/// <summary>
/// One-time tickets that carry a signed-in player from the join (the manager checked its token, see JoinRequestHandler) to its
/// connection to the game hub (GameHub.JoinRoom), so the player is known as its account there and the stats of its life are counted.
/// A ticket is a random secret, works once, for the room it was made for, and for <see cref="Lifetime"/>.
/// </summary>
public sealed class JoinTickets(TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>Tickets kept at most; when full, the one closest to expiring is dropped (that player then plays as a guest).</summary>
    public const int MaxPending = 10_000;

    private sealed record Ticket(PendingAccount Account, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Ticket> _pending = new();

    public string Issue(Guid accountId, string displayName, Guid roomId)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        MakeRoom();
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _pending[ticket] = new Ticket(new PendingAccount(accountId, displayName, roomId), time.GetUtcNow() + Lifetime);
        return ticket;
    }

    /// <summary>The account of the ticket, which is spent; null when there is none, or it expired, or it was made for another room.</summary>
    public PendingAccount? Redeem(string? ticket, Guid roomId)
    {
        if (string.IsNullOrWhiteSpace(ticket) || !_pending.TryRemove(ticket, out var pending))
            return null;
        return pending.ExpiresAt > time.GetUtcNow() && pending.Account.RoomId == roomId ? pending.Account : null;
    }

    private void MakeRoom()
    {
        if (_pending.Count < MaxPending)
            return;
        var now = time.GetUtcNow();
        foreach (var expired in _pending.Where(pending => pending.Value.ExpiresAt <= now).Select(pending => pending.Key).ToArray())
            _pending.TryRemove(expired, out _);
        if (_pending.Count >= MaxPending)
            _pending.TryRemove(_pending.MinBy(pending => pending.Value.ExpiresAt).Key, out _);
    }
}
