namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IPointAxis
{
    double X { get; set; }
    double Y { get; set; }
    double? Z { get; set; }
    double? Angle { get; set; }
}