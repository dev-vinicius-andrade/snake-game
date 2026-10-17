using Library.Commons.Game.Domain.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IRoomJoinRouter
{
    /// <summary>Where a player who wants to join goes: a given room, or any open public room when null.</summary>
    JoinRoute Resolve(Guid? roomId);
}
