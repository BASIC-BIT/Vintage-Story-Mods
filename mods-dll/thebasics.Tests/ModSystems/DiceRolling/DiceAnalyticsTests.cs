using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Tests.ModSystems;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

[Collection(AnalyticsServiceTestCollection.Name)]
public class DiceAnalyticsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AggregateTelemetryNeverContainsRollContents(bool isPrivate)
    {
        var sink = new Sink();
        AnalyticsService.Configure(sink);
        try
        {
            var result = DiceEvaluator.EvaluateInput("2d6+17 # secret reason", _ => 3);
            DiceAnalytics.RecordResult(result, ProximityChatMode.Whisper, isPrivate);
            if (isPrivate) Assert.Empty(sink.Events);
            else
            {
                var item = Assert.Single(sink.Events);
                Assert.Equal("dice", item["feature_name"]);
                Assert.Equal("whisper", item["chat_type"]);
                Assert.Equal(true, item["dice_arithmetic"]);
                Assert.DoesNotContain(item.Values, v => v.ToString()!.Contains("secret") || v.ToString()!.Contains("17"));
                Assert.DoesNotContain("pseudonymous_player_id", item.Keys);
            }
        }
        finally { AnalyticsService.Shutdown(); }
    }

    private sealed class Sink : IAnalyticsSink
    {
        public bool IsEnabled => true;
        public List<IDictionary<string, object>> Events { get; } = new();
        public void Track(string eventName, IDictionary<string, object> properties) => Events.Add(properties);
        public Task FlushAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
