using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
internal record Room(Guid Guid) :IRoom
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public bool IsPrivate { get; set; }
    public int MaxPlayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public RoomGameplay Gameplay { get; set; } = new();
}