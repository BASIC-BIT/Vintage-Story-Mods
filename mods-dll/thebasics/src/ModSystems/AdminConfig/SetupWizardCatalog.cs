using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.AdminConfig;

public sealed record SetupWizardPage(string Id, string TopicId, string Title, string Description, IReadOnlyList<string> SettingKeys);

public static class SetupWizardCatalog
{
    public static IReadOnlyList<SetupWizardPage> Pages { get; } = Array.AsReadOnly(new[]
    {
        Page("chat.basics", "chat", "RP features and delivery", "Choose roleplay formatting and where nearby chat appears. Keep General global with a separate Proximity tab, or use General for nearby speech. Turning RP features off keeps proximity delivery.",
            "DisableRPChat", "UseGeneralChannelAsProximityChat", "ProximityChatPresentationMode", "NormalizeProximityChatText", "EnableChatter"),
        Page("chat.language", "chat", "Languages and OOC", "Languages are an independent choice. Explicit local OOC stays local; sticky local OOC mode has its own permission. Global OOC requires RP features and reaches the server.",
            "EnableLanguageSystem", "EnableGlobalOOC", "AllowOOCToggle", "OOCTogglePermission", "UseNicknameInOOC"),
        Page("chat.ranges", "chat", "Who hears speech?", "A range of 5 reaches players up to 4 blocks away along the grid. The outlines show who can hear each mode. Set -1 for server-wide speech.",
            "ProximityChatModeDistances.Whisper", "ProximityChatModeDistances.Normal", "ProximityChatModeDistances.Yell"),
        Page("chat.obfuscation", "chat", "Distant speech", "Words become harder to understand beyond the dashed circles. This uses straight-line distance; the diamonds show the hearing range measured along the grid.",
            "EnableDistanceObfuscationSystem", "ProximityChatModeObfuscationRanges.Whisper", "ProximityChatModeObfuscationRanges.Normal", "ProximityChatModeObfuscationRanges.Yell"),
        Page("chat.tabs", "chat", "Default chat tab", "Choose the initial tab and whether to remember player choice. The existing /rptext off option bypasses chat-tab filtering; these settings do not enforce every player's chat experience.",
            "ProximityChatAsDefault", "PreserveDefaultChatChoice", "PreventProximityChannelSwitching"),
        Page("teleport.tools", "teleportation", "Requests, homes, and spawn", "Enable each BASICs tool independently. These switches control BASICs commands; other mods may provide their own. Command registration changes require a restart.",
            "AllowPlayerTpa", "Teleportation.RegisterHomeCommands", "Teleportation.RegisterSpawnCommands"),
        Page("teleport.utilities", "teleportation", "Top, back, and stuck", "Choose BASICs travel and recovery commands independently. Stuck is a gear-free emergency route to spawn with its own staff policy.",
            "Teleportation.RegisterTopCommand", "Teleportation.RegisterBackCommand", "Teleportation.RegisterStuckCommand"),
        Page("teleport.requests", "teleportation", "Player request consent", "The other player must accept a request. A temporal gear is charged when the request is submitted. After acceptance, the moving player stands still for the warmup.",
            "TpaRequestPrivilege", "TpaRequireTemporalGear", "Teleportation.TpaWarmupSeconds"),
        Page("teleport.requesttiming", "teleportation", "Request cooldown and expiry", "Request cooldown uses in-game hours. Pending request expiry uses real minutes. These settings preserve their values when their switches are off.",
            "TpaUseCooldown", "TpaCooldownInGameHours", "TpaUseTimeout", "TpaTimeoutMinutes"),
        Page("teleport.homes", "teleportation", "Returning home", "Homes have a per-player limit, a stand-still warmup, and a cooldown in real seconds. Home and spawn share the gear choice; payment occurs only on successful arrival.",
            "Teleportation.MaxHomes", "Teleportation.HomeWarmupSeconds", "Teleportation.HomeCooldownSeconds", "HomeSpawnRequireTemporalGear", "HomeCommandPrivilege"),
        Page("teleport.spawn", "teleportation", "Returning to spawn", "Spawn uses its own stand-still warmup and cooldown in real seconds. Its gear choice is shared with home and is checked before warmup, then charged on successful arrival.",
            "Teleportation.SpawnWarmupSeconds", "Teleportation.SpawnCooldownSeconds", "HomeSpawnRequireTemporalGear", "SpawnCommandPrivilege"),
        Page("teleport.top", "teleportation", "Moving to the surface", "Top has an independent privilege, real-second warmup, cooldown, and gear cost. A required gear is consumed only when the teleport completes.",
            "Teleportation.TopCommandPrivilege", "Teleportation.TopWarmupSeconds", "Teleportation.TopCooldownSeconds", "Teleportation.TopRequireTemporalGear"),
        Page("teleport.back", "teleportation", "Returning to a previous location", "Back keeps a temporary return location. Warmup, cooldown, and expiry use real seconds; zero expiry keeps the location indefinitely. Gear payment occurs on successful completion.",
            "Teleportation.BackCommandPrivilege", "Teleportation.BackWarmupSeconds", "Teleportation.BackCooldownSeconds", "Teleportation.BackExpiresAfterSeconds", "Teleportation.BackRequireTemporalGear"),
        Page("teleport.stuck", "teleportation", "Emergency return", "Stuck is gear-free and returns the player to spawn. It has an independent warmup, real-second cooldown, and reminder interval; zero reminder interval turns reminders off.",
            "Teleportation.StuckCommandPrivilege", "Teleportation.StuckWarmupSeconds", "Teleportation.StuckCooldownSeconds", "Teleportation.StuckReminderIntervalSeconds"),
        Page("teleport.stuckpolicy", "teleportation", "Staff policy for stuck", "Choose which staff privilege receives emergency notices and which online privilege blocks use. An empty blocking privilege disables that restriction.",
            "Teleportation.StuckAdminNotifyPrivilege", "Teleportation.StuckBlockedByOnlinePrivilege"),
        Page("teleport.warmup", "teleportation", "Interrupting a warmup", "Travel warmups require the player to stand still. Choose whether damage and interactions also cancel them.",
            "Teleportation.CancelWarmupOnDamage", "Teleportation.CancelWarmupOnInteraction"),
        Page("notifications.savestart", "notifications", "Save started", "Choose Off, Chat, or Popup and edit the announcement. Turning the message off does not disable saving or save pauses.",
            "SendServerSaveAnnouncement", "ServerSaveAnnouncementAsNotification", "TEXT_ServerSaveAnnouncement"),
        Page("notifications.savefinish", "notifications", "Save finished", "Choose Off, Chat, or Popup independently for the finished message and edit its text.",
            "SendServerSaveFinishedAnnouncement", "ServerSaveFinishedAsNotification", "TEXT_ServerSaveFinished"),
        Page("notifications.sleep", "notifications", "Sleep reminder", "The threshold controls a reminder, not night skipping. With four players, the default 50% reminds at two sleepers. Existing 0% and 100% thresholds send no reminder.",
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
