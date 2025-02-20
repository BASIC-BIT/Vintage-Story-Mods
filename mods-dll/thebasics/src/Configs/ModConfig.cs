using System.Collections.Generic;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Configs
{
    public class ModConfig
    {
        public IDictionary<ProximityChatMode, int> ProximityChatModeDistances = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 90 },
            { ProximityChatMode.Normal, 35 },
            { ProximityChatMode.Whisper, 5 },
            { ProximityChatMode.Sign, 15 }
        };

        public bool ProximityChatAllowPlayersToChangeNicknames = true;

        public bool EnableDistanceObfuscationSystem = true;
        // TODO: Notification range past the falloff range, that says "you hear blah whispering something"
        public IDictionary<ProximityChatMode, int> ProximityChatModeObfuscationRanges = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 45 },
            { ProximityChatMode.Normal, 15 },
            { ProximityChatMode.Whisper, 2 },
            { ProximityChatMode.Sign, 15 }
        };

        
        public bool EnableDistanceFontSizeSystem = true;
        public IDictionary<ProximityChatMode, int> ProximityChatDefaultFontSize = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 30 },
            { ProximityChatMode.Normal, 16 },
            { ProximityChatMode.Whisper, 12 },
            { ProximityChatMode.Sign, 16 }
        };

        // In order to prevent font sizes from being all over the place for every message, clamp them to a set of standard values
        public int[] ProximityChatClampFontSizes = {
            30,
            16,
            12,
            6,
        };
        
        public bool BoldNicknames = false;

        public IDictionary<ProximityChatMode, string[]> ProximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } },
                { ProximityChatMode.Sign, new[] { "signs", "gestures", "motions" } }
            };

        public string ProximityChatModeBabbleVerb = "babbles";

        public IDictionary<ProximityChatMode, string> ProximityChatModePunctuation =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." },
                { ProximityChatMode.Sign, "." }
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationStart =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" },
                { ProximityChatMode.Sign, "<i>\'" }
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationEnd =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" },
                { ProximityChatMode.Sign, "\'</i>" }
            };
        public string ProximityChatName = "Proximity"; //unused if EXPERIMENTAL_UseGeneralChannelAsProximityChat is true
        public bool UseGeneralChannelAsProximityChat = false;
        public bool EnableGlobalOOC = false; // Use dual parens (()) to send a message to the whole server in proximity chat

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
        public int PlayerStatDistanceTravelledTimer = 2000; //ms between checks of player distance travelled

        public bool AllowPlayerTpa = true;
        public bool AllowTpaPrivilegeByDefault = false;
        public bool TpaRequireTemporalGear = true;
        public bool TpaUseCooldown = false;

        public double TpaCooldownInGameHours = 0.5;
        // public bool TpaUseExpiration = true;
        // public double TpaExpirationInGameHours = 0.5;
        // public bool LogTpaToAdminChat = true;

        public bool EnableSleepNotifications = true;
        public double SleepNotificationThreshold = 0.5;
        public string TEXT_SleepNotification = "You start to feel tired...";

        public bool EnableLanguageSystem = true;
        public string ChangeOwnLanguagePermission = "chat";
        public string ChangeOtherLanguagePermission = "commandplayer";
        public int MaxLanguagesPerPlayer = 3; // Maximum number of languages a player can know at once, -1 for unlimited
        
        // public bool AllowDefaultLanguage = true;
        
        public IList<Language> Languages = new Language[]
        {
            new("Common", "The universal language", "c",
                new[] { "al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve" }, "#E9DDCE", true),
            new("Tradeband", "A common language for ease of trade across regions", "tr",
                new[] { "feng", "tar", "kin", "ga", "shin", "ji" }, "#D4A96A"),
            new("Ancient", "A mysterious ancient language", "anc",
                new[] { "xar", "eth", "oth", "ith", "uth", "yth" }, "#8B0000", false, true),
        };

        public bool PreventProximityChannelSwitching = true;

        // Show RP nickname above players heads
        public bool ShowNicknameInNametag = true;
        public bool HideNametagUnlessTargeting = false;
        public bool ShowPlayerNameInNametag = true; // If ShowNicknameInNametag is true, this shows Nickname (PlayerName).  Does nothing if ShowNicknameInNametag is false
        public int NametagRenderRange = 30;
    }
}