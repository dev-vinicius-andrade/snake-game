using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
internal record Player(Guid Guid, string Name) : IPlayer
{
    public IPointAxis CurrentPosition { get; set; } = null!;
    public Direction CurrentDirection { get; set; } 
    public bool IsConnected { get; set; }
    public IMovementDirection Direction { get; set; } = null!;
    public IColor BackgroundColor { get; set; } = null!;
    public IColor? BorderColor { get; set; }
    public IList<IPointAxis> Path { get; set; } = null!;
    [JsonIgnore]
    public IList<IPointAxis> Trail { get; set; } = new List<IPointAxis>();
    public bool IsAlive { get; set; } = true;
    public long Score { get; set; }
    public IList<double> SegmentHealth { get; set; } = new List<double>();
    public int FoodsEaten { get; set; }
    public IList<string> Perks { get; set; } = new List<string>();
    public bool AutoFire { get; set; }
    [JsonIgnore]
    public int BurstRemaining { get; set; }
    [JsonIgnore]
    public int BurstTicks { get; set; }
    public bool IsBoosting { get; set; }
    public int BoostTicks { get; set; }
    public int ShotCooldownTicks { get; set; }
    public double Heading { get; set; }
    public double TargetHeading { get; set; }
    public string Id { get; set; } = null!;
}
