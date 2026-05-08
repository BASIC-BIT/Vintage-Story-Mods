using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;

namespace thebasics.ModSystems.AdminConfig;

public static class ConfigAdminSettingRegistry
{
    private static readonly IReadOnlyList<ConfigAdminSettingDefinition> _settings = BuildSettings();
    private static readonly IDictionary<string, ConfigAdminSettingDefinition> _settingsByKey =
        _settings.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ConfigAdminSettingDefinition> Settings => _settings;

    public static bool TryGet(string key, out ConfigAdminSettingDefinition setting)
    {
        return _settingsByKey.TryGetValue(key, out setting);
    }

    private static IReadOnlyList<ConfigAdminSettingDefinition> BuildSettings()
    {
        return new List<ConfigAdminSettingDefinition>
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
            Bool("SendServerSaveAnnouncement", "Server Notifications", "Announce save start", "Notify players when a server save starts.", ConfigAdminReloadBehavior.Live, c => c.SendServerSaveAnnouncement, (c, v) => c.SendServerSaveAnnouncement = v),
            Bool("SendServerSaveFinishedAnnouncement", "Server Notifications", "Announce save finish", "Notify players when a server save finishes.", ConfigAdminReloadBehavior.Live, c => c.SendServerSaveFinishedAnnouncement, (c, v) => c.SendServerSaveFinishedAnnouncement = v),
            Bool("ServerSaveAnnouncementAsNotification", "Server Notifications", "Save start as popup", "Use notification popup for save start.", ConfigAdminReloadBehavior.Live, c => c.ServerSaveAnnouncementAsNotification, (c, v) => c.ServerSaveAnnouncementAsNotification = v),
            Bool("ServerSaveFinishedAsNotification", "Server Notifications", "Save finish as popup", "Use notification popup for save finish.", ConfigAdminReloadBehavior.Live, c => c.ServerSaveFinishedAsNotification, (c, v) => c.ServerSaveFinishedAsNotification = v),
            Text("TEXT_ServerSaveAnnouncement", "Server Notifications", "Save start text", "Text sent when a server save starts.", ConfigAdminReloadBehavior.Live, c => c.TEXT_ServerSaveAnnouncement, (c, v) => c.TEXT_ServerSaveAnnouncement = v),
            Text("TEXT_ServerSaveFinished", "Server Notifications", "Save finish text", "Text sent when a server save finishes.", ConfigAdminReloadBehavior.Live, c => c.TEXT_ServerSaveFinished, (c, v) => c.TEXT_ServerSaveFinished = v),
            Bool("EnableSleepNotifications", "Server Notifications", "Enable sleep notifications", "Notify players when enough players are sleeping.", ConfigAdminReloadBehavior.Live, c => c.EnableSleepNotifications, (c, v) => c.EnableSleepNotifications = v),
            Decimal("SleepNotificationThreshold", "Server Notifications", "Sleep notification threshold", "Fraction of online players sleeping before notifying.", ConfigAdminReloadBehavior.Live, (c => c.SleepNotificationThreshold, (c, v) => c.SleepNotificationThreshold = v), (0, 1)),
            Text("TEXT_SleepNotification", "Server Notifications", "Sleep notification text", "Text sent when enough players are sleeping.", ConfigAdminReloadBehavior.Live, c => c.TEXT_SleepNotification, (c, v) => c.TEXT_SleepNotification = v),
            Bool("DebugMode", "Diagnostics", "Debug mode", "Enable The BASICs diagnostic logging.", ConfigAdminReloadBehavior.Live, c => c.DebugMode, (c, v) => c.DebugMode = v),
            Bool("EnableRpCharacterSlots", "Characters", "Enable RP character slots", "Enable identity-only RP character slots. Inventory, position, class, and skin remain shared.", ConfigAdminReloadBehavior.RestartRequired, c => c.EnableRpCharacterSlots, (c, v) => c.EnableRpCharacterSlots = v),
            Int("MaxRpCharacterSlots", "Characters", "Max RP character slots", "Maximum active RP character slots per account.", ConfigAdminReloadBehavior.Live, (c => c.MaxRpCharacterSlots, (c, v) => c.MaxRpCharacterSlots = v), (1, 20)),
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
            Bool("UseGeneralChannelAsProximityChat", "Restart Required", "Use General as proximity chat", "Requires restart because chat group migration is startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.UseGeneralChannelAsProximityChat, (c, v) => c.UseGeneralChannelAsProximityChat = v),
            Text("ProximityChatName", "Restart Required", "Proximity chat name", "Requires restart because chat group setup is startup-shaped.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatName, (c, v) => c.ProximityChatName = v),
            Bool("ProximityChatAsDefault", "Restart Required", "Proximity chat as default", "Requires restart/rejoin for chat tab default behavior.", ConfigAdminReloadBehavior.RestartRequired, c => c.ProximityChatAsDefault, (c, v) => c.ProximityChatAsDefault = v),
            Bool("PreserveDefaultChatChoice", "Restart Required", "Preserve default chat choice", "Requires restart/rejoin for chat tab default behavior.", ConfigAdminReloadBehavior.RestartRequired, c => c.PreserveDefaultChatChoice, (c, v) => c.PreserveDefaultChatChoice = v),
            Bool("PreventProximityChannelSwitching", "Restart Required", "Prevent proximity tab switching", "Requires reconnect for existing clients.", ConfigAdminReloadBehavior.RestartRequired, c => c.PreventProximityChannelSwitching, (c, v) => c.PreventProximityChannelSwitching = v)
        };
    }

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
                set(config, value ?? string.Empty);
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
}
