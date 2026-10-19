using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class TagAllocatorTests
{
    private const string Username = "Vini";
    private static readonly IReadOnlySet<int> None = new HashSet<int>();

    private sealed class CountingRandom(Func<int, int> inner)
    {
        public int Calls { get; private set; }
        public List<int> Bounds { get; } = [];

        public int Next(int maxExclusive)
        {
            Calls++;
            Bounds.Add(maxExclusive);
            return inner(maxExclusive);
        }
    }

    private static Func<int, int> Sequence(params int[] values)
    {
        var index = 0;
        return _ => values[Math.Min(index++, values.Length - 1)];
    }

    private static HashSet<int> AllTagsExcept(params int[] free)
    {
        var taken = Enumerable.Range(Tags.Min, Tags.Count).ToHashSet();
        taken.ExceptWith(free);
        return taken;
    }

    [Theory]
    [InlineData(9291)]
    [InlineData(0)]
    [InlineData(99_999)]
    public void Allocate_WantedTagFree_ReturnsIt(int wanted)
    {
        var random = new CountingRandom(_ => 42);

        var tag = TagAllocator.Allocate(Username, wanted, None, random.Next);

        Assert.Equal(wanted, tag);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void Allocate_WantedTagTaken_ReturnsARandomFreeOne()
    {
        var taken = new HashSet<int> { 9291 };

        var tag = TagAllocator.Allocate(Username, 9291, taken, _ => 42);

        Assert.Equal(42, tag);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Allocate_WantedTagOutOfRange_ReturnsARandomFreeOne(int wanted)
    {
        var tag = TagAllocator.Allocate(Username, wanted, None, _ => 7);

        Assert.Equal(7, tag);
    }

    [Fact]
    public void Allocate_NoWantedTag_ReturnsARandomFreeOne()
    {
        var tag = TagAllocator.Allocate(Username, null, None, _ => 12_345);

        Assert.Equal(12_345, tag);
    }

    [Fact]
    public void Allocate_AsksTheRandomSourceForTheWholeRange()
    {
        var random = new CountingRandom(_ => 1);

        TagAllocator.Allocate(Username, null, None, random.Next);

        Assert.All(random.Bounds, bound => Assert.Equal(Tags.Count, bound));
    }

    [Fact]
    public void Allocate_RandomTagTaken_ProbesAgain()
    {
        var taken = new HashSet<int> { 5, 6 };

        var tag = TagAllocator.Allocate(Username, null, taken, Sequence(5, 6, 8));

        Assert.Equal(8, tag);
    }

    [Fact]
    public void Allocate_EveryProbeTaken_ScansFromARandomStart()
    {
        var taken = new HashSet<int> { 10, 11, 12 };

        var tag = TagAllocator.Allocate(Username, null, taken, _ => 10);

        Assert.Equal(13, tag);
    }

    [Fact]
    public void Allocate_ScanReachesTheEnd_WrapsAround()
    {
        var taken = new HashSet<int> { 99_998, 99_999 };

        var tag = TagAllocator.Allocate(Username, null, taken, _ => 99_998);

        Assert.Equal(0, tag);
    }

    [Fact]
    public void Allocate_FullHouse_ReturnsNull()
    {
        var taken = AllTagsExcept();

        var tag = TagAllocator.Allocate(Username, null, taken, new Random(1).Next);

        Assert.Null(tag);
    }

    [Fact]
    public void Allocate_FullHouseAndWantedTag_ReturnsNull()
    {
        var taken = AllTagsExcept();

        var tag = TagAllocator.Allocate(Username, 9291, taken, new Random(1).Next);

        Assert.Null(tag);
    }

    [Fact]
    public void Allocate_FullHousePlusJunkOutOfRange_ReturnsNull()
    {
        var taken = AllTagsExcept();
        taken.UnionWith([-5, 100_000, 250_000]);

        var tag = TagAllocator.Allocate(Username, null, taken, new Random(1).Next);

        Assert.Null(tag);
    }

    [Theory]
    [InlineData(54_321)]
    [InlineData(0)]
    [InlineData(99_999)]
    public void Allocate_NearlyFullHouse_FindsTheLastFreeTag(int free)
    {
        var taken = AllTagsExcept(free);
        var random = new CountingRandom(new Random(7).Next);

        var tag = TagAllocator.Allocate(Username, null, taken, random.Next);

        Assert.Equal(free, tag);
        Assert.InRange(random.Calls, 1, TagAllocator.RandomProbes + 1);
    }

    [Fact]
    public void Allocate_NearlyFullHouse_FindsAFreeTagBeforeTheRandomStart()
    {
        var taken = AllTagsExcept(3);

        var tag = TagAllocator.Allocate(Username, null, taken, _ => 50_000);

        Assert.Equal(3, tag);
    }

    [Fact]
    public void Allocate_NearlyFullHouse_IsFast()
    {
        var taken = AllTagsExcept(77_777);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (var round = 0; round < 20; round++)
            Assert.Equal(77_777, TagAllocator.Allocate(Username, null, taken, new Random(round).Next));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Allocate_SameRandomSource_GivesTheSameTag()
    {
        var taken = new HashSet<int> { 1, 2, 3 };

        var first = TagAllocator.Allocate(Username, null, taken, new Random(42).Next);
        var second = TagAllocator.Allocate(Username, null, taken, new Random(42).Next);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Allocate_FixedRandomSource_IsDeterministic()
    {
        var taken = AllTagsExcept(10, 20, 30);

        var first = TagAllocator.Allocate(Username, null, taken, _ => 15);
        var second = TagAllocator.Allocate(Username, null, taken, _ => 15);

        Assert.Equal(20, first);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Allocate_RandomSourceOutOfRange_StillGivesAValidFreeTag(int value)
    {
        var taken = new HashSet<int> { 0 };

        var tag = TagAllocator.Allocate(Username, null, taken, _ => value);

        Assert.NotNull(tag);
        Assert.True(Tags.IsValid(tag.Value));
        Assert.DoesNotContain(tag.Value, taken);
    }

    [Fact]
    public void Allocate_NoRandomSource_UsesASharedOne()
    {
        var taken = new HashSet<int> { 1 };

        var tag = TagAllocator.Allocate(Username, null, taken);

        Assert.NotNull(tag);
        Assert.True(Tags.IsValid(tag.Value));
        Assert.NotEqual(1, tag);
    }

    [Fact]
    public void Allocate_NullTaken_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TagAllocator.Allocate(Username, null, null!, _ => 0));
    }

    [Theory]
    [InlineData(9291, "09291")]
    [InlineData(0, "00000")]
    [InlineData(5, "00005")]
    [InlineData(99_999, "99999")]
    public void FormatTag_IsFiveDigitsZeroPadded(int tag, string expected)
    {
        var text = TagAllocator.FormatTag(tag);

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    public void FormatTag_OutOfRange_Throws(int tag)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TagAllocator.FormatTag(tag));
    }

    [Theory]
    [InlineData("09291", 9291)]
    [InlineData("9291", 9291)]
    [InlineData("#09291", 9291)]
    [InlineData("#9291", 9291)]
    [InlineData("0", 0)]
    [InlineData("00000", 0)]
    [InlineData("#0", 0)]
    [InlineData("99999", 99_999)]
    [InlineData(" 123 ", 123)]
    public void TryParseTag_DigitsWithOptionalHash_Parses(string text, int expected)
    {
        var parsed = TagAllocator.TryParseTag(text, out var tag);

        Assert.True(parsed);
        Assert.Equal(expected, tag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("##1")]
    [InlineData("1#")]
    [InlineData("123456")]
    [InlineData("000001")]
    [InlineData("#123456")]
    [InlineData("99999999999")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("12a")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("# 12")]
    [InlineData("１２")]
    [InlineData("١٢")]
    public void TryParseTag_AnythingElse_Fails(string? text)
    {
        var parsed = TagAllocator.TryParseTag(text, out var tag);

        Assert.False(parsed);
        Assert.Equal(0, tag);
    }

    [Fact]
    public void FormatThenParse_EveryTag_RoundTrips()
    {
        var mismatches = Enumerable.Range(Tags.Min, Tags.Count)
            .Where(tag => !TagAllocator.TryParseTag(TagAllocator.FormatTag(tag), out var parsed) || parsed != tag);

        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("09291")]
    [InlineData("#09291")]
    [InlineData("9291")]
    public void ParseThenFormat_GivesTheCanonicalForm(string text)
    {
        TagAllocator.TryParseTag(text, out var tag);

        Assert.Equal("09291", TagAllocator.FormatTag(tag));
    }
}
