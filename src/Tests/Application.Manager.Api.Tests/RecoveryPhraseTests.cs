using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class RecoveryPhraseTests
{
    private static readonly string[] Words = ["amber", "otter", "nine", "cable", "plum", "river", "stone", "kite"];

    [Fact]
    public void Generate_GivesFourListWordsJoinedByDashes()
    {
        var phrase = new RecoveryPhrase(Words).Generate();

        var parts = phrase.Split('-');
        Assert.Equal(RecoveryPhrase.WordCount, parts.Length);
        Assert.All(parts, part => Assert.Contains(part, Words));
    }

    [Fact]
    public void Generate_UsesThePickedIndexes()
    {
        var picks = new Queue<int>([0, 1, 2, 7]);

        var phrase = new RecoveryPhrase(Words, _ => picks.Dequeue()).Generate();

        Assert.Equal("amber-otter-nine-kite", phrase);
    }

    [Fact]
    public void Generate_AsksForAnIndexBelowTheWordCount()
    {
        var bounds = new List<int>();

        new RecoveryPhrase(Words, bound => { bounds.Add(bound); return 0; }).Generate();

        Assert.Equal(new[] { 8, 8, 8, 8 }, bounds);
    }

    [Fact]
    public void Generate_ReachesEveryWord()
    {
        var phrase = new RecoveryPhrase(Words);

        var seen = Enumerable.Range(0, 250).SelectMany(_ => phrase.Generate().Split('-')).ToHashSet();

        Assert.Equal(Words.Order(), seen.Order());
    }

    [Fact]
    public void Generate_SpreadsPicksEvenly()
    {
        var phrase = new RecoveryPhrase(Words);

        var counts = Enumerable.Range(0, 1000).SelectMany(_ => phrase.Generate().Split('-')).GroupBy(word => word).ToList();

        Assert.Equal(Words.Length, counts.Count);
        Assert.All(counts, group => Assert.InRange(group.Count(), 350, 650));
    }

    [Fact]
    public void Generate_IsAlreadyNormalized()
    {
        var phrase = new RecoveryPhrase(Words).Generate();

        Assert.Equal(phrase, RecoveryPhrase.Normalize(phrase));
    }

    [Fact]
    public void Generate_WithTheDefaultList_GivesFourLowercaseWords()
    {
        var phrase = new RecoveryPhrase().Generate();

        var parts = phrase.Split('-');
        Assert.Equal(RecoveryPhrase.WordCount, parts.Length);
        Assert.All(parts, part => Assert.Equal(part.ToLowerInvariant(), part));
        Assert.All(parts, part => Assert.NotEmpty(part));
    }

    public static TheoryData<string[]> UnusableWordLists => new()
    {
        Array.Empty<string>(),
        new[] { "amber", "" },
        new[] { "amber", "Otter" },
        new[] { "amber", "ot-ter" },
        new[] { "amber", "ot ter" },
        new[] { "amber", "amber" },
    };

    [Theory]
    [MemberData(nameof(UnusableWordLists))]
    public void Constructor_UnusableWordList_Throws(string[] words)
    {
        Assert.Throws<ArgumentException>(() => new RecoveryPhrase(words));
    }

    [Theory]
    [InlineData("amber-otter-nine")]
    [InlineData("Amber Otter-nine")]
    [InlineData("AMBER-OTTER-NINE")]
    [InlineData("  amber   otter  nine ")]
    [InlineData("amber - otter -- nine")]
    [InlineData("amber\totter\nnine")]
    [InlineData("-amber-otter-nine-")]
    [InlineData("amber otter　nine")]
    [InlineData("Ａmber otter nine")]
    public void Normalize_LooselyTypedPhrases_AllMatch(string typed)
    {
        Assert.Equal("amber-otter-nine", RecoveryPhrase.Normalize(typed));
    }

    [Fact]
    public void Normalize_ComposedAndDecomposedAccents_Match()
    {
        Assert.Equal(RecoveryPhrase.Normalize("café au lait"), RecoveryPhrase.Normalize("Café au lait"));
    }

    [Fact]
    public void Normalize_BrokenUnicode_DoesNotThrow()
    {
        var normalized = RecoveryPhrase.Normalize("Amber \uD800 Otter");

        Assert.Equal("amber-\uD800-otter", normalized);
    }

    [Theory]
    [InlineData("amber-otter-nine-cable")]
    [InlineData("my cat eats pixels at noon")]
    [InlineData("plum-tree-42")]
    [InlineData("Zebra Lamp Violin")]
    public void Validate_StrongPhrase_IsOk(string chosen)
    {
        Assert.Null(RecoveryPhrase.Validate(chosen, "Vini"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("            ")]
    [InlineData("------------")]
    [InlineData("plum-tree-4")]
    [InlineData("   plum-tree-4   ")]
    [InlineData("password1234")]
    [InlineData("PASSWORD1234")]
    [InlineData("Password 1234")]
    [InlineData("qwertyuiop12")]
    [InlineData("123456789012")]
    [InlineData("letmein-letmein")]
    [InlineData("LetMeIn LetMeIn")]
    [InlineData("snakesnakesnake")]
    [InlineData("Snake Snake Snake")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("zzzz zzzz zzzz")]
    [InlineData("777777777777777")]
    public void Validate_WeakPhrase_IsWeak(string? chosen)
    {
        Assert.Equal(RecoveryPhrase.WeakPhrase, RecoveryPhrase.Validate(chosen, "Vini"));
    }

    [Theory]
    [InlineData("Long Player Name", "Long Player Name")]
    [InlineData("long-player-name", "Long Player Name")]
    [InlineData("LongPlayerName", "long player name")]
    public void Validate_PhraseEqualToTheUsername_IsWeak(string chosen, string username)
    {
        Assert.Equal(RecoveryPhrase.WeakPhrase, RecoveryPhrase.Validate(chosen, username));
    }

    [Fact]
    public void Validate_WithoutAUsername_ChecksTheRest()
    {
        Assert.Null(RecoveryPhrase.Validate("Long Player Name", null));
    }
}
