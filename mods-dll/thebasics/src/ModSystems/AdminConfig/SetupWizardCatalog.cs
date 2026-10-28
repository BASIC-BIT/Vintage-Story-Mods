using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.AdminConfig;

public sealed record SetupWizardPage(string Id, string TopicId, string Title, string Description, IReadOnlyList<string> SettingKeys);
public sealed record SetupWizardTopic(string Id, string Title, string Description, string EntryPageId);

public static class SetupWizardCatalog
{
    public static IReadOnlyList<SetupWizardTopic> Topics { get; } = Array.AsReadOnly(new[]
    {
        new SetupWizardTopic("chat", "Chat", "Speech, languages\nand chat tabs", "chat.basics"),
        new SetupWizardTopic("teleportation", "Teleportation", "Requests, homes\nand recovery", "teleport.tools"),
        new SetupWizardTopic("notifications", "Notifications", "Save announcements\nand sleep reminders", "notifications.savestart"),
        new SetupWizardTopic("appearance", "Player display", "Bubbles, typing\nand nametags", "appearance.bubbles"),
        new SetupWizardTopic("map", "Map visibility", "Who appears on\nplayer maps", "map.visibility"),
        new SetupWizardTopic("characters", "Characters", "Sheets, access\nand character slots", "characters.sheets"),
        new SetupWizardTopic("roleplay", "Roleplay tools", "Dice, scene markers\nand player choices", "roleplay.dice"),
        new SetupWizardTopic("server", "Server tools", "History, notes\nand player stats", "server.history")
    });

