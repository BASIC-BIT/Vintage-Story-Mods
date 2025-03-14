using System.Collections.Generic;
using ProtoBuf;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Configs;

namespace thebasics.Configs
{
    [ProtoContract]
    public class ModConfig
    {
        [ProtoMember(1)]
        public IDictionary<ProximityChatMode, int> ProximityChatModeDistances = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 90 },
            { ProximityChatMode.Normal, 35 },
            { ProximityChatMode.Whisper, 5 }
        };

        [ProtoMember(2)]
        public bool ProximityChatAllowPlayersToChangeNicknames = true;
        
        // New configuration options for disabling features
        [ProtoMember(3)]
        public bool DisableNicknames { get; set; } = false;
        
        [ProtoMember(4)]
        public bool DisableRPChat { get; set; } = false;
        
        [ProtoMember(5)]
        public bool ProximityChatAllowPlayersToChangeNicknameColors = true;
        
        [ProtoMember(6)]
        public string ChangeNicknameColorPermission = "chat";
        
        [ProtoMember(7)]
        public bool BoldNicknames = false;

        // Color application configuration
        [ProtoMember(8)]
        public bool ApplyColorsToNicknames { get; set; } = true;  // Apply colors to IC nicknames
        
        [ProtoMember(9)]
        public bool ApplyColorsToPlayerNames { get; set; } = false;  // Apply colors to OOC names

        [ProtoMember(10)]
        public bool EnableDistanceObfuscationSystem = true;
        
        [ProtoMember(11)]
        public IDictionary<ProximityChatMode, int> ProximityChatModeObfuscationRanges = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 45 },
            { ProximityChatMode.Normal, 15 },
            { ProximityChatMode.Whisper, 2 }
        };

        [ProtoMember(12)]
        public bool EnableDistanceFontSizeSystem = true;
        
        [ProtoMember(13)]
        public IDictionary<ProximityChatMode, int> ProximityChatDefaultFontSize = new Dictionary<ProximityChatMode, int>
        {
            { ProximityChatMode.Yell, 30 },
            { ProximityChatMode.Normal, 16 },
            { ProximityChatMode.Whisper, 12 }
        };

        [ProtoMember(14)]
        public int[] ProximityChatClampFontSizes = [
            30,
            16,
            12,
            6,
        ];
        
        [ProtoMember(15)]
        public IDictionary<ProximityChatMode, string[]> ProximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } }
            };

        [ProtoMember(16)]
        public string ProximityChatModeBabbleVerb = "babbles";

        [ProtoMember(17)]
        public IDictionary<ProximityChatMode, string> ProximityChatModePunctuation =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." }
            };

        [ProtoMember(18)]
        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationStart =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };

        [ProtoMember(19)]
        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationEnd =
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };

        [ProtoMember(20)]
        public string ProximityChatName = "Proximity";
        
        [ProtoMember(21)]
        public bool UseGeneralChannelAsProximityChat = false;
        
        [ProtoMember(22)]
        public bool EnableGlobalOOC = false;
        
        [ProtoMember(23)]
        public bool AllowOOCToggle = true;
        
        [ProtoMember(24)]
        public string OOCTogglePermission = "chat";

        [ProtoMember(25)]
        public bool ProximityChatAsDefault = false;
        
        [ProtoMember(26)]
        public bool PreserveDefaultChatChoice = true;

        [ProtoMember(27)]
        public bool SendServerSaveAnnouncement = true;
        
        [ProtoMember(28)]
        public bool SendServerSaveFinishedAnnouncement = false;

        [ProtoMember(29)]
        public string TEXT_ServerSaveAnnouncement = "Server save has started - expect lag for a few seconds.";
        
        [ProtoMember(30)]
        public string TEXT_ServerSaveFinished = "Server save has finished.";

        [ProtoMember(31)]
        public bool PlayerStatSystem = true;

        [ProtoMember(32)]
        public IDictionary<PlayerStatType, bool> PlayerStatToggles = new Dictionary<PlayerStatType, bool>
        {
            { PlayerStatType.Deaths, true },
            { PlayerStatType.NpcKills, true },
            { PlayerStatType.PlayerKills, true },
            { PlayerStatType.BlockBreaks, true },
            { PlayerStatType.DistanceTravelled, true },
        };
        
        [ProtoMember(33)]
        public string PlayerStatClearPermission = "commandplayer";
        
        [ProtoMember(34)]
        public int PlayerStatDistanceTravelledTimer = 2000;

        [ProtoMember(35)]
        public bool AllowPlayerTpa = true;
        
        [ProtoMember(36)]
        public bool AllowTpaPrivilegeByDefault = false;
        
        [ProtoMember(37)]
        public bool TpaRequireTemporalGear = true;
        
        [ProtoMember(38)]
        public bool TpaUseCooldown = false;
        
        [ProtoMember(39)]
        public double TpaCooldownInGameHours = 0.5;

        [ProtoMember(40)]
        public bool EnableSleepNotifications = true;
        
        [ProtoMember(41)]
        public double SleepNotificationThreshold = 0.5;
        
        [ProtoMember(42)]
        public string TEXT_SleepNotification = "You start to feel tired...";

        [ProtoMember(43)]
        public bool EnableLanguageSystem = true;
        
        [ProtoMember(44)]
        public string ChangeOwnLanguagePermission = "chat";
        
        [ProtoMember(45)]
        public string ChangeOtherLanguagePermission = "commandplayer";
        
        [ProtoMember(46)]
        public int MaxLanguagesPerPlayer = 3;
        
        // Sign language configuration
        [ProtoMember(47)]
        public int SignLanguageRange = 60;
        
        [ProtoMember(48)]
        public IList<Language> Languages = new Language[]
        {
            new Language("Common", "The universal language", "c",
                ["al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve"], "#E9DDCE", true, false),
            new Language("Tradeband", "A common language for trade", "tr",
                ["feng", "tar", "kin", "ga", "shin", "ji"], "#D4A96A", false, false)
        };

        [ProtoMember(49)]
        public bool PreventProximityChannelSwitching = true;
        
        [ProtoMember(50)]
        public bool ShowNicknameInNametag = true;
        
        [ProtoMember(51)]
        public bool HideNametagUnlessTargeting = false;
        
        [ProtoMember(52)]
        public bool ShowPlayerNameInNametag = true;
        
        [ProtoMember(53)]
        public int NametagRenderRange = 30;
        
        [ProtoMember(54)]
        public string EmoteColor = "#E9DDCE";

        [ProtoMember(55)]
        public int MinNicknameLength = 3;
        
        [ProtoMember(56)]
        public int MaxNicknameLength = 100;

        // TODO: Should this also warn admins when they do this and/or ask for confirmation?
        // TODO: Catalog all existing nicknames and player names for all users in the server, to implement this functionality
        [ProtoMember(57)]
        public bool DisallowNicknameThatIsAnotherPlayersName = true;
    }
}