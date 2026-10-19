using Application.Game.Server.Services;
using Library.Commons.Game.Server.Constants;

namespace Application.Game.Server.Tests;

internal sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class JoinTicketsTests
{
    private readonly ManualTime _time = new();
    private readonly JoinTickets _tickets;
    private readonly Guid _account = Guid.NewGuid();
    private readonly Guid _room = Guid.NewGuid();

    public JoinTicketsTests() => _tickets = new JoinTickets(_time);

    [Fact]
    public void Redeem_TheTicketJustIssued_GivesTheAccount()
    {
        var ticket = _tickets.Issue(_account, "Vini#09291", _room);

        Assert.Equal(new PendingAccount(_account, "Vini#09291", _room), _tickets.Redeem(ticket, _room));
    }

    [Fact]
    public void Redeem_ATicketTwice_WorksOnce()
    {
        var ticket = _tickets.Issue(_account, "Vini#09291", _room);
        _tickets.Redeem(ticket, _room);

        Assert.Null(_tickets.Redeem(ticket, _room));
    }

    [Fact]
    public void Redeem_InAnotherRoom_GivesNothing()
    {
        var ticket = _tickets.Issue(_account, "Vini#09291", _room);

        Assert.Null(_tickets.Redeem(ticket, Guid.NewGuid()));
    }

    [Fact]
    public void Redeem_AfterItsLifetime_GivesNothing()
    {
        var ticket = _tickets.Issue(_account, "Vini#09291", _room);
        _time.Now += JoinTickets.Lifetime;

        Assert.Null(_tickets.Redeem(ticket, _room));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-ticket")]
    public void Redeem_NoOrAnUnknownTicket_GivesNothing(string? ticket)
    {
        _tickets.Issue(_account, "Vini#09291", _room);

        Assert.Null(_tickets.Redeem(ticket, _room));
    }

    [Fact]
    public void Issue_EveryTicketIsANewRandomSecret()
    {
        var first = _tickets.Issue(_account, "Vini#09291", _room);
        var second = _tickets.Issue(_account, "Vini#09291", _room);

        Assert.NotEqual(first, second);
        Assert.Equal(32, first.Length);
    }

    [Fact]
    public void Issue_WhenFull_DropsTheTicketClosestToExpiring()
    {
        var oldest = _tickets.Issue(_account, "Vini#09291", _room);
        _time.Now += TimeSpan.FromSeconds(1);
        for (var issued = 1; issued < JoinTickets.MaxPending; issued++)
            _tickets.Issue(Guid.NewGuid(), "Other#00001", _room);

        var newest = _tickets.Issue(_account, "Vini#09291", _room);

        Assert.Null(_tickets.Redeem(oldest, _room));
        Assert.NotNull(_tickets.Redeem(newest, _room));
    }
}

public sealed class JoinTicketQueryTests
{
    [Theory]
    [InlineData("https://game.example.com/game", "abc", "https://game.example.com/game?joinTicket=abc")]
    [InlineData("https://game.example.com/game?region=eu", "abc", "https://game.example.com/game?region=eu&joinTicket=abc")]
    [InlineData("https://game.example.com/game", null, "https://game.example.com/game")]
    [InlineData("https://game.example.com/game", " ", "https://game.example.com/game")]
    [InlineData("https://game.example.com/game", "a b&c", "https://game.example.com/game?joinTicket=a%20b%26c")]
    public void AppendTo_PutsTheTicketInTheQuery_OnlyWhenThereIsOne(string hubUrl, string? ticket, string expected)
    {
        Assert.Equal(expected, JoinTicketQuery.AppendTo(hubUrl, ticket));
    }
}
