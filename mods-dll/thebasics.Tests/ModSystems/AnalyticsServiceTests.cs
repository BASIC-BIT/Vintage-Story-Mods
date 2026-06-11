using FluentAssertions;
using thebasics.ModSystems.Analytics;

namespace thebasics.Tests.ModSystems;

[Collection(AnalyticsServiceTestCollection.Name)]
public class AnalyticsServiceTests : IDisposable
{
    public AnalyticsServiceTests()
    {
        AnalyticsService.Shutdown();
    }

    public void Dispose()
    {
        AnalyticsService.Shutdown();
    }

    [Fact]
    public void TrackFailure_WhenErrorTelemetryDisabled_DoesNotSendEvent()
    {
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(sink, allowErrorTelemetry: false);

        AnalyticsService.TrackFailure("config", "load", "critical", "load_failed", new InvalidOperationException("raw details"));

        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void TrackFailure_WhenEnabled_SendsSanitizedFailureEvent()
    {
        var sink = new RecordingAnalyticsSink();
        AnalyticsService.Configure(sink, allowErrorTelemetry: true);

        AnalyticsService.TrackFailure(" config ", " load ", " critical ", " load_failed ", new InvalidOperationException("raw details"));

        var analyticsEvent = sink.Events.Should().ContainSingle().Subject;
        analyticsEvent.Name.Should().Be("mod failure");
        analyticsEvent.Properties.Should().ContainKey("area").WhoseValue.Should().Be("config");
        analyticsEvent.Properties.Should().ContainKey("operation").WhoseValue.Should().Be("load");
        analyticsEvent.Properties.Should().ContainKey("severity").WhoseValue.Should().Be("critical");
        analyticsEvent.Properties.Should().ContainKey("result").WhoseValue.Should().Be("load_failed");
        analyticsEvent.Properties.Should().ContainKey("exception_type").WhoseValue.Should().Be(nameof(InvalidOperationException));
        analyticsEvent.Properties.Should().NotContainKey("exception_message");
        analyticsEvent.Properties.Should().NotContainKey("stack_trace");
    }

    [Fact]
    public void TrackFailure_BeforeConfigure_QueuesUntilErrorTelemetryEnabled()
    {
        AnalyticsService.TrackFailure("config", "load", "critical", "load_failed");
        var sink = new RecordingAnalyticsSink();

        AnalyticsService.Configure(sink, allowErrorTelemetry: true);

        var analyticsEvent = sink.Events.Should().ContainSingle().Subject;
        analyticsEvent.Name.Should().Be("mod failure");
        analyticsEvent.Properties.Should().ContainKey("area").WhoseValue.Should().Be("config");
    }

    [Fact]
    public void TrackFailure_BeforeConfigure_DiscardsQueueWhenErrorTelemetryDisabled()
    {
        AnalyticsService.TrackFailure("config", "load", "critical", "load_failed");
        var sink = new RecordingAnalyticsSink();

        AnalyticsService.Configure(sink, allowErrorTelemetry: false);

        sink.Events.Should().BeEmpty();
    }

    private sealed class RecordingAnalyticsSink : IAnalyticsSink
    {
        public List<RecordedEvent> Events { get; } = new();

        public bool IsEnabled => true;

        public void Track(string eventName, IDictionary<string, object> properties)
        {
            Events.Add(new RecordedEvent(eventName, new Dictionary<string, object>(properties)));
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed record RecordedEvent(string Name, IDictionary<string, object> Properties);
}
