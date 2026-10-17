using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IRoomsService :IAvailableService
{
    IRoom CreateRoom(RoomOptions? options = null);
    IRoom? TryGetRoom(Guid roomId);
    IRoom? GetRoomByCode(string code);
    IRoom? GetRoom(ITrackableId id);
    IEnumerable<IRoom> GetRooms();
    IEnumerable<IRoom> GetAvailableRooms();
    IRoom DeleteRoom(ITrackableId id);
    bool CanCreate();

}