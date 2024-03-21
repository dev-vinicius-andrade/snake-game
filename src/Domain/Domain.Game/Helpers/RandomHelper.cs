using Domain.Game.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Helpers;

public static class RandomHelper
{
    public static IPointAxis RandomPosition(int xMinValue, int xMaxValue, int yMinValue, int yMaxValue)
    {
        var randomizer = new Random();
        return new PointAxis
        {

            X = randomizer.Next(xMinValue, xMaxValue),
            Y = randomizer.Next(yMinValue, yMaxValue),
            Z = 0,
        };
    }
    public static int RandomNumber(int minimunValue, int maximunValue) =>
        new Random().Next(minimunValue, maximunValue);

    public static string RandomColor() =>
        $"#{RandomNumber(0x1000000):X6}";

    public static int RandomNumber(int? maxValue = null) =>  maxValue == null
        ? new Random().Next()
        : new Random().Next(maxValue.Value);
}