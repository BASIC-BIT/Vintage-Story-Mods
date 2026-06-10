using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.AdminConfig;

public static class ConfigAdminSettingRegistry
{
    private readonly record struct SettingMeta(string Key, string Group, string Label, string Description, ConfigAdminReloadBehavior ReloadBehavior);

    private static readonly IReadOnlyList<ConfigAdminSettingDefinition> _settings = BuildSettings();
    private static readonly IDictionary<string, ConfigAdminSettingDefinition> _settingsByKey =
        _settings.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ConfigAdminSettingDefinition> Settings => _settings;

    public static bool TryGet(string key, out ConfigAdminSettingDefinition setting)
    {
        return _settingsByKey.TryGetValue(key, out setting);
    }

    public static IReadOnlyList<string> ValidateConfig(ModConfig config)
    {
        var errors = new List<string>();

        foreach (var mode in EnumValues<ProximityChatMode>())
        {
            var range = GetModeValue(config.ProximityChatModeDistances, mode, DefaultDistance(mode));
            var obfuscationStart = GetModeValue(config.ProximityChatModeObfuscationRanges, mode, DefaultObfuscationStart(mode));
            if (range <= obfuscationStart)
            {
                errors.Add($"{mode} range must be greater than its obfuscation start.");
            }
        }

        return errors;
    }

    private static IReadOnlyList<ConfigAdminSettingDefinition> BuildSettings()
    {
        var settings = new List<ConfigAdminSettingDefinition>();
        AddScalarSettings(settings);
        AddProximityModeSettings(settings);
        AddAudioModeSettings(settings);
        AddDelimiterSettings(settings);
        AddPlayerStatToggleSettings(settings);
        return settings;
    }

    private static void AddScalarSettings(List<ConfigAdminSettingDefinition> settings)
    {
        AddCoreScalarSettings(settings);
        AddServerNotificationScalarSettings(settings);
        AddRemainingScalarSettings(settings);
    }

