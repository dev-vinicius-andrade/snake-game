namespace Library.Commons.Game.Domain.Entities.Eventbus.Payloads;

/// <summary>Asks a game server to make a private room. <see cref="RequesterId"/> is the connection to the manager to answer.</summary>
public class RoomCreatePayload
{
    public string RequesterId { get; set; } = null!;
    public string? Name { get; set; }
    public string Code { get; set; } = null!;
    public int MaxPlayers { get; set; }
}
