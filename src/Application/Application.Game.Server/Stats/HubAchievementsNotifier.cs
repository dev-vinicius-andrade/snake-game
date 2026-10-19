using Application.Game.Server.Hubs;
using Application.Game.Server.Services;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Application.Game.Server.Stats;

/// <summary>
/// Sends <c>AchievementsUnlocked</c> to the game hub connections of the account. A player that is already gone has none, and is skipped.
/// </summary>
public sealed class HubAchievementsNotifier(IHubContext<GameHub, IGameHubSendEvents> hub, AccountConnections connections) : IAchievementsNotifier
{
    public Task NotifyAsync(Guid accountId, IReadOnlyList<string> achievementIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(achievementIds);
        var targets = connections.Of(accountId);
        if (achievementIds.Count == 0 || targets.Count == 0)
            return Task.CompletedTask;
        return hub.Clients.Clients(targets).AchievementsUnlocked(new AchievementsUnlockedEvent { Ids = achievementIds.ToArray() });
    }
}
