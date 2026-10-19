using Domain.Game.Stats;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;

namespace Domain.Game.Tests;

public sealed class LifeStatsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ReportId = Guid.NewGuid();

    private static LifeStatsReport Finish(LifeStats stats, double seconds = 0) => stats.Finish(Start.AddSeconds(seconds), ReportId);

    [Fact]
    public void Finish_AFreshLife_CountsOneGameAndKeepsTheStartingSize()
    {
        var report = Finish(LifeStats.Begin(Start, 3, 6));

        Assert.Equal(StatKeys.Counters.Order(), report.Counters.Keys.Order());
        Assert.Equal(StatKeys.Maxima.Order(), report.Maxima.Keys.Order());
        Assert.Equal(1, report.Counters[StatKeys.Games]);
        Assert.All(report.Counters.Where(counter => counter.Key != StatKeys.Games), counter => Assert.Equal(0, counter.Value));
        Assert.Equal((3L, 6L, 0L), (report.Maxima[StatKeys.BestScore], report.Maxima[StatKeys.LongestSnake], report.Maxima[StatKeys.BestKillsInLife]));
    }

    [Fact]
    public void Finish_GivesTheReportId()
    {
        Assert.Equal(ReportId, Finish(LifeStats.Begin(Start, 0, 6)).ReportId);
    }

    [Fact]
    public void Ate_CountsEveryFood_AndPrizesApart()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.Ate(FoodKind.Normal);
        stats.Ate(FoodKind.Normal);
        stats.Ate(FoodKind.Prize);

        var report = Finish(stats);
        Assert.Equal((3L, 1L), (report.Counters[StatKeys.Foods], report.Counters[StatKeys.Prizes]));
    }

    [Fact]
    public void Killed_CountsTheKills_AndTheirNumberIsTheBestOfTheLife()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.Killed();
        stats.Killed();

        var report = Finish(stats);
        Assert.Equal((2L, 2L), (report.Counters[StatKeys.Kills], report.Maxima[StatKeys.BestKillsInLife]));
    }

    [Fact]
    public void BroughtDownBoss_CountsABossKill()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.BroughtDownBoss();

        Assert.Equal(1, Finish(stats).Counters[StatKeys.BossKills]);
    }

    [Fact]
    public void Struck_CountsLightningAndMeteorsApart()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.Struck(WorldStrikeKind.Lightning);
        stats.Struck(WorldStrikeKind.Meteor);
        stats.Struck(WorldStrikeKind.Meteor);

        var report = Finish(stats);
        Assert.Equal((1L, 2L), (report.Counters[StatKeys.LightningHits], report.Counters[StatKeys.MeteorHits]));
    }

    [Fact]
    public void Died_IsOneDeath_HoweverOftenItIsSaid()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.Died();
        stats.Died();

        Assert.Equal(1, Finish(stats).Counters[StatKeys.Deaths]);
    }

    [Fact]
    public void Observe_KeepsThePeaks_NotTheLastValues()
    {
        var stats = LifeStats.Begin(Start, 0, 6);

        stats.Observe(120, 30);
        stats.Observe(40, 45);
        stats.Observe(10, 8);

        var report = Finish(stats);
        Assert.Equal((120L, 45L), (report.Maxima[StatKeys.BestScore], report.Maxima[StatKeys.LongestSnake]));
    }

    [Theory]
    [InlineData(90.7, 90)]
    [InlineData(0.4, 0)]
    [InlineData(-5, 0)]
    public void Finish_PlaySeconds_AreTheWholeSecondsFromBeginToEnd(double seconds, long expected)
    {
        Assert.Equal(expected, Finish(LifeStats.Begin(Start, 0, 6), seconds).Counters[StatKeys.PlaySeconds]);
    }

    [Fact]
    public void Finish_AValueAboveWhatOneReportMayCarry_IsCapped()
    {
        var stats = LifeStats.Begin(Start, 5_000_000, 6);

        var report = Finish(stats, 30 * 24 * 3600);

        Assert.Equal(LifeStats.MaxReportedValue, report.Maxima[StatKeys.BestScore]);
        Assert.Equal(LifeStats.MaxReportedValue, report.Counters[StatKeys.PlaySeconds]);
    }

    [Fact]
    public void Finish_NegativeScores_AreReportedAsZero()
    {
        Assert.Equal(0, Finish(LifeStats.Begin(Start, -4, 6)).Maxima[StatKeys.BestScore]);
    }
}
