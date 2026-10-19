using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class WorldEventPresetsTests
{
    private static readonly WorldEventOptions Admin = new()
    {
        Lightning = new LightningOptions { MinIntervalSeconds = 40, MaxIntervalSeconds = 100, Radius = 100, TelegraphMilliseconds = 1500, MinStrikes = 6, MaxStrikes = 12, StormSeconds = 6, AimedShare = 0.4 },
        Meteors = new MeteorOptions { MinIntervalSeconds = 100, MaxIntervalSeconds = 200, Radius = 130, TelegraphMilliseconds = 2000, MinMeteors = 6, MaxMeteors = 12, RainSeconds = 8 },
        Turret = new TurretOptions { MinIntervalSeconds = 100, MaxIntervalSeconds = 200, HitPoints = 40, FireIntervalMilliseconds = 1000, Arms = 6, ShotSpeed = 0.5 },
        Zone = new ZoneOptions { MinIntervalSeconds = 100, MaxIntervalSeconds = 200, ShrinkSeconds = 60, FinalRadius = 160, DamageMass = 4 }
    };

    private static WorldEventOptions With(Func<RoomGameplay, RoomGameplay> change) => WorldEventPresets.Apply(Admin, change(new RoomGameplay()));

    [Fact]
    public void Apply_ADefaultRoom_IsTheAdminDefaultItself()
    {
        Assert.Same(Admin, WorldEventPresets.Apply(Admin, new RoomGameplay()));
    }

    [Fact]
    public void Apply_PerksAndPrizesAreNotEvents_SoTheyChangeNothingHere()
    {
        Assert.Same(Admin, WorldEventPresets.Apply(Admin, new RoomGameplay { Perks = FeaturePreset.Off, Prizes = FeaturePreset.Off }));
    }

    [Fact]
    public void Apply_Off_SwitchesOnlyThatEventOff()
    {
        var result = With(gameplay => gameplay with { Meteors = FeaturePreset.Off });

        Assert.False(result.Meteors.Enabled);
        Assert.True(result.Lightning.Enabled);
        Assert.True(result.Turret.Enabled);
        Assert.Equal(Admin.Meteors with { Enabled = false }, result.Meteors);
    }

    [Fact]
    public void Apply_Normal_IsTheAdminNumbersSwitchedOn_EvenWhereTheAdminSwitchedItOff()
    {
        var admin = Admin with { Enabled = false, Zone = Admin.Zone with { Enabled = false } };

        var result = WorldEventPresets.Apply(admin, new RoomGameplay { Zone = FeaturePreset.Normal });

        Assert.True(result.Enabled);
        Assert.Equal(admin.Zone with { Enabled = true }, result.Zone);
    }

    [Fact]
    public void Apply_Inherit_KeepsTheEventAsTheAdminSetItWhileAnotherIsChanged()
    {
        var result = With(gameplay => gameplay with { Zone = FeaturePreset.Wild });

        Assert.Equal(Admin.Lightning, result.Lightning);
        Assert.Equal(Admin.Meteors, result.Meteors);
        Assert.Equal(Admin.Turret, result.Turret);
    }

    [Fact]
    public void Apply_NeverChangesTheAdminOptions()
    {
        var before = Admin with { };

        With(gameplay => gameplay with { Lightning = FeaturePreset.Wild, Turret = FeaturePreset.Calm });

        Assert.Equal(before, Admin);
    }

    [Fact]
    public void Apply_Calm_ComesLessOftenWithFewerAndGentlerStrikes()
    {
        var calm = With(gameplay => gameplay with { Lightning = FeaturePreset.Calm }).Lightning;

        Assert.True(calm.MinIntervalSeconds > Admin.Lightning.MinIntervalSeconds);
        Assert.True(calm.MaxIntervalSeconds > Admin.Lightning.MaxIntervalSeconds);
        Assert.True(calm.MinStrikes < Admin.Lightning.MinStrikes);
        Assert.True(calm.MaxStrikes < Admin.Lightning.MaxStrikes);
        Assert.True(calm.Radius < Admin.Lightning.Radius);
        Assert.True(calm.TelegraphMilliseconds > Admin.Lightning.TelegraphMilliseconds);
        Assert.True(calm.AimedShare < Admin.Lightning.AimedShare);
    }

    [Fact]
    public void Apply_Wild_ComesMoreOftenWithMoreAndHarsherStrikes()
    {
        var wild = With(gameplay => gameplay with { Lightning = FeaturePreset.Wild }).Lightning;

        Assert.True(wild.MinIntervalSeconds < Admin.Lightning.MinIntervalSeconds);
        Assert.True(wild.MinStrikes > Admin.Lightning.MinStrikes);
        Assert.True(wild.MaxStrikes > Admin.Lightning.MaxStrikes);
        Assert.True(wild.Radius > Admin.Lightning.Radius);
        Assert.True(wild.TelegraphMilliseconds < Admin.Lightning.TelegraphMilliseconds);
        Assert.True(wild.AimedShare > Admin.Lightning.AimedShare);
    }

    [Fact]
    public void Apply_Wild_Meteors_Turret_AndZone_AreHarsherToo()
    {
        var wild = With(gameplay => gameplay with { Meteors = FeaturePreset.Wild, Turret = FeaturePreset.Wild, Zone = FeaturePreset.Wild });

        Assert.True(wild.Meteors.MaxMeteors > Admin.Meteors.MaxMeteors);
        Assert.True(wild.Meteors.MinIntervalSeconds < Admin.Meteors.MinIntervalSeconds);
        Assert.True(wild.Turret.HitPoints > Admin.Turret.HitPoints);
        Assert.True(wild.Turret.FireIntervalMilliseconds < Admin.Turret.FireIntervalMilliseconds);
        Assert.True(wild.Turret.Arms > Admin.Turret.Arms);
        Assert.True(wild.Zone.ShrinkSeconds < Admin.Zone.ShrinkSeconds);
        Assert.True(wild.Zone.DamageMass > Admin.Zone.DamageMass);
        Assert.True(wild.Zone.FinalRadius < Admin.Zone.FinalRadius);
    }

    [Fact]
    public void Apply_Calm_Meteors_Turret_AndZone_AreGentlerToo()
    {
        var calm = With(gameplay => gameplay with { Meteors = FeaturePreset.Calm, Turret = FeaturePreset.Calm, Zone = FeaturePreset.Calm });

        Assert.True(calm.Meteors.MaxMeteors < Admin.Meteors.MaxMeteors);
        Assert.True(calm.Turret.HitPoints < Admin.Turret.HitPoints);
        Assert.True(calm.Turret.FireIntervalMilliseconds > Admin.Turret.FireIntervalMilliseconds);
        Assert.True(calm.Zone.ShrinkSeconds > Admin.Zone.ShrinkSeconds);
        Assert.True(calm.Zone.DamageMass < Admin.Zone.DamageMass);
        Assert.True(calm.Zone.FinalRadius > Admin.Zone.FinalRadius);
    }

    [Theory]
    [InlineData(FeaturePreset.Off)]
    [InlineData(FeaturePreset.Calm)]
    [InlineData(FeaturePreset.Normal)]
    [InlineData(FeaturePreset.Wild)]
    public void Apply_WhateverThePreset_TheResultCanRun(FeaturePreset preset)
    {
        var gameplay = new RoomGameplay { Lightning = preset, Meteors = preset, Turret = preset, Zone = preset };

        _ = new WorldEventRules(WorldEventPresets.Apply(Admin, gameplay));
    }

    [Theory]
    [InlineData(FeaturePreset.Calm)]
    [InlineData(FeaturePreset.Wild)]
    public void Apply_AtTheEdgesOfWhatIsAllowed_StaysInsideIt(FeaturePreset preset)
    {
        var edge = new WorldEventOptions
        {
            Lightning = new LightningOptions { MinIntervalSeconds = 1, MaxIntervalSeconds = 1, Radius = 1000, TelegraphMilliseconds = 100, MinStrikes = 40, MaxStrikes = 40, StormSeconds = 120, AimedShare = 1 },
            Meteors = new MeteorOptions { MinIntervalSeconds = 1, MaxIntervalSeconds = 1, Radius = 1, TelegraphMilliseconds = 10000, MinMeteors = 1, MaxMeteors = 1, RainSeconds = 1, AimedShare = 0 },
            Turret = new TurretOptions { MinIntervalSeconds = 1, MaxIntervalSeconds = 1, HitPoints = 1, FireIntervalMilliseconds = 100, Arms = 16, ShotSpeed = 3, Size = 20 },
            Zone = new ZoneOptions { MinIntervalSeconds = 1, MaxIntervalSeconds = 1, ShrinkSeconds = 5, FinalRadius = 30, DamageMass = 1, WarningSeconds = 1, HoldSeconds = 0 }
        };
        var gameplay = new RoomGameplay { Lightning = preset, Meteors = preset, Turret = preset, Zone = preset };

        _ = new WorldEventRules(WorldEventPresets.Apply(edge, gameplay));
    }

    [Fact]
    public void Apply_ALightningConfigFromBeforeStorms_StaysAStormOfOneUnlessItIsMadeWilder()
    {
        var old = new WorldEventOptions { Lightning = new LightningOptions() };

        var normal = WorldEventPresets.Apply(old, new RoomGameplay { Lightning = FeaturePreset.Normal }).Lightning;
        var wild = WorldEventPresets.Apply(old, new RoomGameplay { Lightning = FeaturePreset.Wild }).Lightning;

        IStormSettings settings = normal;
        Assert.Equal((1, 1), (settings.MinCount, settings.MaxCount));
        IStormSettings wilder = wild;
        Assert.True(wilder.MaxCount >= 1);
        _ = new WorldEventRules(new WorldEventOptions { Lightning = wild });
    }

    [Fact]
    public void OptionsFor_APlainRoom_GetsTheAdminOptions()
    {
        var rules = new WorldEventRules(Admin);
        var room = Substitute.For<IRoom>();
        room.Gameplay.Returns(new RoomGameplay());

        Assert.Same(rules.Options, rules.OptionsFor(room));
        Assert.Same(rules.Options, rules.OptionsFor(null));
    }

    [Fact]
    public void OptionsFor_ARoomWithoutGameplayAtAll_GetsTheAdminOptions()
    {
        var rules = new WorldEventRules(Admin);

        Assert.Same(rules.Options, rules.OptionsFor(Substitute.For<IRoom>()));
    }

    [Fact]
    public void OptionsFor_ARoomWithChoices_GetsThemOverTheAdminOptions()
    {
        var rules = new WorldEventRules(Admin);
        var room = Substitute.For<IRoom>();
        room.Gameplay.Returns(new RoomGameplay { Turret = FeaturePreset.Off });

        var options = rules.OptionsFor(room);

        Assert.False(options.Turret.Enabled);
        Assert.True(options.Lightning.Enabled);
    }

    [Fact]
    public void OptionsFor_FollowsTheAdminWhenTheAdminChangesTheDefault()
    {
        var rules = new WorldEventRules(Admin);
        var room = Substitute.For<IRoom>();
        room.Gameplay.Returns(new RoomGameplay { Lightning = FeaturePreset.Wild });
        var before = rules.OptionsFor(room).Lightning.Radius;

        rules.Reconfigure(Admin with { Lightning = Admin.Lightning with { Radius = 300 } });

        Assert.True(rules.OptionsFor(room).Lightning.Radius > before);
    }

    [Fact]
    public void OptionsFor_FollowsTheRoomWhenItsChoicesChange()
    {
        var rules = new WorldEventRules(Admin);
        var room = Substitute.For<IRoom>();
        room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Off });
        Assert.False(rules.OptionsFor(room).Zone.Enabled);

        room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Normal });

        Assert.True(rules.OptionsFor(room).Zone.Enabled);
    }

    [Fact]
    public void OptionsFor_TheSameChoicesTwice_AreWorkedOutOnce()
    {
        var rules = new WorldEventRules(Admin);
        var room = Substitute.For<IRoom>();
        room.Gameplay.Returns(new RoomGameplay { Zone = FeaturePreset.Wild });

        Assert.Same(rules.OptionsFor(room), rules.OptionsFor(room));
    }

    [Fact]
    public void Step_UsesTheOptionsItIsGiven_NotTheAdminOnes()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var rules = new WorldEventRules(Admin, () => 0);
        var quiet = Admin with { Lightning = Admin.Lightning with { Enabled = false } };

        var step = rules.Step(WorldEventKind.Lightning, StormState.None, start, 1000, 500, [], quiet);

        Assert.Same(StormState.None, step.State);
    }
}
