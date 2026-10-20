using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Domain.Game.Tests;

/// <summary>Extra lives, precision, event resistance, plunder, cooldown abilities, and rewards that give perks, tiers, lives and mass.</summary>
public sealed partial class GameStateServiceTests
{
    private static PerkDefinition RewardPerk(string id, PerkEffect effect, double amount, int foods = 1, string? target = null,
        int cooldown = 0, int duration = 0, List<PerkTier>? tiers = null)
        => new()
        {
            Id = id, Name = id, FoodsRequired = foods, Effects = [new PerkEffectEntry { Effect = effect, Amount = amount, Target = target }],
            CooldownSeconds = cooldown, DurationSeconds = duration, Tiers = tiers ?? []
        };

    private GameStateService WithPerks(params PerkDefinition[] perks)
        => Build(new PerkRules(new PerkOptions { ShotSpreadDegrees = 20, ExtraLifeKeepShare = 0.5, ExtraLifeProtectSeconds = 3, Perks = perks.ToList() }));

    private Player AddFed(string name, double x, double y, int length = InitialLength, long score = 0, int foods = 1)
    {
        var player = AddPlayer(name, x, y, length, score);
        player.FoodsEaten = foods;
        return player;
    }

    private async Task ShootAtTheHead(GameStateService service, Player victim)
    {
        var shooter = AddPlayer("shooter" + Guid.NewGuid().ToString("N")[..4], 100, 900);
        AddShot(shooter, victim.CurrentPosition.X, victim.CurrentPosition.Y);
        await service.CheckObstacleCollision(_state, victim);
    }

    // Extra life.

