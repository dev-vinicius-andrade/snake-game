using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPlayerRoomService

{
    
    bool ConnectPlayer(IPlayer player, IRoom? room);
    bool DisconnectPlayer(IPlayer player, IRoom room);
    long CountPlayers(IRoom room);

    /// <summary>Forgets a room that no longer exists, so no worker keeps looking at it.</summary>
    void RemoveRoom(Guid roomId);
    bool IsPlayerConnected(IPlayer player, IRoom room);
    bool IsRoomFull(IRoom room);
    IReadOnlyDictionary<Guid, IList<IPlayer>> GetRoomsPlayers();

    ITrackableId? GetRoomTrackableId(IPlayer player);
    void UpdatePlayer(IPlayer player, IRoom gameStateRoom);
}