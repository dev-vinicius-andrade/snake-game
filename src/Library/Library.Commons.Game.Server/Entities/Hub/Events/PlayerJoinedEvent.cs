using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record PlayerJoinedEvent
{
    public const string EventName = "PlayerJoined";
    public IPlayer Player { get; set; } = null!;
}