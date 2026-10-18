using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record Food(Guid Guid) :IFood
{
    public IPointAxis Position { get; set; } = null!;
    public IColor Color { get; set; } = null!;
    public long Value { get; init; } = 1;
    public FoodKind Kind { get; init; } = FoodKind.Normal;
    public int FoodsCredit { get; init; } = 1;
}