    public static IReadOnlyList<SetupWizardPage> Pages { get; } = Array.AsReadOnly(new[]
    {
        Page("chat.basics", "chat", "Chat style and channels", "Use General for nearby speech, or keep it global with a separate Proximity tab. Choose how speech looks and sounds. Nearby chat still works with roleplay features off.",
            "DisableRPChat", "UseGeneralChannelAsProximityChat", "ProximityChatPresentationMode", "NormalizeProximityChatText", "EnableChatter"),
        Page("chat.language", "chat", "Languages and OOC", "Languages and global OOC need roleplay features. Local OOC uses the speech range, while global OOC reaches everyone. Set who can stay in local OOC mode.",
            "EnableLanguageSystem", "EnableGlobalOOC", "AllowOOCToggle", "OOCTogglePermission", "UseNicknameInOOC", "UseNicknameInGlobalOOC"),
        Page("chat.wording", "chat", "Speech wording", "Choose verbs and punctuation for each speech mode. Lists accept comma-separated choices.",
            "AttributeFreeformMessagesToPlayerName", "ProseNicknameToken", "ProximityChatModeBabbleVerb", "ProximityChatModePunctuation.Normal", "ProximityChatModePunctuation.Whisper", "ProximityChatModePunctuation.Yell", "ProximityChatModeQuestionVerbs.Normal", "ProximityChatModeQuestionVerbs.Whisper", "ProximityChatModeQuestionVerbs.Yell", "ProximityChatModeVerbs.Normal", "ProximityChatModeVerbs.Whisper", "ProximityChatModeVerbs.Yell", "RPTextTogglePermission"),
        Page("chat.shortcuts", "chat", "Formatting shortcuts", "Leave End blank for a prefix-only shortcut. For bold, italics, emotes and environmental text, Start can also close the text.",
            "ChatDelimiters.Bold.End", "ChatDelimiters.Bold.Start", "ChatDelimiters.Emote.End", "ChatDelimiters.Emote.Start", "ChatDelimiters.Environmental.End", "ChatDelimiters.Environmental.Start", "ChatDelimiters.GlobalOOC.End", "ChatDelimiters.GlobalOOC.Start", "ChatDelimiters.Italic.End", "ChatDelimiters.Italic.Start", "ChatDelimiters.OOC.End", "ChatDelimiters.OOC.Start", "ChatDelimiters.PlacedEnvironmental.End", "ChatDelimiters.PlacedEnvironmental.Start", "ChatDelimiters.Quote.End", "ChatDelimiters.Quote.Start", "ChatDelimiters.SignLanguageQuote.End", "ChatDelimiters.SignLanguageQuote.Start"),
        Page("chat.colors", "chat", "Chat colors", "Choose colors for emotes, local OOC and global OOC.",
            "EmoteColor", "OOCColor", "GlobalOOCColor"),
        Page("chat.languageeditor", "chat", "Languages", "Edit your server's languages and preview their chat text."),
        Page("chat.languageaccess", "chat", "Learning and granting languages", "Choose who can add or remove languages. The player limit applies to /addlang. Staff grants and learning through speech can exceed it.",
            "ChangeOwnLanguagePermission", "ChangeOtherLanguagePermission", "MaxLanguagesPerPlayer", "RemoveGrantedLanguagesOnChange"),
        Page("chat.signlanguage", "chat", "Reading sign language", "Set how far players can read signing, and whether they need a clear view. Distance counts blocks along the grid and stops before the limit. There is no unlimited range.",
            "SignLanguageRange", "RequireLineOfSightForSignLanguage"),
        Page("chat.ranges", "chat", "Hearing distance", "Distance counts blocks along the grid, including height. A range of 5 reaches up to 4 blocks away. Use -1 for server-wide speech.",
            "ProximityChatModeDistances.Whisper", "ProximityChatModeDistances.Normal", "ProximityChatModeDistances.Yell"),
        Page("chat.obfuscation", "chat", "Understanding distant speech", "Speech becomes harder to read with distance and walls. These distances use a straight line. Speech with a hearing range of -1 stays clear.",
            "EnableDistanceObfuscationSystem", "ProximityChatModeObfuscationRanges.Whisper", "ProximityChatModeObfuscationRanges.Normal", "ProximityChatModeObfuscationRanges.Yell"),
        Page("chat.tabs", "chat", "Opening and switching tabs", "Start players in General or Proximity, remember their last choice, and control automatic tab switching. Using General for nearby speech always opens General.",
            "ProximityChatAsDefault", "PreserveDefaultChatChoice", "PreventProximityChannelSwitching", "ProximityChatName", "ProximityChatTabPosition"),
        Page("teleport.tools", "teleportation", "Visiting players and saved places", "Let players visit each other with accepted requests, return to saved homes, and travel to spawn. Turn on each tool your server allows.",
            "AllowPlayerTpa", "Teleportation.RegisterHomeCommands", "Teleportation.RegisterSpawnCommands"),
        Page("teleport.utilities", "teleportation", "Travel and recovery commands", "/top reaches the surface, /back returns to where a player last teleported from, and /stuck goes to spawn. Turn on each command your server allows.",
            "Teleportation.RegisterTopCommand", "Teleportation.RegisterBackCommand", "Teleportation.RegisterStuckCommand"),
        Page("teleport.requests", "teleportation", "Travel requests", "The other player must accept. The requester pays any required gear when sending, with refunds for declined, expired, or cancelled requests. The traveler stands still during the warmup.",
            "TpaRequestPrivilege", "TpaRequireTemporalGear", "Teleportation.TpaWarmupSeconds"),
        Page("teleport.requesttiming", "teleportation", "Request limits", "The cooldown uses in-game hours. Request expiry uses real minutes. Turn either limit off to remove it.",
            "TpaUseCooldown", "TpaCooldownInGameHours", "TpaUseTimeout", "TpaTimeoutMinutes"),
        Page("teleport.homes", "teleportation", "Saved homes", "Set a home limit per player, a warmup while standing still, and a cooldown in real seconds. Home and spawn share an optional gear cost, paid just before teleporting.",
            "Teleportation.MaxHomes", "Teleportation.HomeWarmupSeconds", "Teleportation.HomeCooldownSeconds", "HomeSpawnRequireTemporalGear", "HomeCommandPrivilege", "SetHomeCommandPrivilege"),
        Page("teleport.spawn", "teleportation", "World spawn", "Set a warmup while standing still and a cooldown in real seconds. Home and spawn share an optional gear cost, paid just before teleporting.",
            "Teleportation.SpawnWarmupSeconds", "Teleportation.SpawnCooldownSeconds", "HomeSpawnRequireTemporalGear", "SpawnCommandPrivilege", "SetSpawnCommandPrivilege"),
        Page("teleport.top", "teleportation", "Reaching the surface", "Set who can use /top, its warmup and cooldown in real seconds, and whether it costs a gear. A required gear is spent just before teleporting.",
            "Teleportation.TopCommandPrivilege", "Teleportation.TopWarmupSeconds", "Teleportation.TopCooldownSeconds", "Teleportation.TopRequireTemporalGear"),
        Page("teleport.back", "teleportation", "Returning with /back", "Set the warmup, cooldown, and return location expiry in real seconds. An expiry of 0 removes the time limit. A required gear is spent just before teleporting.",
            "Teleportation.BackCommandPrivilege", "Teleportation.BackWarmupSeconds", "Teleportation.BackCooldownSeconds", "Teleportation.BackExpiresAfterSeconds", "Teleportation.BackRequireTemporalGear"),
        Page("teleport.stuck", "teleportation", "Escaping with /stuck", "/stuck returns players to spawn without a gear. Set its warmup, cooldown, and reminder interval in real seconds. Use 0 to turn reminders off.",
            "Teleportation.StuckCommandPrivilege", "Teleportation.StuckWarmupSeconds", "Teleportation.StuckCooldownSeconds", "Teleportation.StuckReminderIntervalSeconds"),
        Page("teleport.stuckpolicy", "teleportation", "Staff and /stuck", "Choose who receives /stuck notices. You can also block /stuck while another player with the chosen staff permission is online. Leave the blocking permission blank for no restriction.",
            "Teleportation.StuckAdminNotifyPrivilege", "Teleportation.StuckBlockedByOnlinePrivilege"),
        Page("teleport.warmup", "teleportation", "Interrupted travel", "Movement cancels every travel warmup. Decide whether taking damage or interacting with the world also cancels it.",
            "Teleportation.CancelWarmupOnDamage", "Teleportation.CancelWarmupOnInteraction"),
        Page("notifications.savestart", "notifications", "When saving starts", "Tell players when the world starts saving. Show the message as regular or highlighted chat in General, or turn it off.",
            "SendServerSaveAnnouncement", "ServerSaveAnnouncementAsNotification", "TEXT_ServerSaveAnnouncement"),
        Page("notifications.savefinish", "notifications", "When saving finishes", "Let players know saving has finished. Show the message as regular or highlighted chat in General, or turn it off.",
            "SendServerSaveFinishedAnnouncement", "ServerSaveFinishedAsNotification", "TEXT_ServerSaveFinished"),
        Page("notifications.sleep", "notifications", "Inviting players to sleep", "Send a reminder when enough players are in bed. At 50%, it appears when the second of four players goes to bed. The game's night-skipping rules stay the same.",
            "EnableSleepNotifications", "SleepNotificationThreshold", "TEXT_SleepNotification"),
        Page("notifications.deaths", "notifications", "Nearby deaths", "Show deaths to members of the nearby chat channel inside the Talk range (35 blocks when Talk is unlimited). Distance counts blocks along the grid. This only controls nearby re-sent notices.",
            "EnableNearbyDeathMessagesInProximityChat"),
        Page("appearance.bubbles", "appearance", "Overhead speech", "Show formatted speech above players, use the game's plain bubbles, or turn overhead bubbles off. Formatted bubbles need roleplay features. Minimum time applies to custom bubbles.",
            "OverheadChatBubbleMode", "SpeechBubbleMinimumDisplayMilliseconds"),
        Page("appearance.typing", "appearance", "Typing indicators", "Show when nearby players have chat open, are composing, or are typing. Indicators need a clear view and stay hidden for sneaking players and spectators. A range of 0 hides them.",
            "EnableTypingIndicator", "TypingIndicatorDisplayMode", "TypingIndicatorMaxRange", "TypingIndicatorTimeoutSeconds", "TypingIndicatorTextOverride"),
        Page("appearance.names", "appearance", "Names above players", "Show character names, account names, or both. Limit how far away other nametags appear, require a clear view, or show them only when targeted.",
            "ShowNicknameInNametag", "ShowPlayerNameInNametag", "HideNametagUnlessTargeting", "NametagRenderRange", "NametagRequiresLineOfSight"),
        Page("appearance.nametagcolors", "appearance", "Nametag colors", "Set default background and border colors for BASICs nametags. Empty colors use the game theme. Players' saved colors take priority over these defaults.",
            "NametagBackgroundColor", "NametagBorderColor", "AllowPlayersToChangeNametagColors", "ChangeNametagColorPermission"),
        Page("map.visibility", "map", "Players on the map", "Choose who appears on the minimap and world map. Managed maps turn off same-group visibility. Use -1 for unlimited range. The preview uses sample players; when management is off, world map settings apply.",
            "ManageMapPlayerVisibility", "MapHideOtherPlayers", "MapPlayerRenderDistance"),
        Page("characters.sheets", "characters", "Character sheets", "Let players write a character sheet and inspect nearby characters. Opening a sheet with the C menu is a server default that players can override for themselves.",
            "EnableCharacterSheets", "CharacterSheetAutoOpenOnCharacterScreen"),
        Page("characters.builder", "characters", "Sheet builder", "Edit the server sheet template with fictional answers from Pip."),
        Page("characters.access", "characters", "Viewing and editing sheets", "Choose who can use sheet-editing commands or manage other players' sheets. Limit nearby viewing by distance and line of sight. Required fields can block roleplay until complete.",
            "CharacterSheetSetPermission", "CharacterSheetAdminPermission", "CharacterSheetLookRange", "CharacterSheetLookRequiresLineOfSight", "CharacterSheetRequireRequiredFieldsForRoleplay"),
        Page("characters.slots", "characters", "Multiple characters", "Character slots keep separate identities, appearances, inventories, body states, and positions. They require character sheets. The slot limit applies per account.",
            "EnableRpCharacterSlots", "MaxRpCharacterSlots"),
        Page("roleplay.dice", "roleplay", "Dice rolls", "Public rolls follow the player's speech range. Private rolls go only to the roller. Dice sounds reach eligible listeners within 8 blocks, and players can mute them.",
            "EnableDiceRolling", "EnableDiceRollSounds"),
        Page("roleplay.descriptions", "roleplay", "Placed descriptions", "Set how far a player can aim when placing environmental text. Distance is measured in blocks. This controls placed chat descriptions, separately from scene markers.",
            "MaxEnvironmentPlacementDistance"),
        Page("roleplay.scenemarkers", "roleplay", "Scene markers", "Let players leave descriptions in the world. Turning markers off stops crafting, placement, and editing while keeping existing markers and their text.",
            "EnableSceneMarkers"),
        Page("roleplay.spectators", "roleplay", "Spectator chat", "Protect invisible spectators from accidental speech or emotes. They can still choose OOC or narration. Typing indicators and player-attached bubbles stay hidden.",
            "ProtectSpectatorRoleplayChat", "AllowSpectatorPlacedEnvironmentalMessages", "UseNicknameInSpectatorOOC"),
        Page("roleplay.nicknamechanges", "roleplay", "Player nickname choices", "Let players set their own nicknames and colors. Color changes need the chosen permission. Turning these commands off keeps saved names.",
            "ProximityChatAllowPlayersToChangeNicknames", "ProximityChatAllowPlayersToChangeNicknameColors", "ChangeNicknameColorPermission"),
        Page("roleplay.nicknamerules", "roleplay", "Nickname rules", "Set nickname length limits, or disable nickname commands and the nickname requirement. Saved names can still appear in chat and nametags.",
            "DisableNicknames", "MinNicknameLength", "MaxNicknameLength"),
        Page("server.history", "server", "Chat history", "Store chat for staff search. You can also include chat sent outside The BASICs. Search and export or deletion can have different permissions.",
            "EnableChatHistory", "ChatHistoryCaptureNonBasicChat", "ChatHistoryPermission", "ChatHistoryManagePermission"),
        Page("server.retention", "server", "History limits", "Remove older entries by age or keep only the newest entries. Set either retention limit to 0 to remove that limit. Search limits control how many results staff receive.",
            "ChatHistoryRetentionDays", "ChatHistoryMaxEntries", "ChatHistorySearchMaxResults", "ChatHistoryFlushIntervalMilliseconds"),
        Page("server.staffnotes", "server", "Staff notes", "Keep staff-only notes about player accounts. Offer separate timestamped entries, one freeform ledger, or both. Only players with the chosen permission can access them.",
            "EnableAdminNotes", "EnableStructuredAdminNotes", "EnableAdminNoteLedger", "AdminNotesPermission", "MaxAdminNotesPerTarget"),
        Page("server.personalnotes", "server", "Personal notes", "Give players private notes and a freeform notebook. Notes belong to their author and can describe different players. The entry limit counts all of an author's notes.",
            "EnablePlayerNotes", "PlayerNotesPermission", "MaxPlayerNotesPerAuthor"),
        Page("server.notelimits", "server", "Note length limits", "Set the maximum characters in each note body and freeform ledger. These limits apply to both staff and personal notes.",
            "MaxNoteLength", "MaxFreeformNoteLength"),
        Page("server.stats", "server", "Player activity stats", "Record player activity for /playerstats. The tracking switch takes effect after restart. Choose who can clear saved statistics.",
            "PlayerStatSystem", "PlayerStatClearPermission", "PlayerStatToggles.BlockBreaks", "PlayerStatToggles.Deaths", "PlayerStatToggles.DistanceTravelled", "PlayerStatToggles.NpcKills", "PlayerStatToggles.PlayerKills", "PlayerStatDistanceTravelledTimer"),
        Page("server.integrations", "server", "Integrations", "Allow the optional th3essentials Discord relay. The integration must also be installed and configured on the server. You can configure this switch in advance; this example does not check availability or send messages.",
            "EnableTh3EssentialsDiscordRelay"),
        Page("server.diagnostics", "server", "Diagnostics", "Write extra BASICs diagnostic and performance messages to the server logs. Enable this while investigating a problem.",
            "DebugMode")
    });

    private static readonly HashSet<string> SettingKeys = Pages.SelectMany(page => page.SettingKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSettingKey(string key) => key != null && SettingKeys.Contains(key);

    public static bool TryGetPage(string id, out SetupWizardPage page)
    {
        page = Pages.FirstOrDefault(candidate => candidate.Id == id);
        return page != null;
    }

    public static bool TryGetTopic(string id, out SetupWizardTopic topic)
    {
        topic = Topics.FirstOrDefault(candidate => candidate.Id == id);
        return topic != null;
    }

    public static SetupWizardTopic GetTopicForPage(string pageId) =>
        TryGetPage(pageId, out var page) && TryGetTopic(page.TopicId, out var topic) ? topic : null;

    private static SetupWizardPage Page(string id, string topicId, string title, string description, params string[] keys) =>
        new(id, topicId, title, description, Array.AsReadOnly(keys));
}
