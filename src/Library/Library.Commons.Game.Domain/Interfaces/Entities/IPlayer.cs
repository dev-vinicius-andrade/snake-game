using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IPlayer : IId, ITrackableId
{
    string Name { get; }
    IPointAxis CurrentPosition { get; set; }
    Direction CurrentDirection { get; set; }
    IMovementDirection Direction { get; set; }
    IColor BackgroundColor { get; set; }
    IColor? BorderColor { get; set; }
    IList<IPointAxis> Path { get; set; }
    bool IsAlive { get; set; }
    long Score { get; set; }
}