using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class UsernameRulesTests
{
    private sealed class BlockNothing : IWordFilter
    {
        public bool IsBlocked(string cleanedUsername) => false;
    }

    private sealed class RecordingFilter : IWordFilter
    {
        public string? Seen { get; private set; }

        public bool IsBlocked(string cleanedUsername)
        {
            Seen = cleanedUsername;
            return true;
        }
    }

    private static readonly IWordFilter NoFilter = new BlockNothing();

    [Theory]
    [InlineData("Vini")]
    [InlineData("vi")]
    [InlineData("Player_One")]
    [InlineData("dash-name")]
    [InlineData("dot.name")]
    [InlineData("Two Words")]
    [InlineData("12345")]
    [InlineData("abcdefghijklmnopqrst")]
    public void Validate_AllowedName_KeepsIt(string raw)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.True(result.IsValid);
        Assert.Equal(raw, result.Username);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("João")]
    [InlineData("李雷")]
    [InlineData("Ærøskøbing")]
    [InlineData("Владимир")]
    [InlineData("محمد")]
    [InlineData("さくら")]
    public void Validate_LettersOfAnyLanguage_AreAllowed(string raw)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.True(result.IsValid);
        Assert.Equal(raw, result.Username);
    }

    [Fact]
    public void Validate_DecomposedAccent_IsComposed()
    {
        var decomposed = "João";

        var result = UsernameRules.Validate(decomposed, NoFilter);

        Assert.True(result.IsValid);
        Assert.Equal("João", result.Username);
    }

    [Theory]
    [InlineData("  Vini  ", "Vini")]
    [InlineData("Vini   Andrade", "Vini Andrade")]
    [InlineData(" a   b   c ", "a b c")]
    [InlineData("Vini  Andrade", "Vini Andrade")]
    [InlineData("Vini　Andrade", "Vini Andrade")]
    [InlineData("\nVini\r\n", "Vini")]
    public void Validate_Whitespace_IsTrimmedAndCollapsed(string raw, string expected)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("  a  ")]
    [InlineData("abcdefghijklmnopqrstu")]
    public void Validate_WrongLength_IsInvalid(string? raw)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.False(result.IsValid);
        Assert.Null(result.Username);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
    }

    [Fact]
    public void Validate_LengthIsCountedAfterCleaning()
    {
        var raw = "   abcdefghij     klmnopqrs   ";

        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.True(result.IsValid);
        Assert.Equal("abcdefghij klmnopqrs", result.Username);
    }

    [Theory]
    [InlineData("Vini#09291")]
    [InlineData("#Vini")]
    [InlineData("Vini#")]
    [InlineData("Vi@ni")]
    [InlineData("Vini!")]
    [InlineData("Vini+1")]
    [InlineData("<script>")]
    [InlineData("Vini\U0001F600")]
    [InlineData("Vi​ni")]
    [InlineData("Vini＃")]
    public void Validate_ForbiddenCharacters_AreInvalid(string raw)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.False(result.IsValid);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
    }

    [Theory]
    [InlineData("Vi\tni")]
    [InlineData("Vi\nni")]
    [InlineData("Vi\u0000ni")]
    [InlineData("Vi\u001Bni")]
    [InlineData("Vi\u007Fni")]
    [InlineData("Vi\u0085ni")]
    public void Validate_ControlCharacters_AreInvalid(string raw)
    {
        var result = UsernameRules.Validate(raw, NoFilter);

        Assert.False(result.IsValid);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
    }

    [Fact]
    public void Validate_LoneSurrogate_IsInvalid()
    {
        var result = UsernameRules.Validate("Vi\uD800ni", NoFilter);

        Assert.False(result.IsValid);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
    }

    [Fact]
    public void Validate_BlockedByTheFilter_IsInvalid()
    {
        var filter = new RecordingFilter();

        var result = UsernameRules.Validate("  Some   Name ", filter);

        Assert.False(result.IsValid);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
        Assert.Equal("Some Name", filter.Seen);
    }

    [Fact]
    public void Validate_InvalidShape_DoesNotAskTheFilter()
    {
        var filter = new RecordingFilter();

        UsernameRules.Validate("x", filter);

        Assert.Null(filter.Seen);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Moderator")]
    [InlineData("SYSTEM")]
    [InlineData("staff")]
    public void Validate_WithoutAFilter_UsesTheDefaultBlocklist(string raw)
    {
        var result = UsernameRules.Validate(raw);

        Assert.False(result.IsValid);
        Assert.Equal(UsernameRules.InvalidUsername, result.Error);
    }

    [Fact]
    public void Validate_WithoutAFilter_AllowsAnOrdinaryName()
    {
        var result = UsernameRules.Validate("Vini");

        Assert.True(result.IsValid);
        Assert.Equal("Vini", result.Username);
    }

    [Theory]
    [InlineData("  Vini   Andrade ", "Vini Andrade")]
    [InlineData("João", "João")]
    [InlineData(null, "")]
    [InlineData("Vi#ni", "Vi#ni")]
    public void Normalize_CleansWithoutValidating(string? raw, string expected)
    {
        var cleaned = UsernameRules.Normalize(raw);

        Assert.Equal(expected, cleaned);
    }

    [Theory]
    [InlineData("Vini", "vini")]
    [InlineData("VINI", "vini")]
    [InlineData("ViNi", "vini")]
    [InlineData("JOÃO", "joão")]
    [InlineData("Владимир", "владимир")]
    [InlineData("李雷", "李雷")]
    public void Key_IsTheLowerInvariantForm(string username, string expected)
    {
        var key = UsernameRules.Key(username);

        Assert.Equal(expected, key);
    }

    [Fact]
    public void Key_SameNameInAnyCase_IsTheSameKey()
    {
        var keys = new[] { "Vini", "vini", "VINI", "vInI" }.Select(UsernameRules.Key).Distinct();

        Assert.Single(keys);
    }

    [Fact]
    public void Key_CleansBeforeLowering()
    {
        var key = UsernameRules.Key("  Vini   Andrade ");

        Assert.Equal("vini andrade", key);
    }

    [Fact]
    public void Key_DifferentNames_AreDifferentKeys()
    {
        Assert.NotEqual(UsernameRules.Key("Vini"), UsernameRules.Key("Vini2"));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("TheAdmin")]
    [InlineData("a d m i n")]
    [InlineData("a_d-m.i n")]
    [InlineData("Ad.Min")]
    [InlineData("Real Moderator")]
    [InlineData("system32")]
    [InlineData("Staff_Member")]
    [InlineData("official")]
    public void SimpleWordFilter_BlockedWordAnywhere_IsBlocked(string cleaned)
    {
        var blocked = SimpleWordFilter.Default.IsBlocked(cleaned);

        Assert.True(blocked);
    }

    [Theory]
    [InlineData("Vini")]
    [InlineData("João")]
    [InlineData("李雷")]
    [InlineData("Adam")]
    [InlineData("Starfish")]
    [InlineData("Mod")]
    [InlineData("Kike")]
    public void SimpleWordFilter_OrdinaryName_IsNotBlocked(string cleaned)
    {
        var blocked = SimpleWordFilter.Default.IsBlocked(cleaned);

        Assert.False(blocked);
    }

    [Fact]
    public void SimpleWordFilter_DefaultList_IsShortAndHasTheImpersonationWords()
    {
        var words = SimpleWordFilter.DefaultWords;

        Assert.InRange(words.Count, 4, 20);
        Assert.Contains("admin", words);
        Assert.Contains("moderator", words);
        Assert.Contains("system", words);
        Assert.Contains("staff", words);
    }

    [Fact]
    public void SimpleWordFilter_CustomWords_AreSquashedAndCaseInsensitive()
    {
        var filter = new SimpleWordFilter(["Game Master", "", "  "]);

        Assert.True(filter.IsBlocked("the-GAMEMASTER"));
        Assert.True(filter.IsBlocked("game_master"));
        Assert.False(filter.IsBlocked("Vini"));
    }

    [Fact]
    public void SimpleWordFilter_EmptyList_BlocksNothing()
    {
        var filter = new SimpleWordFilter([]);

        Assert.False(filter.IsBlocked("admin"));
    }
}
