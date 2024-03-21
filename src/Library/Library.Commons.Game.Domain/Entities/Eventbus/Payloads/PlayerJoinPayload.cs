namespace Library.Commons.Game.Domain.Entities.Eventbus.Payloads;

public class PlayerJoinPayload
{
    public string Id { get; set; } = null!;
    public string Nickname { get; set; } = null!;
    public Guid? RoomId { get; set; }
}