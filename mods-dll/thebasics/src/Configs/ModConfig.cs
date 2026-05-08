using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ProtoBuf;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;

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
            TpaRequestPrivilege = string.IsNullOrWhiteSpace(TpaRequestPrivilege) ? "chat" : TpaRequestPrivilege;

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

            ProximityChatClampFontSizes ??= [30, 16, 12, 6];

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

            RPTTS_ModeGain ??= new Dictionary<ProximityChatMode, float>
            {
                { ProximityChatMode.Yell, 1.7f },
                { ProximityChatMode.Normal, 1f },
                { ProximityChatMode.Whisper, 0.65f }
            };

            RPTTS_ModeFalloff ??= new Dictionary<ProximityChatMode, float>
            {
                { ProximityChatMode.Yell, 1f },
                { ProximityChatMode.Normal, 1.5f },
                { ProximityChatMode.Whisper, 5f }
            };

            PlayerStatToggles ??= new Dictionary<PlayerStatType, bool>
            {
                { PlayerStatType.Deaths, true },
                { PlayerStatType.NpcKills, true },
                { PlayerStatType.PlayerKills, true },
                { PlayerStatType.BlockBreaks, true },
                { PlayerStatType.DistanceTravelled, true }
            };

            Languages ??=
            [
                new Language("Common", "The universal language", "c",
                    new string[] { "al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve" },
                    "#E9DDCE", true, false),
                new Language("Tradeband", "A common language for trade", "tr",
                    new string[] { "feng", "tar", "kin", "ga", "shin", "ji" },
                    "#D4A96A", false, false)
            ];

            ChatterModeVolume ??= new Dictionary<ProximityChatMode, float>
            {
                { ProximityChatMode.Yell, 1.4f },
                { ProximityChatMode.Normal, 0.8f },
                { ProximityChatMode.Whisper, 0.4f }
            };

            ChatterModePitch ??= new Dictionary<ProximityChatMode, float>
            {
                { ProximityChatMode.Yell, 1.1f },
                { ProximityChatMode.Normal, 1.0f },
                { ProximityChatMode.Whisper, 0.95f }
            };

            // Initialize chat delimiters and ensure nested defaults even for legacy configs
            ChatDelimiters ??= new ChatDelimiters();
            ChatDelimiters.InitializeDefaultsIfNeeded();

            ProximityChatPresentationMode = ProximityChatPresentationModes.Normalize(ProximityChatPresentationMode);
            OverheadChatBubbleMode = OverheadChatBubbleModes.Normalize(OverheadChatBubbleMode, DisableRpOverheadBubbles);
            ProseNicknameToken ??= "@";
            InitializeCharacterSheetDefaults();
            ReviewedConfigSettingKeys ??= new List<string>();
            MaxRpCharacterSlots = MaxRpCharacterSlots <= 0 ? 3 : MaxRpCharacterSlots;
        }

        private void InitializeCharacterSheetDefaults()
        {
            CharacterSheetSetPermission = string.IsNullOrWhiteSpace(CharacterSheetSetPermission) ? "chat" : CharacterSheetSetPermission;
            CharacterSheetAdminPermission = string.IsNullOrWhiteSpace(CharacterSheetAdminPermission) ? "commandplayer" : CharacterSheetAdminPermission;
            CharacterSheetFields ??= CreateDefaultCharacterSheetFields();

            foreach (var field in CharacterSheetFields)
            {
                NormalizeCharacterSheetField(field);
            }
        }

        private static void NormalizeCharacterSheetField(CharacterSheetFieldDefinition field)
        {
            field.Id ??= string.Empty;
            field.Label ??= field.Id;
            field.Type = string.IsNullOrWhiteSpace(field.Type) ? CharacterSheetFieldTypes.String : field.Type.ToLowerInvariant();
            field.Options ??= new List<string>();
            field.BindTo ??= string.Empty;
            field.Visibility = string.IsNullOrWhiteSpace(field.Visibility) ? CharacterSheetFieldVisibilities.Public : field.Visibility.ToLowerInvariant();
            field.EditorRows = field.EditorRows < 0 ? 0 : field.EditorRows;
        }

        private static IList<CharacterSheetFieldDefinition> CreateDefaultCharacterSheetFields()
        {
            return
            [
                new CharacterSheetFieldDefinition
                {
                    Id = "fullName",
                    Label = "Full Name",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = false,
                    BindTo = "thebasics.fullName",
                    MaxLength = 100,
                    Visibility = CharacterSheetFieldVisibilities.Public
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "nickname",
                    Label = "Nickname",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    BindTo = "thebasics.nickname",
                    MaxLength = 100,
                    Visibility = CharacterSheetFieldVisibilities.Public
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "pronouns",
                    Label = "Pronouns",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 64,
                    Visibility = CharacterSheetFieldVisibilities.Public
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "species",
                    Label = "Species / Heritage",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 100,
                    Visibility = CharacterSheetFieldVisibilities.Public
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "age",
                    Label = "Age",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 64,
                    Visibility = CharacterSheetFieldVisibilities.Public
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "appearance",
                    Label = "Appearance",
                    Type = CharacterSheetFieldTypes.LongString,
                    Optional = true,
                    MaxLength = 600,
                    Visibility = CharacterSheetFieldVisibilities.Nearby,
                    EditorRows = 4
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "background",
                    Label = "Background",
                    Type = CharacterSheetFieldTypes.LongString,
                    Optional = true,
                    MaxLength = 1500,
                    Visibility = CharacterSheetFieldVisibilities.Self,
                    ShowInLook = false,
                    EditorRows = 8
                }
            ];
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

        // ProtoMember(18) - REMOVED/DEPRECATED - Previously ProximityChatModeQuotationStart
        // ProtoMember(19) - REMOVED/DEPRECATED - Previously ProximityChatModeQuotationEnd
        // Quote handling is now done directly in transformers based on language type

        [ProtoMember(20)]
        public string ProximityChatName { get; set; } = "Proximity";

        [ProtoMember(21)]
        public bool UseGeneralChannelAsProximityChat { get; set; } = false;

        [ProtoMember(22)]
        public bool EnableGlobalOOC { get; set; } = true;

        [ProtoMember(23)]
        public bool AllowOOCToggle { get; set; } = true;

        [ProtoMember(24)]
        public string OOCTogglePermission { get; set; } = "chat";

        [ProtoMember(25)]
        public bool ProximityChatAsDefault { get; set; } = true;

        [ProtoMember(26)]
        public bool PreserveDefaultChatChoice { get; set; } = true;

        [ProtoMember(27)]
        public bool SendServerSaveAnnouncement { get; set; } = true;

        [ProtoMember(28)]
        public bool SendServerSaveFinishedAnnouncement { get; set; } = true;

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

        // ProtoMember(36) - RESERVED/DEPRECATED - Previously AllowTpaPrivilegeByDefault.
        // Do not reuse this number to avoid deserialization issues with existing config/network payloads.
        [ProtoMember(36)]
        [JsonIgnore]
        [Obsolete("Use TpaRequestPrivilege. This property is ignored.")]
        public bool AllowTpaPrivilegeByDefaultReserved { get; set; } = true;

        [ProtoIgnore]
        [JsonProperty("AllowTpaPrivilegeByDefault", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Use TpaRequestPrivilege.")]
        public bool? AllowTpaPrivilegeByDefaultLegacy
        {
            get => null;
            set
            {
                if (value.HasValue)
                {
                    TpaRequestPrivilege = value.Value ? "chat" : "tpa";
                }
            }
        }

        [ProtoMember(37)]
        public bool TpaRequireTemporalGear { get; set; } = true;

        [ProtoMember(38)]
        public bool TpaUseCooldown { get; set; } = false;

        [ProtoMember(39)]
        public double TpaCooldownInGameHours { get; set; } = 0.5;

        [ProtoMember(62)]
        public bool TpaUseTimeout { get; set; } = true;

        [ProtoMember(63)]
        public double TpaTimeoutMinutes { get; set; } = 2.0;

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

        // ProtoMember(57) - RESERVED/BLACKLISTED
        // Previously used for DisallowNicknameThatIsAnotherPlayersName (removed - now always enforced)
        // Do not reuse this number to avoid deserialization issues with existing config files

        [ProtoMember(58)]
        public string OOCColor { get; set; } = "#eaf188";

        [ProtoMember(59)]
        public string GlobalOOCColor { get; set; } = "#f1b288";

        [ProtoMember(60)]
        public bool UseNicknameInGlobalOOC { get; set; } = false;

        [ProtoMember(61)]
        public bool UseNicknameInOOC { get; set; } = true;

        [ProtoMember(64)]
        public bool RemoveGrantedLanguagesOnChange { get; set; } = true;

        [ProtoMember(65)]
        public ChatDelimiters ChatDelimiters { get; set; }

        [ProtoIgnore]
        [JsonProperty("RemoveClassLanguagesOnClassChange", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Use RemoveGrantedLanguagesOnChange")]
        public bool? RemoveClassLanguagesOnClassChangeLegacy
        {
            get => null;
            set
            {
                if (value == true)
                {
                    RemoveGrantedLanguagesOnChange = true;
                }
            }
        }

        [ProtoMember(66)]
        public IDictionary<ProximityChatMode, float> RPTTS_ModeGain { get; set; }

        [ProtoMember(67)]
        public IDictionary<ProximityChatMode, float> RPTTS_ModeFalloff { get; set; }

        // ----- Typing Indicator (client-side UI feature, server-configured) ----- //

        [ProtoMember(68)]
        public bool EnableTypingIndicator { get; set; } = true;

        [ProtoMember(69)]
        public int TypingIndicatorMaxRange { get; set; } = 30;

        [ProtoMember(70)]
        public float TypingIndicatorTimeoutSeconds { get; set; } = 5f;

        // If empty/null, client uses lang key `thebasics:typingindicator-typing`.
        [ProtoMember(71)]
        public string TypingIndicatorTextOverride { get; set; } = "";

        // DEPRECATED: Use OverheadChatBubbleMode instead. This property is retained only
        // for protobuf deserialization compatibility with existing config files on disk.
        // It is no longer read by any runtime code.
        [ProtoMember(72)]
        [Obsolete("Use OverheadChatBubbleMode. This property is ignored.")]
        public bool OverrideSpeechBubblesWithRpText { get; set; } = true;

        // When true, enables verbose debug logging and diagnostic instrumentation.
        // Intended for temporary use while investigating reports.
        [ProtoMember(73)]
        public bool DebugMode { get; set; } = false;

        // When true, server save announcements use EnumChatType.Notification (popup-style).
        // When false, send as a regular chat line (less intrusive).
        [ProtoMember(74)]
        public bool ServerSaveAnnouncementAsNotification { get; set; } = true;

        // When true, server save finished announcements use EnumChatType.Notification (popup-style).
        // When false, send as a regular chat line (less intrusive).
        [ProtoMember(75)]
        public bool ServerSaveFinishedAsNotification { get; set; } = true;

        // Controls what the typing indicator renders: Icon, Text, or Both.
        // Disabled entirely when EnableTypingIndicator is false.
        [ProtoMember(76)]
        public TypingIndicatorDisplayMode TypingIndicatorDisplayMode { get; set; } = TypingIndicatorDisplayMode.Both;

        // Permission for the toggling of bypassing proximity chat restrictions entirely, allowing a player to speak globally regardless of distance or mode.
        [ProtoMember(77)]
        public string RPTextTogglePermission { get; set; } = "chat";

        // ----- Chatter (seraph voice sounds on chat) ----- //

        // When true, characters play their seraph instrument voice when sending speech messages.
        // Players can individually opt out with /chatter off.
        [ProtoMember(78)]
        public bool EnableChatter { get; set; } = true;

        // Volume modifier per chat mode for chatter sounds.
        // Defaults lean quiet — chatter is ambient flavor, not a notification.
        [ProtoMember(79)]
        public IDictionary<ProximityChatMode, float> ChatterModeVolume { get; set; }

        // Pitch modifier per chat mode for chatter sounds.
        [ProtoMember(80)]
        public IDictionary<ProximityChatMode, float> ChatterModePitch { get; set; }

        // Maximum raycast distance (in blocks) for placed environmental messages (!! prefix / /envhere).
        // If the raycast hits nothing within this distance, the message falls back to a
        // standard environmental message above the sender's head.
        [ProtoMember(81)]
        public double MaxEnvironmentPlacementDistance { get; set; } = 30.0;

        // Multiplier applied only when sending chatter back to the speaking player.
        // Other listeners receive the normal mode volume.
        [ProtoMember(82)]
        public float ChatterSelfVolumeMultiplier { get; set; } = 0.4f;

        // When true, sign language requires line of sight at send time.
        [ProtoMember(83)]
        public bool RequireLineOfSightForSignLanguage { get; set; } = true;

        // When true, client-side nametag rendering requires line of sight.
        [ProtoMember(84)]
        public bool NametagRequiresLineOfSight { get; set; } = true;

        // DEPRECATED: Use OverheadChatBubbleMode="Vanilla" instead.
        // Still honored only when OverheadChatBubbleMode is missing/empty.
        [ProtoMember(85)]
        public bool DisableRpOverheadBubbles { get; set; } = false;

        // Privilege required to initiate /tpa and /tpahere. Use "chat" for all normal players,
        // or "tpa" to require explicitly granted access.
        [ProtoMember(86)]
        public string TpaRequestPrivilege { get; set; } = "chat";

        // Minimum overhead speech bubble lifetime in milliseconds. Vanilla can show very short
        // messages for less than this because duration is based on message length.
        [ProtoMember(87)]
        public int SpeechBubbleMinimumDisplayMilliseconds { get; set; } = 3500;

        // How speech is presented in the chat window and overhead bubbles.
        // Allowed: StandardRoleplay, SimpleSpeech, PlainProximity, Prose.
        [ProtoMember(88)]
        public string ProximityChatPresentationMode { get; set; } = string.Empty;

        // When true, RP speech, emotes, and environmental messages receive automatic
        // capitalization/punctuation. When false, typed casing/punctuation are preserved.
        [ProtoMember(89)]
        public bool NormalizeProximityChatText { get; set; } = true;

        // Controls overhead chat bubbles. Allowed: RpText, Vanilla, Off.
        [ProtoMember(90)]
        public string OverheadChatBubbleMode { get; set; } = string.Empty;

        // In Prose mode, this standalone token is replaced with the sender's formatted RP nickname.
        // Set to empty to disable nickname substitution.
        [ProtoMember(91)]
        public string ProseNicknameToken { get; set; } = "@";

        // When true, Prose and environmental messages are prefixed with the account name in brackets.
        // This is a moderation/auditability aid for servers that allow freeform unattributed text.
        [ProtoMember(92)]
        public bool AttributeFreeformMessagesToPlayerName { get; set; } = false;

        [ProtoMember(93)]
        public bool EnableCharacterSheets { get; set; } = true;

        [ProtoMember(94)]
        public string CharacterSheetSetPermission { get; set; } = "chat";

        [ProtoMember(95)]
        public string CharacterSheetAdminPermission { get; set; } = "commandplayer";

        [ProtoMember(96)]
        public IList<CharacterSheetFieldDefinition> CharacterSheetFields { get; set; }

        [ProtoMember(97)]
        public double CharacterSheetLookRange { get; set; } = 12.0;

        [ProtoMember(98)]
        public bool CharacterSheetLookRequiresLineOfSight { get; set; } = true;

        [ProtoMember(99)]
        public bool CharacterSheetRequireRequiredFieldsForRoleplay { get; set; } = false;

        // Settings the server owner has acknowledged in the in-game config panel.
        [ProtoMember(100)]
        public IList<string> ReviewedConfigSettingKeys { get; set; }

        // Enables RP character slots.
        [ProtoMember(101)]
        public bool EnableRpCharacterSlots { get; set; } = false;

        [ProtoMember(102)]
        public int MaxRpCharacterSlots { get; set; } = 3;
    }
}