    private static void AddCoreScalarSettings(List<ConfigAdminSettingDefinition> settings)
    {
        settings.AddRange(new[]
        {
            Bool("EnableChatter", "Chat/RP", "Enable chatter sounds", "Play seraph voice chatter for speech messages.", ConfigAdminReloadBehavior.Live, c => c.EnableChatter, (c, v) => c.EnableChatter = v),
            Select("ProximityChatPresentationMode", "Chat/RP", "Chat presentation mode", "Controls how proximity speech appears in chat.", ConfigAdminReloadBehavior.Live, (c => ProximityChatPresentationModes.Normalize(c.ProximityChatPresentationMode), (c, v) => c.ProximityChatPresentationMode = v), new[] { "StandardRoleplay", "SimpleSpeech", "PlainProximity", "Prose" }),
            Bool("NormalizeProximityChatText", "Chat/RP", "Normalize proximity text", "Automatically capitalize and punctuate RP speech, emotes, and environmental messages.", ConfigAdminReloadBehavior.Live, c => c.NormalizeProximityChatText, (c, v) => c.NormalizeProximityChatText = v),
            Text("ProseNicknameToken", "Chat/RP", "Prose nickname token", "Standalone token replaced with the sender's RP nickname in Prose mode.", ConfigAdminReloadBehavior.Live, c => c.ProseNicknameToken, (c, v) => c.ProseNicknameToken = v),
            Bool("AttributeFreeformMessagesToPlayerName", "Chat/RP", "Attribute freeform messages", "Prefix Prose and environmental messages with account names for auditability.", ConfigAdminReloadBehavior.Live, c => c.AttributeFreeformMessagesToPlayerName, (c, v) => c.AttributeFreeformMessagesToPlayerName = v),
            Text("ProximityChatModeBabbleVerb", "Chat/RP", "Babble verb", "Verb used for unintelligible/babble speech.", ConfigAdminReloadBehavior.Live, c => c.ProximityChatModeBabbleVerb, (c, v) => c.ProximityChatModeBabbleVerb = v),
            Text("EmoteColor", "Chat/RP", "Emote color", "VTML color used for emote text.", ConfigAdminReloadBehavior.Live, c => c.EmoteColor, (c, v) => c.EmoteColor = v),
            Text("OOCColor", "Chat/RP", "OOC color", "VTML color used for local OOC text.", ConfigAdminReloadBehavior.Live, c => c.OOCColor, (c, v) => c.OOCColor = v),
            Text("GlobalOOCColor", "Chat/RP", "Global OOC color", "VTML color used for global OOC text.", ConfigAdminReloadBehavior.Live, c => c.GlobalOOCColor, (c, v) => c.GlobalOOCColor = v),
            Bool("UseNicknameInOOC", "Chat/RP", "Use nickname in OOC", "Use RP nicknames in local OOC messages.", ConfigAdminReloadBehavior.Live, c => c.UseNicknameInOOC, (c, v) => c.UseNicknameInOOC = v),
            Bool("UseNicknameInGlobalOOC", "Chat/RP", "Use nickname in global OOC", "Use RP nicknames in global OOC messages.", ConfigAdminReloadBehavior.Live, c => c.UseNicknameInGlobalOOC, (c, v) => c.UseNicknameInGlobalOOC = v),
            Bool("AllowOOCToggle", "Chat/RP", "Allow OOC toggle", "Allow players to toggle local OOC mode.", ConfigAdminReloadBehavior.Live, c => c.AllowOOCToggle, (c, v) => c.AllowOOCToggle = v),
            Bool("EnableDistanceObfuscationSystem", "Chat/RP", "Enable distance obfuscation", "Obfuscate speech by distance when configured.", ConfigAdminReloadBehavior.Live, c => c.EnableDistanceObfuscationSystem, (c, v) => c.EnableDistanceObfuscationSystem = v),
            Bool("EnableDistanceFontSizeSystem", "Chat/RP", "Enable distance font size", "Change chat font size by speech distance when configured.", ConfigAdminReloadBehavior.Live, c => c.EnableDistanceFontSizeSystem, (c, v) => c.EnableDistanceFontSizeSystem = v),
            Decimal("MaxEnvironmentPlacementDistance", "Chat/RP", "Max placed env distance", "Maximum raycast distance for placed environmental messages.", ConfigAdminReloadBehavior.Live, (c => c.MaxEnvironmentPlacementDistance, (c, v) => c.MaxEnvironmentPlacementDistance = v), (0, 512)),
            Bool("EnableChatHistory", "Chat History", "Enable chat history", "Store structured chat history for admin search.", ConfigAdminReloadBehavior.Live, c => c.EnableChatHistory, (c, v) => c.EnableChatHistory = v),
            Bool("ChatHistoryCaptureNonBasicChat", "Chat History", "Capture other chat", "Capture non-BASICs player chat observed through the server chat event.", ConfigAdminReloadBehavior.Live, c => c.ChatHistoryCaptureNonBasicChat, (c, v) => c.ChatHistoryCaptureNonBasicChat = v),
            Int("ChatHistoryRetentionDays", "Chat History", "Retention days", "Delete chat history older than this many days. 0 keeps history forever by age.", ConfigAdminReloadBehavior.Live, (c => c.ChatHistoryRetentionDays, (c, v) => c.ChatHistoryRetentionDays = v), (0, 36500)),
            Int("ChatHistoryMaxEntries", "Chat History", "Max retained entries", "Keep only the newest N chat entries. 0 keeps unlimited entries by count.", ConfigAdminReloadBehavior.Live, (c => c.ChatHistoryMaxEntries, (c, v) => c.ChatHistoryMaxEntries = v), (0, 10000000)),
            Int("ChatHistorySearchMaxResults", "Chat History", "Max search results", "Maximum chat history rows returned to an admin search request.", ConfigAdminReloadBehavior.Live, (c => c.ChatHistorySearchMaxResults, (c, v) => c.ChatHistorySearchMaxResults = v), (1, 1000)),
            Int("ChatHistoryFlushIntervalMilliseconds", "Chat History", "Flush interval ms", "How often queued chat history entries are flushed to disk.", ConfigAdminReloadBehavior.RestartRequired, (c => c.ChatHistoryFlushIntervalMilliseconds, (c, v) => c.ChatHistoryFlushIntervalMilliseconds = v), (100, 60000)),
            Bool("EnableTypingIndicator", "Client UX", "Enable typing indicator", "Show typing state above players.", ConfigAdminReloadBehavior.Live, c => c.EnableTypingIndicator, (c, v) => c.EnableTypingIndicator = v),
            Select("TypingIndicatorDisplayMode", "Client UX", "Typing indicator display", "Controls icon/text rendering for typing indicators.", ConfigAdminReloadBehavior.Live, (c => c.TypingIndicatorDisplayMode.ToString(), (c, v) => c.TypingIndicatorDisplayMode = Enum.Parse<TypingIndicatorDisplayMode>(v, true)), EnumNames<TypingIndicatorDisplayMode>()),
            Int("TypingIndicatorMaxRange", "Client UX", "Typing indicator range", "Maximum range in blocks for typing indicators.", ConfigAdminReloadBehavior.Live, (c => c.TypingIndicatorMaxRange, (c, v) => c.TypingIndicatorMaxRange = v), (0, 512)),
            Decimal("TypingIndicatorTimeoutSeconds", "Client UX", "Typing indicator timeout", "Seconds before typing state expires.", ConfigAdminReloadBehavior.Live, (c => c.TypingIndicatorTimeoutSeconds, (c, v) => c.TypingIndicatorTimeoutSeconds = (float)v), (0, 60)),
            Text("TypingIndicatorTextOverride", "Client UX", "Typing indicator text override", "Optional custom typing indicator text. Empty uses lang default.", ConfigAdminReloadBehavior.Live, c => c.TypingIndicatorTextOverride, (c, v) => c.TypingIndicatorTextOverride = v),
            Select("OverheadChatBubbleMode", "Client UX", "Overhead chat bubbles", "Controls RP text, vanilla, or disabled overhead bubbles.", ConfigAdminReloadBehavior.Live, (c => c.OverheadChatBubbleMode, (c, v) => c.OverheadChatBubbleMode = v), new[] { "RpText", "Vanilla", "Off" }),
            Int("SpeechBubbleMinimumDisplayMilliseconds", "Client UX", "Minimum bubble time", "Minimum speech bubble lifetime in milliseconds.", ConfigAdminReloadBehavior.Live, (c => c.SpeechBubbleMinimumDisplayMilliseconds, (c, v) => c.SpeechBubbleMinimumDisplayMilliseconds = v), (0, 60000)),
            Bool("ShowNicknameInNametag", "Client UX", "Show nickname in nametag", "Include RP nickname in rendered nametags.", ConfigAdminReloadBehavior.Live, c => c.ShowNicknameInNametag, (c, v) => c.ShowNicknameInNametag = v),
            Bool("ShowPlayerNameInNametag", "Client UX", "Show account name in nametag", "Include account name in rendered nametags.", ConfigAdminReloadBehavior.Live, c => c.ShowPlayerNameInNametag, (c, v) => c.ShowPlayerNameInNametag = v),
            Bool("HideNametagUnlessTargeting", "Client UX", "Hide nametag unless targeted", "Only show nametags when targeted.", ConfigAdminReloadBehavior.Live, c => c.HideNametagUnlessTargeting, (c, v) => c.HideNametagUnlessTargeting = v),
            Int("NametagRenderRange", "Client UX", "Nametag render range", "Maximum nametag render range in blocks.", ConfigAdminReloadBehavior.Live, (c => c.NametagRenderRange, (c, v) => c.NametagRenderRange = v), (0, 512)),
            Bool("NametagRequiresLineOfSight", "Client UX", "Nametag requires line of sight", "Require client line-of-sight for nametag rendering.", ConfigAdminReloadBehavior.Live, c => c.NametagRequiresLineOfSight, (c, v) => c.NametagRequiresLineOfSight = v),
            Bool("BoldNicknames", "Client UX", "Bold nicknames", "Render RP nicknames in bold where supported.", ConfigAdminReloadBehavior.Live, c => c.BoldNicknames, (c, v) => c.BoldNicknames = v),
            Bool("ApplyColorsToNicknames", "Client UX", "Color RP nicknames", "Apply nickname colors to IC nicknames.", ConfigAdminReloadBehavior.Live, c => c.ApplyColorsToNicknames, (c, v) => c.ApplyColorsToNicknames = v),
            Bool("ApplyColorsToPlayerNames", "Client UX", "Color account names", "Apply nickname colors to OOC account names.", ConfigAdminReloadBehavior.Live, c => c.ApplyColorsToPlayerNames, (c, v) => c.ApplyColorsToPlayerNames = v),
            Bool("EnableGlobalOOC", "Chat/RP", "Enable global OOC", "Allow global OOC formatting and command support.", ConfigAdminReloadBehavior.RestartRequired, c => c.EnableGlobalOOC, (c, v) => c.EnableGlobalOOC = v),
            Bool("TpaRequireTemporalGear", "TPA", "Require temporal gear", "Require temporal gear for /tpa requests.", ConfigAdminReloadBehavior.Live, c => c.TpaRequireTemporalGear, (c, v) => c.TpaRequireTemporalGear = v),
            Bool("TpaUseCooldown", "TPA", "Use TPA cooldown", "Apply cooldown between outgoing TPA requests.", ConfigAdminReloadBehavior.Live, c => c.TpaUseCooldown, (c, v) => c.TpaUseCooldown = v),
            Decimal("TpaCooldownInGameHours", "TPA", "TPA cooldown hours", "Cooldown length in in-game hours.", ConfigAdminReloadBehavior.Live, (c => c.TpaCooldownInGameHours, (c, v) => c.TpaCooldownInGameHours = v), (0, 720)),
            Bool("TpaUseTimeout", "TPA", "Use TPA timeout", "Expire pending TPA requests after a timeout.", ConfigAdminReloadBehavior.Live, c => c.TpaUseTimeout, (c, v) => c.TpaUseTimeout = v),
            Decimal("TpaTimeoutMinutes", "TPA", "TPA timeout minutes", "Real minutes before pending TPA requests expire.", ConfigAdminReloadBehavior.Live, (c => c.TpaTimeoutMinutes, (c, v) => c.TpaTimeoutMinutes = v), (0.1, 1440)),
        });
    }

