using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IFood:ITrackableId
{
    IPointAxis Position { get; set; }
    IColor Color { get; set; }
    /// <summary>Mass the snake gains by eating it.</summary>
    long Value { get; }
    /// <summary>Ordinary food, or the prize snakes fight over.</summary>
    FoodKind Kind { get; }
    /// <summary>How many foods eating this counts for, toward perks.</summary>
    int FoodsCredit { get; }
    
}