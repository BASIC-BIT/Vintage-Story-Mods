using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace thebasics.ModSystems.Analytics;

public interface IAnalyticsSink : IDisposable
{
    bool IsEnabled { get; }

    void Track(string eventName, IDictionary<string, object> properties);

    Task FlushAsync();
}
