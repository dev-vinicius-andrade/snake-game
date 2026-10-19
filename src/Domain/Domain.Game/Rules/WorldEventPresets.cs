using Library.Commons.Game.Domain.Entities;

namespace Domain.Game.Rules;

/// <summary>
/// What whoever made a room picked for the world events, put over the options the administrator set for every room. A preset is a
/// way of changing the numbers of the administrator, never a set of numbers of its own: calm waits twice as long and strikes half as
/// hard, wild waits half as long and strikes harder, normal is the numbers as they are (switched on, even if the administrator left
/// the event off). Every result is kept inside what <see cref="WorldEventRules"/> accepts.
/// </summary>
public static class WorldEventPresets
{
    /// <summary>The options for a room with these choices. The administrator's own options, untouched, when the room changes nothing here.</summary>
    public static WorldEventOptions Apply(WorldEventOptions admin, RoomGameplay gameplay)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentNullException.ThrowIfNull(gameplay);
        var events = new[] { gameplay.Lightning, gameplay.Meteors, gameplay.Turret, gameplay.Zone };
        if (events.All(preset => preset == FeaturePreset.Inherit)) return admin;

        return admin with
        {
            Enabled = admin.Enabled || events.Any(IsOn),
            Lightning = Choose(gameplay.Lightning, admin.Lightning, Lightning),
            Meteors = Choose(gameplay.Meteors, admin.Meteors, Meteors),
            Turret = Choose(gameplay.Turret, admin.Turret, Turret),
            Zone = Choose(gameplay.Zone, admin.Zone, Zone)
        };
    }

    private static bool IsOn(FeaturePreset preset) => preset is FeaturePreset.Calm or FeaturePreset.Normal or FeaturePreset.Wild;

    private static T Choose<T>(FeaturePreset preset, T admin, Func<T, double, T> scale) where T : class
        => preset switch
        {
            FeaturePreset.Off => Switched(admin, false),
            FeaturePreset.Normal => Switched(admin, true),
            FeaturePreset.Calm => Switched(scale(admin, Gentler), true),
            FeaturePreset.Wild => Switched(scale(admin, Harsher), true),
            _ => admin
        };

    private static T Switched<T>(T options, bool enabled) where T : class => options switch
    {
        LightningOptions lightning => (lightning with { Enabled = enabled } as T)!,
        MeteorOptions meteors => (meteors with { Enabled = enabled } as T)!,
        TurretOptions turret => (turret with { Enabled = enabled } as T)!,
        ZoneOptions zone => (zone with { Enabled = enabled } as T)!,
        _ => options
    };

    // The factor a preset applies. Calm is under 1 and wild over it for how hard things are; how often they come is the other way round.
    private const double Gentler = 0.5;
    private const double Harsher = 1.6;

    /// <summary>How much longer a wait gets when the room is calm, and how much shorter when it is wild.</summary>
    private static double Wait(double factor) => factor < 1 ? 2 : 0.5;

    private static int Scaled(double value, double factor, int min, int max) => (int)Math.Clamp(Math.Round(value * factor), min, max);

    private static double Share(double value, double factor) => Math.Clamp(value * factor, 0, 1);

    private static LightningOptions Lightning(LightningOptions admin, double factor)
    {
        IStormSettings settings = admin;
        return admin with
        {
            MinIntervalSeconds = Scaled(admin.MinIntervalSeconds, Wait(factor), 1, 100000),
            MaxIntervalSeconds = Scaled(admin.MaxIntervalSeconds, Wait(factor), 1, 100000),
            Radius = Scaled(admin.Radius, factor < 1 ? 0.8 : 1.2, 1, 1000),
            TelegraphMilliseconds = Scaled(admin.TelegraphMilliseconds, factor < 1 ? 1.3 : 0.8, 100, 10000),
            MinStrikes = Scaled(settings.MinCount, factor, 1, WorldEventRules.MaxStrikesPerStorm),
            MaxStrikes = Scaled(settings.MaxCount, factor, 1, WorldEventRules.MaxStrikesPerStorm),
            StormSeconds = settings.DurationSeconds,
            AimedShare = Share(admin.AimedShare, factor < 1 ? 0.5 : 1.5)
        };
    }

    private static MeteorOptions Meteors(MeteorOptions admin, double factor) => admin with
    {
        MinIntervalSeconds = Scaled(admin.MinIntervalSeconds, Wait(factor), 1, 100000),
        MaxIntervalSeconds = Scaled(admin.MaxIntervalSeconds, Wait(factor), 1, 100000),
        Radius = Scaled(admin.Radius, factor < 1 ? 0.8 : 1.2, 1, 1000),
        TelegraphMilliseconds = Scaled(admin.TelegraphMilliseconds, factor < 1 ? 1.3 : 0.8, 100, 10000),
        MinMeteors = Scaled(admin.MinMeteors, factor, 1, WorldEventRules.MaxStrikesPerStorm),
        MaxMeteors = Scaled(admin.MaxMeteors, factor, 1, WorldEventRules.MaxStrikesPerStorm),
        AimedShare = Share(admin.AimedShare, factor < 1 ? 0.5 : 1.5)
    };

    private static TurretOptions Turret(TurretOptions admin, double factor) => admin with
    {
        MinIntervalSeconds = Scaled(admin.MinIntervalSeconds, Wait(factor), 1, 100000),
        MaxIntervalSeconds = Scaled(admin.MaxIntervalSeconds, Wait(factor), 1, 100000),
        HitPoints = Scaled(admin.HitPoints, factor < 1 ? 0.6 : 1.5, 1, 10000),
        FireIntervalMilliseconds = Scaled(admin.FireIntervalMilliseconds, factor < 1 ? 1.5 : 0.7, 100, 10000),
        Arms = Scaled(admin.Arms, factor < 1 ? 0.7 : 1.5, 1, 16),
        ShotSpeed = Math.Clamp(admin.ShotSpeed * (factor < 1 ? 0.8 : 1.3), 0.1, 3)
    };

    private static ZoneOptions Zone(ZoneOptions admin, double factor) => admin with
    {
        MinIntervalSeconds = Scaled(admin.MinIntervalSeconds, Wait(factor), 1, 100000),
        MaxIntervalSeconds = Scaled(admin.MaxIntervalSeconds, Wait(factor), 1, 100000),
        ShrinkSeconds = Scaled(admin.ShrinkSeconds, factor < 1 ? 1.5 : 0.7, 5, 3600),
        DamageMass = Scaled(admin.DamageMass, factor < 1 ? 0.5 : 2, 1, 1000),
        FinalRadius = Scaled(admin.FinalRadius, factor < 1 ? 1.3 : 0.8, 30, 2000)
    };
}
