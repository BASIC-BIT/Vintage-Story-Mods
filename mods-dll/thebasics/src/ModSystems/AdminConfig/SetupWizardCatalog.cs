using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.AdminConfig;

public sealed record SetupWizardPage(string Id, string TopicId, string Title, string Description, IReadOnlyList<string> SettingKeys);

public static class SetupWizardCatalog
{
    public static IReadOnlyList<SetupWizardPage> Pages { get; } = Array.AsReadOnly(new[]
    {
        Page("chat.basics", "chat", "Chat style and channels", "Use General for nearby speech, or keep it global with a separate Proximity tab. Choose how speech looks and sounds. Nearby chat still works with roleplay features off.",
            "DisableRPChat", "UseGeneralChannelAsProximityChat", "ProximityChatPresentationMode", "NormalizeProximityChatText", "EnableChatter"),
        Page("chat.language", "chat", "Languages and OOC", "Languages and global OOC need roleplay features. Local OOC uses the speech range, while global OOC reaches everyone. Set who can stay in local OOC mode.",
            "EnableLanguageSystem", "EnableGlobalOOC", "AllowOOCToggle", "OOCTogglePermission", "UseNicknameInOOC"),
        Page("chat.ranges", "chat", "Hearing distance", "Distance counts blocks along the grid, including height. A range of 5 reaches up to 4 blocks away. Use -1 for server-wide speech.",
            "ProximityChatModeDistances.Whisper", "ProximityChatModeDistances.Normal", "ProximityChatModeDistances.Yell"),
        Page("chat.obfuscation", "chat", "Understanding distant speech", "Speech becomes harder to read with distance and walls. These distances use a straight line. Speech with a hearing range of -1 stays clear.",
            "EnableDistanceObfuscationSystem", "ProximityChatModeObfuscationRanges.Whisper", "ProximityChatModeObfuscationRanges.Normal", "ProximityChatModeObfuscationRanges.Yell"),
        Page("chat.tabs", "chat", "Opening and switching tabs", "Start players in General or Proximity, remember their last choice, and control automatic tab switching. Using General for nearby speech always opens General.",
            "ProximityChatAsDefault", "PreserveDefaultChatChoice", "PreventProximityChannelSwitching"),
        Page("teleport.tools", "teleportation", "Visiting players and saved places", "Let players visit each other with accepted requests, return to saved homes, and travel to spawn. Turn on each tool your server allows.",
            "AllowPlayerTpa", "Teleportation.RegisterHomeCommands", "Teleportation.RegisterSpawnCommands"),
        Page("teleport.utilities", "teleportation", "Travel and recovery commands", "/top reaches the surface, /back returns to where a player last teleported from, and /stuck goes to spawn. Turn on each command your server allows.",
            "Teleportation.RegisterTopCommand", "Teleportation.RegisterBackCommand", "Teleportation.RegisterStuckCommand"),
        Page("teleport.requests", "teleportation", "Travel requests", "The other player must accept. The requester pays any required gear when sending, with refunds for declined, expired, or cancelled requests. The traveler stands still during the warmup.",
            "TpaRequestPrivilege", "TpaRequireTemporalGear", "Teleportation.TpaWarmupSeconds"),
        Page("teleport.requesttiming", "teleportation", "Request limits", "The cooldown uses in-game hours. Request expiry uses real minutes. Turn either limit off to remove it.",
            "TpaUseCooldown", "TpaCooldownInGameHours", "TpaUseTimeout", "TpaTimeoutMinutes"),
        Page("teleport.homes", "teleportation", "Saved homes", "Set a home limit per player, a warmup while standing still, and a cooldown in real seconds. Home and spawn share an optional gear cost, paid just before teleporting.",
            "Teleportation.MaxHomes", "Teleportation.HomeWarmupSeconds", "Teleportation.HomeCooldownSeconds", "HomeSpawnRequireTemporalGear", "HomeCommandPrivilege"),
        Page("teleport.spawn", "teleportation", "World spawn", "Set a warmup while standing still and a cooldown in real seconds. Home and spawn share an optional gear cost, paid just before teleporting.",
            "Teleportation.SpawnWarmupSeconds", "Teleportation.SpawnCooldownSeconds", "HomeSpawnRequireTemporalGear", "SpawnCommandPrivilege"),
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
            "EnableSleepNotifications", "SleepNotificationThreshold", "TEXT_SleepNotification")
    });

    private static readonly HashSet<string> SettingKeys = Pages.SelectMany(page => page.SettingKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSettingKey(string key) => key != null && SettingKeys.Contains(key);

    public static bool TryGetPage(string id, out SetupWizardPage page)
    {
        page = Pages.FirstOrDefault(candidate => candidate.Id == id);
        return page != null;
    }

    private static SetupWizardPage Page(string id, string topicId, string title, string description, params string[] keys) =>
        new(id, topicId, title, description, Array.AsReadOnly(keys));
}
