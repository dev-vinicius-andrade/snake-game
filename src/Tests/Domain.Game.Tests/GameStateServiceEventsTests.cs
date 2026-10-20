using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Tests;

/// <summary>What the mine field, the bounty and the sugar rush do inside <see cref="Domain.Game.Services.GameStateService"/>.</summary>
public sealed partial class GameStateServiceTests
{
    private WorldMine AddMine(double x, double y, double radius = 40, double trigger = 10, int armedInMilliseconds = 0)
    {
        var mine = new WorldMine(Guid.NewGuid(), x, y, radius, trigger, _clock.GetUtcNow().AddMilliseconds(armedInMilliseconds));
        _state.Mines[mine.Id] = mine;
        return mine;
    }

    // Sugar rush: boosting costs no mass while it lasts.

    [Fact]
    public async Task MovePlayer_DuringASugarRush_BoostingCostsNoMass()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        player.IsBoosting = true;
        _state.SugarRushEndsAt = _clock.GetUtcNow().AddSeconds(30);

        foreach (var _ in Enumerable.Range(0, 60)) await _service.MovePlayer(_state, player);

        Assert.Equal(5, player.Score);
    }

    [Fact]
    public async Task MovePlayer_DuringASugarRush_ASnakeWithNoMassStillBoosts()
    {
        var player = AddPlayer("a", 500, 500, score: 0);
        player.IsBoosting = true;
        _state.SugarRushEndsAt = _clock.GetUtcNow().AddSeconds(30);

        await _service.MovePlayer(_state, player);

        Assert.Equal(500 + 3.75 * 1.8, player.CurrentPosition.X, 3);
        Assert.Equal(0, player.Score);
    }

    [Fact]
    public async Task MovePlayer_AfterTheSugarRush_BoostingCostsMassAgain()
    {
        var player = AddPlayer("a", 500, 500, score: 5);
        player.IsBoosting = true;
        _state.SugarRushEndsAt = _clock.GetUtcNow().AddSeconds(30);
        _clock.Advance(TimeSpan.FromSeconds(31));

        foreach (var _ in Enumerable.Range(0, 30)) await _service.MovePlayer(_state, player);

        Assert.Equal(4, player.Score);
    }

    // ResolveMines: a head that touches an armed mine sets it off.

    [Fact]
    public void ResolveMines_AHeadOnAnArmedMine_SetsItOffAndBreaksTheSegmentsNearIt()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        var head = victim.CurrentPosition;
        AddMine(Middle(head.X), Middle(head.Y), radius: 20);

        _service.ResolveMines(_state);

        Assert.Empty(_state.Mines);
        Assert.True(victim.IsAlive);
        Assert.True(victim.Path.Count < 11);
        Assert.True(victim.Score < 10);
        Assert.NotEmpty(_state.Foods);
        Assert.Contains(_state.Hits, hit => hit.Kind == DamageKind.Lost && hit.TargetGuid == victim.Guid);
    }

    [Fact]
    public void ResolveMines_TheBlastNeverKillsTheSnakeThatTouchedIt()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        AddMine(Middle(victim.CurrentPosition.X), Middle(victim.CurrentPosition.Y), radius: 500);

        _service.ResolveMines(_state);

        Assert.True(victim.IsAlive);
        Assert.Equal(InitialLength, victim.Path.Count);
    }

    [Fact]
    public void ResolveMines_AMineThatIsNotArmedYet_IsLeftAlone()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        AddMine(Middle(victim.CurrentPosition.X), Middle(victim.CurrentPosition.Y), armedInMilliseconds: 1000);

        _service.ResolveMines(_state);

        Assert.Single(_state.Mines);
        Assert.Equal(11, victim.Path.Count);
    }

    [Fact]
    public void ResolveMines_ItIsArmedOnceItsMomentHasCome()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        AddMine(Middle(victim.CurrentPosition.X), Middle(victim.CurrentPosition.Y), armedInMilliseconds: 1000);
        _clock.Advance(TimeSpan.FromMilliseconds(1000));

        _service.ResolveMines(_state);

        Assert.Empty(_state.Mines);
    }

    [Fact]
    public void ResolveMines_NobodyCloseEnough_TheMineStays()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        AddMine(Middle(victim.CurrentPosition.X) + 100, Middle(victim.CurrentPosition.Y));

        _service.ResolveMines(_state);

        Assert.Single(_state.Mines);
        Assert.Equal(11, victim.Path.Count);
    }

    [Fact]
    public void ResolveMines_ABodySegmentOnAMineDoesNotSetItOff_OnlyAHead()
    {
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 10);
        var tail = victim.Path[0];
        AddMine(Middle(tail.X), Middle(tail.Y));

        _service.ResolveMines(_state);

        Assert.Single(_state.Mines);
    }

    [Fact]
    public void ResolveMines_AnInvulnerableSnakeNeitherSetsItOffNorIsHurt()
    {
        var service = WithScript("function modify() { return { invulnerable: true }; }", _room.Guid.ToString());
        var shielded = AddPlayer("shielded", 500, 500, length: 11, score: 10);
        shielded.FoodsEaten = 1;
        AddMine(Middle(shielded.CurrentPosition.X), Middle(shielded.CurrentPosition.Y));

        service.ResolveMines(_state);

        Assert.Single(_state.Mines);
        Assert.Equal(11, shielded.Path.Count);
    }

    [Fact]
    public void ResolveMines_ADeadSnakeSetsNothingOff()
    {
        var dead = AddPlayer("dead", 500, 500, length: 11, score: 10);
        dead.IsAlive = false;
        AddMine(Middle(dead.CurrentPosition.X), Middle(dead.CurrentPosition.Y));

        _service.ResolveMines(_state);

        Assert.Single(_state.Mines);
    }

    [Fact]
    public void ResolveMines_TheBlastAlsoHurtsOtherSnakesBodiesInReach()
    {
        var toucher = AddPlayer("toucher", 500, 500, length: 11, score: 10);
        var bystander = AddPlayer("bystander", 500, 560, length: 11, score: 10);
        AddMine(Middle(toucher.CurrentPosition.X), Middle(toucher.CurrentPosition.Y), radius: 80);

        _service.ResolveMines(_state);

        Assert.True(bystander.Path.Count < 11);
        Assert.True(bystander.IsAlive);
    }

    // Bounty: killing the wanted snake pays.

    private WorldBounty Wanted(IPlayer target, long reward = 100, int foods = 7)
    {
        var bounty = new WorldBounty(target.Guid, _clock.GetUtcNow().AddSeconds(90), reward, foods);
        _state.Bounty = bounty;
        return bounty;
    }

    [Fact]
    public async Task Bounty_ShootingTheWantedSnakeInTheHead_PaysTheShooterAndEndsTheMark()
    {
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100, score: 20);
        Wanted(target);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.False(target.IsAlive);
        Assert.Equal(120, shooter.Score);
        Assert.Equal(7 + new PerkOptions().KillFoodsCredit, shooter.FoodsEaten);
        Assert.Null(_state.Bounty);
    }

    [Fact]
    public async Task Bounty_TheWantedSnakeRunningIntoABody_PaysTheSnakeItRanInto()
    {
        var wall = AddPlayer("wall", 500, 500, score: 10);
        var target = AddPlayer("target", 900, 900, length: 8, score: 2);
        target.CurrentPosition = At(wall.Path[2].X, 505);
        target.Path[^1] = target.CurrentPosition;
        Wanted(target, reward: 100);

        await _service.ResolveSnakeCollisions(_state);

        Assert.False(target.IsAlive);
        Assert.Equal(110, wall.Score);
        Assert.Null(_state.Bounty);
    }

    [Fact]
    public async Task Bounty_KillingSomeoneElse_PaysNothingAndKeepsTheMark()
    {
        var target = AddPlayer("target", 100, 100, length: 11, score: 5);
        var victim = AddPlayer("victim", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100, score: 20);
        Wanted(target);
        AddShot(shooter, victim.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, victim);

        Assert.Equal(20, shooter.Score);
        Assert.NotNull(_state.Bounty);
    }

    [Fact]
    public async Task Bounty_NoMarkNoPayment()
    {
        var target = AddPlayer("target", 500, 500, length: 11, score: 5);
        var shooter = AddPlayer("shooter", 1500, 100, score: 20);
        AddShot(shooter, target.CurrentPosition.X, 500);

        await _service.CheckObstacleCollision(_state, target);

        Assert.Equal(20, shooter.Score);
    }
}
