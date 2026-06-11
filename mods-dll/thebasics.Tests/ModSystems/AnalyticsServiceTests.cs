using FluentAssertions;
using thebasics.Configs;
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

    [Fact]
    public void TrackConfigSnapshot_IncludesTeleportationSettings()
    {
        var sink = new RecordingAnalyticsSink();
        var config = new ModConfig
        {
            TpaRequestPrivilege = "tpauser",
            TpaCooldownInGameHours = 2,
            TpaTimeoutMinutes = 12,
            HomeCommandPrivilege = "homeuser",
            SetHomeCommandPrivilege = "sethomeuser",
            SpawnCommandPrivilege = "spawnuser",
            SetSpawnCommandPrivilege = "setspawnuser",
            Teleportation = new TeleportationConfig
            {
                MaxHomes = 5,
                HomeWarmupSeconds = 6,
                SpawnWarmupSeconds = 7,
                TpaWarmupSeconds = 8,
                TopWarmupSeconds = 9,
                StuckWarmupSeconds = 90,
                HomeCooldownSeconds = 120,
                SpawnCooldownSeconds = 180,
                TopCooldownSeconds = 240,
                StuckCooldownSeconds = 7200,
                StuckReminderIntervalSeconds = 60,
                CancelWarmupOnDamage = false,
                CancelWarmupOnInteraction = false,
                StuckCommandPrivilege = "stuckuser",
                StuckAdminNotifyPrivilege = "staff",
                StuckBlockedByOnlinePrivilege = "helper",
                TopCommandPrivilege = "topuser"
            }
        };
        AnalyticsService.Configure(sink);

        AnalyticsService.TrackConfigSnapshot(config);

        var properties = sink.Events.Should().ContainSingle().Subject.Properties;
        properties.Should().ContainKey("tpa_request_custom_privilege").WhoseValue.Should().Be(true);
        properties.Should().ContainKey("home_custom_privilege").WhoseValue.Should().Be(true);
        properties.Should().ContainKey("stuck_admin_notify_custom_privilege").WhoseValue.Should().Be(true);
        properties.Should().ContainKey("stuck_blocks_when_privilege_online").WhoseValue.Should().Be(true);
        properties.Should().ContainKey("top_custom_privilege").WhoseValue.Should().Be(true);
        properties.Should().ContainKey("max_homes_bucket").WhoseValue.Should().Be("1-5");
        properties.Should().ContainKey("top_warmup_seconds_bucket").WhoseValue.Should().Be("6-10");
        properties.Should().ContainKey("home_cooldown_seconds_bucket").WhoseValue.Should().Be("101+");
        properties.Should().ContainKey("spawn_cooldown_seconds_bucket").WhoseValue.Should().Be("101+");
        properties.Should().ContainKey("top_cooldown_seconds_bucket").WhoseValue.Should().Be("101+");
        properties.Should().ContainKey("stuck_cooldown_seconds_bucket").WhoseValue.Should().Be("101+");
        properties.Should().ContainKey("stuck_reminder_interval_seconds_bucket").WhoseValue.Should().Be("51-100");
        properties.Should().ContainKey("teleport_cancel_warmup_on_damage").WhoseValue.Should().Be(false);
        properties.Should().ContainKey("teleport_cancel_warmup_on_interaction").WhoseValue.Should().Be(false);
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
