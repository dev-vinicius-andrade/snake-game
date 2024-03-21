using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IRoomsService :IAvailableService
{
    IRoom CreateRoom();
    IRoom? GetRoom(ITrackableId id);
    IEnumerable<IRoom> GetRooms();
    IEnumerable<IRoom> GetAvailableRooms();
    IRoom DeleteRoom(ITrackableId id);
    bool CanCreate();

}