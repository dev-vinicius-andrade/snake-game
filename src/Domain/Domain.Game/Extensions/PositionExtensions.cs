using Domain.Game.Helpers;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Extensions;

public static class PositionExtensions
{
    public static IList<T> GetNearBy<T>(this IList<T> positions, IPointAxis position, int delta = 0)
        where T : IPointAxis
        =>
            positions.Where(p =>
            {
                var xPositionCompare = CalculationsHelper.Distance(p.X, position.X) <=
                                       delta;
                var yPositionCompare = CalculationsHelper.Distance(p.Y, position.Y) <=
                                       delta;
                return xPositionCompare && yPositionCompare;
            }).ToList();
}