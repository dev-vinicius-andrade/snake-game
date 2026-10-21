using System.Text.Json;
using Application.Game.Server.Services;
using Domain.Game.Rules;

namespace Application.Game.Server.Tests;

public sealed class GameplayApplierTests
{
    private readonly PerkRules _perks = new(new PerkOptions());
    private readonly PrizeRules _prize = new(new PrizeOptions());
    private readonly RoomLifetimeSettings _lifetime = new();
    private readonly WorldEventRules _worldEvents = new(new WorldEventOptions());
    private readonly GameplayApplier _applier;

    public GameplayApplierTests()
    {
        _applier = new GameplayApplier(_perks, _prize, _lifetime, _worldEvents);
    }

    private static string Document(string revision, GameplayConfig config)
        => JsonSerializer.Serialize(new GameplayDocument(revision, config), GameplayConfig.Json);

    private static GameplayConfig Config(long prizeValue = 77, int foods = 3) => new()
    {
        Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = foods, Effect = PerkEffect.Magnet, Amount = 90 }] },
        Prize = new PrizeOptions { Value = prizeValue }
    };

    [Fact]
    public void Apply_SwitchesThePerkAndPrizeRules()
    {
        var applied = _applier.Apply(Document("r1", Config()));

        Assert.True(applied);
        Assert.Equal(77, _prize.Options.Value);
        Assert.Equal(90, _perks.EffectsFor(3).MagnetRadius);
        Assert.Equal("r1", _applier.Revision);
    }

    [Fact]
    public void Apply_APrizeKeepsTheCatalogRewardsItPicked_SoARoomCanSwitchOneOff()
    {
        var config = Config() with
        {
            Rewards = [new() { Id = "life", Name = "Life", Reward = new() { ExtraLives = 1 } }, new() { Id = "mass", Name = "Mass", Reward = new() { BonusMass = 30 } }],
            Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", RewardIds = ["life", "mass"] }] }
        };

        _applier.Apply(Document("r1", config));

        var prize = _prize.Definitions.Single();
        Assert.Equal(2, prize.PickedRewards.Count);
        Assert.Equal((1, 30L), (prize.Reward.ExtraLives, prize.Reward.BonusMass));
    }

    [Fact]
    public void Apply_TheSameRevisionAgain_ChangesNothing()
    {
        _applier.Apply(Document("r1", Config(prizeValue: 77)));

        var applied = _applier.Apply(Document("r1", Config(prizeValue: 5)));

        Assert.False(applied);
        Assert.Equal(77, _prize.Options.Value);
    }

    [Fact]
    public void Apply_ANewRevisionReplacesTheOldOne()
    {
        _applier.Apply(Document("r1", Config(prizeValue: 77)));

        Assert.True(_applier.Apply(Document("r2", Config(prizeValue: 5))));

        Assert.Equal(5, _prize.Options.Value);
        Assert.Equal("r2", _applier.Revision);
    }

    [Fact]
    public void Apply_ValuesThatCannotWork_AreRefusedAndTheRulesStay()
    {
        _applier.Apply(Document("r1", Config(prizeValue: 77)));

        var applied = _applier.Apply(Document("r2", Config(foods: 0)));

        Assert.False(applied);
        Assert.Equal(77, _prize.Options.Value);
        Assert.Equal("r1", _applier.Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void Apply_SomethingThatIsNotADocument_ChangesNothing(string? json)
    {
        Assert.False(_applier.Apply(json));
        Assert.Null(_applier.Revision);
    }

    [Fact]
    public void Apply_AfterARefusedRevision_TheSameOneIsTriedAgainLater()
    {
        _applier.Apply(Document("r2", Config(foods: 0)));

        Assert.True(_applier.Apply(Document("r2", Config())));
    }

    [Fact]
    public void Apply_SwitchesTheRoomLifetimeToo()
    {
        var config = Config() with { Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 4, MaxPrivateRoomMinutes = 120 } };

        _applier.Apply(Document("r1", config));

        Assert.Equal(TimeSpan.FromMinutes(4), _lifetime.EmptyFor);
        Assert.Equal(TimeSpan.FromMinutes(120), _lifetime.MaxAge);
    }

    [Fact]
    public void Apply_RoomLifetimeThatCannotWorkRefusesTheWholeConfig()
    {
        _applier.Apply(Document("r1", Config(prizeValue: 77)));

        var applied = _applier.Apply(Document("r2", Config(prizeValue: 5) with { Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 0 } }));

        Assert.False(applied);
        Assert.Equal(77, _prize.Options.Value);
    }

    [Fact]
    public void Apply_SwitchesTheWorldEventsToo()
    {
        var config = Config() with { WorldEvents = new WorldEventOptions { Lightning = new LightningOptions { Radius = 150 } } };

        Assert.True(_applier.Apply(Document("r1", config)));

        Assert.Equal(150, _worldEvents.Options.Lightning.Radius);
    }

    [Fact]
    public void Apply_WorldEventsThatCannotWorkRefuseTheWholeConfig()
    {
        _applier.Apply(Document("r1", Config(prizeValue: 77)));
        var bad = Config(prizeValue: 5) with { WorldEvents = new WorldEventOptions { Lightning = new LightningOptions { Radius = 0 } } };

        var applied = _applier.Apply(Document("r2", bad));

        Assert.False(applied);
        Assert.Equal(77, _prize.Options.Value);
        Assert.Equal(90, _worldEvents.Options.Lightning.Radius);
    }
}
