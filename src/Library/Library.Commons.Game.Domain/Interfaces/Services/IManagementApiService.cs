using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IManagementApiService
{
    Task<bool> PlayerJoinRoomAsync(string playerConnectionId, string playerName, IRoom room);
}