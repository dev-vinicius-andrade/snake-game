using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPlayerRoomService<in TRoom, in TPlayer>
    where TRoom : class, IRoom
    where TPlayer : class, IPlayer
{
    
    bool ConnectPlayer(TPlayer player, TRoom room);
    bool DisconnectPlayer(TPlayer player, TRoom room);
    long CountPlayers(TRoom room);
    bool IsPlayerConnected(TPlayer player, TRoom room);
    bool IsRoomFull(TRoom room);
}