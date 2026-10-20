using Application.Manager.Api.Gameplay;
using Domain.Game.Rules;

namespace Application.Manager.Api.Tests;

public sealed class GameplayConfigStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-gameplay-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string StorePath => Path.Combine(_folder, "gameplay.json");

    private static GameplayConfig Config(int perkFoods = 5, long prizeValue = 20) => new()
    {
        Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = perkFoods, Effect = PerkEffect.Magnet, Amount = 50 }] },
        Prize = new PrizeOptions { Value = prizeValue }
    };

    [Fact]
    public void Current_StartsWithTheDefaultsAndARevision()
    {
        var store = new GameplayConfigStore();

        Assert.Equal(PerkOptions.BuiltIn.Count, store.Current.Config.Perks.Perks.Count);
        Assert.False(string.IsNullOrEmpty(store.Current.Revision));
    }

    [Fact]
    public void Update_ChangesWhatIsServedAndGivesANewRevision()
    {
        var store = new GameplayConfigStore();
        var before = store.Current.Revision;

        var saved = store.Update(Config(prizeValue: 99));

        Assert.Equal(99, store.Current.Config.Prize.Value);
        Assert.NotEqual(before, saved.Revision);
        Assert.Equal(saved, store.Current);
    }

    [Fact]
    public void Update_WithAnEmptyPerkListSavesTheBuiltInOnes()
    {
        var store = new GameplayConfigStore();

        store.Update(new GameplayConfig());

        Assert.Equal(PerkOptions.BuiltIn.Count, store.Current.Config.Perks.Perks.Count);
    }

    [Fact]
    public void Update_WithBadValues_ThrowsAndKeepsTheOldConfig()
    {
        var store = new GameplayConfigStore();
        store.Update(Config(prizeValue: 30));
        var before = store.Current;

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(Config(perkFoods: 0)));

        Assert.Equal(before, store.Current);
    }

    [Fact]
    public void Update_IsKeptOnDiskAndComesBackAfterARestart()
    {
        var first = new GameplayConfigStore(StorePath);
        var saved = first.Update(Config(prizeValue: 77));

        var second = new GameplayConfigStore(StorePath);

        Assert.Equal(77, second.Current.Config.Prize.Value);
        Assert.Equal(saved.Revision, second.Current.Revision);
        Assert.Equal(PerkEffect.Magnet, second.Current.Config.Perks.Perks.Single().Effects.Single().Effect);
    }

    [Fact]
    public void Constructor_ACorruptFileFallsBackToTheDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, "{ this is not json");

        var store = new GameplayConfigStore(StorePath);

        Assert.Equal(PerkOptions.BuiltIn.Count, store.Current.Config.Perks.Perks.Count);
    }

    [Fact]
    public void Constructor_AFileWithValuesThatCannotWorkFallsBackToTheDefaults()
    {
        var first = new GameplayConfigStore(StorePath);
        first.Update(Config());
        File.WriteAllText(StorePath, File.ReadAllText(StorePath).Replace("\"foodsRequired\": 5", "\"foodsRequired\": 0"));

        var store = new GameplayConfigStore(StorePath);

        Assert.Equal(PerkOptions.BuiltIn.Count, store.Current.Config.Perks.Perks.Count);
    }

    [Fact]
    public void Update_WhenTheFileCannotBeWritten_StillKeepsTheChangeInMemory()
    {
        Directory.CreateDirectory(_folder);
        var blocked = Path.Combine(_folder, "blocked");
        Directory.CreateDirectory(blocked);
        var store = new GameplayConfigStore(blocked);

        store.Update(Config(prizeValue: 55));

        Assert.Equal(55, store.Current.Config.Prize.Value);
    }

    private static GameplayConfig WithScript(string script, params string[] rooms) => new()
    {
        Perks = new PerkOptions
        {
            Perks = [new() { Id = "scripted", Name = "Scripted", FoodsRequired = 5, Script = script, Rooms = rooms.ToList() }]
        }
    };

    [Fact]
    public void Update_AcceptsAScriptedPerkAndKeepsItsRoomsAndScript()
    {
        var store = new GameplayConfigStore();

        store.Update(WithScript("function modify() { return { extraShots: 1 }; }", "ABC234"));

        var perk = store.Current.Config.Perks.Perks.Single();
        Assert.Equal(new[] { "ABC234" }, perk.Rooms);
        Assert.Contains("extraShots", perk.Script);
        Assert.Empty(perk.Effects);
    }

    [Fact]
    public void Update_ABrokenScriptIsRefusedAndTheOldConfigStays()
    {
        var store = new GameplayConfigStore();
        var before = store.Current;

        var failure = Assert.Throws<ArgumentException>(() => store.Update(WithScript("function modify( {", "ABC234")));

        Assert.Contains("scripted", failure.Message);
        Assert.Equal(before, store.Current);
    }

    [Fact]
    public void Update_AScriptedPerkSurvivesARestart()
    {
        var first = new GameplayConfigStore(StorePath);
        first.Update(WithScript("function modify() { return { extraShots: 2 }; }", "ABC234"));

        var second = new GameplayConfigStore(StorePath);

        var perk = second.Current.Config.Perks.Perks.Single();
        Assert.Equal(new[] { "ABC234" }, perk.Rooms);
        Assert.Contains("extraShots: 2", perk.Script);
    }

    [Fact]
    public void Update_TheNewWorldEventsWithBadValuesAreRefused_AndTheOldConfigStays()
    {
        var store = new GameplayConfigStore();
        store.Update(Config());
        var before = store.Current;

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(Config() with
        {
            WorldEvents = new WorldEventOptions { Mines = new MineFieldOptions { MinMines = 5, MaxMines = 2 } }
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(Config() with
        {
            WorldEvents = new WorldEventOptions { SugarRush = new SugarRushOptions { DurationSeconds = 0 } }
        }));

        Assert.Equal(before, store.Current);
    }

    [Fact]
    public void Update_TheNewWorldEventsAreKeptOnDiskAndComeBackAfterARestart()
    {
        var first = new GameplayConfigStore(StorePath);
        first.Update(Config() with
        {
            WorldEvents = new WorldEventOptions
            {
                FruitShower = new FruitShowerOptions { FruitsPerSecond = 15 },
                Bounty = new BountyOptions { RewardMass = 400, Rooms = ["arena"] },
                Mines = new MineFieldOptions { MaxMines = 20 },
                SugarRush = new SugarRushOptions { DurationSeconds = 45, Enabled = false }
            }
        });

        var events = new GameplayConfigStore(StorePath).Current.Config.WorldEvents;

        Assert.Equal(15, events.FruitShower.FruitsPerSecond);
        Assert.Equal(400L, events.Bounty.RewardMass);
        Assert.Equal(["arena"], events.Bounty.Rooms);
        Assert.Equal(20, events.Mines.MaxMines);
        Assert.Equal((45, false), (events.SugarRush.DurationSeconds, events.SugarRush.Enabled));
    }

    [Fact]
    public void Load_AFileFromBeforeTheNewEventsGivesThemTheirDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, "{\"revision\":\"abc\",\"config\":{\"worldEvents\":{\"enabled\":true,\"zone\":{\"enabled\":true}}}}");

        var events = new GameplayConfigStore(StorePath).Current.Config.WorldEvents;

        Assert.True(events.FruitShower.Enabled);
        Assert.Equal(new BountyOptions().RewardMass, events.Bounty.RewardMass);
        Assert.Equal(new MineFieldOptions().MinMines, events.Mines.MinMines);
        Assert.Equal(new SugarRushOptions().DurationSeconds, events.SugarRush.DurationSeconds);
    }
}
