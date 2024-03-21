using Library.Commons.Game.Domain.Enums;

namespace Domain.Game.Extensions;

public static class DirectionsExtensions
{
    public static Direction ToDirectionsEnum(this int value) => (Direction)value;
}