using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPlayerRoomService

{
    
    bool ConnectPlayer(IPlayer player, IRoom? room);
    bool DisconnectPlayer(IPlayer player, IRoom room);
    long CountPlayers(IRoom room);
    bool IsPlayerConnected(IPlayer player, IRoom room);
    bool IsRoomFull(IRoom room);
    IReadOnlyDictionary<Guid, IList<IPlayer>> GetRoomsPlayers();

    ITrackableId? GetRoomTrackableId(IPlayer player);
    void UpdatePlayer(IPlayer player, IRoom gameStateRoom);
}