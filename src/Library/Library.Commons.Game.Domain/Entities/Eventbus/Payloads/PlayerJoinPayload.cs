namespace Library.Commons.Game.Domain.Entities.Eventbus.Payloads;

public class PlayerJoinPayload
{
    public string Id { get; set; } = null!;
    public string Nickname { get; set; } = null!;
    public Guid? RoomId { get; set; }
    /// <summary>The account of a signed-in player (the manager checked its token); null for a guest. <see cref="Nickname"/> is then its display name.</summary>
    public Guid? AccountId { get; set; }
}