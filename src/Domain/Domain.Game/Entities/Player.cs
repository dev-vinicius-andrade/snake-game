using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
internal record Player(Guid Guid, string Name) : IPlayer
{
    public IPointAxis CurrentPosition { get; set; } = null!;
    public Direction CurrentDirection { get; set; } 
    public bool IsConnected { get; set; }
    public IMovementDirection Direction { get; set; } = null!;
    public IColor BackgroundColor { get; set; } = null!;
    public IColor? BorderColor { get; set; }
    public IList<IPointAxis> Path { get; set; } = null!;
    public bool IsAlive { get; set; } = true;
    public long Score { get; set; }
    public string Id { get; set; } = null!;
}
