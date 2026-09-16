using System.Text.Json;

namespace AgentControl;

internal abstract class ActionStep
{
    protected readonly int Index;
    protected readonly string Type;
    protected readonly IAgentGame Game;
    protected readonly long StartedMs;
    private readonly int _timeoutMs;

    protected ActionStep(int index, string type, IAgentGame game, IMonotonicClock clock, int timeoutMs)
    {
        Index = index;
        Type = type;
        Game = game;
        StartedMs = clock.Milliseconds;
        _timeoutMs = timeoutMs;
    }

    public bool Tick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nowMs - StartedMs > _timeoutMs)
        {
            throw new TimeoutException($"Action {Index} ({Type}) exceeded {_timeoutMs}ms.");
        }

        return OnTick(nowMs, cancellationToken, out receipt);
    }

    protected abstract bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt);

    protected ActionReceipt Complete(long nowMs, object? detail = null) =>
        new(Index, Type, "completed", StartedMs, nowMs, detail);

    public static ActionStep Create(
        int index,
        JsonElement action,
        IAgentGame game,
        IMonotonicClock clock,
        AgentControlConfig config,
        string executionId)
    {
        var type = action.GetProperty("type").GetString() ?? throw new InvalidOperationException("Action type is required.");
        var requestedTimeoutMs = action.TryGetProperty("timeoutMs", out var timeout)
            ? timeout.GetInt32()
            : config.MaxActionDurationMs;
        var timeoutMs = requestedTimeoutMs <= 0
            ? config.MaxActionDurationMs
            : Math.Min(requestedTimeoutMs, config.MaxActionDurationMs);
        return type switch
        {
            "look" => new ImmediateStep(index, type, game, clock, timeoutMs, () =>
            {
                game.SetLook(action.GetProperty("yaw").GetSingle(), action.GetProperty("pitch").GetSingle());
                return null;
            }),
            "control" => new TimedControlStep(index, game, clock, timeoutMs, action),
            "select_slot" => new ImmediateStep(index, type, game, clock, timeoutMs, () =>
            {
                game.SelectSlot(action.GetProperty("slot").GetInt32());
                return null;
            }),
            "send" => new ImmediateStep(index, type, game, clock, timeoutMs, () =>
            {
                game.Send(action.GetProperty("text").GetString() ?? string.Empty);
                return new { kind = "client-send" };
            }),
            "wait" => new DelayStep(index, type, game, clock, timeoutMs, action.GetProperty("durationMs").GetInt32()),
            "wait_for" => new WaitForStep(index, game, clock, timeoutMs, action.GetProperty("condition").Clone()),
            "extension.invoke" => new ExtensionStep(index, game, clock, timeoutMs, executionId, action),
            _ => throw new InvalidOperationException($"Unknown action type '{type}'.")
        };
    }
}

internal sealed class ImmediateStep : ActionStep
{
    private readonly Func<object?> _action;
    private bool _done;

    public ImmediateStep(int index, string type, IAgentGame game, IMonotonicClock clock, int timeoutMs, Func<object?> action)
        : base(index, type, game, clock, timeoutMs) => _action = action;

    protected override bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        if (_done)
        {
            receipt = null;
            return true;
        }

        var detail = _action();
        _done = true;
        receipt = Complete(nowMs, detail);
        return true;
    }
}

internal sealed class DelayStep : ActionStep
{
    private readonly int _durationMs;

    public DelayStep(int index, string type, IAgentGame game, IMonotonicClock clock, int timeoutMs, int durationMs)
        : base(index, type, game, clock, timeoutMs)
    {
        _durationMs = Math.Max(0, durationMs);
    }

    protected override bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        if (nowMs - StartedMs < _durationMs)
        {
            receipt = null;
            return false;
        }

        receipt = Complete(nowMs);
        return true;
    }
}

internal sealed class TimedControlStep : ActionStep
{
    private readonly IReadOnlyDictionary<string, bool> _controls;
    private readonly int _durationMs;
    private bool _asserted;

    public TimedControlStep(int index, IAgentGame game, IMonotonicClock clock, int timeoutMs, JsonElement action)
        : base(index, "control", game, clock, timeoutMs)
    {
        _durationMs = action.GetProperty("durationMs").GetInt32();
        _controls = action.GetProperty("controls").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetBoolean(), StringComparer.Ordinal);
    }

    protected override bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        if (!_asserted)
        {
            foreach (var control in _controls)
            {
                Game.SetControl(control.Key, control.Value);
            }
            _asserted = true;
        }

        if (nowMs - StartedMs < _durationMs)
        {
            receipt = null;
            return false;
        }

        Game.ReleaseOwnedControls();
        receipt = Complete(nowMs, new { durationMs = _durationMs });
        return true;
    }
}

internal sealed class WaitForStep : ActionStep
{
    private readonly JsonElement _condition;

    public WaitForStep(int index, IAgentGame game, IMonotonicClock clock, int timeoutMs, JsonElement condition)
        : base(index, "wait_for", game, clock, timeoutMs) => _condition = condition;

    protected override bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        if (!Game.Evaluate(_condition))
        {
            receipt = null;
            return false;
        }

        receipt = Complete(nowMs, new { condition = _condition });
        return true;
    }
}

internal sealed class ExtensionStep : ActionStep
{
    private readonly string _operation;
    private readonly string _callId;
    private readonly JsonElement _arguments;
    private ValueTask<JsonElement>? _pending;

    public ExtensionStep(int index, IAgentGame game, IMonotonicClock clock, int timeoutMs, string executionId, JsonElement action)
        : base(index, "extension.invoke", game, clock, timeoutMs)
    {
        _operation = action.GetProperty("operation").GetString() ?? throw new InvalidOperationException("Extension operation is required.");
        _callId = $"{executionId}:{index}";
        _arguments = action.TryGetProperty("arguments", out var arguments)
            ? arguments.Clone()
            : JsonSerializer.SerializeToElement(new { });
    }

    protected override bool OnTick(long nowMs, CancellationToken cancellationToken, out ActionReceipt? receipt)
    {
        _pending ??= Game.InvokeExtension(_operation, _callId, _arguments, cancellationToken);
        if (!_pending.Value.IsCompleted)
        {
            receipt = null;
            return false;
        }

        receipt = Complete(nowMs, _pending.Value.GetAwaiter().GetResult());
        return true;
    }
}
