using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class RoomInputQueueTests
{
    private readonly IGameStateService _gameStateService = Substitute.For<IGameStateService>();
    private readonly Guid _roomId = Guid.NewGuid();

    private RoomInputQueue CreateQueue(int maxCommandsPerDrain = 256)
        => new(_gameStateService, maxCommandsPerDrain);

    private static IGameState StateOf(Guid roomId)
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(roomId);
        var state = Substitute.For<IGameState>();
        state.Room.Returns(room);
        return state;
    }

    [Fact]
    public async Task DrainAsync_QueuedCommands_ExecutesInFifoOrder()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new RecordingCommand("first", executed));
        queue.Enqueue(_roomId, new RecordingCommand("second", executed));
        queue.Enqueue(_roomId, new RecordingCommand("third", executed));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(new[] { "first", "second", "third" }, executed);
    }

    [Fact]
    public async Task DrainAsync_OtherRoomCommands_AreNotExecuted()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(Guid.NewGuid(), new RecordingCommand("other room", executed));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Empty(executed);
    }

    [Fact]
    public async Task DrainAsync_NothingQueued_DoesNothing()
        => await CreateQueue().DrainAsync(StateOf(_roomId));

    [Fact]
    public async Task DrainAsync_CommandEnqueuedWhileDraining_WaitsForNextDrain()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new EnqueuingCommand(queue, _roomId, new RecordingCommand("late", executed)));

        await queue.DrainAsync(StateOf(_roomId));
        Assert.Empty(executed);

        await queue.DrainAsync(StateOf(_roomId));
        Assert.Equal(new[] { "late" }, executed);
    }

    [Fact]
    public async Task DrainAsync_MoreThanCap_LeavesRestForNextDrain()
    {
        var queue = CreateQueue(maxCommandsPerDrain: 2);
        var executed = new List<string>();
        foreach (var name in new[] { "a", "b", "c" })
            queue.Enqueue(_roomId, new RecordingCommand(name, executed));

        await queue.DrainAsync(StateOf(_roomId));
        Assert.Equal(new[] { "a", "b" }, executed);

        await queue.DrainAsync(StateOf(_roomId));
        Assert.Equal(new[] { "a", "b", "c" }, executed);
    }

    [Fact]
    public async Task DrainAsync_CommandThrows_FaultsItsCompletionAndRunsTheRest()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        var failing = new FailingCommand();
        queue.Enqueue(_roomId, failing);
        queue.Enqueue(_roomId, new RecordingCommand("after", executed));

        await queue.DrainAsync(StateOf(_roomId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.Completion);
        Assert.Equal(new[] { "after" }, executed);
    }

    [Fact]
    public async Task DrainAsync_CommandReceivesGameStateAndService()
    {
        var queue = CreateQueue();
        var state = StateOf(_roomId);
        var command = new ContextCapturingCommand();
        queue.Enqueue(_roomId, command);

        await queue.DrainAsync(state);

        Assert.Same(state, command.Captured!.GameState);
        Assert.Same(_gameStateService, command.Captured.GameStateService);
    }

    [Fact]
    public async Task DrainAsync_SameCoalesceKey_OnlyTheLastCommandRuns()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "left", executed));
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "up", executed));
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "right", executed));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(new[] { "right" }, executed);
    }

    [Fact]
    public async Task DrainAsync_DifferentCoalesceKeys_AllRunInOrder()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "a-up", executed));
        queue.Enqueue(_roomId, new KeyedCommand("direction:b", "b-left", executed));
        queue.Enqueue(_roomId, new KeyedCommand("boost:a", "a-boost", executed));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(new[] { "a-up", "b-left", "a-boost" }, executed);
    }

    [Fact]
    public async Task DrainAsync_CoalescedCommandsInterleavedWithOthers_KeepTheLastOnesPosition()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "first", executed));
        queue.Enqueue(_roomId, new RecordingCommand("shoot", executed));
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "second", executed));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(new[] { "shoot", "second" }, executed);
    }

    [Fact]
    public async Task DrainAsync_SupersededCommand_CompletesWithDefaultInsteadOfHanging()
    {
        var queue = CreateQueue();
        var older = new KeyedCommand("direction:a", "older", new List<string>());
        queue.Enqueue(_roomId, older);
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "newer", new List<string>()));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(0, await older.Completion.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task DrainAsync_CoalescingNeverCrossesDrains()
    {
        var queue = CreateQueue();
        var executed = new List<string>();
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "one", executed));
        await queue.DrainAsync(StateOf(_roomId));
        queue.Enqueue(_roomId, new KeyedCommand("direction:a", "two", executed));
        await queue.DrainAsync(StateOf(_roomId));

        Assert.Equal(new[] { "one", "two" }, executed);
    }

    [Fact]
    public void RoomsWithPendingCommands_ReportsOnlyRoomsWithQueuedCommands()
    {
        var queue = CreateQueue();
        var busyRoom = Guid.NewGuid();
        queue.Enqueue(busyRoom, new RecordingCommand("x", new List<string>()));

        Assert.Equal(new[] { busyRoom }, queue.RoomsWithPendingCommands());
    }

    [Fact]
    public async Task RoomsWithPendingCommands_AfterDrain_IsEmpty()
    {
        var queue = CreateQueue();
        queue.Enqueue(_roomId, new RecordingCommand("x", new List<string>()));

        await queue.DrainAsync(StateOf(_roomId));

        Assert.Empty(queue.RoomsWithPendingCommands());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidCap_Throws(int cap)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RoomInputQueue(_gameStateService, cap));

    private sealed class RecordingCommand(string name, List<string> executed) : PlayerCommand<int>
    {
        protected override Task<int> RunAsync(CommandContext context, CancellationToken cancellationToken)
        {
            executed.Add(name);
            return Task.FromResult(0);
        }
    }

    private sealed class KeyedCommand(string key, string name, List<string> executed) : PlayerCommand<int>, ICoalescableCommand
    {
        public string CoalesceKey => key;

        protected override Task<int> RunAsync(CommandContext context, CancellationToken cancellationToken)
        {
            executed.Add(name);
            return Task.FromResult(1);
        }
    }

    private sealed class FailingCommand : PlayerCommand<int>
    {
        protected override Task<int> RunAsync(CommandContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    private sealed class EnqueuingCommand(RoomInputQueue queue, Guid roomId, IPlayerCommand next) : PlayerCommand<int>
    {
        protected override Task<int> RunAsync(CommandContext context, CancellationToken cancellationToken)
        {
            queue.Enqueue(roomId, next);
            return Task.FromResult(0);
        }
    }

    private sealed class ContextCapturingCommand : PlayerCommand<int>
    {
        public CommandContext? Captured { get; private set; }

        protected override Task<int> RunAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Captured = context;
            return Task.FromResult(0);
        }
    }
}
