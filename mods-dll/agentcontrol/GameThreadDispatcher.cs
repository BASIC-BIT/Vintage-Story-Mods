using System.Collections.Concurrent;

namespace AgentControl;

internal sealed class GameThreadDispatcher
{
    private readonly ConcurrentQueue<Action<bool>> _work = new();
    private volatile bool _shutdown;

    public Task<T> Invoke<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(shuttingDown =>
        {
            if (shuttingDown)
            {
                completion.TrySetException(new ObjectDisposedException(nameof(GameThreadDispatcher)));
                return;
            }
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });
        if (_shutdown)
        {
            CancelPending();
        }
        return completion.Task;
    }

    public void Drain(int maxItems = 32)
    {
        while (!_shutdown && maxItems-- > 0 && _work.TryDequeue(out var action))
        {
            action(false);
        }
    }

    public void Shutdown()
    {
        _shutdown = true;
        CancelPending();
    }

    private void CancelPending()
    {
        while (_work.TryDequeue(out var action))
        {
            action(true);
        }
    }
}
