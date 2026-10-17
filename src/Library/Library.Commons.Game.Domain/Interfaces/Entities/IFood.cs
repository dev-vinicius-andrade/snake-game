namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IFood:ITrackableId
{
    IPointAxis Position { get; set; }
    IColor Color { get; set; }
    /// <summary>Mass the snake gains by eating it.</summary>
    long Value { get; }
    
}