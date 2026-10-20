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
                new PerkDefinition { Id = "plain", Name = "Plain", FoodsRequired = 3, Enabled = false, Effects = [new PerkEffectEntry { Effect = PerkEffect.AutoFire, Amount = 1 }] },
            ],
        },
        Prize = new PrizeOptions
        {
            Enabled = false,
            Prizes = [new PrizeDefinition { Id = "gold", Name = "Gold", Value = 55, FoodsCredit = 9, MinPlayers = 3, GrantsPerk = "scripted", Color = "#112233", Rooms = ["lobby"], ExcludedRooms = ["quiet", "other"] }],
        },
        Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = 15, MaxPrivateRoomMinutes = 120 },
        WorldEvents = new WorldEventOptions
        {
            Enabled = false,
            Lightning = new LightningOptions { Enabled = false, MinIntervalSeconds = 50, MaxIntervalSeconds = 70, Radius = 80, TelegraphMilliseconds = 1200, MinStrikes = 3, MaxStrikes = 9, StormSeconds = 5, AimedShare = 0.4, Rooms = ["lobby"] },
            Meteors = new MeteorOptions { MinMeteors = 2, MaxMeteors = 4, RainSeconds = 3, ExcludedRooms = ["quiet"] },
            Turret = new TurretOptions { HitPoints = 77, Arms = 8, GrantsPerk = "scripted", Rooms = ["boss-room"] },
            Zone = new ZoneOptions { FinalRadius = 111, DamageMass = 9, ExcludedRooms = ["quiet"] },
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

        store.Update(Custom() with { Perks = Custom().Perks with { Perks = [Custom().Perks.Perks[1]] }, Prize = new PrizeOptions(), WorldEvents = new WorldEventOptions() });

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
