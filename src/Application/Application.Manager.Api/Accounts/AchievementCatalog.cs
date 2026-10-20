using System.Text.RegularExpressions;
using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Accounts;

/// <summary>What an administrator can get wrong when changing an achievement (docs/ACHIEVEMENTS.md), as the codes of the admin api.</summary>
public static class AchievementErrors
{
    public const string InvalidId = "invalidId";
    public const string InvalidIcon = "invalidIcon";
    public const string InvalidStat = "invalidStat";
    public const string InvalidThreshold = "invalidThreshold";
    public const string InvalidText = "invalidText";
    public const string ReadOnly = "readOnly";
    public const string InUse = "inUse";
    public const string NotFound = "notFound";
}

/// <summary>The name and the line that says how to get an achievement, in one language.</summary>
public sealed record AchievementText(string Name, string Description);

/// <summary>
/// An achievement as the catalog keeps it: what unlocks it, whether it is on, and the texts an administrator gave it, by language. The built-in
/// achievements have no texts here: the site has them (and they are what is shown when none is given for a language).
/// </summary>
public sealed record AchievementEntry(string Id, string Icon, string Stat, long Threshold, bool Enabled = true,
    IReadOnlyDictionary<string, AchievementText>? Texts = null)
{
    public AchievementDefinition ToDefinition() => new(Id, Icon, Stat, Threshold);
}

public enum AchievementChange
{
    Saved,
    Deleted,
    NotFound,
    /// <summary>Somebody has unlocked it, so it cannot be deleted (it can be switched off).</summary>
    InUse,
    /// <summary>The catalog is the code, and the code cannot be changed from here.</summary>
    ReadOnly,
}

/// <summary>Where the achievements of the game are kept: the database (changeable from the admin page), or the code when there is no database.</summary>
public interface IAchievementCatalog
{
    /// <summary>False when the achievements are the code's and nothing can be saved.</summary>
    bool IsWritable { get; }

    /// <summary>Every achievement, switched off ones too, in the order of the game.</summary>
    IReadOnlyList<AchievementEntry> All();

    /// <summary>Adds the achievement, or changes the one with that id (it keeps its place). The entry must have passed <see cref="AchievementRules"/>.</summary>
    AchievementChange Save(AchievementEntry entry);

    AchievementChange Delete(string id);
}

public static class AchievementCatalogExtensions
{
    /// <summary>What can be unlocked now: the ones that are on.</summary>
    public static IReadOnlyList<AchievementDefinition> Definitions(this IAchievementCatalog catalog)
        => catalog.All().Where(entry => entry.Enabled).Select(entry => entry.ToDefinition()).ToList().AsReadOnly();
}

/// <summary>What makes an achievement acceptable. The admin api refuses anything else, and says which part.</summary>
public static partial class AchievementRules
{
    public const int MaxThreshold = 1_000_000_000;
    public const int MaxName = 40;
    public const int MaxDescription = 200;

    /// <summary>The languages the site has texts for.</summary>
    public static IReadOnlyList<string> Locales { get; } = ["en", "pt"];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Slug();

    [GeneratedRegex("^[a-z0-9-]+:[a-z0-9-]+$")]
    private static partial Regex IconName();

    /// <summary>The <see cref="AchievementErrors"/> code of the first thing wrong, or null.</summary>
    public static string? Validate(AchievementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id is not { Length: >= 2 and <= 40 } || !Slug().IsMatch(entry.Id)) return AchievementErrors.InvalidId;
        if (entry.Icon is not { Length: > 0 and <= 128 } || !IconName().IsMatch(entry.Icon)) return AchievementErrors.InvalidIcon;
        if (!StatKeys.IsCounter(entry.Stat ?? "") && !StatKeys.IsMaximum(entry.Stat ?? "")) return AchievementErrors.InvalidStat;
        if (entry.Threshold is < 1 or > MaxThreshold) return AchievementErrors.InvalidThreshold;
        return entry.Texts is null || entry.Texts.All(text => TextIsValid(text.Key, text.Value)) ? null : AchievementErrors.InvalidText;
    }

    private static bool TextIsValid(string locale, AchievementText? text)
        => Locales.Contains(locale)
           && text is not null
           && !string.IsNullOrWhiteSpace(text.Name) && text.Name.Trim().Length <= MaxName
           && (text.Description ?? "").Trim().Length <= MaxDescription;
}

/// <summary>The achievements of the code, with no way to change them: what a manager without a database has.</summary>
public sealed class CodeAchievementCatalog : IAchievementCatalog
{
    private static readonly IReadOnlyList<AchievementEntry> Entries = Achievements.Defaults
        .Select(definition => new AchievementEntry(definition.Id, definition.Icon, definition.Stat, definition.Threshold))
        .ToList()
        .AsReadOnly();

    public bool IsWritable => false;

    public IReadOnlyList<AchievementEntry> All() => Entries;

    public AchievementChange Save(AchievementEntry entry) => AchievementChange.ReadOnly;

    public AchievementChange Delete(string id) => AchievementChange.ReadOnly;
}
