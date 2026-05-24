using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.Analytics;

public sealed class AnalyticsEvent
{
    public AnalyticsEvent(string name, IDictionary<string, object> properties)
    {
        Name = name;
        Properties = new Dictionary<string, object>(properties);
        TimestampUtc = DateTime.UtcNow;
    }

    public string Name { get; }

    public Dictionary<string, object> Properties { get; }

    public DateTime TimestampUtc { get; }
}
