using System.Text.Json;
using FluentAssertions;

namespace AgentControl.Tests;

public sealed class ExecutionEngineTests
{
    [Fact]
    public async Task SequentialBatch_UsesMonotonicTicksAndProducesReceipts()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock);
        var execution = engine.Enqueue("batch-1", Actions(
            """{"type":"control","controls":{"forward":true},"durationMs":100}""",
            """{"type":"wait","durationMs":50}""",
            """{"type":"select_slot","slot":3}"""));

        engine.Tick();
        game.Controls["forward"].Should().BeTrue();
        clock.Advance(99);
        engine.Tick();
        execution.IsCompleted.Should().BeFalse();
        clock.Advance(1);
        engine.Tick();
        game.Controls["forward"].Should().BeFalse();
        engine.Tick();
        clock.Advance(50);
        engine.Tick();
        engine.Tick();
        engine.Tick();

        var receipt = await execution;
        receipt.Status.Should().Be("completed");
        receipt.Actions.Should().HaveCount(3);
        game.SelectedSlot.Should().Be(3);
        game.ReleaseCalls.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Cancel_ReleasesAssertedInputAndReturnsCancelled()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock);
        var execution = engine.Enqueue("cancel-me", Actions(
            """{"type":"control","controls":{"forward":true,"sprint":true},"durationMs":5000}"""));
        engine.Tick();
        engine.Cancel("cancel-me").Should().BeTrue();
        engine.Tick();

        var receipt = await execution;
        receipt.Status.Should().Be("cancelled");
        game.Controls.Values.Should().OnlyContain(value => !value);
        game.ReleaseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Timeout_ReleasesInputAndReturnsTimedOutOrFailedReceipt()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock, maxActionMs: 100);
        var execution = engine.Enqueue("timeout", Actions(
            """{"type":"control","controls":{"forward":true},"durationMs":1000}"""));
        engine.Tick();
        clock.Advance(101);
        engine.Tick();

        var receipt = await execution;
        receipt.Status.Should().Be("failed");
        receipt.Error.Should().Contain("exceeded");
        game.Controls["forward"].Should().BeFalse();
    }

    [Fact]
    public async Task NonPositiveTimeout_FallsBackToTheConfiguredMaximum()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock, maxActionMs: 100);
        var execution = engine.Enqueue("no-timeout", Actions(
            """{"type":"wait","durationMs":10,"timeoutMs":-1}"""));
        engine.Tick();
        clock.Advance(10);
        engine.Tick();
        engine.Tick();

        var receipt = await execution;
        receipt.Status.Should().Be("completed");
    }

    [Fact]
    public void Queue_IsBounded()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock, queueCapacity: 2);
        _ = engine.Enqueue("one", Actions("""{"type":"wait","durationMs":10}"""));
        _ = engine.Enqueue("two", Actions("""{"type":"wait","durationMs":10}"""));

        Action enqueue = () => _ = engine.Enqueue("three", Actions("""{"type":"wait","durationMs":10}"""));
        enqueue.Should().Throw<InvalidOperationException>().WithMessage("*queue is full*");
    }

    [Fact]
    public void Batch_IsBounded()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock, maxActions: 2);
        Action enqueue = () => _ = engine.Enqueue("too-many", Actions(
            """{"type":"wait","durationMs":1}""",
            """{"type":"wait","durationMs":1}""",
            """{"type":"wait","durationMs":1}"""));

        enqueue.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CancelAll_CancelsActiveAndQueuedExecutions()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock);
        var active = engine.Enqueue("active", Actions("""{"type":"control","controls":{"forward":true},"durationMs":5000}"""));
        var queued = engine.Enqueue("queued", Actions("""{"type":"select_slot","slot":9}"""));
        engine.Tick();
        engine.CancelAll();
        engine.Tick();
        engine.Tick();
        engine.Tick();

        (await active).Status.Should().Be("cancelled");
        (await queued).Status.Should().Be("cancelled");
        game.SelectedSlot.Should().Be(-1);
        game.Controls["forward"].Should().BeFalse();
    }

    [Fact]
    public async Task UnknownActions_FailClosedAndReleaseInput()
    {
        var clock = new FakeClock();
        var game = new FakeGame();
        using var engine = Create(game, clock);
        var execution = engine.Enqueue("unknown", Actions("""{"type":"shell.exec","command":"whoami"}"""));
        engine.Tick();

        var receipt = await execution;
        receipt.Status.Should().Be("failed");
        receipt.Error.Should().Contain("Unknown action type");
        game.ReleaseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RandomInvalidActionTypes_NeverEscapeTheClosedCatalog()
    {
        var random = new Random(2202);
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var clock = new FakeClock();
            var game = new FakeGame();
            using var engine = Create(game, clock);
            var type = Convert.ToHexString(RandomBytes(random, random.Next(1, 24)));
            var execution = engine.Enqueue($"fuzz-{iteration}", Actions(
                JsonSerializer.Serialize(new { type })));
            engine.Tick();
            var receipt = await execution;
            receipt.Status.Should().Be("failed");
            game.ReleaseCalls.Should().BeGreaterThan(0);
        }
    }

    private static ExecutionEngine Create(
        FakeGame game,
        FakeClock clock,
        int queueCapacity = 4,
        int maxActions = 32,
        int maxActionMs = 10_000) =>
        new(game, clock, new AgentControlConfig
        {
            QueueCapacity = queueCapacity,
            MaxActionsPerExecution = maxActions,
            MaxActionDurationMs = maxActionMs,
            MaxBatchDurationMs = 30_000
        });

    private static JsonElement[] Actions(params string[] json) =>
        json.Select(value => JsonDocument.Parse(value).RootElement.Clone()).ToArray();

    private static byte[] RandomBytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }
}

internal sealed class FakeClock : IMonotonicClock
{
    public long Milliseconds { get; private set; }
    public void Advance(long milliseconds) => Milliseconds += milliseconds;
}

internal sealed class FakeGame : IAgentGame
{
    public Dictionary<string, bool> Controls { get; } = new(StringComparer.Ordinal);
    public int SelectedSlot { get; private set; } = -1;
    public int ReleaseCalls { get; private set; }
    public List<(string Event, object Details)> AuditEvents { get; } = [];

    public object Observe() => new { connected = true };
    public void SetLook(float yaw, float pitch) { }
    public void SetControl(string control, bool pressed) => Controls[control] = pressed;
    public void ReleaseOwnedControls()
    {
        foreach (var control in Controls.Keys.ToArray())
        {
            Controls[control] = false;
        }
        ReleaseCalls++;
    }
    public void SelectSlot(int slot) => SelectedSlot = slot;
    public void Send(string text) { }
    public bool Evaluate(JsonElement condition) => true;
    public ValueTask<JsonElement> InvokeExtension(string operation, string callId, JsonElement arguments, CancellationToken cancellationToken) =>
        ValueTask.FromResult(JsonSerializer.SerializeToElement(new { operation }));
    public void Audit(string eventName, object details) => AuditEvents.Add((eventName, details));
}
