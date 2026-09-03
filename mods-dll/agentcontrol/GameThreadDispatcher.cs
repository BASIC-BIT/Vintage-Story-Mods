using System.Collections.Concurrent;

namespace AgentControl;

internal sealed class GameThreadDispatcher
{
    private readonly ConcurrentQueue<Action> _work = new();

    public Task<T> Invoke<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });
        return completion.Task;
    }

    public void Drain(int maxItems = 32)
    {
        while (maxItems-- > 0 && _work.TryDequeue(out var action))
        {
            action();
        }
    }
}
