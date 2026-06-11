using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using thebasics.Configs;

namespace thebasics.ModSystems.Analytics;

public static class AnalyticsService
{
    private const int MaxPendingFailureEvents = 25;
    private static IAnalyticsSink _sink = NoopAnalyticsSink.Instance;
    private static readonly Queue<PendingAnalyticsEvent> PendingFailureEvents = new();
    private static readonly object PendingFailureLock = new();
    private static volatile bool _allowErrorTelemetry;
    private static volatile bool _errorTelemetryConfigured;

    public static bool IsEnabled => _sink.IsEnabled;

    public static void Configure(IAnalyticsSink sink, bool allowErrorTelemetry = false)
    {
        var next = sink ?? NoopAnalyticsSink.Instance;
        var previous = Interlocked.Exchange(ref _sink, next);
        _allowErrorTelemetry = allowErrorTelemetry;
        _errorTelemetryConfigured = true;

        if (!ReferenceEquals(previous, NoopAnalyticsSink.Instance))
        {
            previous.Dispose();
        }

        if (!allowErrorTelemetry || !next.IsEnabled)
        {
            ClearPendingFailures();
            return;
        }

        FlushPendingFailures(next);
    }

    public static void Track(string eventName, IDictionary<string, object> properties = null)
    {
        try
        {
            _sink.Track(eventName, properties ?? new Dictionary<string, object>());
        }
        catch
        {
            // Analytics must never affect gameplay.
        }
    }

    public static void TrackCommandUsed(string commandName, bool success, string result = null, IDictionary<string, object> properties = null)
    {
        var eventProperties = new Dictionary<string, object>
        {
            ["command_name"] = commandName,
            ["success"] = success,
            ["result"] = result ?? (success ? "success" : "failure")
        };
        AddProperties(eventProperties, properties);
        Track("command used", eventProperties);
    }

    public static void TrackFeatureUsed(string featureName, string action = null, bool success = true, string result = null, IDictionary<string, object> properties = null)
    {
        var eventProperties = new Dictionary<string, object>
        {
            ["feature_name"] = featureName,
            ["action"] = action ?? "used",
            ["success"] = success,
            ["result"] = result ?? (success ? "success" : "failure")
        };
        AddProperties(eventProperties, properties);
        Track("feature used", eventProperties);
    }

    public static void TrackFailure(string area, string operation, string severity, string result, Exception exception = null, bool recovered = true, IDictionary<string, object> properties = null)
    {
        var eventProperties = new Dictionary<string, object>
        {
            ["area"] = NormalizeLabel(area, "unknown"),
            ["operation"] = NormalizeLabel(operation, "unknown"),
            ["severity"] = NormalizeLabel(severity, "error"),
            ["result"] = NormalizeLabel(result, "failure"),
            ["success"] = false,
            ["recovered"] = recovered
        };

        if (exception != null)
        {
            eventProperties["exception_type"] = exception.GetType().Name;
        }

        AddProperties(eventProperties, properties);

        if (!_allowErrorTelemetry)
        {
            if (!_errorTelemetryConfigured)
            {
                QueuePendingFailure(eventProperties);
            }

            return;
        }

        if (!IsEnabled)
        {
            return;
        }

        Track("mod failure", eventProperties);
    }

    public static IDictionary<string, object> ChatProperties(string chatType)
    {
        return new Dictionary<string, object>
        {
            ["chat_type"] = chatType
        };
    }

