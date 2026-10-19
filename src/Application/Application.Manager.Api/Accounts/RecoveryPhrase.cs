using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// The recovery phrase: the secret that signs a player in on a new device (docs/ACCOUNTS.md). Random by default, <see cref="WordCount"/>
/// words from a list joined by '-', or chosen by the player when <see cref="Validate"/> accepts it.
/// Phrases are always compared (and hashed) in their <see cref="Normalize"/>d form, so a phrase typed loosely still matches.
/// </summary>
public sealed partial class RecoveryPhrase
{
    public const int WordCount = 4;
    public const int MinLength = 12;
    public const string WeakPhrase = "weakPhrase";
    private const char Separator = '-';

    private static readonly FrozenSet<string> CommonPasswords = new[]
    {
        "password1234", "password12345", "password123456", "passwordpassword", "passw0rd1234", "p@ssw0rd1234", "mypassword123",
        "mysecretpassword", "secretsecret", "qwertyuiop12", "qwertyuiop123", "qwertyuiopasdf", "qwerty123456", "qwertyqwerty",
        "1qaz2wsx3edc", "1q2w3e4r5t6y", "asdfghjkl123", "asdfasdfasdf", "zxcvbnm12345", "123456789012", "1234567890123",
        "12345678901234", "1234567890qwerty", "123456123456", "123123123123", "098765432109", "abcdefghijkl", "abc123abc123",
        "abcd1234abcd", "letmein-letmein", "iloveyou1234", "iloveyouiloveyou", "welcome12345", "welcome123456", "changeme1234",
        "administrator", "administrator1", "adminadmin123", "trustno1trustno1", "football1234", "baseball1234", "monkey123456",
        "dragon123456", "sunshine1234", "princess1234", "superman1234", "starwars1234", "correct horse battery staple",
        "snakesnakesnake", "snakegame123", "snakegame1234", "slitherio123", "recovery phrase", "my recovery phrase",
    }.Select(password => WithoutSeparators(Normalize(password))).ToFrozenSet(StringComparer.Ordinal);

    private readonly string[] _words;
    private readonly Func<int, int> _pickIndex;

    /// <summary>Phrases from <see cref="RecoveryWords.All"/>, picked with a cryptographic random number generator.</summary>
    public RecoveryPhrase() : this(RecoveryWords.All)
    {
    }

    /// <summary>Phrases from <paramref name="words"/>, picked with a cryptographic random number generator.</summary>
    public RecoveryPhrase(IReadOnlyList<string> words) : this(words, RandomNumberGenerator.GetInt32)
    {
    }

    /// <summary>
    /// Phrases from <paramref name="words"/>; <paramref name="pickIndex"/> gets the word count and returns an index below it, uniformly.
    /// Words must be distinct, lowercase, and free of spaces and '-'.
    /// </summary>
    public RecoveryPhrase(IReadOnlyList<string> words, Func<int, int> pickIndex)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(pickIndex);
        EnsureUsable(words);
        _words = words.ToArray();
        _pickIndex = pickIndex;
    }

    /// <summary>A new random phrase, like <c>amber-otter-nine-cable</c>. It is already normalized.</summary>
    public string Generate()
        => string.Join(Separator, Enumerable.Range(0, WordCount).Select(_ => _words[_pickIndex(_words.Length)]));

    /// <summary>
    /// Null when <paramref name="chosen"/> may be used as a recovery phrase, otherwise <see cref="WeakPhrase"/>. Weak: null, shorter than
    /// <see cref="MinLength"/> once normalized, a common password, one repeated character, or the username itself. The last three
    /// ignore separators, so <c>Password 1234</c> is as weak as <c>password1234</c>.
    /// </summary>
    public static string? Validate(string? chosen, string? username)
        => IsStrong(chosen, username) ? null : WeakPhrase;

    /// <summary>
    /// The form phrases are compared and hashed in: Unicode compatibility form (NFKC), lowercase, and every run of whitespace and/or '-'
    /// turned into one '-', with none at the ends. Space and '-' are the same separator, so <c>Amber Otter-nine</c>,
    /// <c>amber - otter  nine</c> and <c>amber-otter-nine</c> all give <c>amber-otter-nine</c>.
    /// </summary>
    public static string Normalize(string phrase)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        var lowered = CompatibilityForm(phrase).ToLowerInvariant();
        return SeparatorRun().Replace(lowered, Separator.ToString()).Trim(Separator);
    }

    private static bool IsStrong(string? chosen, string? username)
    {
        if (chosen is null)
            return false;
        var normalized = Normalize(chosen);
        var squashed = WithoutSeparators(normalized);
        return normalized.Length >= MinLength
            && !CommonPasswords.Contains(squashed)
            && !IsOneRepeatedCharacter(squashed)
            && !IsUsername(squashed, username);
    }

    private static bool IsOneRepeatedCharacter(string squashed)
        => squashed.Length > 0 && squashed.AsSpan().IndexOfAnyExcept(squashed[0]) < 0;

    private static bool IsUsername(string squashed, string? username)
        => username is not null && squashed == WithoutSeparators(Normalize(username));

    private static string WithoutSeparators(string normalized) => normalized.Replace(Separator.ToString(), string.Empty);

    private static string CompatibilityForm(string phrase)
    {
        try
        {
            return phrase.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            return phrase;
        }
    }

    private static void EnsureUsable(IReadOnlyList<string> words)
    {
        if (words.Count == 0)
            throw new ArgumentException("The word list is empty.", nameof(words));
        if (words.Any(word => word is null || word.Length == 0 || Normalize(word) != word || word.Contains(Separator)))
            throw new ArgumentException("Words must be lowercase and free of spaces and '-'.", nameof(words));
        if (words.Distinct(StringComparer.Ordinal).Count() != words.Count)
            throw new ArgumentException("Words must be distinct.", nameof(words));
    }

    [GeneratedRegex(@"[\s\-]+")]
    private static partial Regex SeparatorRun();
}
