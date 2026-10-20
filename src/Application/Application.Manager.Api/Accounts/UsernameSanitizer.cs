using System.Globalization;
using System.Text;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Turns a name a social provider gave (a login, a display name, with accents, emoji, an email even) into something that passes the
/// username rules, or into nothing when nothing usable is left. Used for the username and the display name of an account made from a
/// social login: what the provider says is never trusted to already fit.
/// </summary>
public static class UsernameSanitizer
{
    /// <summary>The name kept, or null when it is too short, blocked or empty once cleaned.</summary>
    public static string? Clean(string? raw, IWordFilter? filter = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var kept = new StringBuilder();
        foreach (var rune in UsernameRules.Normalize(raw).EnumerateRunes())
            if (IsAllowed(rune))
                kept.Append(rune.ToString());
        var shortened = Shorten(UsernameRules.Normalize(kept.ToString()));
        var result = UsernameRules.Validate(shortened, filter);
        return result.IsValid ? result.Username : null;
    }

    private static bool IsAllowed(Rune rune)
    {
        if (Rune.IsLetterOrDigit(rune) && rune.Value < 0x1F000)
            return true;
        return rune.Value is ' ' or '_' or '-' or '.';
    }

    /// <summary>At most the longest username, cut between characters (a surrogate pair or a letter with its accent is never split) and without a trailing space.</summary>
    private static string Shorten(string name)
    {
        if (name.Length <= UsernameRules.MaxLength)
            return name;
        var cut = name[..UsernameRules.MaxLength];
        if (char.IsHighSurrogate(cut[^1]))
            cut = cut[..^1];
        return cut.TrimEnd();
    }
}
