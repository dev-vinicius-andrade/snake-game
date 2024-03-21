namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IFood:ITrackableId
{
    IPointAxis Position { get; set; }
    IColor Color { get; set; }
    
}