    public static void TrackConfigSnapshot(ModConfig config)
    {
        if (config == null)
        {
            return;
        }

        config.Teleportation ??= new TeleportationConfig();
        config.Teleportation.InitializeDefaultsIfNeeded();

        Track("config snapshot", new Dictionary<string, object>
        {
            ["disable_nicknames"] = config.DisableNicknames,
            ["disable_rp_chat"] = config.DisableRPChat,
            ["allow_player_nicknames"] = config.ProximityChatAllowPlayersToChangeNicknames,
            ["allow_player_nickname_colors"] = config.ProximityChatAllowPlayersToChangeNicknameColors,
            ["enable_distance_obfuscation"] = config.EnableDistanceObfuscationSystem,
            ["enable_distance_font_size"] = config.EnableDistanceFontSizeSystem,
            ["use_general_channel_as_proximity_chat"] = config.UseGeneralChannelAsProximityChat,
            ["enable_global_ooc"] = config.EnableGlobalOOC,
            ["allow_ooc_toggle"] = config.AllowOOCToggle,
            ["proximity_chat_as_default"] = config.ProximityChatAsDefault,
            ["player_stat_system"] = config.PlayerStatSystem,
            ["allow_player_tpa"] = config.AllowPlayerTpa,
            ["tpa_require_temporal_gear"] = config.TpaRequireTemporalGear,
            ["tpa_use_cooldown"] = config.TpaUseCooldown,
            ["tpa_use_timeout"] = config.TpaUseTimeout,
            ["home_spawn_require_temporal_gear"] = config.HomeSpawnRequireTemporalGear,
            ["max_homes_bucket"] = AnalyticsBuckets.Count(config.Teleportation.MaxHomes),
            ["home_warmup_seconds_bucket"] = AnalyticsBuckets.Count(config.Teleportation.HomeWarmupSeconds),
            ["spawn_warmup_seconds_bucket"] = AnalyticsBuckets.Count(config.Teleportation.SpawnWarmupSeconds),
            ["tpa_warmup_seconds_bucket"] = AnalyticsBuckets.Count(config.Teleportation.TpaWarmupSeconds),
            ["stuck_warmup_seconds_bucket"] = AnalyticsBuckets.Count(config.Teleportation.StuckWarmupSeconds),
            ["enable_sleep_notifications"] = config.EnableSleepNotifications,
            ["enable_language_system"] = config.EnableLanguageSystem,
            ["prevent_proximity_channel_switching"] = config.PreventProximityChannelSwitching,
            ["show_nickname_in_nametag"] = config.ShowNicknameInNametag,
            ["hide_nametag_unless_targeting"] = config.HideNametagUnlessTargeting,
            ["show_player_name_in_nametag"] = config.ShowPlayerNameInNametag,
            ["enable_typing_indicator"] = config.EnableTypingIndicator,
            ["server_save_announcement_as_notification"] = config.ServerSaveAnnouncementAsNotification,
            ["typing_indicator_display_mode"] = config.TypingIndicatorDisplayMode.ToString(),
            ["enable_chatter"] = config.EnableChatter,
            ["require_line_of_sight_for_sign_language"] = config.RequireLineOfSightForSignLanguage,
            ["nametag_requires_line_of_sight"] = config.NametagRequiresLineOfSight,
            ["overhead_chat_bubble_mode"] = OverheadChatBubbleModes.Normalize(config.OverheadChatBubbleMode, config.DisableRpOverheadBubbles),
            ["proximity_chat_presentation_mode"] = ProximityChatPresentationModes.Normalize(config.ProximityChatPresentationMode),
            ["normalize_proximity_chat_text"] = config.NormalizeProximityChatText,
            ["attribute_freeform_messages_to_player_name"] = config.AttributeFreeformMessagesToPlayerName,
            ["enable_character_sheets"] = config.EnableCharacterSheets,
            ["enable_character_headshots"] = config.EnableCharacterHeadshots,
            ["show_headshot_in_nametag"] = config.ShowHeadshotInNametag,
            ["headshot_url_allowed"] = config.HeadshotUrlAllowed,
            ["use_custom_nametag_renderer"] = config.UseCustomNametagRenderer,
            ["enable_rp_character_slots"] = config.EnableRpCharacterSlots,
            ["max_rp_character_slots_bucket"] = AnalyticsBuckets.Count(config.MaxRpCharacterSlots),
            ["enable_admin_notes"] = config.EnableAdminNotes,
            ["enable_structured_admin_notes"] = config.EnableStructuredAdminNotes,
            ["enable_admin_note_ledger"] = config.EnableAdminNoteLedger,
            ["enable_player_notes"] = config.EnablePlayerNotes,
            ["character_sheet_field_count_bucket"] = AnalyticsBuckets.Count(config.CharacterSheetFields?.Count ?? 0),
            ["language_count_bucket"] = AnalyticsBuckets.Count(config.Languages?.Count ?? 0)
        });
    }

    public static Task FlushAsync()
    {
        return _sink.FlushAsync();
    }

    public static void Shutdown()
    {
        Configure(NoopAnalyticsSink.Instance);
        _errorTelemetryConfigured = false;
    }

    private static void AddProperties(Dictionary<string, object> eventProperties, IDictionary<string, object> properties)
    {
        if (properties == null)
        {
            return;
        }

        foreach (var property in properties)
        {
            eventProperties[property.Key] = property.Value;
        }
    }

    private static string NormalizeLabel(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static void QueuePendingFailure(Dictionary<string, object> properties)
    {
        lock (PendingFailureLock)
        {
            while (PendingFailureEvents.Count >= MaxPendingFailureEvents)
            {
                PendingFailureEvents.Dequeue();
            }

            PendingFailureEvents.Enqueue(new PendingAnalyticsEvent("mod failure", properties));
        }
    }

    private static void FlushPendingFailures(IAnalyticsSink sink)
    {
        List<PendingAnalyticsEvent> pending;
        lock (PendingFailureLock)
        {
            if (PendingFailureEvents.Count == 0)
            {
                return;
            }

            pending = PendingFailureEvents.ToList();
            PendingFailureEvents.Clear();
        }

        foreach (var analyticsEvent in pending)
        {
            try
            {
                sink.Track(analyticsEvent.Name, analyticsEvent.Properties);
            }
            catch
            {
                // Analytics must never affect gameplay.
            }
        }
    }

    private static void ClearPendingFailures()
    {
        lock (PendingFailureLock)
        {
            PendingFailureEvents.Clear();
        }
    }

    private sealed class PendingAnalyticsEvent
    {
        public PendingAnalyticsEvent(string name, IDictionary<string, object> properties)
        {
            Name = name;
            Properties = new Dictionary<string, object>(properties);
        }

        public string Name { get; }

        public IDictionary<string, object> Properties { get; }
    }

}
