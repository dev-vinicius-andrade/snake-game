using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IManagementApiService
{
    Task<bool> PlayerJoinRoomAsync(string playerConnectionId, string playerName, IRoom room);
    Task<bool> PlayerJoinFailedAsync(string playerConnectionId, string reason);
    Task<bool> RoomCreatedAsync(string requesterId, IRoom room);
    Task<bool> RoomCreateFailedAsync(string requesterId, string reason);

    /// <summary>Tells the manager which rooms this server has, so it can list them and find them by code.</summary>
    Task<bool> SendHeartbeatAsync(IReadOnlyCollection<RoomSnapshot> rooms);

    /// <summary>The gameplay config (perks and prize) as the manager serves it, as JSON; null when it could not be fetched.</summary>
    Task<string?> GetGameplayJsonAsync();
}
