using System.Collections.Concurrent;

namespace Application.Game.Server.Services;

/// <summary>
/// The game hub connections of signed-in players on this server, kept from their join until they disconnect, to tell an account what
/// it unlocked. A dead snake's connection stays known while the player looks at the death screen.
/// </summary>
public sealed class AccountConnections
{
    private readonly ConcurrentDictionary<string, Guid> _accounts = new();

    public void Add(string connectionId, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        _accounts[connectionId] = accountId;
    }

    public void Remove(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        _accounts.TryRemove(connectionId, out _);
    }

    public IReadOnlyList<string> Of(Guid accountId)
        => _accounts.Where(connection => connection.Value == accountId).Select(connection => connection.Key).ToList();
}
