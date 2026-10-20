using Library.Commons.Game.Server.Entities.Hub.Requests;
using System.Threading.Tasks;

namespace Library.Commons.Game.Server.Interfaces;

public interface IGameHubListenEvents
{
    Task JoinRoom(JoinRoomRequest request);
    Task ChangeDirection(ChangeDirectionRequest request);
    Task LeaveRoom(LeaveRoomRequest request);
    Task Shoot(ShootRequest request);
    Task Boost(BoostRequest request);
    Task UseAbility(UseAbilityRequest request);
    Task SetAutoAbilities(AutoAbilitiesRequest request);
    Task Steer(SteerRequest request);
    /// <summary>Does nothing; the time it takes to answer is the ping.</summary>
    Task Ping();
}