    private static void AddServerNotificationScalarSettings(List<ConfigAdminSettingDefinition> settings)
    {
        settings.AddRange(new[]
        {
            Bool("SendServerSaveAnnouncement", "Server Notifications", "Enable save-start message", "Actually send a message when a server save starts. Turn this off to disable the start announcement.", ConfigAdminReloadBehavior.Live, c => c.SendServerSaveAnnouncement, (c, v) => c.SendServerSaveAnnouncement = v),
            Bool("SendServerSaveFinishedAnnouncement", "Server Notifications", "Enable save-finish message", "Actually send a message when a server save finishes. Turn this off to disable the finish announcement.", ConfigAdminReloadBehavior.Live, c => c.SendServerSaveFinishedAnnouncement, (c, v) => c.SendServerSaveFinishedAnnouncement = v),
            Bool("ServerSaveAnnouncementAsNotification", "Server Notifications", "Save-start uses popup", "Only changes the start announcement from chat text to popup style; it does not disable the announcement.", ConfigAdminReloadBehavior.Live, c => c.ServerSaveAnnouncementAsNotification, (c, v) => c.ServerSaveAnnouncementAsNotification = v),
            Bool("ServerSaveFinishedAsNotification", "Server Notifications", "Save-finish uses popup", "Only changes the finish announcement from chat text to popup style; it does not disable the announcement.", ConfigAdminReloadBehavior.Live, c => c.ServerSaveFinishedAsNotification, (c, v) => c.ServerSaveFinishedAsNotification = v),
            Text("TEXT_ServerSaveAnnouncement", "Server Notifications", "Save start text", "Text sent when a server save starts.", ConfigAdminReloadBehavior.Live, c => c.TEXT_ServerSaveAnnouncement, (c, v) => c.TEXT_ServerSaveAnnouncement = v),
            Text("TEXT_ServerSaveFinished", "Server Notifications", "Save finish text", "Text sent when a server save finishes.", ConfigAdminReloadBehavior.Live, c => c.TEXT_ServerSaveFinished, (c, v) => c.TEXT_ServerSaveFinished = v),
            Bool("EnableNearbyDeathMessagesInProximityChat", "Server Notifications", "Nearby death messages in proximity", "Suppress vanilla join/leave/death lifecycle spam from proximity, then re-send death messages only to nearby players.", ConfigAdminReloadBehavior.Live, c => c.EnableNearbyDeathMessagesInProximityChat, (c, v) => c.EnableNearbyDeathMessagesInProximityChat = v),
            Bool("EnableSleepNotifications", "Server Notifications", "Enable sleep notifications", "Notify players when enough players are sleeping.", ConfigAdminReloadBehavior.Live, c => c.EnableSleepNotifications, (c, v) => c.EnableSleepNotifications = v),
            Decimal("SleepNotificationThreshold", "Server Notifications", "Sleep notification threshold", "Fraction of online players sleeping before notifying.", ConfigAdminReloadBehavior.Live, (c => c.SleepNotificationThreshold, (c, v) => c.SleepNotificationThreshold = v), (0, 1)),
            Text("TEXT_SleepNotification", "Server Notifications", "Sleep notification text", "Text sent when enough players are sleeping.", ConfigAdminReloadBehavior.Live, c => c.TEXT_SleepNotification, (c, v) => c.TEXT_SleepNotification = v),
        });
    }

