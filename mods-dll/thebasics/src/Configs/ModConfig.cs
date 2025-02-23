using System.Collections.Generic;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Configs;

namespace thebasics.Configs
{
    public class ModConfig
    {
        public IDictionary<ProximityChatMode, int> ProximityChatModeDistances = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 90 },
            { ProximityChatMode.Normal, 35 },
            { ProximityChatMode.Whisper, 5 }
        };

        public bool ProximityChatAllowPlayersToChangeNicknames = true;
        
        // New configuration options for disabling features
        public bool DisableNicknames { get; set; } = false;
        
        public bool DisableRPChat { get; set; } = false;
        
        public bool ProximityChatAllowPlayersToChangeNicknameColors = true;
        public string ChangeNicknameColorPermission = "chat";
        public bool BoldNicknames = false;

        // Color application configuration
        public bool ApplyColorsToNicknames { get; set; } = true;  // Apply colors to IC nicknames
        public bool ApplyColorsToPlayerNames { get; set; } = false;  // Apply colors to OOC names

        public bool EnableDistanceObfuscationSystem = true;
        public IDictionary<ProximityChatMode, int> ProximityChatModeObfuscationRanges = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 45 },
            { ProximityChatMode.Normal, 15 },
            { ProximityChatMode.Whisper, 2 }
        };

        public bool EnableDistanceFontSizeSystem = true;
        public IDictionary<ProximityChatMode, int> ProximityChatDefaultFontSize = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 30 },
            { ProximityChatMode.Normal, 16 },
            { ProximityChatMode.Whisper, 12 }
        };

        public int[] ProximityChatClampFontSizes = {
            30,
            16,
            12,
            6,
        };
        
        public IDictionary<ProximityChatMode, string[]> ProximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } }
            };

        public string ProximityChatModeBabbleVerb = "babbles";

        public IDictionary<ProximityChatMode, string> ProximityChatModePunctuation =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." }
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationStart =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationEnd =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };

        public string ProximityChatName = "Proximity";
        public bool UseGeneralChannelAsProximityChat = false;
        public bool EnableGlobalOOC = false;
        public bool AllowOOCToggle = true;
        public string OOCTogglePermission = "chat";

        public bool ProximityChatAsDefault = false;
        public bool PreserveDefaultChatChoice = true;

        public bool SendServerSaveAnnouncement = true;
        public bool SendServerSaveFinishedAnnouncement = false;

        public string TEXT_ServerSaveAnnouncement = "Server save has started - expect lag for a few seconds.";
        public string TEXT_ServerSaveFinished = "Server save has finished.";

        public bool PlayerStatSystem = true;

        public IDictionary<PlayerStatType, bool> PlayerStatToggles = new Dictionary<PlayerStatType, bool>
        {
            { PlayerStatType.Deaths, true },
            { PlayerStatType.NpcKills, true },
            { PlayerStatType.PlayerKills, true },
            { PlayerStatType.BlockBreaks, true },
            { PlayerStatType.DistanceTravelled, true },
        };
        public string PlayerStatClearPermission = "commandplayer";
        public int PlayerStatDistanceTravelledTimer = 2000;

        public bool AllowPlayerTpa = true;
        public bool AllowTpaPrivilegeByDefault = false;
        public bool TpaRequireTemporalGear = true;
        public bool TpaUseCooldown = false;
        public double TpaCooldownInGameHours = 0.5;

        public bool EnableSleepNotifications = true;
        public double SleepNotificationThreshold = 0.5;
        public string TEXT_SleepNotification = "You start to feel tired...";

        public bool EnableLanguageSystem = true;
        public string ChangeOwnLanguagePermission = "chat";
        public string ChangeOtherLanguagePermission = "commandplayer";
        public int MaxLanguagesPerPlayer = 3;
        
        public IList<Language> Languages = new Language[]
        {
            new Language("Common", "The universal language", "c",
                new[] { "al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve" }, "#E9DDCE", true, false),
            new Language("Tradeband", "A common language for trade", "tr",
                new[] { "feng", "tar", "kin", "ga", "shin", "ji" }, "#D4A96A", false, false),
            new Language("Sign", "A visual language using hand gestures and movements", "sign",
                new string[] { }, "#A0A0A0", false, false, true, 60, true)
        };

        public bool PreventProximityChannelSwitching = true;
        public bool ShowNicknameInNametag = true;
        public bool HideNametagUnlessTargeting = false;
        public bool ShowPlayerNameInNametag = true;
        public int NametagRenderRange = 30;
    }
}