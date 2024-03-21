using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record FoodGeneratedEvent
{
    public IFood Food { get; init; }
    public int Count { get; set; }
}