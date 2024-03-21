using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPlayerService

{
    IPlayer Create(string id, string name, IColor backgroundColor, IMovementDirection direction, IPointAxis initialPosition,
        IList<IPointAxis>? paths = null);
    IPlayer Get(Guid id);
    IEnumerable<IPlayer> GetAll();
    IPlayer Save(IPlayer player);

    IPlayer? Get(string contextConnectionId);
    IMovementDirection CreateDirection(Direction direction, IPointAxis axis);
    void Delete(ITrackableId player);
}