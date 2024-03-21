namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record RoomDetailsUpdatedEvent
{
    public int Width { get; set; }
    public int Height { get; set; }
}