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
        // Called after deserialization
        [ProtoAfterDeserialization]
        private void OnDeserialized()
        {
            // If collections are null after deserialization, initialize them with defaults
            InitializeDefaultsIfNeeded();
        }
        
        // Helper method to initialize default values only if not already set
        public void InitializeDefaultsIfNeeded()
        {
            // Initialize dictionaries only if they're null
            ProximityChatModeDistances ??= new Dictionary<ProximityChatMode, int>
            {
                { ProximityChatMode.Yell, 90 },
                { ProximityChatMode.Normal, 35 },
                { ProximityChatMode.Whisper, 5 }
            };
            
            ProximityChatModeObfuscationRanges ??= new Dictionary<ProximityChatMode, int>
            {
                { ProximityChatMode.Yell, 45 },
                { ProximityChatMode.Normal, 15 },
                { ProximityChatMode.Whisper, 2 }
            };
            
            ProximityChatDefaultFontSize ??= new Dictionary<ProximityChatMode, int>
            {
                { ProximityChatMode.Yell, 30 },
                { ProximityChatMode.Normal, 16 },
                { ProximityChatMode.Whisper, 12 }
            };
            
            ProximityChatClampFontSizes ??= new int[] { 30, 16, 12, 6 };
            
            ProximityChatModeVerbs ??= new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } }
            };
            
            ProximityChatModePunctuation ??= new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." }
            };
            
            ProximityChatModeQuotationStart ??= new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };
            
            ProximityChatModeQuotationEnd ??= new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" }
            };
            
            PlayerStatToggles ??= new Dictionary<PlayerStatType, bool>
            {
                { PlayerStatType.Deaths, true },
                { PlayerStatType.NpcKills, true },
                { PlayerStatType.PlayerKills, true },
                { PlayerStatType.BlockBreaks, true },
                { PlayerStatType.DistanceTravelled, true }
            };
            
            Languages ??= new Language[]
            {
                new Language("Common", "The universal language", "c",
                    new string[] { "al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve" },
                    "#E9DDCE", true, false),
                new Language("Tradeband", "A common language for trade", "tr",
                    new string[] { "feng", "tar", "kin", "ga", "shin", "ji" },
                    "#D4A96A", false, false)
            };
        }

        [ProtoMember(1)]
        public IDictionary<ProximityChatMode, int> ProximityChatModeDistances { get; set; }

        [ProtoMember(2)]
        public bool ProximityChatAllowPlayersToChangeNicknames { get; set; } = true;
        
        // New configuration options for disabling features
        [ProtoMember(3)]
        public bool DisableNicknames { get; set; } = false;
        
        [ProtoMember(4)]
        public bool DisableRPChat { get; set; } = false;
        
        [ProtoMember(5)]
        public bool ProximityChatAllowPlayersToChangeNicknameColors { get; set; } = true;
        
        [ProtoMember(6)]
        public string ChangeNicknameColorPermission { get; set; } = "chat";
        
        [ProtoMember(7)]
        public bool BoldNicknames { get; set; } = false;

        // Color application configuration
        [ProtoMember(8)]
        public bool ApplyColorsToNicknames { get; set; } = true;  // Apply colors to IC nicknames
        
        [ProtoMember(9)]
        public bool ApplyColorsToPlayerNames { get; set; } = false;  // Apply colors to OOC names

        [ProtoMember(10)]
        public bool EnableDistanceObfuscationSystem { get; set; } = true;
        
        [ProtoMember(11)]
        public IDictionary<ProximityChatMode, int> ProximityChatModeObfuscationRanges { get; set; }

        [ProtoMember(12)]
        public bool EnableDistanceFontSizeSystem { get; set; } = true;
        
        [ProtoMember(13)]
        public IDictionary<ProximityChatMode, int> ProximityChatDefaultFontSize { get; set; }

        [ProtoMember(14)]
        public int[] ProximityChatClampFontSizes { get; set; }
        
        [ProtoMember(15)]
        public IDictionary<ProximityChatMode, string[]> ProximityChatModeVerbs { get; set; }

        [ProtoMember(16)]
        public string ProximityChatModeBabbleVerb { get; set; } = "babbles";

        [ProtoMember(17)]
        public IDictionary<ProximityChatMode, string> ProximityChatModePunctuation { get; set; }

        [ProtoMember(18)]
        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationStart { get; set; }

        [ProtoMember(19)]
        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationEnd { get; set; }

        [ProtoMember(20)]
        public string ProximityChatName { get; set; } = "Proximity";
        
        [ProtoMember(21)]
        public bool UseGeneralChannelAsProximityChat { get; set; } = false;
        
        [ProtoMember(22)]
        public bool EnableGlobalOOC { get; set; } = false;
        
        [ProtoMember(23)]
        public bool AllowOOCToggle { get; set; } = true;
        
        [ProtoMember(24)]
        public string OOCTogglePermission { get; set; } = "chat";

        [ProtoMember(25)]
        public bool ProximityChatAsDefault { get; set; } = false;
        
        [ProtoMember(26)]
        public bool PreserveDefaultChatChoice { get; set; } = true;

        [ProtoMember(27)]
        public bool SendServerSaveAnnouncement { get; set; } = true;
        
        [ProtoMember(28)]
        public bool SendServerSaveFinishedAnnouncement { get; set; } = false;

        [ProtoMember(29)]
        public string TEXT_ServerSaveAnnouncement { get; set; } = "Server save has started - expect lag for a few seconds.";
        
        [ProtoMember(30)]
        public string TEXT_ServerSaveFinished { get; set; } = "Server save has finished.";

        [ProtoMember(31)]
        public bool PlayerStatSystem { get; set; } = true;

        [ProtoMember(32)]
        public IDictionary<PlayerStatType, bool> PlayerStatToggles { get; set; }
        
        [ProtoMember(33)]
        public string PlayerStatClearPermission { get; set; } = "commandplayer";
        
        [ProtoMember(34)]
        public int PlayerStatDistanceTravelledTimer { get; set; } = 2000;

        [ProtoMember(35)]
        public bool AllowPlayerTpa { get; set; } = true;
        
        [ProtoMember(36)]
        public bool AllowTpaPrivilegeByDefault { get; set; } = false;
        
        [ProtoMember(37)]
        public bool TpaRequireTemporalGear { get; set; } = true;
        
        [ProtoMember(38)]
        public bool TpaUseCooldown { get; set; } = false;
        
        [ProtoMember(39)]
        public double TpaCooldownInGameHours { get; set; } = 0.5;

        [ProtoMember(40)]
        public bool EnableSleepNotifications { get; set; } = true;
        
        [ProtoMember(41)]
        public double SleepNotificationThreshold { get; set; } = 0.5;
        
        [ProtoMember(42)]
        public string TEXT_SleepNotification { get; set; } = "You start to feel tired...";

        [ProtoMember(43)]
        public bool EnableLanguageSystem { get; set; } = true;
        
        [ProtoMember(44)]
        public string ChangeOwnLanguagePermission { get; set; } = "chat";
        
        [ProtoMember(45)]
        public string ChangeOtherLanguagePermission { get; set; } = "commandplayer";
        
        [ProtoMember(46)]
        public int MaxLanguagesPerPlayer { get; set; } = 3;
        
        // Sign language configuration
        [ProtoMember(47)]
        public int SignLanguageRange { get; set; } = 60;
        
        [ProtoMember(48)]
        public IList<Language> Languages { get; set; }

        [ProtoMember(49)]
        public bool PreventProximityChannelSwitching { get; set; } = true;
        
        [ProtoMember(50)]
        public bool ShowNicknameInNametag { get; set; } = true;
        
        [ProtoMember(51)]
        public bool HideNametagUnlessTargeting { get; set; } = false;
        
        [ProtoMember(52)]
        public bool ShowPlayerNameInNametag { get; set; } = true;
        
        [ProtoMember(53)]
        public int NametagRenderRange { get; set; } = 30;
        
        [ProtoMember(54)]
        public string EmoteColor { get; set; } = "#E9DDCE";

        [ProtoMember(55)]
        public int MinNicknameLength { get; set; } = 3;
        
        [ProtoMember(56)]
        public int MaxNicknameLength { get; set; } = 100;

        // TODO: Should this also warn admins when they do this and/or ask for confirmation?
        // TODO: Catalog all existing nicknames and player names for all users in the server, to implement this functionality
        [ProtoMember(57)]
        public bool DisallowNicknameThatIsAnotherPlayersName { get; set; } = true;
    }
}