    private static void AddRemainingScalarSettings(List<ConfigAdminSettingDefinition> settings)
    {
        settings.AddRange(new[]
        {
            Bool("DebugMode", "Diagnostics", "Debug mode", "Enable The BASICs diagnostic logging.", ConfigAdminReloadBehavior.Live, c => c.DebugMode, (c, v) => c.DebugMode = v),
            Bool("EnableRpCharacterSlots", "Characters", "Enable RP character slots", "Enable full RP character slots with per-character identity, appearance, inventory, body state, and position. Requires character sheets to be enabled.", ConfigAdminReloadBehavior.RestartRequired, c => c.EnableRpCharacterSlots, (c, v) => c.EnableRpCharacterSlots = v),
            Int("MaxRpCharacterSlots", "Characters", "Max RP character slots", "Maximum active RP character slots per account.", ConfigAdminReloadBehavior.Live, (c => c.MaxRpCharacterSlots, (c, v) => c.MaxRpCharacterSlots = v), (1, 20)),
            Bool("EnableAdminNotes", "Notes", "Enable admin notes", "Enable staff-only notes about player accounts.", ConfigAdminReloadBehavior.Live, c => c.EnableAdminNotes, (c, v) => c.EnableAdminNotes = v),
            Bool("EnableStructuredAdminNotes", "Notes", "Enable structured admin notes", "Enable timestamped staff notes with separate entries.", ConfigAdminReloadBehavior.Live, c => c.EnableStructuredAdminNotes, (c, v) => c.EnableStructuredAdminNotes = v),
            Bool("EnableAdminNoteLedger", "Notes", "Enable admin ledger", "Enable one freeform staff ledger per player account.", ConfigAdminReloadBehavior.Live, c => c.EnableAdminNoteLedger, (c, v) => c.EnableAdminNoteLedger = v),
            Bool("EnablePlayerNotes", "Notes", "Enable personal notes", "Enable private player-authored notes.", ConfigAdminReloadBehavior.Live, c => c.EnablePlayerNotes, (c, v) => c.EnablePlayerNotes = v),
            Int("MaxNoteLength", "Notes", "Max note length", "Maximum characters in a structured note.", ConfigAdminReloadBehavior.Live, (c => c.MaxNoteLength, (c, v) => c.MaxNoteLength = v), (1, 20000)),
            Int("MaxFreeformNoteLength", "Notes", "Max freeform notes length", "Maximum characters in a freeform notes field.", ConfigAdminReloadBehavior.Live, (c => c.MaxFreeformNoteLength, (c, v) => c.MaxFreeformNoteLength = v), (1, 200000)),
            Int("MaxAdminNotesPerTarget", "Notes", "Max admin notes per player", "Maximum structured admin notes per target account.", ConfigAdminReloadBehavior.Live, (c => c.MaxAdminNotesPerTarget, (c, v) => c.MaxAdminNotesPerTarget = v), (1, 1000)),
            Int("MaxPlayerNotesPerAuthor", "Notes", "Max personal notes per player", "Maximum personal notes one player can store.", ConfigAdminReloadBehavior.Live, (c => c.MaxPlayerNotesPerAuthor, (c, v) => c.MaxPlayerNotesPerAuthor = v), (1, 5000)),
            Bool("EnableTh3EssentialsDiscordRelay", "Integrations", "Relay proximity to Discord", "Opt-in bridge to Th3Essentials Discord chat relay. Makes local proximity RP chat visible in Discord.", ConfigAdminReloadBehavior.Live, c => c.EnableTh3EssentialsDiscordRelay, (c, v) => c.EnableTh3EssentialsDiscordRelay = v),
            Decimal("ChatterSelfVolumeMultiplier", "Chat/RP", "Chatter self volume multiplier", "Volume multiplier when chatter is sent back to the speaker.", ConfigAdminReloadBehavior.Live, (c => c.ChatterSelfVolumeMultiplier, (c, v) => c.ChatterSelfVolumeMultiplier = (float)v), (0, 4)),
            Bool("ProximityChatAllowPlayersToChangeNicknames", "Restart Required", "Players can change nicknames", "Requires command gating work before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatAllowPlayersToChangeNicknames, (c, v) => c.ProximityChatAllowPlayersToChangeNicknames = v),
            Bool("ProximityChatAllowPlayersToChangeNicknameColors", "Restart Required", "Players can change nickname colors", "Requires command gating work before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatAllowPlayersToChangeNicknameColors, (c, v) => c.ProximityChatAllowPlayersToChangeNicknameColors = v),
            Text("ChangeNicknameColorPermission", "Permissions", "Nickname color privilege", "Privilege required to change nickname colors.", ConfigAdminReloadBehavior.Live, c => c.ChangeNicknameColorPermission, (c, v) => c.ChangeNicknameColorPermission = v),
            Int("MinNicknameLength", "Restart Required", "Minimum nickname length", "Minimum player nickname length.", ConfigAdminReloadBehavior.RestartRequired, (c => c.MinNicknameLength, (c, v) => c.MinNicknameLength = v), (1, 256)),
            Int("MaxNicknameLength", "Restart Required", "Maximum nickname length", "Maximum player nickname length.", ConfigAdminReloadBehavior.RestartRequired, (c => c.MaxNicknameLength, (c, v) => c.MaxNicknameLength = v), (1, 512)),
            Bool("DisableRPChat", "Restart Required", "Disable RP chat", "Requires restart because commands and transformers are startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.DisableRPChat, (c, v) => c.DisableRPChat = v),
            Bool("DisableNicknames", "Restart Required", "Disable nicknames", "Requires restart because nickname commands are startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.DisableNicknames, (c, v) => c.DisableNicknames = v),
            Bool("AllowPlayerTpa", "Restart Required", "Allow player TPA", "Requires restart because TPA commands are startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.AllowPlayerTpa, (c, v) => c.AllowPlayerTpa = v),
            Bool("EnableLanguageSystem", "Restart Required", "Enable language system", "Requires restart because language commands and joins are startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.EnableLanguageSystem, (c, v) => c.EnableLanguageSystem = v),
            Int("MaxLanguagesPerPlayer", "Restart Required", "Max languages per player", "Requires language-system reconciliation before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, (c => c.MaxLanguagesPerPlayer, (c, v) => c.MaxLanguagesPerPlayer = v), (1, 100)),
            Int("SignLanguageRange", "Restart Required", "Sign language range", "Requires language-system reconciliation before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, (c => c.SignLanguageRange, (c, v) => c.SignLanguageRange = v), (0, 512)),
            Bool("RemoveGrantedLanguagesOnChange", "Restart Required", "Remove granted languages on change", "Affects future class/model language reconciliation.", ConfigAdminReloadBehavior.RestartRequired, c => c.RemoveGrantedLanguagesOnChange, (c, v) => c.RemoveGrantedLanguagesOnChange = v),
            Bool("RequireLineOfSightForSignLanguage", "Restart Required", "Sign language requires LOS", "Requires language-system reconciliation before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, c => c.RequireLineOfSightForSignLanguage, (c, v) => c.RequireLineOfSightForSignLanguage = v),
            Bool("PlayerStatSystem", "Restart Required", "Enable player stats", "Requires restart until stat event subscriptions are refactored.", ConfigAdminReloadBehavior.RestartRequired, c => c.PlayerStatSystem, (c, v) => c.PlayerStatSystem = v),
            Int("PlayerStatDistanceTravelledTimer", "Restart Required", "Player stat movement interval", "Requires stat tick listener refresh before it can be fully live.", ConfigAdminReloadBehavior.RestartRequired, (c => c.PlayerStatDistanceTravelledTimer, (c, v) => c.PlayerStatDistanceTravelledTimer = v), (100, 600000)),
            Text("TpaRequestPrivilege", "Permissions", "TPA request privilege", "Privilege required to initiate /tpa and /tpahere.", ConfigAdminReloadBehavior.Live, c => c.TpaRequestPrivilege, (c, v) => c.TpaRequestPrivilege = v),
            Text("RPTextTogglePermission", "Permissions", "RP text toggle privilege", "Privilege required to toggle RP text bypass.", ConfigAdminReloadBehavior.Live, c => c.RPTextTogglePermission, (c, v) => c.RPTextTogglePermission = v),
            Text("OOCTogglePermission", "Permissions", "OOC toggle privilege", "Privilege required to toggle OOC mode.", ConfigAdminReloadBehavior.Live, c => c.OOCTogglePermission, (c, v) => c.OOCTogglePermission = v),
            Text("ChangeOwnLanguagePermission", "Permissions", "Own language privilege", "Privilege required for player language add/remove commands.", ConfigAdminReloadBehavior.Live, c => c.ChangeOwnLanguagePermission, (c, v) => c.ChangeOwnLanguagePermission = v),
            Text("ChangeOtherLanguagePermission", "Permissions", "Other language privilege", "Privilege required for admin language commands.", ConfigAdminReloadBehavior.Live, c => c.ChangeOtherLanguagePermission, (c, v) => c.ChangeOtherLanguagePermission = v),
            Text("PlayerStatClearPermission", "Permissions", "Clear stats privilege", "Privilege required to clear player stats.", ConfigAdminReloadBehavior.Live, c => c.PlayerStatClearPermission, (c, v) => c.PlayerStatClearPermission = v),
            Text("AdminNotesPermission", "Permissions", "Admin notes privilege", "Privilege required to view and edit admin notes.", ConfigAdminReloadBehavior.Live, c => c.AdminNotesPermission, (c, v) => c.AdminNotesPermission = v),
            Text("PlayerNotesPermission", "Permissions", "Personal notes privilege", "Privilege required to use personal notes.", ConfigAdminReloadBehavior.Live, c => c.PlayerNotesPermission, (c, v) => c.PlayerNotesPermission = v),
            Text("ChatHistoryPermission", "Permissions", "Chat history privilege", "Privilege required to search chat history.", ConfigAdminReloadBehavior.Live, c => c.ChatHistoryPermission, (c, v) => c.ChatHistoryPermission = v),
            Text("ChatHistoryManagePermission", "Permissions", "Chat history manage privilege", "Privilege required to export or purge chat history.", ConfigAdminReloadBehavior.Live, c => c.ChatHistoryManagePermission, (c, v) => c.ChatHistoryManagePermission = v),
            Bool("UseGeneralChannelAsProximityChat", "Restart Required", "Use General as proximity chat", "Requires restart because chat group migration is startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.UseGeneralChannelAsProximityChat, (c, v) => c.UseGeneralChannelAsProximityChat = v),
            Text("ProximityChatName", "Restart Required", "Proximity chat name", "Requires restart because chat group setup is startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatName, (c, v) => c.ProximityChatName = v),
            Bool("ProximityChatAsDefault", "Restart Required", "Proximity chat as default", "Requires restart/rejoin for chat tab default behavior.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatAsDefault, (c, v) => c.ProximityChatAsDefault = v),
            Bool("PreserveDefaultChatChoice", "Restart Required", "Preserve default chat choice", "Requires restart/rejoin for chat tab default behavior.", ConfigAdminReloadBehavior.RestartRequired, c => c.PreserveDefaultChatChoice, (c, v) => c.PreserveDefaultChatChoice = v),
            Bool("PreventProximityChannelSwitching", "Restart Required", "Prevent proximity tab switching", "Requires reconnect for existing clients.", ConfigAdminReloadBehavior.RestartRequired, c => c.PreventProximityChannelSwitching, (c, v) => c.PreventProximityChannelSwitching = v)
        });
    }

    private static void AddProximityModeSettings(List<ConfigAdminSettingDefinition> settings)
    {
        foreach (var mode in EnumValues<ProximityChatMode>())
        {
            settings.Add(ModeInt(ModeMeta("ProximityChatModeDistances", "Chat/Ranges", mode, "range", "Maximum delivery range in blocks."), mode, c => c.ProximityChatModeDistances, DefaultDistance(mode), (1, 512)));
            settings.Add(ModeInt(ModeMeta("ProximityChatModeObfuscationRanges", "Chat/Ranges", mode, "obfuscation start", "Distance where speech starts obfuscating."), mode, c => c.ProximityChatModeObfuscationRanges, DefaultObfuscationStart(mode), (0, 512)));
            settings.Add(ModeInt(ModeMeta("ProximityChatDefaultFontSize", "Chat/Font Sizes", mode, "default font size", "Font size used near the speaker."), mode, c => c.ProximityChatDefaultFontSize, DefaultFontSize(mode), (1, 128)));
            settings.Add(ModeText(ModeMeta("ProximityChatModePunctuation", "Chat/RP Text", mode, "punctuation", "Punctuation appended by auto-punctuation."), mode, c => c.ProximityChatModePunctuation, DefaultPunctuation(mode), maxLength: 8));
            settings.Add(ModeTextArray(ModeMeta("ProximityChatModeVerbs", "Chat/RP Text", mode, "verbs", "Comma-separated speech verbs for this mode."), mode, c => c.ProximityChatModeVerbs, DefaultVerbs(mode)));
        }

        settings.Add(IntArray(new SettingMeta("ProximityChatClampFontSizes", "Chat/Font Sizes", "Clamp font sizes", "Comma-separated allowed distance font sizes.", ConfigAdminReloadBehavior.Live), c => c.ProximityChatClampFontSizes, (c, v) => c.ProximityChatClampFontSizes = v, (1, 128)));
    }

    private static void AddAudioModeSettings(List<ConfigAdminSettingDefinition> settings)
    {
        foreach (var mode in EnumValues<ProximityChatMode>())
        {
            settings.Add(ModeFloat(ModeMeta("RPTTS_ModeGain", "Chat/RPTTS Audio", mode, "RPTTS gain", "Speech audio gain for this mode."), mode, c => c.RPTTS_ModeGain, DefaultRpttsGain(mode), (0, 4)));
            settings.Add(ModeFloat(ModeMeta("RPTTS_ModeFalloff", "Chat/RPTTS Audio", mode, "RPTTS falloff", "Speech audio falloff for this mode."), mode, c => c.RPTTS_ModeFalloff, DefaultRpttsFalloff(mode), (0.1, 10)));
            settings.Add(ModeFloat(ModeMeta("ChatterModeVolume", "Chat/Chatter Audio", mode, "chatter volume", "Seraph chatter volume for this mode."), mode, c => c.ChatterModeVolume, DefaultChatterVolume(mode), (0, 4)));
            settings.Add(ModeFloat(ModeMeta("ChatterModePitch", "Chat/Chatter Audio", mode, "chatter pitch", "Seraph chatter pitch for this mode."), mode, c => c.ChatterModePitch, DefaultChatterPitch(mode), (0.1, 4)));
        }
    }

    private static void AddDelimiterSettings(List<ConfigAdminSettingDefinition> settings)
    {
        foreach (var delimiter in GetDelimiterDefinitions())
        {
            settings.Add(DelimiterText(delimiter.Key + ".Start", delimiter.Label + " start", "Opening delimiter text.", config => delimiter.Get(config.ChatDelimiters).Start, (config, value) => delimiter.Get(config.ChatDelimiters).Start = value, required: true));
            settings.Add(DelimiterText(delimiter.Key + ".End", delimiter.Label + " end", "Closing delimiter text. May be empty for prefix-style delimiters.", config => delimiter.Get(config.ChatDelimiters).End, (config, value) => delimiter.Get(config.ChatDelimiters).End = value, required: false));
        }
    }

    private static void AddPlayerStatToggleSettings(List<ConfigAdminSettingDefinition> settings)
    {
        foreach (var stat in EnumValues<PlayerStatType>())
        {
            var title = StatTypes.Types.TryGetValue(stat, out var definition) ? definition.Title : stat.ToString();
            settings.Add(Bool($"PlayerStatToggles.{stat}", "Player Stats/Toggles", title, "Enable or disable tracking for this stat.", ConfigAdminReloadBehavior.Live,
                c => GetModeValue(c.PlayerStatToggles, stat, true),
                (c, v) => c.PlayerStatToggles[stat] = v));
        }
    }

    private static SettingMeta ModeMeta(string keyPrefix, string group, ProximityChatMode mode, string labelSuffix, string description)
    {
        return new SettingMeta($"{keyPrefix}.{mode}", group, $"{mode} {labelSuffix}", description, ConfigAdminReloadBehavior.Live);
    }

    private static ConfigAdminSettingDefinition ModeInt(SettingMeta meta, ProximityChatMode mode, Func<ModConfig, IDictionary<ProximityChatMode, int>> get, int fallback, (int Min, int Max) range)
    {
        return Int(meta.Key, meta.Group, meta.Label, meta.Description, meta.ReloadBehavior,
            (config => GetModeValue(get(config), mode, fallback), (config, value) => get(config)[mode] = value), range);
    }

    private static ConfigAdminSettingDefinition ModeFloat(SettingMeta meta, ProximityChatMode mode, Func<ModConfig, IDictionary<ProximityChatMode, float>> get, float fallback, (double Min, double Max) range)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = meta.Key,
            Group = meta.Group,
            Label = meta.Label,
            Description = meta.Description,
            Kind = ConfigAdminSettingKind.Decimal,
            ReloadBehavior = meta.ReloadBehavior,
            GetValue = config => GetModeValue(get(config), mode, fallback).ToString("0.########", CultureInfo.InvariantCulture),
            SetValue = (config, value) =>
            {
                if (!ConfigAdminSettingDefinition.TryParseDecimal(value, out var parsed) || parsed < range.Min || parsed > range.Max)
                {
                    return $"{meta.Key} must be a number from {range.Min.ToString(CultureInfo.InvariantCulture)} to {range.Max.ToString(CultureInfo.InvariantCulture)}.";
                }

                get(config)[mode] = (float)parsed;
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition ModeText(SettingMeta meta, ProximityChatMode mode, Func<ModConfig, IDictionary<ProximityChatMode, string>> get, string fallback, int maxLength)
    {
        return ValidatedText(meta,
            config => GetModeValue(get(config), mode, fallback),
            (config, value) => get(config)[mode] = value,
            value => value.Length > maxLength ? $"{meta.Key} must be {maxLength} characters or fewer." : null);
    }

    private static ConfigAdminSettingDefinition ModeTextArray(SettingMeta meta, ProximityChatMode mode, Func<ModConfig, IDictionary<ProximityChatMode, string[]>> get, string[] fallback)
    {
        return ValidatedText(meta,
            config => FormatStringArray(GetModeValue(get(config), mode, fallback)),
            (config, value) => get(config)[mode] = ParseStringArray(value),
            value => ParseStringArray(value).Length == 0 ? $"{meta.Key} must contain at least one value." : null);
    }

    private static ConfigAdminSettingDefinition IntArray(SettingMeta meta, Func<ModConfig, int[]> get, Action<ModConfig, int[]> set, (int Min, int Max) range)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = meta.Key,
            Group = meta.Group,
            Label = meta.Label,
            Description = meta.Description,
            Kind = ConfigAdminSettingKind.Text,
            ReloadBehavior = meta.ReloadBehavior,
            GetValue = config => string.Join(", ", get(config) ?? Array.Empty<int>()),
            SetValue = (config, value) =>
            {
                var error = ValidateIntArray(meta.Key, value, range, out var parsed);
                if (error != null)
                {
                    return error;
                }

                set(config, parsed);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition DelimiterText(string key, string label, string description, Func<ModConfig, string> get, Action<ModConfig, string> set, bool required)
    {
        return ValidatedText(new SettingMeta(key, "Chat/Delimiters", label, description, ConfigAdminReloadBehavior.Live), get, set,
            value => required && string.IsNullOrEmpty(value) ? $"{key} cannot be empty." : null);
    }

    private static IReadOnlyList<(string Key, string Label, Func<ChatDelimiters, ChatDelimiter> Get)> GetDelimiterDefinitions()
    {
        return new List<(string, string, Func<ChatDelimiters, ChatDelimiter>)>
        {
            ("ChatDelimiters.Bold", "Bold", delimiters => delimiters.Bold),
            ("ChatDelimiters.Italic", "Italic", delimiters => delimiters.Italic),
            ("ChatDelimiters.Emote", "Emote", delimiters => delimiters.Emote),
            ("ChatDelimiters.Environmental", "Environmental", delimiters => delimiters.Environmental),
            ("ChatDelimiters.PlacedEnvironmental", "Placed environmental", delimiters => delimiters.PlacedEnvironmental),
            ("ChatDelimiters.OOC", "Local OOC", delimiters => delimiters.OOC),
            ("ChatDelimiters.GlobalOOC", "Global OOC", delimiters => delimiters.GlobalOOC),
            ("ChatDelimiters.Quote", "Quote", delimiters => delimiters.Quote),
            ("ChatDelimiters.SignLanguageQuote", "Sign language quote", delimiters => delimiters.SignLanguageQuote)
        };
    }

    private static TValue GetModeValue<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, TValue fallback)
    {
        return dictionary != null && dictionary.TryGetValue(key, out var value) ? value : fallback;
    }

    private static string FormatStringArray(IEnumerable<string> values)
    {
        return string.Join(", ", values ?? Array.Empty<string>());
    }

    private static string[] ParseStringArray(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string ValidateIntArray(string key, string value, (int Min, int Max) range, out int[] parsedValues)
    {
        var parsed = new List<int>();
        var parts = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            parsedValues = Array.Empty<int>();
            return $"{key} must contain at least one whole number.";
        }

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var item) || item < range.Min || item > range.Max)
            {
                parsedValues = Array.Empty<int>();
                return $"{key} must contain whole numbers from {range.Min} to {range.Max}, separated by commas.";
            }

            parsed.Add(item);
        }

        parsedValues = parsed.ToArray();
        return null;
    }

    private static int DefaultDistance(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 90,
        ProximityChatMode.Whisper => 5,
        _ => 35
    };

    private static int DefaultObfuscationStart(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 45,
        ProximityChatMode.Whisper => 2,
        _ => 15
    };

    private static int DefaultFontSize(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 30,
        ProximityChatMode.Whisper => 12,
        _ => 16
    };

    private static string[] DefaultVerbs(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => new[] { "yells", "shouts", "exclaims" },
        ProximityChatMode.Whisper => new[] { "whispers", "mumbles", "mutters" },
        _ => new[] { "says", "states", "mentions" }
    };

    private static string DefaultPunctuation(ProximityChatMode mode) => mode == ProximityChatMode.Yell ? "!" : ".";

    private static float DefaultRpttsGain(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 1.7f,
        ProximityChatMode.Whisper => 0.65f,
        _ => 1f
    };

    private static float DefaultRpttsFalloff(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Whisper => 5f,
        ProximityChatMode.Normal => 1.5f,
        _ => 1f
    };

    private static float DefaultChatterVolume(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 1.4f,
        ProximityChatMode.Whisper => 0.4f,
        _ => 0.8f
    };

    private static float DefaultChatterPitch(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => 1.1f,
        ProximityChatMode.Whisper => 0.95f,
        _ => 1f
    };

    private static ConfigAdminSettingDefinition Bool(string key, string group, string label, string description, ConfigAdminReloadBehavior reloadBehavior, Func<ModConfig, bool> get, Action<ModConfig, bool> set)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = key,
            Group = group,
            Label = label,
            Description = description,
            Kind = ConfigAdminSettingKind.Boolean,
            ReloadBehavior = reloadBehavior,
            GetValue = config => ConfigAdminSettingDefinition.FormatBool(get(config)),
            SetValue = (config, value) =>
            {
                if (!ConfigAdminSettingDefinition.TryParseBool(value, out var parsed))
                {
                    return $"{key} must be true/false or 1/0.";
                }

                set(config, parsed);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition Int(string key, string group, string label, string description, ConfigAdminReloadBehavior reloadBehavior, (Func<ModConfig, int> Get, Action<ModConfig, int> Set) accessors, (int Min, int Max) range)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = key,
            Group = group,
            Label = label,
            Description = description,
            Kind = ConfigAdminSettingKind.Integer,
            ReloadBehavior = reloadBehavior,
            GetValue = config => accessors.Get(config).ToString(CultureInfo.InvariantCulture),
            SetValue = (config, value) =>
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < range.Min || parsed > range.Max)
                {
                    return $"{key} must be a whole number from {range.Min} to {range.Max}.";
                }

                accessors.Set(config, parsed);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition Decimal(string key, string group, string label, string description, ConfigAdminReloadBehavior reloadBehavior, (Func<ModConfig, double> Get, Action<ModConfig, double> Set) accessors, (double Min, double Max) range)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = key,
            Group = group,
            Label = label,
            Description = description,
            Kind = ConfigAdminSettingKind.Decimal,
            ReloadBehavior = reloadBehavior,
            GetValue = config => ConfigAdminSettingDefinition.FormatDecimal(accessors.Get(config)),
            SetValue = (config, value) =>
            {
                if (!ConfigAdminSettingDefinition.TryParseDecimal(value, out var parsed) || parsed < range.Min || parsed > range.Max)
                {
                    return $"{key} must be a number from {range.Min.ToString(CultureInfo.InvariantCulture)} to {range.Max.ToString(CultureInfo.InvariantCulture)}.";
                }

                accessors.Set(config, parsed);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition Text(string key, string group, string label, string description, ConfigAdminReloadBehavior reloadBehavior, Func<ModConfig, string> get, Action<ModConfig, string> set)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = key,
            Group = group,
            Label = label,
            Description = description,
            Kind = ConfigAdminSettingKind.Text,
            ReloadBehavior = reloadBehavior,
            GetValue = config => get(config) ?? string.Empty,
            SetValue = (config, value) =>
            {
                var normalizedValue = value ?? string.Empty;
                set(config, normalizedValue);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition ValidatedText(SettingMeta meta, Func<ModConfig, string> get, Action<ModConfig, string> set, Func<string, string> validate)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = meta.Key,
            Group = meta.Group,
            Label = meta.Label,
            Description = meta.Description,
            Kind = ConfigAdminSettingKind.Text,
            ReloadBehavior = meta.ReloadBehavior,
            GetValue = config => get(config) ?? string.Empty,
            SetValue = (config, value) =>
            {
                var normalizedValue = value ?? string.Empty;
                var error = validate(normalizedValue);
                if (error != null)
                {
                    return error;
                }

                set(config, normalizedValue);
                return null;
            }
        });
    }

    private static ConfigAdminSettingDefinition Select(string key, string group, string label, string description, ConfigAdminReloadBehavior reloadBehavior, (Func<ModConfig, string> Get, Action<ModConfig, string> Set) accessors, string[] options)
    {
        return new ConfigAdminSettingDefinition(new ConfigAdminSettingDefinitionOptions
        {
            Key = key,
            Group = group,
            Label = label,
            Description = description,
            Kind = ConfigAdminSettingKind.Select,
            ReloadBehavior = reloadBehavior,
            GetValue = config => accessors.Get(config) ?? options[0],
            SetValue = (config, value) =>
            {
                if (!options.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    return $"{key} must be one of: {string.Join(", ", options)}.";
                }

                var canonicalValue = options.First(option => option.Equals(value, StringComparison.OrdinalIgnoreCase));
                accessors.Set(config, canonicalValue);
                return null;
            },
            Options = options,
            OptionNames = options
        });
    }

    private static string[] EnumNames<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>();
    }

    private static TEnum[] EnumValues<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>();
    }
}
