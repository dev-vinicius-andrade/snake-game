using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPointAxisServices
{
    IPointAxis Create(int x, int y, int z = 0, double? angle = null);
    IPointAxis CreateWith(IPointAxis pointAxis);
    double WrapPosition(double position, double boundary, double offset);
}