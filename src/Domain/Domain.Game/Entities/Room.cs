using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
internal record Room(Guid Guid) :IRoom
{
    public int Width { get; set; }
    public int Height { get; set; }
}