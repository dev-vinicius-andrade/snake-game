using Library.Commons.Game.Domain.Entities;
using System.Text.Json;
using Application.Manager.Api.Persistence;
using Domain.Game.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class EfGameplayConfigStoreTests : IDisposable
{
    private readonly PersistenceTestHost _host = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _host.Dispose();

    private EfGameplayConfigStore Store(Func<GameplayConfig>? seed = null) => new(_host.Factory, _time, seed);

    private static string Written(GameplayConfig config) => JsonSerializer.Serialize(config, GameplayConfig.Json);

    private static GameplayConfig Custom() => GameplayConfig.Defaults with
    {
        Perks = GameplayConfig.Defaults.Perks with
        {
            ShotSpreadDegrees = 9,
            BurstIntervalMilliseconds = 70,
            KillFoodsCredit = 4,
            ExtraLifeKeepShare = 0.25,
            ExtraLifeProtectSeconds = 7,
            Perks =
            [
                new PerkDefinition
                {
                    Id = "scripted", Name = "Scripted", FoodsRequired = 7, Icon = "mdi-shield", Description = "Does a thing.",
                    Script = "function onTick(ctx) { }",
                    Effects = [new PerkEffectEntry { Effect = PerkEffect.Armor, Amount = 0.2 }, new PerkEffectEntry { Effect = PerkEffect.Magnet, Amount = 40 }],
                    Tiers = [new PerkTier { FoodsRequired = 20, Multiplier = 1.5 }, new PerkTier { FoodsRequired = 40, Multiplier = 2 }],
                    Rooms = ["lobby", "AB12"], ExcludedRooms = ["quiet"],
                },
                new PerkDefinition
                {
                    Id = "stormproof", Name = "Stormproof", FoodsRequired = 11, CooldownSeconds = 30, DurationSeconds = 4,
                    Effects =
                    [
                        new PerkEffectEntry { Effect = PerkEffect.EventResistance, Amount = 0.5, Target = "zone" },
                        new PerkEffectEntry { Effect = PerkEffect.EventResistance, Amount = 0.2 },
                        new PerkEffectEntry { Effect = PerkEffect.ShieldBurst, Amount = 1 },
                        new PerkEffectEntry { Effect = PerkEffect.ExtraLife, Amount = 2 },
                    ],
                },
                new PerkDefinition { Id = "plain", Name = "Plain", FoodsRequired = 3, Enabled = false, Effects = [new PerkEffectEntry { Effect = PerkEffect.AutoFire, Amount = 1 }] },
            ],
        },
        Prize = new PrizeOptions
        {
            Enabled = false,
            Prizes = [new PrizeDefinition { Id = "gold", Name = "Gold", Value = 55, FoodsCredit = 9, MinPlayers = 3, GrantsPerk = "scripted", Color = "#112233", Rooms = ["lobby"], ExcludedRooms = ["quiet", "other"],
                Reward = new PerkReward { GrantsRandomPerk = true, ExtraLives = 2, TierSurgeMin = 1, TierSurgeMax = 3, TierSurgePerk = "scripted", BonusMass = 90, Mend = true } }],
        },
        Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 15, MaxPrivateRoomMinutes = 120 },
        WorldEvents = new WorldEventOptions
        {
            Enabled = false,
            Lightning = new LightningOptions { Enabled = false, MinIntervalSeconds = 50, MaxIntervalSeconds = 70, Radius = 80, TelegraphMilliseconds = 1200, MinStrikes = 3, MaxStrikes = 9, StormSeconds = 5, AimedShare = 0.4, Rooms = ["lobby"] },
            Meteors = new MeteorOptions { MinMeteors = 2, MaxMeteors = 4, RainSeconds = 3, ExcludedRooms = ["quiet"] },
            Turret = new TurretOptions
            {
                HitPoints = 77, Arms = 8, GrantsPerk = "scripted", Rooms = ["boss-room"],
                Reward = new PerkReward { ExtraLives = 1, TierSurgeMin = 2, TierSurgeMax = 2 },
            },
            Zone = new ZoneOptions { FinalRadius = 111, DamageMass = 9, ExcludedRooms = ["quiet"] },
            FruitShower = new FruitShowerOptions
            {
                Enabled = false, MinIntervalSeconds = 11, MaxIntervalSeconds = 22, DurationSeconds = 33, FruitsPerSecond = 4, MinPlayers = 2,
                Rooms = ["lobby", "AB12"], ExcludedRooms = ["quiet"],
            },
            Bounty = new BountyOptions
            {
                Enabled = false, MinIntervalSeconds = 12, MaxIntervalSeconds = 24, DurationSeconds = 36, RewardMass = 4321, FoodsCredit = 17, MinPlayers = 5,
                Reward = new PerkReward { GrantsRandomPerk = true, Mend = true, BonusMass = 12 },
                Rooms = ["arena"], ExcludedRooms = ["quiet", "other"],
            },
            Mines = new MineFieldOptions
            {
                Enabled = false, MinIntervalSeconds = 13, MaxIntervalSeconds = 26, MinMines = 3, MaxMines = 5, Radius = 77, TriggerRadius = 15,
                ArmMilliseconds = 1500, MinPlayers = 4, Rooms = ["mine-room"], ExcludedRooms = ["lobby"],
            },
            SugarRush = new SugarRushOptions
            {
                Enabled = false, MinIntervalSeconds = 14, MaxIntervalSeconds = 28, DurationSeconds = 42, MinPlayers = 6,
                Rooms = ["sweet", "AB12"], ExcludedRooms = ["sour"],
            },
            Maze = new MazeOptions
            {
                Enabled = false, MinIntervalSeconds = 15, MaxIntervalSeconds = 30, DurationSeconds = 77, Columns = 9, Rows = 7, CorridorHeads = 5,
                Twistiness = 33, WallMassLoss = 6, FoodPercent = 40, MinPlayers = 3, PrizeMass = 555, PrizeFoodsCredit = 31, PrizeGrantsPerk = "scripted",
                PrizeColor = "#abcdef", Rooms = ["maze-room", "AB12"], ExcludedRooms = ["lobby"],
                PrizeReward = new PerkReward { ExtraLives = 3, TierSurgeMin = 1, TierSurgeMax = 1 },
            },
        },
    };

    [Fact]
    public void Current_OnAnEmptyDatabase_SeedsTheDefaults_AndKeepsThemInTables()
    {
        var store = Store();

        var current = store.Current;

        Assert.Equal(Written(GameplayConfig.Defaults), Written(current.Config));
        Assert.False(string.IsNullOrEmpty(current.Revision));
        using var context = _host.Factory.CreateDbContext();
        Assert.Equal(PerkOptions.BuiltIn.Count, context.Perks.Count());
        Assert.Equal(PerkOptions.BuiltIn.Sum(perk => perk.AllEffects().Count), context.PerkEffects.Count());
        Assert.Single(context.GameplaySettings);
        Assert.Single(context.Bosses);
    }

    [Fact]
    public void Update_TheNewFieldsAreReallyInTheTables()
    {
        Store().Update(Custom());

        using var context = _host.Factory.CreateDbContext();
        var settings = context.GameplaySettings.Single();
        Assert.Equal((0.25, 7), (settings.ExtraLifeKeepShare, settings.ExtraLifeProtectSeconds));
        var perk = context.Perks.Single(row => row.Id == "stormproof");
        Assert.Equal((30, 4), (perk.CooldownSeconds, perk.DurationSeconds));
        Assert.Equal(["zone", null], context.PerkEffects.Where(row => row.PerkId == "stormproof").OrderBy(row => row.Position).Take(2).Select(row => row.Target).ToArray());
        var prize = context.Prizes.Single();
        Assert.Equal((true, 2, 1, 3, "scripted", 90L, true),
            (prize.RewardRandomPerk, prize.RewardExtraLives, prize.RewardTierSurgeMin, prize.RewardTierSurgeMax, prize.RewardTierSurgePerk, prize.RewardBonusMass, prize.RewardMend));
        Assert.Equal(1, context.Bosses.Single().RewardExtraLives);
        Assert.Equal(12L, context.BountyEvents.Single().RewardBonusMass);
        Assert.Equal(3, context.MazeEvents.Single().RewardExtraLives);
    }

    [Fact]
    public void Update_ARewardLeftOff_IsStoredAsNothing_AndReadsBackAsNothing()
    {
        Store().Update(GameplayConfig.Defaults);

        var back = Store().Current.Config;

        Assert.True(back.Prize.Prizes.All(prize => prize.Reward.IsEmpty));
        Assert.True(back.WorldEvents.Turret.Reward.IsEmpty);
        Assert.True(back.WorldEvents.Bounty.Reward.IsEmpty);
        Assert.True(back.WorldEvents.Maze.PrizeReward.IsEmpty);
    }

    [Fact]
    public void Current_SeedsFromTheSeedGivenAndNotTheDefaults_Once()
    {
        var seed = Custom();

        var first = Store(() => seed).Current;
        var second = Store(() => GameplayConfig.Defaults).Current;

        Assert.Equal(Written(seed.Normalized()), Written(first.Config));
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(Written(first.Config), Written(second.Config));
    }

    [Fact]
    public void Update_AConfigWithEverythingSet_ComesBackExactlyFromTheTables()
    {
        var store = Store();

        var saved = store.Update(Custom());

        var fresh = Store().Current;
        Assert.Equal(Written(Custom().Normalized()), Written(fresh.Config));
        Assert.Equal(saved.Revision, fresh.Revision);
    }

    [Fact]
    public void Current_AConfigSavedBeforeTheNewEvents_LoadsWithTheirDefaults_AndTheNextSaveWritesTheirRows()
    {
        var saved = Store().Update(Custom());
        using (var context = _host.Factory.CreateDbContext())
        {
            context.FruitShowerEvents.ExecuteDelete();
            context.BountyEvents.ExecuteDelete();
            context.MineFieldEvents.ExecuteDelete();
            context.SugarRushEvents.ExecuteDelete();
            context.WorldEventRooms.Where(row => row.Event == "fruit_shower" || row.Event == "bounty" || row.Event == "mine_field" || row.Event == "sugar_rush")
                .ExecuteDelete();
        }

        var loaded = Store().Current;

        Assert.Equal(saved.Revision, loaded.Revision);
        var events = loaded.Config.WorldEvents;
        Assert.Equal(Written(GameplayConfig.Defaults with { WorldEvents = new WorldEventOptions() }), Written(GameplayConfig.Defaults with
        {
            WorldEvents = new WorldEventOptions { FruitShower = events.FruitShower, Bounty = events.Bounty, Mines = events.Mines, SugarRush = events.SugarRush },
        }));
        Assert.Equal(111, events.Zone.FinalRadius);

        Store().Update(Custom());

        using var after = _host.Factory.CreateDbContext();
        Assert.Single(after.FruitShowerEvents);
        Assert.Single(after.BountyEvents);
        Assert.Single(after.MineFieldEvents);
        Assert.Single(after.SugarRushEvents);
        Assert.Equal(Written(Custom().Normalized()), Written(Store().Current.Config));
    }

    [Fact]
    public void Current_AConfigSavedBeforeTheMaze_LoadsWithTheMazeDefaults_AndTheNextSaveWritesItsRow()
    {
        var saved = Store().Update(Custom());
        using (var context = _host.Factory.CreateDbContext())
        {
            context.MazeEvents.ExecuteDelete();
            context.WorldEventRooms.Where(row => row.Event == "maze").ExecuteDelete();
        }

        var loaded = Store().Current;

        Assert.Equal(saved.Revision, loaded.Revision);
        Assert.Equal(JsonSerializer.Serialize(new MazeOptions(), GameplayConfig.Json), JsonSerializer.Serialize(loaded.Config.WorldEvents.Maze, GameplayConfig.Json));
        Assert.Equal(111, loaded.Config.WorldEvents.Zone.FinalRadius);

        Store().Update(Custom());

        using var after = _host.Factory.CreateDbContext();
        Assert.Single(after.MazeEvents);
        Assert.Equal(Written(Custom().Normalized()), Written(Store().Current.Config));
    }

    [Fact]
    public void Update_TheMazeComesBackWithEveryFieldAndItsRooms()
    {
        Store().Update(Custom());

        var maze = Store().Current.Config.WorldEvents.Maze;

        Assert.Equal((false, 15, 30, 77), (maze.Enabled, maze.MinIntervalSeconds, maze.MaxIntervalSeconds, maze.DurationSeconds));
        Assert.Equal((9, 7, 5, 33), (maze.Columns, maze.Rows, maze.CorridorHeads, maze.Twistiness));
        Assert.Equal((6L, 40, 3), (maze.WallMassLoss, maze.FoodPercent, maze.MinPlayers));
        Assert.Equal((555L, 31, "scripted", "#abcdef"), (maze.PrizeMass, maze.PrizeFoodsCredit, maze.PrizeGrantsPerk, maze.PrizeColor));
        Assert.Equal(["maze-room", "AB12"], maze.Rooms);
        Assert.Equal(["lobby"], maze.ExcludedRooms);
    }

    [Fact]
    public void Update_AMazePrizeThatGivesAPerkThatDoesNotExist_IsRefusedAndNothingChanges()
    {
        var store = Store();
        var before = store.Current;
        var config = Custom() with { WorldEvents = Custom().WorldEvents with { Maze = new MazeOptions { PrizeGrantsPerk = "missing" } } };

        Assert.ThrowsAny<ArgumentException>(() => store.Update(config));

        Assert.Equal(before.Revision, Store().Current.Revision);
    }

    [Fact]
    public void Update_GivesANewRevision_AndWhatIsServedChanges()
    {
        var store = Store();
        var before = store.Current.Revision;

        var saved = store.Update(Custom());

        Assert.NotEqual(before, saved.Revision);
        Assert.Equal(saved.Revision, store.Current.Revision);
        Assert.Equal(77, store.Current.Config.WorldEvents.Turret.HitPoints);
    }

    [Fact]
    public void Update_ARemovedPerk_LeavesNoRowsBehind()
    {
        var store = Store();
        store.Update(Custom());

        store.Update(Custom() with { Perks = Custom().Perks with { Perks = [Custom().Perks.Perks.Single(perk => perk.Id == "plain")] }, Prize = new PrizeOptions(), WorldEvents = new WorldEventOptions() });

        using var context = _host.Factory.CreateDbContext();
        Assert.Equal(new[] { "plain" }, context.Perks.Select(perk => perk.Id));
        Assert.DoesNotContain(context.PerkEffects, effect => effect.PerkId == "scripted");
        Assert.Empty(context.PerkTiers);
        Assert.Empty(context.PerkRooms);
    }

    [Fact]
    public void Update_AConfigThatCannotRun_IsRefusedAndNothingChanges()
    {
        var store = Store();
        var before = store.Current;

        Assert.ThrowsAny<ArgumentException>(() => store.Update(Custom() with { Prize = new PrizeOptions { Prizes = [new PrizeDefinition { Id = "x", Name = "X", GrantsPerk = "missing" }] } }));

        Assert.Equal(before.Revision, Store().Current.Revision);
        Assert.Equal(Written(before.Config), Written(Store().Current.Config));
    }

    [Fact]
    public void Update_APrizeAndTheBossCanOnlyGiveAPerkThatIsInTheTable()
    {
        Store().Update(Custom());
        using var context = _host.Factory.CreateDbContext();

        context.Prizes.Single().GrantsPerk = "not-a-perk";

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Update_TheMazePrizeCanOnlyGiveAPerkThatIsInTheTable()
    {
        Store().Update(Custom());
        using var context = _host.Factory.CreateDbContext();

        context.MazeEvents.Single().PrizeGrantsPerk = "not-a-perk";

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Current_AnotherStoreSeesAChangeAfterTheRefreshTime_AndNotBefore()
    {
        var first = Store();
        var second = Store();
        var seen = second.Current.Revision;

        var saved = first.Update(Custom());

        Assert.Equal(seen, second.Current.Revision);
        _time.Advance(EfGameplayConfigStore.RefreshEvery + TimeSpan.FromSeconds(1));
        Assert.Equal(saved.Revision, second.Current.Revision);
        Assert.Equal(77, second.Current.Config.WorldEvents.Turret.HitPoints);
    }

    [Fact]
    public void Current_IsTheSameObjectUntilSomethingChanges()
    {
        var store = Store();

        Assert.Same(store.Current, store.Current);
    }
}
