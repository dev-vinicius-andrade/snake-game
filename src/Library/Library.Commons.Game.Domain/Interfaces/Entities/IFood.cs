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
    /// <summary>The kind of prize it is, for a prize.</summary>
    string? PrizeId { get; }
    /// <summary>The id of a perk the snake that eats it gets, for a prize that gives one.</summary>
    string? GrantsPerk { get; }
    /// <summary>What else it gives the snake that eats it: a random perk, lives, a tier surge, mass. Null: nothing.</summary>
    Library.Commons.Game.Domain.Entities.PerkReward? Reward { get; }
    
}