using Application.Game.Server.Services;

namespace Application.Game.Server.Tests;

public sealed class DetachedTests
{
    [Fact]
    public async Task Run_ASendThatNeverFinishes_ReturnsAtOnce()
    {
        var gate = new TaskCompletionSource();
        var started = new TaskCompletionSource();

        Detached.Run(() => { started.SetResult(); return gate.Task; }, _ => { });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        gate.SetResult();
    }

    [Fact]
    public async Task Run_ASendThatFails_ReportsTheFailure()
    {
        var reported = new TaskCompletionSource<Exception>();

        Detached.Run(() => Task.FromException(new InvalidOperationException("link broke")), reported.SetResult);

        var failure = await reported.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("link broke", failure.Message);
    }

    [Fact]
    public async Task Run_ASendThatThrowsBeforeReturningATask_ReportsTheFailure()
    {
        var reported = new TaskCompletionSource<Exception>();

        Detached.Run(() => throw new InvalidOperationException("no connection"), reported.SetResult);

        var failure = await reported.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("no connection", failure.Message);
    }
}
