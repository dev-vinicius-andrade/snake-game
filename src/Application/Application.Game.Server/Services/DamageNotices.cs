using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Server.Entities.Hub.Events;

namespace Application.Game.Server.Services;

/// <summary>One damage event for one player connection.</summary>
public sealed record DamageNotice(string ConnectionId, DamageDealtEvent Event);

/// <summary>Turns the damage a room has dealt since the last look into messages for the players involved.</summary>
public static class DamageNotices
{
    public static IReadOnlyList<DamageNotice> Drain(IGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var notices = new List<DamageNotice>();
        while (gameState.Hits.TryDequeue(out var hit))
            notices.AddRange(NoticesFor(gameState, hit));
        return notices;
    }

    private static IEnumerable<DamageNotice> NoticesFor(IGameState gameState, DamageDealt hit)
    {
        var message = new DamageDealtEvent
        {
            ShooterGuid = hit.ShooterGuid, TargetGuid = hit.TargetGuid, X = hit.X, Y = hit.Y, Damage = hit.Damage, Kind = hit.Kind
        };
        return new[] { hit.ShooterGuid, hit.TargetGuid }
            .Distinct()
            .Where(guid => gameState.Players.ContainsKey(guid))
            .Select(guid => new DamageNotice(gameState.Players[guid].Id, message));
    }
}
