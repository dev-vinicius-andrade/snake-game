using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
internal record PointAxis : IPointAxis
{
    public double X { get; set; }
    public double Y { get; set; }
    public double? Z { get; set; }
    public double? Angle { get; set; }
}

