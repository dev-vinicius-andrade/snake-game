using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Server.Entities.Hub.Events;

namespace Library.Commons.Game.Server.Interfaces;

public interface IGameHubSendEvents
{
    /// <summary>The state of the room as the players draw it; see SnapshotBuilder.</summary>
    Task Snapshot(SnapshotEvent snapshot);
    Task PlayerJoined(PlayerJoinedEvent eventArgs);
    
    Task PlayerLefted(PlayerLeftedEvent eventArgs);
    Task ConnectedPlayersUpdated(ConnectedPlayersUpdatedEvent eventArgs);
    Task FoodGenerated(FoodGeneratedEvent eventArgs);
    Task RoomDetailsUpdated(RoomDetailsUpdatedEvent eventArgs);
    Task PlayerDied(PlayerDiedEvent eventArgs);
    Task RoomClosed(RoomClosedEvent eventArgs);
    Task DamageDealt(DamageDealtEvent eventArgs);
    Task ScoreUpdated(IList<IScore> score);
    Task PlayerScoreUpdated(long score=0);
}