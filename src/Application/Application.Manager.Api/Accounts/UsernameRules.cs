using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Manager.Api.Accounts;

/// <summary>Decides whether a cleaned username is not allowed (slurs, names that pose as the game's staff).</summary>
public interface IWordFilter
{
    bool IsBlocked(string cleanedUsername);
}

/// <summary>
/// A short blocklist matched anywhere in the name, case-insensitive, after spaces, '_', '-' and '.' are removed
/// ("A d-m.i_n" is "admin").
/// </summary>
public sealed class SimpleWordFilter : IWordFilter
{
    private static readonly string[] Separators = [" ", "_", "-", "."];

    /// <summary>The words <see cref="Default"/> blocks.</summary>
    public static IReadOnlyList<string> DefaultWords { get; } =
    [
        "admin",
        "moderator",
        "system",
        "staff",
        "official",
        "hitler",
        "nigger",
        "nigga",
        "faggot",
        "retard",
    ];

    public static SimpleWordFilter Default { get; } = new(DefaultWords);

    private readonly string[] _words;

    public SimpleWordFilter(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        _words = words.Select(Squash).Where(word => word.Length > 0).Distinct().ToArray();
    }

    public bool IsBlocked(string cleanedUsername)
    {
        ArgumentNullException.ThrowIfNull(cleanedUsername);
        var squashed = Squash(cleanedUsername);
        return _words.Any(word => squashed.Contains(word, StringComparison.Ordinal));
    }

    private static string Squash(string text)
        => Separators.Aggregate(text, (current, separator) => current.Replace(separator, string.Empty, StringComparison.Ordinal))
            .ToLowerInvariant();
}

/// <summary>The outcome of <see cref="UsernameRules.Validate"/>: the username as it is kept, or an error code.</summary>
public sealed record UsernameResult
{
    private UsernameResult(string? username, string? error)
    {
        Username = username;
        Error = error;
    }

    /// <summary>The cleaned username; null when invalid.</summary>
    public string? Username { get; }

    /// <summary>The error code for the HTTP body (<see cref="UsernameRules.InvalidUsername"/>); null when valid.</summary>
    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Username))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Error is null;

    public static UsernameResult Valid(string username) => new(username, null);

    public static UsernameResult Invalid(string error) => new(null, error);
}

/// <summary>
/// The rules of a username (docs/ACCOUNTS.md): trimmed, runs of spaces collapsed, 2 to 20 characters of letters (any language), digits,
/// space, '_', '-', '.'; never '#', never a control character, nothing the word filter blocks.
/// </summary>
public static partial class UsernameRules
{
    public const int MinLength = 2;
    public const int MaxLength = 20;
    public const string InvalidUsername = "invalidUsername";

    /// <summary>Cleans and checks a username. Without a filter, <see cref="SimpleWordFilter.Default"/> is used.</summary>
    public static UsernameResult Validate(string? raw, IWordFilter? filter = null)
    {
        var cleaned = Normalize(raw);
        if (!HasAllowedShape(cleaned))
            return UsernameResult.Invalid(InvalidUsername);
        if ((filter ?? SimpleWordFilter.Default).IsBlocked(cleaned))
            return UsernameResult.Invalid(InvalidUsername);
        return UsernameResult.Valid(cleaned);
    }

    /// <summary>
    /// Cleans without checking: trims, composes accents (NFC) and turns each run of spaces (any Unicode space) into one ' '.
    /// Control characters are kept, so validation rejects them.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;
        return SpaceRuns().Replace(ComposeWhenWellFormed(raw.Trim()), " ");
    }

    /// <summary>The form used for uniqueness: the cleaned username in lower case (invariant), so "Vini" and "vini" are the same.</summary>
    public static string Key(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return Normalize(username).ToLowerInvariant();
    }

    private static bool HasAllowedShape(string cleaned)
        => cleaned.Length is >= MinLength and <= MaxLength && cleaned.All(IsAllowed);

    private static bool IsAllowed(char character)
        => char.IsLetter(character) || char.IsDigit(character) || character is ' ' or '_' or '-' or '.';

    private static string ComposeWhenWellFormed(string text)
    {
        try
        {
            return text.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return text;
        }
    }

    [GeneratedRegex(@"\p{Z}+")]
    private static partial Regex SpaceRuns();
}
