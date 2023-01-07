using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IRoomsService<out TRoom> :IAvailableService
where TRoom : class, IRoom
{
    TRoom CreateRoom();
    TRoom GetRoom(Guid id);
    IEnumerable<TRoom> GetRooms();
    IEnumerable<TRoom> GetAvailableRooms();
    TRoom DeleteRoom(Guid id);
}