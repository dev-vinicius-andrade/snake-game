using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Domain.Game.Tests;

/// <summary>Rewards as a catalog that prizes, the turret, the bounty and the maze pick from, and rooms that switch single things.</summary>
public sealed class RewardCatalogTests
{
    private static RewardDefinition Reward(string id, PerkReward reward) => new() { Id = id, Name = id, Reward = reward };

    private static IRoom RoomWith(RoomGameplay gameplay, string name = "arena")
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns(name);
        room.Code.Returns("ABC123");
        room.Guid.Returns(Guid.NewGuid());
        room.Gameplay.Returns(gameplay);
        return room;
    }

    private static GameplayConfig Config(params RewardDefinition[] catalog) => GameplayConfig.Defaults with { Rewards = catalog.ToList() };

    private static GameplayConfig WithPrize(GameplayConfig config, params string[] rewardIds)
        => config with { Prize = new PrizeOptions { Prizes = [new PrizeDefinition { Id = "gift", Name = "Gift", RewardIds = rewardIds.ToList() }] } };

    [Fact]
    public void Normalized_APrizeThatPicksRewards_GivesTheCombinationOfThemAll()
    {
        var config = WithPrize(Config(Reward("life", new PerkReward { ExtraLives = 1 }), Reward("mass", new PerkReward { BonusMass = 50 })), "life", "mass").Normalized();

        var prize = config.Prize.Prizes.Single();
        Assert.Equal((1, 50L), (prize.Reward.ExtraLives, prize.Reward.BonusMass));
        Assert.Equal(2, prize.PickedRewards.Count);
    }

    [Fact]
    public void Normalized_AnEmbeddedRewardWithNoPick_MovesIntoTheCatalog_SoNothingIsLost()
    {
        var config = (GameplayConfig.Defaults with
        {
            Prize = new PrizeOptions { Prizes = [new PrizeDefinition { Id = "gift", Name = "Gift", Reward = new PerkReward { Mend = true, ExtraLives = 2 } }] },
            WorldEvents = GameplayConfig.Defaults.WorldEvents with { Bounty = new BountyOptions { Reward = new PerkReward { BonusMass = 9 } } }
        }).Normalized();

        var prize = config.Prize.Prizes.Single();
        var entry = Assert.Single(config.Rewards, r => r.Id == Assert.Single(prize.RewardIds));
        Assert.Equal(new PerkReward { Mend = true, ExtraLives = 2 }, entry.Reward);
        Assert.Equal(new PerkReward { BonusMass = 9 }, config.Rewards.Single(r => r.Id == Assert.Single(config.WorldEvents.Bounty.RewardIds)).Reward);
        Assert.Equal(2, config.Rewards.Count);
    }

    [Fact]
    public void Normalized_AnEmptyEmbeddedReward_AddsNothingToTheCatalog()
        => Assert.Empty(GameplayConfig.Defaults.Normalized().Rewards);

    [Fact]
    public void Normalized_IsStable_AskingTwiceChangesNothing()
    {
        var config = GameplayConfig.Defaults with { Prize = new PrizeOptions { Prizes = [new PrizeDefinition { Id = "gift", Name = "Gift", Reward = new PerkReward { Mend = true } }] } };

        var first = config.Normalized();
        var second = first.Normalized();

        Assert.Equal(first.Rewards, second.Rewards);
        Assert.Equal(first.Prize.Prizes[0].RewardIds, second.Prize.Prizes[0].RewardIds);
    }

    [Fact]
    public void EnsureValid_APickThatIsNotInTheCatalog_IsRefused()
        => Assert.Throws<ArgumentException>(WithPrize(Config(), "ghost").Normalized().EnsureValid);

    [Fact]
    public void EnsureValid_TheBossBountyAndMazePicksAreCheckedToo()
    {
        var baseConfig = Config(Reward("ok", new PerkReward { Mend = true })).Normalized();
        var events = baseConfig.WorldEvents;

        Assert.Throws<ArgumentException>((baseConfig with { WorldEvents = events with { Turret = events.Turret with { RewardIds = ["ghost"] } } }).EnsureValid);
        Assert.Throws<ArgumentException>((baseConfig with { WorldEvents = events with { Bounty = events.Bounty with { RewardIds = ["ghost"] } } }).EnsureValid);
        Assert.Throws<ArgumentException>((baseConfig with { WorldEvents = events with { Maze = events.Maze with { RewardIds = ["ghost"] } } }).EnsureValid);
        (baseConfig with { WorldEvents = events with { Maze = events.Maze with { RewardIds = ["ok"] } } }).EnsureValid();
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("a", "")]
    public void EnsureValid_ARewardNeedsAnIdAndAName(string id, string name)
        => Assert.Throws<ArgumentException>(Config(new RewardDefinition { Id = id, Name = name }).EnsureValid);

    [Fact]
    public void EnsureValid_TwoRewardsWithTheSameId_AreRefused()
        => Assert.Throws<ArgumentException>(Config(Reward("a", new PerkReward()), Reward("A", new PerkReward())).EnsureValid);

    [Fact]
    public void EnsureValid_ARewardOutOfRange_OrLiftingAMissingPerk_IsRefused()
    {
        Assert.Throws<ArgumentException>(Config(Reward("a", new PerkReward { ExtraLives = 99 })).EnsureValid);
        Assert.Throws<ArgumentException>(Config(Reward("a", new PerkReward { TierSurgeMin = 1, TierSurgeMax = 1, TierSurgePerk = "ghost" })).EnsureValid);
    }

    [Fact]
    public void Json_TheCatalogAndThePicksRoundTrip()
    {
        var config = WithPrize(Config(Reward("life", new PerkReward { ExtraLives = 1 })), "life").Normalized();

        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json), GameplayConfig.Json)!.Normalized();

        Assert.Equal(config.Rewards, back.Rewards);
        Assert.Equal(["life"], back.Prize.Prizes[0].RewardIds);
        Assert.Equal(1, back.Prize.Prizes[0].Reward.ExtraLives);
    }

    [Fact]
    public void RewardFor_ARoomThatSwitchedARewardOff_DoesNotGiveIt()
    {
        var prize = WithPrize(Config(Reward("life", new PerkReward { ExtraLives = 1 }), Reward("mass", new PerkReward { BonusMass = 50 })), "life", "mass").Normalized().Prize.Prizes[0];
        var room = RoomWith(new RoomGameplay().WithToggle("reward", "life", false));

        Assert.Equal(new PerkReward { BonusMass = 50 }, prize.RewardFor(room));
        Assert.Equal(1, prize.RewardFor(null).ExtraLives);
        Assert.Equal(1, prize.RewardFor(RoomWith(new RoomGameplay())).ExtraLives);
    }

    [Fact]
    public void RewardFor_WithNoPicksAtAll_IsTheEmbeddedRewardAsItWas()
        => Assert.Equal(new PerkReward { Mend = true }, new PrizeDefinition { Reward = new PerkReward { Mend = true } }.RewardFor(null));

    // Room toggles.

    [Fact]
    public void APerkSwitchedOffInARoom_DoesNotApplyThere_ButStillAppliesElsewhere()
    {
        var perk = new PerkDefinition { Id = "magnet", Name = "Magnet" };
        var off = RoomWith(new RoomGameplay().WithToggle("perk", "magnet", false));

        Assert.False(perk.AppliesTo(off));
        Assert.True(perk.AppliesTo(RoomWith(new RoomGameplay())));
        Assert.True(perk.AppliesTo(RoomWith(new RoomGameplay().WithToggle("perk", "other", false))));
    }

    [Fact]
    public void AScriptedPerk_IsOffInEveryRoomUntilARoomSwitchesItOn()
    {
        var perk = new PerkDefinition { Id = "magnet", Name = "Magnet", Script = "function onTick(){}" };

        Assert.False(perk.AppliesTo(RoomWith(new RoomGameplay())));
        Assert.True(perk.AppliesTo(RoomWith(new RoomGameplay().WithToggle("perk", "magnet", true))));
    }

    [Fact]
    public void ARoomToggle_BeatsTheOldRoomLists_ButWithoutOneTheListsStillWork()
    {
        var perk = new PerkDefinition { Id = "magnet", Name = "Magnet", Rooms = ["arena"] };

        Assert.True(perk.AppliesTo(RoomWith(new RoomGameplay())));
        Assert.False(perk.AppliesTo(RoomWith(new RoomGameplay(), name: "other")));
        Assert.True(perk.AppliesTo(RoomWith(new RoomGameplay().WithToggle("perk", "magnet", true), name: "other")));
        Assert.False(perk.AppliesTo(RoomWith(new RoomGameplay().WithToggle("perk", "magnet", false))));
    }

    [Fact]
    public void ADisabledDefinition_StaysOff_EvenWhenARoomSwitchesItOn()
        => Assert.False(new PerkDefinition { Id = "magnet", Name = "Magnet", Enabled = false }.AppliesTo(RoomWith(new RoomGameplay().WithToggle("perk", "magnet", true))));

    [Fact]
    public void APrizeAndEachWorldEvent_AreSwitchedByTheirOwnName()
    {
        Assert.False(new PrizeDefinition { Id = "gold" }.AppliesTo(RoomWith(new RoomGameplay().WithToggle("prize", "gold", false))));
        Assert.True(new PrizeDefinition { Id = "gold" }.AppliesTo(RoomWith(new RoomGameplay().WithToggle("prize", "silver", false))));

        (IScheduledEvent Event, string Name)[] scheduled =
        [
            (new FruitShowerOptions(), "fruit_shower"), (new BountyOptions(), "bounty"), (new MineFieldOptions(), "mine_field"),
            (new SugarRushOptions(), "sugar_rush"), (new MazeOptions(), "maze")
        ];
        foreach (var (options, name) in scheduled)
        {
            Assert.False(options.AppliesTo(RoomWith(new RoomGameplay().WithToggle("event", name, false))));
            Assert.True(options.AppliesTo(RoomWith(new RoomGameplay())));
        }
        IRoom Off(string name) => RoomWith(new RoomGameplay().WithToggle("event", name, false));
        Assert.False(new LightningOptions().AppliesTo(Off("lightning")));
        Assert.False(new MeteorOptions().AppliesTo(Off("meteors")));
        Assert.False(new ZoneOptions().AppliesTo(Off("zone")));
        Assert.False(new TurretOptions().AppliesTo(Off("turret")));
        Assert.True(new TurretOptions().AppliesTo(Off("zone")));
    }
}
