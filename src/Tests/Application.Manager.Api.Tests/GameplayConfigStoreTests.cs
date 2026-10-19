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
        Assert.Equal(PerkEffect.Magnet, second.Current.Config.Perks.Perks.Single().Effect);
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
}
