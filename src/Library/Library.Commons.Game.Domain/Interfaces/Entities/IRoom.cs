namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IRoom : ITrackableId
{
    int Width { get; set; }
    int Height { get; set; }
}