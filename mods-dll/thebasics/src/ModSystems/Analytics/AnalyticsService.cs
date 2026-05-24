using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using thebasics.Configs;

namespace thebasics.ModSystems.Analytics;

public static class AnalyticsService
{
    private static IAnalyticsSink _sink = NoopAnalyticsSink.Instance;

    public static bool IsEnabled => _sink.IsEnabled;

    public static void Configure(IAnalyticsSink sink)
    {
        var previous = Interlocked.Exchange(ref _sink, sink ?? NoopAnalyticsSink.Instance);
        if (!ReferenceEquals(previous, NoopAnalyticsSink.Instance))
        {
            previous.Dispose();
        }
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

    public static void TrackCommandUsed(string commandName, bool success, string result = null)
    {
        Track("command used", new Dictionary<string, object>
        {
            ["command_name"] = commandName,
            ["success"] = success,
            ["result"] = result ?? (success ? "success" : "failure")
        });
    }

    public static void TrackFeatureUsed(string featureName, string action = null, bool success = true, string result = null)
    {
        Track("feature used", new Dictionary<string, object>
        {
            ["feature_name"] = featureName,
            ["action"] = action ?? "used",
            ["success"] = success,
            ["result"] = result ?? (success ? "success" : "failure")
        });
    }

    public static void TrackConfigSnapshot(ModConfig config)
    {
        if (config == null)
        {
            return;
        }

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
            ["attribute_freeform_messages_to_player_name"] = config.AttributeFreeformMessagesToPlayerName
        });
    }

    public static Task FlushAsync()
    {
        return _sink.FlushAsync();
    }

    public static void Shutdown()
    {
        Configure(NoopAnalyticsSink.Instance);
    }
}
