using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace AgentControl;

public interface IAgentGame
{
    object Observe();
    void SetLook(float yaw, float pitch);
    void SetControl(string control, bool pressed);
    void ReleaseOwnedControls();
    void SelectSlot(int slot);
    void Send(string text);
    bool Evaluate(JsonElement condition);
    ValueTask<JsonElement> InvokeExtension(string operation, string callId, JsonElement arguments, CancellationToken cancellationToken);
    void Audit(string eventName, object details);
}

public interface IMonotonicClock
{
    long Milliseconds { get; }
}

public sealed class StopwatchClock : IMonotonicClock
{
    private readonly long _started = Stopwatch.GetTimestamp();
    public long Milliseconds => (long)Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
}

internal sealed record ExecutionRequest(
    string Id,
    IReadOnlyList<JsonElement> Actions,
    TaskCompletionSource<ExecutionReceipt> Completion,
    CancellationTokenSource Cancellation);

public sealed class ExecutionEngine : IDisposable
{
    private readonly IAgentGame _game;
    private readonly IMonotonicClock _clock;
    private readonly AgentControlConfig _config;
    private readonly ConcurrentQueue<ExecutionRequest> _queue = new();
    private ExecutionRequest? _active;
    private IEnumerator<ActionStep>? _steps;
    private ActionStep? _currentStep;
    private readonly List<ActionReceipt> _receipts = [];
    private int _queued;
    private bool _disposed;

    public ExecutionEngine(IAgentGame game, IMonotonicClock clock, AgentControlConfig config)
    {
        _game = game;
        _clock = clock;
        _config = config;
    }

    public bool IsActive => _active is not null;
    public int QueuedCount => Volatile.Read(ref _queued);

    public Task<ExecutionReceipt> Enqueue(string executionId, IReadOnlyList<JsonElement> actions)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEngine));
        }

        if (actions.Count is 0 || actions.Count > _config.MaxActionsPerExecution)
        {
            throw new ArgumentOutOfRangeException(nameof(actions), $"Action count must be between 1 and {_config.MaxActionsPerExecution}.");
        }

        if (Interlocked.Increment(ref _queued) > _config.QueueCapacity)
        {
            Interlocked.Decrement(ref _queued);
            throw new InvalidOperationException("Execution queue is full.");
        }

        var completion = new TaskCompletionSource<ExecutionReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new ExecutionRequest(executionId, actions, completion, new CancellationTokenSource()));
        return completion.Task;
    }

    public bool Cancel(string? executionId = null)
    {
        var active = _active;
        if (active is not null &&
            (executionId is null || string.Equals(executionId, active.Id, StringComparison.Ordinal)))
        {
            active.Cancellation.Cancel();
            return true;
        }

        if (executionId is not null)
        {
            var queued = _queue.FirstOrDefault(item => string.Equals(item.Id, executionId, StringComparison.Ordinal));
            if (queued is not null)
            {
                queued.Cancellation.Cancel();
                return true;
            }
        }
        return false;
    }

    public void CancelAll()
    {
        _active?.Cancellation.Cancel();
        foreach (var queued in _queue)
        {
            queued.Cancellation.Cancel();
        }
        _game.ReleaseOwnedControls();
    }

    public void Tick()
    {
        if (_disposed)
        {
            return;
        }

        if (_active is null)
        {
            StartNext();
        }

        if (_active is null || _steps is null)
        {
            return;
        }

        try
        {
            if (_active.Cancellation.IsCancellationRequested)
            {
                Finish("cancelled");
                return;
            }

            if (_clock.Milliseconds - _batchStartedMs > _config.MaxBatchDurationMs)
            {
                Finish("timed_out", "Batch deadline exceeded.");
                return;
            }

            if (_currentStep is null && !_steps.MoveNext())
            {
                Finish("completed");
                return;
            }

            _currentStep ??= _steps.Current;
            if (_currentStep.Tick(_clock.Milliseconds, _active.Cancellation.Token, out var receipt))
            {
                if (receipt is not null)
                {
                    _receipts.Add(receipt);
                }
                _currentStep = null;
            }
        }
        catch (Exception ex)
        {
            Finish("failed", ex.Message);
        }
    }

    private long _batchStartedMs;

    private void StartNext()
    {
        if (!_queue.TryDequeue(out var request))
        {
            return;
        }

        Interlocked.Decrement(ref _queued);
        _active = request;
        _batchStartedMs = _clock.Milliseconds;
        _receipts.Clear();
        _steps = BuildSteps(request).GetEnumerator();
        _game.Audit("execution.started", new { request.Id, ActionCount = request.Actions.Count });
    }

    private IEnumerable<ActionStep> BuildSteps(ExecutionRequest request)
    {
        for (var index = 0; index < request.Actions.Count; index++)
        {
            yield return ActionStep.Create(index, request.Actions[index], _game, _clock, _config, request.Id);
        }
    }

    private void Finish(string status, string? error = null)
    {
        var active = _active!;
        _game.ReleaseOwnedControls();
        _steps?.Dispose();
        _steps = null;
        _currentStep = null;
        var receipt = new ExecutionReceipt(active.Id, status, _batchStartedMs, _clock.Milliseconds, _receipts.ToArray(), error);
        _game.Audit("execution.finished", new { active.Id, Status = status, Error = error });
        active.Completion.TrySetResult(receipt);
        active.Cancellation.Dispose();
        _active = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _active?.Cancellation.Cancel();
        if (_active is not null)
        {
            Finish("cancelled", "Controller disposed.");
        }

        while (_queue.TryDequeue(out var queued))
        {
            Interlocked.Decrement(ref _queued);
            queued.Completion.TrySetResult(new ExecutionReceipt(queued.Id, "cancelled", _clock.Milliseconds, _clock.Milliseconds, [], "Controller disposed."));
            queued.Cancellation.Dispose();
        }

        _game.ReleaseOwnedControls();
    }
}
