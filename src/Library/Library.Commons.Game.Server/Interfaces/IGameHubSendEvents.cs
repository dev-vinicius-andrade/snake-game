using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Server.Entities.Hub.Events;

namespace Library.Commons.Game.Server.Interfaces;

public interface IGameHubSendEvents
{
    Task GameStateUpdated(IGameState gameState);
    Task PlayerJoined(PlayerJoinedEvent eventArgs);
    
    Task PlayerLefted(PlayerLeftedEvent eventArgs);
    Task ConnectedPlayersUpdated(ConnectedPlayersUpdatedEvent eventArgs);
    Task FoodGenerated(FoodGeneratedEvent eventArgs);
    Task RoomDetailsUpdated(RoomDetailsUpdatedEvent eventArgs);
    Task PlayerDied(PlayerDiedEvent eventArgs);
    Task ScoreUpdated(IList<IScore> score);
    Task PlayerScoreUpdated(long score=0);
}