    [Fact]
    public async Task ExtraLife_ALethalShotOnTheHead_SavesTheSnakeOnce_WhichKeepsHalfItsMass()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 1));
        var victim = AddFed("v", 500, 500, score: 40);

        await ShootAtTheHead(service, victim);

        Assert.True(victim.IsAlive);
        Assert.Equal(20, victim.Score);
        Assert.Equal(1, victim.LivesUsed);
        Assert.Equal(_clock.GetUtcNow().AddSeconds(3), victim.ProtectedUntil);
        Assert.Contains(_state.Hits, hit => hit.Kind == DamageKind.Saved && hit.TargetGuid == victim.Guid && hit.Damage == 0);
        Assert.DoesNotContain(_state.Hits, hit => hit.Kind == DamageKind.Kill);
    }

    [Fact]
    public async Task ExtraLife_TheMassItDoesNotKeepFallsAsFood()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 1));
        var victim = AddFed("v", 500, 500, score: 40);

        await ShootAtTheHead(service, victim);

        Assert.Equal(20, _state.Foods.Values.Sum(food => food.Value));
    }

    [Fact]
    public async Task ExtraLife_ASavedSnake_IsHealedAndCannotBeHurtWhileProtected()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 2));
        var victim = AddFed("v", 500, 500, score: 40);
        victim.SegmentHealth = victim.SegmentHealth.Select(_ => 0.2).ToList();
        await ShootAtTheHead(service, victim);

        Assert.All(victim.SegmentHealth, health => Assert.Equal(1.0, health));

        await ShootAtTheHead(service, victim);

        Assert.True(victim.IsAlive);
        Assert.Equal(1, victim.LivesUsed);
    }

    [Fact]
    public async Task ExtraLife_WhenTheProtectionIsOver_TheNextLifeIsUsed_AndThenTheSnakeDies()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 2));
        var victim = AddFed("v", 500, 500, score: 40);

        await ShootAtTheHead(service, victim);
        _clock.Advance(TimeSpan.FromSeconds(4));
        await ShootAtTheHead(service, victim);
        Assert.True(victim.IsAlive);
        Assert.Equal(2, victim.LivesUsed);

        _clock.Advance(TimeSpan.FromSeconds(4));
        await ShootAtTheHead(service, victim);

        Assert.False(victim.IsAlive);
        Assert.Contains(_state.Hits, hit => hit.Kind == DamageKind.Kill);
    }

    [Fact]
    public async Task ExtraLife_ALifeFromAReward_SavesToo_AndTheKillerGetsNoKill()
    {
        var victim = AddFed("v", 500, 500, score: 10);
        victim.BonusLives = 1;
        var shooter = AddPlayer("s", 100, 900);
        AddShot(shooter, victim.CurrentPosition.X, victim.CurrentPosition.Y);

        await _service.CheckObstacleCollision(_state, victim);

        Assert.True(victim.IsAlive);
        Assert.Equal(1, victim.LivesUsed);
        Assert.Equal(0, shooter.FoodsEaten);
    }

    [Fact]
    public async Task ExtraLife_ASnakeThatRunsIntoABody_IsSaved_AndTheOtherGetsNoKillCredit()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 1));
        var wall = AddPlayer("wall", 500, 500, length: 10);
        var runner = AddFed("runner", 0, 0);
        runner.Path = [At(wall.Path[3].X, wall.Path[3].Y)];
        runner.CurrentPosition = runner.Path[^1];
        runner.SegmentHealth = [1.0];

        await service.ResolveSnakeCollisions(_state);

        Assert.True(runner.IsAlive);
        Assert.Equal(0, wall.FoodsEaten);
    }

    [Fact]
    public void ExtraLife_TheZoneKillingASnakeWithNoMass_IsSaved()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 1));
        var victim = AddFed("v", 100, 100, score: 0);

        var died = service.ApplyZoneDamage(_state, 1500, 900, 50, 4);

        Assert.Empty(died);
        Assert.True(victim.IsAlive);
        Assert.Equal(1, victim.LivesUsed);
    }

    [Fact]
    public void ExtraLife_ALightningStrikeOnTheHead_IsSaved()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 1));
        var victim = AddFed("v", 500, 500, score: 30);

        var died = service.StrikeArea(_state, Middle(500), Middle(500), 5);

        Assert.Empty(died);
        Assert.True(victim.IsAlive);
        Assert.Equal(15, victim.Score);
    }

    [Fact]
    public async Task ExtraLife_TheSavedSnakeCanBeSeenByTheSnapshot_ThroughLivesLeft()
    {
        var service = WithPerks(RewardPerk("life", PerkEffect.ExtraLife, 2));
        var victim = AddFed("v", 500, 500, score: 40);
        await service.CheckFoodCollision(_state, victim);
        AddFood(victim.CurrentPosition.X, victim.CurrentPosition.Y, 1);
        await service.CheckFoodCollision(_state, victim);
        Assert.Equal(2, victim.LivesLeft);

        await ShootAtTheHead(service, victim);

        Assert.Equal(1, victim.LivesLeft);
    }

    // Precision.

    private static double AngleOf(IObstacle shot) => shot.Direction.Axis.Angle ?? 0;

    [Fact]
    public async Task Shoot_WithPrecision_TheVolleyFansOutLess()
    {
        var plain = WithPerks(RewardPerk("twin", PerkEffect.ExtraShots, 2));
        var sharp = WithPerks(RewardPerk("twin", PerkEffect.ExtraShots, 2), RewardPerk("aim", PerkEffect.Precision, 0.5));
        var first = AddFed("a", 500, 500, score: 5);
        var second = AddFed("b", 500, 700, score: 5);

        await plain.Shoot(_state, _room, first);
        var wide = _state.Obstacles.Values.Select(AngleOf).ToArray();
        _state.Obstacles.Clear();
        await sharp.Shoot(_state, _room, second);
        var narrow = _state.Obstacles.Values.Select(AngleOf).ToArray();

        Assert.Equal(3, wide.Length);
        Assert.Equal(3, narrow.Length);
        Assert.Equal((wide.Max() - wide.Min()) / 2, narrow.Max() - narrow.Min(), 6);
    }

    // Event resistance.

    [Fact]
    public void ApplyZoneDamage_WithResistance_LosesLess()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.5, target: "zone"));
        var victim = AddFed("v", 100, 100, score: 50);

        service.ApplyZoneDamage(_state, 1500, 900, 50, 10);

        Assert.Equal(45, victim.Score);
    }

    [Fact]
    public void ApplyZoneDamage_ResistanceToAnotherEvent_DoesNothing()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.5, target: "lightning"));
        var victim = AddFed("v", 100, 100, score: 50);

        service.ApplyZoneDamage(_state, 1500, 900, 50, 10);

        Assert.Equal(40, victim.Score);
    }

    [Fact]
    public void ApplyZoneDamage_ResistanceNeverMakesTheSnakeImmune()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.9));
        var victim = AddFed("v", 100, 100, score: 50);

        service.ApplyZoneDamage(_state, 1500, 900, 50, 2);

        Assert.Equal(49, victim.Score);
    }

    [Fact]
    public void StrikeArea_WithResistance_FewerSegmentsBreak()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.5, target: "lightning"));
        var spared = AddFed("spared", 500, 500, length: 11, score: 10);
        var plain = AddPlayer("plain", 500, 800, length: 11, score: 10);

        service.StrikeArea(_state, Middle(spared.Path[1].X), Middle(500), HeadSize * 1.2);
        service.StrikeArea(_state, Middle(plain.Path[1].X), Middle(800), HeadSize * 1.2);

        Assert.True(spared.Path.Count > plain.Path.Count);
        Assert.True(spared.Path.Count < 11);
    }

    [Fact]
    public void StrikeArea_AMeteorUsesTheMeteorResistance_NotTheLightningOne()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.95, target: "lightning"));
        var victim = AddFed("v", 500, 500, length: 11, score: 10);

        service.StrikeArea(_state, Middle(victim.Path[1].X), Middle(500), HeadSize * 1.2, WorldStrikeKind.Meteor);

        Assert.True(victim.Path.Count < 11 - 1);
    }

    [Fact]
    public void ResolveMines_WithResistance_FewerSegmentsBreak()
    {
        var service = WithPerks(RewardPerk("tough", PerkEffect.EventResistance, 0.95, target: "mines"));
        var victim = AddFed("v", 500, 500, length: 11, score: 10);
        AddMine(Middle(victim.CurrentPosition.X), Middle(victim.CurrentPosition.Y), radius: HeadSize * 2.5);

        service.ResolveMines(_state);

        Assert.Equal(11, victim.Path.Count);
    }

    // Plunder.

    [Fact]
    public async Task Plunder_AKillPaysAShareOfTheVictimsMass()
    {
        var service = WithPerks(RewardPerk("loot", PerkEffect.Plunder, 0.5));
        var killer = AddFed("k", 100, 900, score: 10);
        var victim = AddPlayer("v", 500, 500, score: 40);
        AddShot(killer, victim.CurrentPosition.X, victim.CurrentPosition.Y);

        await service.CheckObstacleCollision(_state, victim);

        Assert.False(victim.IsAlive);
        Assert.Equal(30, killer.Score);
    }

    // Rewards.

    private void Eat(GameStateService service, Player player, PerkReward reward, string? grants = null)
    {
        var food = new Food(Guid.NewGuid())
        {
            Position = At(player.CurrentPosition.X, player.CurrentPosition.Y), Color = new Color("#00ff00", "#00ff00"), Value = 1,
            Kind = FoodKind.Prize, Reward = reward, GrantsPerk = grants
        };
        _state.Foods.Add(food.Guid, food);
        service.CheckFoodCollision(_state, player).GetAwaiter().GetResult();
    }

    [Fact]
    public void Reward_BonusMassAndMend_AreGivenAtOnce()
    {
        var player = AddFed("p", 500, 500, score: 10);
        player.SegmentHealth = player.SegmentHealth.Select(_ => 0.3).ToList();

        Eat(_service, player, new PerkReward { BonusMass = 25, Mend = true });

        Assert.Equal(10 + 1 + 25, player.Score);
        Assert.All(player.SegmentHealth, health => Assert.Equal(1.0, health));
    }

    [Fact]
    public void Reward_ExtraLives_AreAddedToTheLivesOfTheSnake()
    {
        var player = AddFed("p", 500, 500);

        Eat(_service, player, new PerkReward { ExtraLives = 2 });

        Assert.Equal(2, player.BonusLives);
        Assert.Equal(2, player.LivesLeft);
    }

    [Fact]
    public void Reward_ARandomPerk_IsOneTheSnakeDoesNotHave()
    {
        var service = WithPerks(RewardPerk("have", PerkEffect.Magnet, 10), RewardPerk("other", PerkEffect.Armor, 0.2, foods: 100));
        var player = AddFed("p", 500, 500);

        Eat(service, player, new PerkReward { GrantsRandomPerk = true });

        Assert.Contains("other", player.GrantedPerks);
        Assert.Equal(["have", "other"], player.Perks);
    }

    [Fact]
    public void Reward_ARandomPerk_WhenTheSnakeHasThemAll_GivesNothing()
    {
        var service = WithPerks(RewardPerk("have", PerkEffect.Magnet, 10));
        var player = AddFed("p", 500, 500);

        Eat(service, player, new PerkReward { GrantsRandomPerk = true });

        Assert.Empty(player.GrantedPerks);
    }

    private static readonly List<PerkTier> FourTiers =
    [
        new() { FoodsRequired = 10, Multiplier = 1.5 }, new() { FoodsRequired = 20, Multiplier = 2 }, new() { FoodsRequired = 30, Multiplier = 3 }
    ];

    [Fact]
    public void Reward_ATierSurge_LiftsAPerkTheSnakeHasBeyondItsFoods()
    {
        var service = WithPerks(RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers));
        var player = AddFed("p", 500, 500, foods: 1);

        Eat(service, player, new PerkReward { TierSurgeMin = 2, TierSurgeMax = 2 });

        Assert.Equal(2, player.TierSurges["magnet"]);
        Assert.Equal([3], player.PerkTiers);
    }

    [Fact]
    public void Reward_ATierSurge_OfANamedPerk_LiftsThatOne()
    {
        var service = WithPerks(
            RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers), RewardPerk("armor", PerkEffect.Armor, 0.1, tiers: FourTiers));
        var player = AddFed("p", 500, 500);

        Eat(service, player, new PerkReward { TierSurgeMin = 1, TierSurgeMax = 1, TierSurgePerk = "armor" });

        Assert.Equal(1, player.TierSurges["armor"]);
        Assert.DoesNotContain("magnet", player.TierSurges.Keys);
    }

    [Fact]
    public void Reward_ATierSurge_IsPickedInItsRange()
    {
        var service = WithPerks(RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers));
        var amounts = Enumerable.Range(0, 40).Select(_ =>
        {
            var player = AddFed("p" + Guid.NewGuid().ToString("N")[..4], 500, 500);
            Eat(service, player, new PerkReward { TierSurgeMin = 1, TierSurgeMax = 2 });
            return player.TierSurges["magnet"];
        }).ToArray();

        Assert.All(amounts, amount => Assert.InRange(amount, 1, 2));
        Assert.Contains(1, amounts);
        Assert.Contains(2, amounts);
    }

    [Fact]
    public void Reward_ATierSurge_NeverGoesPastTheLastTier()
    {
        var service = WithPerks(RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers));
        var player = AddFed("p", 500, 500);

        Eat(service, player, new PerkReward { TierSurgeMin = 9, TierSurgeMax = 9 });
        Eat(service, player, new PerkReward { TierSurgeMin = 9, TierSurgeMax = 9 });

        Assert.Equal([4], player.PerkTiers);
        Assert.Equal(3, player.TierSurges["magnet"]);
    }

    [Fact]
    public void Reward_ATierSurge_WithNothingToLift_DoesNothing()
    {
        var service = WithPerks(RewardPerk("flat", PerkEffect.Magnet, 100));
        var player = AddFed("p", 500, 500);

        Eat(service, player, new PerkReward { TierSurgeMin = 2, TierSurgeMax = 2 });

        Assert.Empty(player.TierSurges);
    }

    [Fact]
    public void Reward_ATierSurge_ShowsInTheEffectsAtOnce()
    {
        var service = WithPerks(RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers));
        var player = AddFed("p", 500, 500);
        AddFood(300, 300, 1);
        var near = _state.Foods.Values.Single();

        Eat(service, player, new PerkReward { TierSurgeMin = 3, TierSurgeMax = 3 });
        player.CurrentPosition = At(300 + 280, 300);
        service.CheckFoodCollision(_state, player).GetAwaiter().GetResult();

        Assert.True(near.Position.X > 300);
    }

    [Fact]
    public void Reward_ASurge_IsForgottenWhenTheSnakeDiesAndComesBack()
    {
        var service = WithPerks(RewardPerk("magnet", PerkEffect.Magnet, 100, tiers: FourTiers));
        var player = AddFed("p", 500, 500);
        Eat(service, player, new PerkReward { TierSurgeMin = 2, TierSurgeMax = 2 });

        var fresh = AddFed("q", 500, 700);

        Assert.Empty(fresh.TierSurges);
    }

    [Fact]
    public void Reward_TheBoss_GivesItsRewardToTheSnakeThatBringsItDown()
    {
        var boss = AddBoss(hp: 1);
        var shooter = AddFed("s", 100, 100);
        var reward = new PerkReward { ExtraLives = 1, BonusMass = 7 };
        var loaded = new WorldBoss
        {
            Id = boss.Id, X = boss.X, Y = boss.Y, Size = boss.Size, Hp = 1, MaxHp = 1, Arms = 4, ShotSpeed = 0.5, RewardMass = 0, Reward = reward
        };
        _state.Boss = loaded;
        var shot = new Obstacle(Guid.NewGuid(), HeadSize, _room, shooter)
        {
            InitialPosition = At(boss.X, boss.Y), CurrentPosition = At(boss.X - 7, boss.Y - 7), EndPosition = At(5000, 5000),
            Direction = new MovementDirection(Direction.Right, At(1, 0))
        };
        _state.Obstacles.Add(shot.Guid, shot);

        _service.ResolveBossHits(_state).GetAwaiter().GetResult();

        Assert.Null(_state.Boss);
        Assert.Equal(1, shooter.BonusLives);
        Assert.Equal(7, shooter.Score);
    }

    [Fact]
    public async Task Reward_TheBounty_GivesItsRewardToTheKiller()
    {
        var killer = AddFed("k", 100, 900);
        var target = AddPlayer("t", 500, 500, score: 5);
        _state.Bounty = new WorldBounty(target.Guid, _clock.GetUtcNow().AddMinutes(1), 10, 0, new PerkReward { ExtraLives = 1 });
        AddShot(killer, target.CurrentPosition.X, target.CurrentPosition.Y);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Equal(1, killer.BonusLives);
    }

    // Cooldown abilities.

    private Player AbilityUser(PerkDefinition perk, long score = 5)
    {
        var player = AddFed("user", 500, 500, score: score);
        _playerService.Get("user").Returns(player);
        return player;
    }

    private static PerkDefinition Shield(int cooldown = 30, int duration = 3)
        => RewardPerk("shield", PerkEffect.ShieldBurst, 1, cooldown: cooldown, duration: duration);

    private async Task<GameStateService> Refreshed(Player player, GameStateService service)
    {
        AddFood(player.CurrentPosition.X, player.CurrentPosition.Y, 1);
        await service.CheckFoodCollision(_state, player);
        return service;
    }

    [Fact]
    public async Task UseAbility_AShieldBurst_ProtectsForItsDurationThenRecharges()
    {
        var service = WithPerks(Shield());
        var player = AbilityUser(Shield());
        await Refreshed(player, service);

        Assert.True(service.UseAbilityByPlayerId("user"));
        await ShootAtTheHead(service, player);
        Assert.True(player.IsAlive);
        Assert.Equal(0, player.LivesUsed);

        _clock.Advance(TimeSpan.FromSeconds(4));
        await ShootAtTheHead(service, player);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public async Task UseAbility_WhileRecharging_DoesNothing_AndWorksAgainWhenReady()
    {
        var service = WithPerks(Shield(cooldown: 30));
        var player = AbilityUser(Shield());
        await Refreshed(player, service);

        Assert.True(service.UseAbilityByPlayerId("user"));
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.False(service.UseAbilityByPlayerId("user"));

        _clock.Advance(TimeSpan.FromSeconds(21));
        Assert.True(service.UseAbilityByPlayerId("user"));
    }

    [Fact]
    public async Task UseAbility_TheCooldownIsKept_ForTheSnapshot()
    {
        var service = WithPerks(Shield(cooldown: 30, duration: 3));
        var player = AbilityUser(Shield());
        await Refreshed(player, service);

        service.UseAbilityByPlayerId("user");

        var timer = player.Abilities["shield"];
        Assert.Equal(_clock.GetUtcNow().AddSeconds(30), timer.ReadyAt);
        Assert.Equal(_clock.GetUtcNow().AddSeconds(3), timer.ActiveUntil);
    }

    [Fact]
    public async Task UseAbility_WithoutAnAbility_ReturnsFalse()
    {
        var player = AbilityUser(Shield());
        await Refreshed(player, _service);

        Assert.False(_service.UseAbilityByPlayerId("user"));
    }

    [Fact]
    public async Task UseAbility_ADead_Snake_CannotUseIt()
    {
        var service = WithPerks(Shield());
        var player = AbilityUser(Shield());
        await Refreshed(player, service);
        player.IsAlive = false;

        Assert.False(service.UseAbilityByPlayerId("user"));
    }

    [Fact]
    public async Task UseAbility_ADash_IsFasterForItsDuration_WithoutSpendingMass()
    {
        var dash = RewardPerk("dash", PerkEffect.Dash, 1.0, cooldown: 10, duration: 2);
        var service = WithPerks(dash);
        var player = AbilityUser(dash, score: 5);
        await Refreshed(player, service);
        var before = player.CurrentPosition.X;
        await service.MovePlayer(_state, player);
        var plainStep = player.CurrentPosition.X - before;

        service.UseAbilityByPlayerId("user");
        before = player.CurrentPosition.X;
        await service.MovePlayer(_state, player);
        var dashStep = player.CurrentPosition.X - before;

        Assert.Equal(plainStep * 2, dashStep, 3);
        Assert.Equal(6, player.Score);
    }

    [Fact]
    public async Task UseAbility_ADash_IsOverAfterItsDuration()
    {
        var dash = RewardPerk("dash", PerkEffect.Dash, 1.0, cooldown: 10, duration: 2);
        var service = WithPerks(dash);
        var player = AbilityUser(dash);
        await Refreshed(player, service);
        service.UseAbilityByPlayerId("user");
        _clock.Advance(TimeSpan.FromSeconds(3));
        var before = player.CurrentPosition.X;

        await service.MovePlayer(_state, player);

        Assert.Equal(3.75, player.CurrentPosition.X - before, 3);
    }

    [Fact]
    public async Task UseAbility_AHealPulse_HealsTheBodyAtOnce()
    {
        var pulse = RewardPerk("pulse", PerkEffect.HealPulse, 1000, cooldown: 10);
        var service = WithPerks(pulse);
        var player = AbilityUser(pulse);
        await Refreshed(player, service);
        player.SegmentHealth = player.SegmentHealth.Select(_ => 0.1).ToList();

        service.UseAbilityByPlayerId("user");

        Assert.All(player.SegmentHealth, health => Assert.Equal(1.0, health, 6));
    }

    [Fact]
    public async Task UseAbility_AVacuumBurst_PullsFoodInForItsDuration()
    {
        var vacuum = RewardPerk("vacuum", PerkEffect.VacuumBurst, 300, cooldown: 10, duration: 4);
        var service = WithPerks(vacuum);
        var player = AbilityUser(vacuum);
        await Refreshed(player, service);
        AddFood(player.CurrentPosition.X + 200, player.CurrentPosition.Y, 1);
        var far = _state.Foods.Values.Single();

        await service.CheckFoodCollision(_state, player);
        Assert.Equal(player.CurrentPosition.X + 200, far.Position.X);

        service.UseAbilityByPlayerId("user");
        await service.CheckFoodCollision(_state, player);

        Assert.True(far.Position.X < player.CurrentPosition.X + 200);
    }

    [Fact]
    public async Task AutoAbilities_AShieldBurst_GoesOffWhenAShotIsNear()
    {
        var service = WithPerks(Shield());
        var player = AbilityUser(Shield());
        await Refreshed(player, service);
        player.AutoAbilities = true;
        var shooter = AddPlayer("s", 100, 900);

        await service.MovePlayer(_state, player);
        Assert.Equal(default, player.Abilities["shield"].ActiveUntil);

        AddShot(shooter, player.CurrentPosition.X - 60, player.CurrentPosition.Y);
        await service.MovePlayer(_state, player);

        Assert.True(player.Abilities["shield"].ActiveUntil > _clock.GetUtcNow());
    }

    [Fact]
    public async Task AutoAbilities_Off_NothingGoesOffByItself()
    {
        var service = WithPerks(Shield());
        var player = AbilityUser(Shield());
        await Refreshed(player, service);
        var shooter = AddPlayer("s", 100, 900);
        AddShot(shooter, player.CurrentPosition.X - 60, player.CurrentPosition.Y);

        await service.MovePlayer(_state, player);

        Assert.Equal(default, player.Abilities["shield"].ActiveUntil);
    }

    [Fact]
    public async Task AutoAbilities_AHealPulse_GoesOffWhenTheBodyIsHurt()
    {
        var pulse = RewardPerk("pulse", PerkEffect.HealPulse, 1000, cooldown: 10);
        var service = WithPerks(pulse);
        var player = AbilityUser(pulse);
        await Refreshed(player, service);
        player.AutoAbilities = true;
        await service.MovePlayer(_state, player);
        Assert.Equal(default, player.Abilities["pulse"].ActiveUntil);
        Assert.True(player.Abilities["pulse"].ReadyAt <= _clock.GetUtcNow());

        player.SegmentHealth = player.SegmentHealth.Select(_ => 0.3).ToList();
        await service.MovePlayer(_state, player);

        Assert.True(player.Abilities["pulse"].ReadyAt > _clock.GetUtcNow());
    }

    [Fact]
    public async Task AutoAbilities_ADash_GoesOffWhenTheSnakeBoosts()
    {
        var dash = RewardPerk("dash", PerkEffect.Dash, 1.0, cooldown: 10, duration: 2);
        var service = WithPerks(dash);
        var player = AbilityUser(dash);
        await Refreshed(player, service);
        player.AutoAbilities = true;

        await service.MovePlayer(_state, player);
        Assert.True(player.Abilities["dash"].ReadyAt <= _clock.GetUtcNow());

        player.IsBoosting = true;
        await service.MovePlayer(_state, player);

        Assert.True(player.Abilities["dash"].ReadyAt > _clock.GetUtcNow());
    }

    [Fact]
    public void SetAutoAbilities_StoresTheFlag()
    {
        var player = AbilityUser(Shield());

        Assert.True(_service.SetAutoAbilitiesByPlayerId("user", true));
        Assert.True(player.AutoAbilities);
    }

    [Fact]
    public async Task Abilities_ARefreshAddsReadyTimers_AndDropsTheOnesOfPerksNoLongerHad()
    {
        var service = WithPerks(Shield());
        var player = AbilityUser(Shield());
        player.Abilities["gone"] = new AbilityTimer(_clock.GetUtcNow().AddMinutes(1), default);

        await Refreshed(player, service);

        Assert.Contains("shield", player.Abilities.Keys);
        Assert.DoesNotContain("gone", player.Abilities.Keys);
        Assert.True(player.Abilities["shield"].ReadyAt <= _clock.GetUtcNow());
    }
}
