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
/// off); the world events (lightning, meteors, turret, zone) also have calm and wild. On top of the presets, a room can switch single
/// things the administrator made (<see cref="Toggles"/>): a perk, a prize, a reward or a world event, by kind and id.
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

    /// <summary>The kinds of thing a room can switch, as the first part of a toggle key (<c>perk:magnet</c>).</summary>
    public static IReadOnlyList<string> ToggleKinds { get; } = ["perk", "prize", "reward", "event"];

    /// <summary>
    /// What the room switched on (true) or off (false), by <c>kind:id</c>. A thing that is not here follows the default, so a room can
    /// only choose among what the administrator defined; an id that does not exist does nothing.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Toggles { get; init; } = new Dictionary<string, bool>();

    public static string ToggleKey(string kind, string id) => $"{kind}:{id}";

    /// <summary>The room's own choice for one thing, or null when it follows the default.</summary>
    public bool? ToggleOf(string kind, string id) => Toggles.TryGetValue(ToggleKey(kind, id), out var on) ? on : null;

    /// <summary>The same choices with one thing switched on, off, or (null) back to the default.</summary>
    public RoomGameplay WithToggle(string kind, string id, bool? on)
    {
        var next = new Dictionary<string, bool>(Toggles);
        if (on is { } value) next[ToggleKey(kind, id)] = value;
        else next.Remove(ToggleKey(kind, id));
        return this with { Toggles = next };
    }

    /// <summary>True when the room changes nothing: it is just the default.</summary>
    public bool IsDefault => Toggles.Count == 0 && Features.All(feature => PresetOf(feature) == FeaturePreset.Inherit);

    public bool Equals(RoomGameplay? other)
        => other is not null && Perks == other.Perks && Prizes == other.Prizes && Lightning == other.Lightning && Meteors == other.Meteors
           && Turret == other.Turret && Zone == other.Zone && Toggles.Count == other.Toggles.Count
           && Toggles.All(pair => other.Toggles.TryGetValue(pair.Key, out var on) && on == pair.Value);

    public override int GetHashCode() => HashCode.Combine(Perks, Prizes, Lightning, Meteors, Turret, Zone, Toggles.Count);

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

    /// <summary>The wire form: every feature, with its preset in lower case, then each toggle as <c>kind:id</c> to <c>on</c> or <c>off</c>.</summary>
    public IReadOnlyDictionary<string, string> ToMap()
    {
        var map = Features.ToDictionary(feature => feature, feature => PresetOf(feature).ToString().ToLowerInvariant());
        foreach (var (key, on) in Toggles) map[key] = on ? "on" : "off";
        return map;
    }

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
            Meteors = Read("meteors"), Turret = Read("turret"), Zone = Read("zone"),
            Toggles = ReadToggles(map)
        };
    }

    private static Dictionary<string, bool> ReadToggles(IReadOnlyDictionary<string, string> map)
    {
        var toggles = new Dictionary<string, bool>();
        foreach (var (key, value) in map)
        {
            var split = key.IndexOf(':');
            if (split <= 0 || split == key.Length - 1) continue;
            var kind = ToggleKinds.FirstOrDefault(known => string.Equals(known, key[..split], StringComparison.OrdinalIgnoreCase));
            var on = value?.Trim().ToLowerInvariant() switch { "on" => true, "off" => false, _ => (bool?)null };
            if (kind is not null && on is { } choice) toggles[ToggleKey(kind, key[(split + 1)..])] = choice;
        }
        return toggles;
    }
}

public static class RoomGameplayExtensions
{
    /// <summary>The gameplay of a room, the default when there is no room or it has none set.</summary>
    public static RoomGameplay GameplayOf(this Library.Commons.Game.Domain.Interfaces.Entities.IRoom? room) => room?.Gameplay ?? RoomGameplay.Default;
}
