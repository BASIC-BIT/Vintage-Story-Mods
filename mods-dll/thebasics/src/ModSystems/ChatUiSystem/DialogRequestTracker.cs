using System;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class DialogRequestTracker
{
    private long _nextId;
    private long _pendingId;
    private Action _onFailure;

    public long Begin(Action onFailure, Action<Action, int> scheduleTimeout)
    {
        var id = ++_nextId;
        _pendingId = id;
        _onFailure = onFailure;
        scheduleTimeout(() => Fail(id), 30000);
        return id;
    }

    public bool Accept(long id)
    {
        // Untracked opens use zero and are accepted only while idle. They must
        // not consume a pending save/reload or prevent its timeout from firing.
        if (id != _pendingId) return false;
        Reset();
        return true;
    }

    public void Fail(long id)
    {
        if (id == 0 || id != _pendingId) return;
        var failure = _onFailure;
        Reset();
        failure?.Invoke();
    }

    public void Reset()
    {
        _pendingId = 0;
        _onFailure = null;
    }
}
