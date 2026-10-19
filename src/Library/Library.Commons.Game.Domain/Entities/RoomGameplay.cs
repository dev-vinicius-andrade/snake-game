namespace Library.Commons.Game.Domain.Entities;

/// <summary>
/// What a room does with one feature of the gameplay. <see cref="Inherit"/> is what the administrator set for every room; the others
/// are what whoever made the room picked: switch it off, or have it on with its default numbers made gentler or harsher.
/// </summary>
public enum FeaturePreset
{
    Inherit = 0,
    Off = 1,
    Calm = 2,
    Normal = 3,
    Wild = 4
}

/// <summary>
/// The gameplay choices of one room, over the default every room inherits. Perks and prizes only know off and on (anything but
/// off); the world events (lightning, meteors, turret, zone) also have calm and wild.
/// </summary>
public sealed record RoomGameplay
{
    /// <summary>The names of the features, as they are written in the wire form (<see cref="ToMap"/>).</summary>
    public static IReadOnlyList<string> Features { get; } = ["perks", "prizes", "lightning", "meteors", "turret", "zone"];

    /// <summary>The room that changes nothing.</summary>
    public static RoomGameplay Default { get; } = new();

    public FeaturePreset Perks { get; init; }
    public FeaturePreset Prizes { get; init; }
    public FeaturePreset Lightning { get; init; }
    public FeaturePreset Meteors { get; init; }
    public FeaturePreset Turret { get; init; }
    public FeaturePreset Zone { get; init; }

    /// <summary>True when the room changes nothing: it is just the default.</summary>
    public bool IsDefault => Features.All(feature => PresetOf(feature) == FeaturePreset.Inherit);

    public bool PerksOff => Perks == FeaturePreset.Off;

    public bool PrizesOff => Prizes == FeaturePreset.Off;

    public FeaturePreset PresetOf(string feature) => feature switch
    {
        "perks" => Perks,
        "prizes" => Prizes,
        "lightning" => Lightning,
        "meteors" => Meteors,
        "turret" => Turret,
        "zone" => Zone,
        _ => FeaturePreset.Inherit
    };

    /// <summary>The wire form: every feature, with its preset in lower case.</summary>
    public IReadOnlyDictionary<string, string> ToMap()
        => Features.ToDictionary(feature => feature, feature => PresetOf(feature).ToString().ToLowerInvariant());

    /// <summary>Reads the wire form. Names and presets it does not know are ignored, so a newer client never breaks an older server.</summary>
    public static RoomGameplay FromMap(IReadOnlyDictionary<string, string>? map)
    {
        if (map is null) return new RoomGameplay();
        FeaturePreset Read(string feature)
        {
            var entry = map.FirstOrDefault(pair => string.Equals(pair.Key, feature, StringComparison.OrdinalIgnoreCase));
            return entry.Value is not null && Enum.TryParse<FeaturePreset>(entry.Value, ignoreCase: true, out var preset) && Enum.IsDefined(preset)
                ? preset
                : FeaturePreset.Inherit;
        }
        return new RoomGameplay
        {
            Perks = Read("perks"), Prizes = Read("prizes"), Lightning = Read("lightning"),
            Meteors = Read("meteors"), Turret = Read("turret"), Zone = Read("zone")
        };
    }
}

public static class RoomGameplayExtensions
{
    /// <summary>The gameplay of a room, the default when there is no room or it has none set.</summary>
    public static RoomGameplay GameplayOf(this Library.Commons.Game.Domain.Interfaces.Entities.IRoom? room) => room?.Gameplay ?? RoomGameplay.Default;
}
