using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record Food(Guid Guid) :IFood
{
    public IPointAxis Position { get; set; } = null!;
    public IColor Color { get; set; } = null!;
}