using System.Text.Json;
using Application.Game.Server.Services;

namespace Application.Game.Server.Tests;

public sealed class GameJsonOptionsTests
{
    private static readonly JsonSerializerOptions Options = GameJsonOptions.Create();

    [Fact]
    public void Create_IsNotIndented_SoSnapshotsStaySmall()
    {
        var json = JsonSerializer.Serialize(new { name = "a", path = new[] { new { x = 1.0, y = 2.0 } } }, Options);

        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain("  ", json);
    }

    [Fact]
    public void Create_RoundsNumbersToTwoDecimals()
    {
        var json = JsonSerializer.Serialize(new { x = 503.7500000000123, y = 0.123456789, z = 7.0 }, Options);

        Assert.Equal("{\"x\":503.75,\"y\":0.12,\"z\":7}", json);
    }

    [Fact]
    public void Create_KeepsWholeNumbersAndSmallNegativesSensible()
    {
        Assert.Equal("[-1.5,0,100]", JsonSerializer.Serialize(new[] { -1.5, 0.0, 100.0 }, Options));
    }

    [Fact]
    public void Create_StillReadsDoublesBack()
    {
        Assert.Equal(12.5, JsonSerializer.Deserialize<double>("12.5", Options));
    }

    [Fact]
    public void Create_KeepsCamelCaseNamesEnumStringsAndSkipsNulls()
    {
        var json = JsonSerializer.Serialize(new { FirstName = "a", Gone = (string?)null, Kind = DayOfWeek.Monday }, Options);

        Assert.Equal("{\"firstName\":\"a\",\"kind\":\"monday\"}", json);
    }
}
