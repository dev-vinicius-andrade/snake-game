using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Picks the tag of a new account (docs/ACCOUNTS.md): the one the player asked for when it is valid and free, otherwise a random free one.
/// A few random tries first; when they all hit taken tags, a scan from a random start, so a free tag is always found when one exists.
/// </summary>
public static class TagAllocator
{
    /// <summary>Random tries before the scan.</summary>
    public const int RandomProbes = 8;

    private const int MaxDigits = 5;

    /// <summary>
    /// The tag for <paramref name="username"/>, or null when all <see cref="Tags.Count"/> tags are in <paramref name="taken"/>.
    /// <paramref name="next"/> works like <see cref="Random.Next(int)"/> (called with <see cref="Tags.Count"/>); <see cref="Random.Shared"/> when null.
    /// </summary>
    public static int? Allocate(string username, int? wantedTag, IReadOnlySet<int> taken, Func<int, int>? next = null)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var random = next ?? Random.Shared.Next;
        if (IsFree(wantedTag, taken))
            return wantedTag;
        return ProbeRandomly(taken, random) ?? ScanFrom(random(Tags.Count), taken);
    }

    /// <summary>The tag as players see it: 5 digits, zero padded (09291).</summary>
    public static string FormatTag(int tag)
    {
        if (!Tags.IsValid(tag))
            throw new ArgumentOutOfRangeException(nameof(tag), tag, $"A tag is from {Tags.Min} to {Tags.Max}.");
        return tag.ToString("D5", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads "09291", "9291" or "#09291" (surrounding spaces ignored): 1 to 5 ASCII digits, an optional leading '#'.</summary>
    public static bool TryParseTag(string? text, out int tag)
    {
        tag = 0;
        var digits = StripHash(text?.Trim());
        if (!IsTagDigits(digits))
            return false;
        tag = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool IsFree(int? tag, IReadOnlySet<int> taken)
        => tag is int candidate && Tags.IsValid(candidate) && !taken.Contains(candidate);

    private static int? ProbeRandomly(IReadOnlySet<int> taken, Func<int, int> random)
        => Enumerable.Range(0, RandomProbes)
            .Select(_ => ToTag(random(Tags.Count)))
            .Where(tag => !taken.Contains(tag))
            .Select(tag => (int?)tag)
            .FirstOrDefault();

    private static int? ScanFrom(int start, IReadOnlySet<int> taken)
        => Enumerable.Range(0, Tags.Count)
            .Select(offset => ToTag(Wrap(start) + offset))
            .Where(tag => !taken.Contains(tag))
            .Select(tag => (int?)tag)
            .FirstOrDefault();

    private static int ToTag(int index) => Tags.Min + Wrap(index);

    private static int Wrap(int index) => (index % Tags.Count + Tags.Count) % Tags.Count;

    private static string? StripHash(string? text)
        => text is not null && text.StartsWith('#') ? text[1..] : text;

    private static bool IsTagDigits([NotNullWhen(true)] string? digits)
        => digits is { Length: >= 1 and <= MaxDigits } && digits.All(char.IsAsciiDigit);
}
