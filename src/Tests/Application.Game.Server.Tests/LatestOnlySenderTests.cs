using Application.Game.Server.Services;

namespace Application.Game.Server.Tests;

public sealed class LatestOnlySenderTests
{
    private readonly List<(string Connection, int Value)> _delivered = new();
    private readonly Dictionary<string, TaskCompletionSource> _gates = new();
    private readonly List<(string Connection, Exception Failure)> _failures = new();
    private readonly HashSet<string> _blocked = new();

    private LatestOnlySender<int> Sender() => new(Deliver, (connection, failure) => { lock (_failures) _failures.Add((connection, failure)); });

    private async Task Deliver(string connection, int value)
    {
        TaskCompletionSource? gate = null;
        lock (_gates)
        {
            if (_blocked.Contains(connection)) _gates[connection + value] = gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (gate is not null) await gate.Task;
        if (value < 0) throw new InvalidOperationException("link broke");
        lock (_delivered) _delivered.Add((connection, value));
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private void Release(string connection, int value)
    {
        lock (_gates) _gates[connection + value].SetResult();
    }

    private int DeliveredCount { get { lock (_delivered) return _delivered.Count; } }

    [Fact]
    public async Task Send_ToAFreeConnection_DeliversIt()
    {
        var sender = Sender();

        sender.Send("a", 1);

        await Until(() => DeliveredCount == 1);
        Assert.Equal(("a", 1), _delivered[0]);
    }

    [Fact]
    public async Task Send_WhileADeliveryIsStuck_ReturnsAtOnceAndKeepsOnlyTheNewest()
    {
        _blocked.Add("slow");
        var sender = Sender();
        sender.Send("slow", 1);
        await Until(() => { lock (_gates) return _gates.ContainsKey("slow1"); });

        sender.Send("slow", 2);
        sender.Send("slow", 3);
        sender.Send("slow", 4);
        _blocked.Remove("slow");
        Release("slow", 1);

        await Until(() => DeliveredCount == 2);
        Assert.Equal(new[] { 1, 4 }, _delivered.Select(entry => entry.Value));
    }

    [Fact]
    public async Task Send_ToASlowConnection_DoesNotDelayAnother()
    {
        _blocked.Add("slow");
        var sender = Sender();
        sender.Send("slow", 1);

        sender.Send("fast", 7);

        await Until(() => DeliveredCount == 1);
        Assert.Equal(("fast", 7), _delivered[0]);
        Release("slow", 1);
    }

    [Fact]
    public async Task Send_AfterAFailedDelivery_StillDelivers()
    {
        var sender = Sender();
        sender.Send("a", -1);
        await Until(() => { lock (_failures) return _failures.Count == 1; });

        sender.Send("a", 5);

        await Until(() => DeliveredCount == 1);
        Assert.Equal(("a", 5), _delivered[0]);
        Assert.Equal("a", _failures[0].Connection);
    }
}
