using Domain.Game.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

internal class PointAxisServices: IPointAxisServices
{
    public IPointAxis Create(int x, int y, int z=0,double? angle=null)
    {
        return new PointAxis
        {
            X = x,
            Y = y,
            Z = z,
            Angle = angle
        };
    }

    public IPointAxis CreateWith(IPointAxis pointAxis)
    {
        return new PointAxis
        {
            X = pointAxis.X,
            Y = pointAxis.Y,
            Z = pointAxis.Z,
            Angle = pointAxis.Angle
        };
    }

    public double WrapPosition(double position, double boundary, double offset)
    {
        if (position < 0)
            return boundary - offset;
        if (position >= boundary)
            return 0;
        return position;
    }
}