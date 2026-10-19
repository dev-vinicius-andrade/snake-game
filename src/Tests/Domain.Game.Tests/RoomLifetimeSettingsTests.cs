using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class RoomLifetimeSettingsTests
{
    [Fact]
    public void Defaults_AnEmptyPrivateRoomGoesAfterTenMinutesAndThereIsNoOtherLimit()
    {
        var settings = new RoomLifetimeSettings(new RoomLifetimeOptions());

        Assert.Equal(TimeSpan.FromMinutes(10), settings.EmptyFor);
        Assert.Null(settings.MaxAge);
    }

    [Fact]
    public void MaxAge_IsSetWhenAskedFor()
    {
        var settings = new RoomLifetimeSettings(new RoomLifetimeOptions { MaxPrivateRoomMinutes = 90 });

        Assert.Equal(TimeSpan.FromMinutes(90), settings.MaxAge);
    }

    [Fact]
    public void Reconfigure_ChangesTheLimitsFromNowOn()
    {
        var settings = new RoomLifetimeSettings(new RoomLifetimeOptions());

        settings.Reconfigure(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 3, MaxPrivateRoomMinutes = 60 });

        Assert.Equal(TimeSpan.FromMinutes(3), settings.EmptyFor);
        Assert.Equal(TimeSpan.FromMinutes(60), settings.MaxAge);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(20000, 0)]
    [InlineData(10, -5)]
    [InlineData(10, 99999)]
    public void Reconfigure_RefusesValuesThatCannotWork_AndKeepsTheOldOnes(int emptyMinutes, int maxMinutes)
    {
        var settings = new RoomLifetimeSettings(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 7 });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.Reconfigure(new RoomLifetimeOptions { EmptyPrivateRoomMinutes = emptyMinutes, MaxPrivateRoomMinutes = maxMinutes }));

        Assert.Equal(TimeSpan.FromMinutes(7), settings.EmptyFor);
    }

    [Fact]
    public void GameplayConfig_CarriesTheRoomSettingsAndChecksThem()
    {
        var bad = new GameplayConfig { Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 0 } };

        Assert.Throws<ArgumentOutOfRangeException>(bad.EnsureValid);
        Assert.Equal(10, GameplayConfig.Defaults.Rooms.EmptyPrivateRoomMinutes);
    }
}
