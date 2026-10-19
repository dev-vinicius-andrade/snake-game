using System.Collections.Concurrent;
using Domain.Game.Rules;
using Domain.Game.Scripting;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Services;

/// <summary>
/// Runs what the perk scripts do for a whole room rather than for a snake: <c>onTick</c>, about once a second, which may ask for a
/// prize to drop. What the scripts remember about the room (<c>room.state</c>) is kept until the room is empty.
/// </summary>
public sealed class PerkWorld
{
    private readonly PerkRules _perks;
    private readonly PrizeSpawner _prizes;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, ScriptStates> _states = new();

    public PerkWorld(PerkRules perks, PrizeSpawner prizes, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(perks);
        ArgumentNullException.ThrowIfNull(prizes);
        ArgumentNullException.ThrowIfNull(time);
        _perks = perks;
        _prizes = prizes;
        _time = time;
    }

    /// <summary>Call about once a second for every room. A room without players forgets what its scripts remembered.</summary>
    public void Update(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var room = gameState.Room;
        if (gameState.Players.Count == 0)
        {
            _states.TryRemove(room.Guid, out _);
            return;
        }

        var view = new ScriptRoom(
            gameState.Players.Count, room.Name ?? string.Empty, room.Code ?? string.Empty, room.Width, room.Height, _time.GetUtcNow().ToUnixTimeMilliseconds());
        foreach (var kind in _perks.TickRoom(room, view, _states.GetOrAdd(room.Guid, _ => new ScriptStates())))
            _prizes.Drop(gameState, kind);
    }
}
