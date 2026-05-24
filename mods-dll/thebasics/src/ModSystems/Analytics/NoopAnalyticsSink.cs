using System.Collections.Generic;
using System.Threading.Tasks;

namespace thebasics.ModSystems.Analytics;

public sealed class NoopAnalyticsSink : IAnalyticsSink
{
    public static readonly NoopAnalyticsSink Instance = new();

    private NoopAnalyticsSink()
    {
    }

    public bool IsEnabled => false;

    public void Track(string eventName, IDictionary<string, object> properties)
    {
    }

    public Task FlushAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
