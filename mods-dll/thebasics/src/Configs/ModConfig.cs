#pragma warning disable S1133 // Deprecated config members are retained for live config compatibility.
#pragma warning disable S1168 // Legacy shims must getter-return null: NullValueHandling.Ignore only
                              // skips nulls, so an empty collection would write the retired key back
                              // into every saved config.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
            InitializeProximityChatDefaults();
            InitializeLanguageDefaults();
            InitializeChatterDefaults();
            InitializeCharacterSheetDefaults();
            InitializeGeneralFeatureDefaults();
            InitializeNotesDefaults();
            InitializeChatHistoryDefaults();
            SemanticLanguageLearning ??= new SemanticLanguageLearningConfig();
            SemanticLanguageLearning.Normalize();
            InitializeHomeSpawnDefaults();
        }

        /// <summary>
        /// Sentinel for <see cref="ProximityChatModeDistances"/> meaning "deliver to every online player",
        /// matching the convention <see cref="MapPlayerRenderDistance"/> already uses.
        /// </summary>
        public const int UnlimitedRange = -1;

        // Exact match, not "any negative". A hand-edited typo such as -2 would otherwise turn a
        // proximity mode into a server-wide channel silently; ValidateConfig rejects it instead.
        public static bool IsUnlimitedRange(int range) => range == UnlimitedRange;

        /// <summary>
        /// Per-mode dictionaries are only defaulted wholesale, so a hand-edited config can be missing
        /// a mode entirely. Reading one of these with the indexer would throw on the chat hot path
        /// and take the message down for every recipient, so every lookup falls back instead.
        /// </summary>
        public static T GetModeValue<T>(IDictionary<ProximityChatMode, T> valuesByMode, ProximityChatMode mode, T fallback)
        {
            return valuesByMode != null && valuesByMode.TryGetValue(mode, out var value) ? value : fallback;
        }

        public int GetModeDistance(ProximityChatMode mode) =>
            GetModeValue(ProximityChatModeDistances, mode, DefaultModeDistance(mode));

        public int GetModeObfuscationRange(ProximityChatMode mode) =>
            GetModeValue(ProximityChatModeObfuscationRanges, mode, DefaultModeObfuscationRange(mode));

        public int GetModeDefaultFontSize(ProximityChatMode mode) =>
            GetModeValue(ProximityChatDefaultFontSize, mode, DefaultModeFontSize(mode));

        public string GetModePunctuation(ProximityChatMode mode) =>
            GetModeValue(ProximityChatModePunctuation, mode, DefaultModePunctuation(mode));

        /// <summary>
        /// Never empty. A mode missing from a hand-edited dictionary falls back to that mode's real
        /// default verbs, not to the enum name, which would render as `Alice normal "Hello"`.
        /// </summary>
        public string[] GetModeVerbs(ProximityChatMode mode) =>
            TryGetUsableVerbs(ProximityChatModeVerbs, mode, out var verbs) ? verbs : DefaultModeVerbs(mode);

        /// <summary>
        /// False when this mode has no question verbs configured, so callers fall back to the mode's
        /// ordinary verbs. That fallback is deliberate: a server that clears the question verbs for a
        /// mode is asking for questions to read like any other line in that mode, which is why this
        /// is not defaulted the way <see cref="GetModeVerbs"/> is.
        /// </summary>
        public bool TryGetModeQuestionVerbs(ProximityChatMode mode, out string[] verbs) =>
            TryGetUsableVerbs(ProximityChatModeQuestionVerbs, mode, out verbs);

        private static bool TryGetUsableVerbs(IDictionary<ProximityChatMode, string[]> verbsByMode, ProximityChatMode mode, out string[] verbs)
        {
            // Hand-edited configs can leave a mode's list present but empty or blank-filled.
            var usable = GetModeValue(verbsByMode, mode, null)?
                .Where(verb => !string.IsNullOrWhiteSpace(verb))
                .ToArray();

            verbs = usable is { Length: > 0 } ? usable : [];
            return verbs.Length > 0;
        }

        private static string[] DefaultModeVerbs(ProximityChatMode mode) => mode switch
        {
            ProximityChatMode.Yell => ["yells", "shouts", "exclaims"],
            ProximityChatMode.Whisper => ["whispers", "mumbles", "mutters"],
            _ => ["says", "states", "mentions"]
        };

        public float GetRpttsGain(ProximityChatMode mode) =>
            GetModeValue(RPTTS_ModeGain, mode, DefaultRpttsGain(mode));

        public float GetRpttsFalloff(ProximityChatMode mode) =>
            GetModeValue(RPTTS_ModeFalloff, mode, DefaultRpttsFalloff(mode));

        // These mirror the per-mode defaults above and in ConfigAdminSettingRegistry. A generic
        // fallback would silently retune a mode that a partial config happens to omit, which is a
        // worse failure than the exception the fallback was added to prevent.
        private static string DefaultModePunctuation(ProximityChatMode mode) =>
            mode == ProximityChatMode.Yell ? "!" : ".";

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

        /// <summary>
        /// Sign language has no unlimited sentinel, so a hand-edited negative falls back to the
        /// default. Read through here everywhere: the recipient filter and the deferred-delivery
        /// retry compare against this range, and normalising in only one of them means a queued
        /// listener can never be delivered to.
        /// </summary>
        public int GetSignLanguageRange() =>
            SignLanguageRange < 0 ? DefaultSignLanguageRange : SignLanguageRange;

        private const int DefaultSignLanguageRange = 60;

        /// <summary>
        /// Distance font sizes, largest to smallest. The floor is the size a listener at maximum
        /// range reads at, so it has to stay legible: unreadable text conveys nothing while still
        /// taking up chat.
        /// </summary>
        internal static readonly int[] DefaultClampFontSizes = [30, 16, 12, 9];

        /// <summary>
        /// The floor shipped before <see cref="DefaultClampFontSizes"/>. Size 6 was not
        /// small-but-legible, it was unreadable. Retained so upgrades can recognise and replace it.
        /// </summary>
        internal static readonly int[] RetiredClampFontSizes = [30, 16, 12, 6];

        private static int DefaultModeDistance(ProximityChatMode mode) => mode switch
        {
            ProximityChatMode.Yell => 90,
            ProximityChatMode.Whisper => 5,
            _ => 35
        };

        private static int DefaultModeObfuscationRange(ProximityChatMode mode) => mode switch
        {
            ProximityChatMode.Yell => 45,
            ProximityChatMode.Whisper => 2,
            _ => 15
        };

        private static int DefaultModeFontSize(ProximityChatMode mode) => mode switch
        {
            ProximityChatMode.Yell => 30,
            ProximityChatMode.Whisper => 12,
            _ => 16
        };

        private void InitializeProximityChatDefaults()
        {
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

            ProximityChatClampFontSizes ??= [.. DefaultClampFontSizes];

            // Every successful load rewrites the config to disk, so an already-running server has
            // the retired default written out explicitly and the ??= above never fires for it. Left
            // alone, the readability fix would only ever reach fresh installs.
            //
            // Only the exact retired array is replaced, so a genuinely custom set survives. The cost
            // is that this one array can no longer be chosen deliberately — it is the unreadable
            // default that prompted the change, and any other floor is still available.
            if (ProximityChatClampFontSizes.SequenceEqual(RetiredClampFontSizes))
            {
                ProximityChatClampFontSizes = [.. DefaultClampFontSizes];
            }


            InitializeProximityChatVerbDefaults();

            ProximityChatModePunctuation ??= new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." }
            };

            ChatDelimiters ??= new ChatDelimiters();
            ChatDelimiters.InitializeDefaultsIfNeeded();

            ProximityChatPresentationMode = ProximityChatPresentationModes.Normalize(ProximityChatPresentationMode);
            OverheadChatBubbleMode = OverheadChatBubbleModes.Normalize(OverheadChatBubbleMode, DisableRpOverheadBubbles);
            ProseNicknameToken ??= "@";
        }

        private void InitializeProximityChatVerbDefaults()
        {
            ProximityChatModeVerbs ??= new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } }
            };

            ProximityChatModeQuestionVerbs ??= new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "asks" } },
                { ProximityChatMode.Normal, new[] { "asks" } },
                { ProximityChatMode.Whisper, new[] { "asks" } }
            };

            RequireClearSoundPathForSpeech ??= new Dictionary<ProximityChatMode, bool>
            {
                { ProximityChatMode.Yell, false },
                { ProximityChatMode.Normal, false },
                { ProximityChatMode.Whisper, false }
            };
        }

        private void InitializeLanguageDefaults()
        {
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
        }

        private void InitializeChatterDefaults()
        {
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
        }

        private void InitializeGeneralFeatureDefaults()
        {
            ReviewedConfigSettingKeys ??= new List<string>();
            MaxRpCharacterSlots = MaxRpCharacterSlots <= 0 ? 3 : MaxRpCharacterSlots;
        }

        private void InitializeNotesDefaults()
        {
            AdminNotesPermission = string.IsNullOrWhiteSpace(AdminNotesPermission) ? "commandplayer" : AdminNotesPermission;
            PlayerNotesPermission = string.IsNullOrWhiteSpace(PlayerNotesPermission) ? "chat" : PlayerNotesPermission;
            MaxNoteLength = MaxNoteLength <= 0 ? 2000 : MaxNoteLength;
            MaxFreeformNoteLength = MaxFreeformNoteLength <= 0 ? 20000 : MaxFreeformNoteLength;
            MaxAdminNotesPerTarget = MaxAdminNotesPerTarget <= 0 ? 100 : MaxAdminNotesPerTarget;
            MaxPlayerNotesPerAuthor = MaxPlayerNotesPerAuthor <= 0 ? 200 : MaxPlayerNotesPerAuthor;
        }

        private void InitializeChatHistoryDefaults()
        {
            ChatHistoryPermission = string.IsNullOrWhiteSpace(ChatHistoryPermission) ? "commandplayer" : ChatHistoryPermission;
            ChatHistoryManagePermission = string.IsNullOrWhiteSpace(ChatHistoryManagePermission) ? "commandplayer" : ChatHistoryManagePermission;
            ChatHistoryRetentionDays = Math.Max(0, ChatHistoryRetentionDays);
            ChatHistoryMaxEntries = Math.Max(0, ChatHistoryMaxEntries);
            ChatHistorySearchMaxResults = ChatHistorySearchMaxResults <= 0 ? 100 : ChatHistorySearchMaxResults;
            ChatHistoryFlushIntervalMilliseconds = ChatHistoryFlushIntervalMilliseconds <= 0 ? 1000 : Math.Max(100, ChatHistoryFlushIntervalMilliseconds);
        }

        private void InitializeHomeSpawnDefaults()
        {
            HomeCommandPrivilege = string.IsNullOrWhiteSpace(HomeCommandPrivilege) ? "chat" : HomeCommandPrivilege;
            SetHomeCommandPrivilege = string.IsNullOrWhiteSpace(SetHomeCommandPrivilege) ? "chat" : SetHomeCommandPrivilege;
            SpawnCommandPrivilege = string.IsNullOrWhiteSpace(SpawnCommandPrivilege) ? "chat" : SpawnCommandPrivilege;
            SetSpawnCommandPrivilege = string.IsNullOrWhiteSpace(SetSpawnCommandPrivilege) ? "commandplayer" : SetSpawnCommandPrivilege;
            Teleportation ??= new TeleportationConfig();
            Teleportation.InitializeDefaultsIfNeeded();
        }

        private void InitializeCharacterSheetDefaults()
        {
            CharacterSheetSetPermission = string.IsNullOrWhiteSpace(CharacterSheetSetPermission) ? "chat" : CharacterSheetSetPermission;
            CharacterSheetAdminPermission = string.IsNullOrWhiteSpace(CharacterSheetAdminPermission) ? "commandplayer" : CharacterSheetAdminPermission;
            CharacterSheetFields = CharacterSheetFields?
                .Where(field => field != null)
                .ToList();

            if (CharacterSheetFields == null || CharacterSheetFields.Count == 0)
            {
                CharacterSheetFields = CreateDefaultCharacterSheetFields();
            }

            foreach (var field in CharacterSheetFields)
            {
                NormalizeCharacterSheetField(field);
            }
        }

        private static void NormalizeCharacterSheetField(CharacterSheetFieldDefinition field)
        {
            field.Id = TrimOrEmpty(field.Id);
            field.Label = NormalizeCharacterSheetLabel(field.Id, field.Label);
            field.Description = TrimOrEmpty(field.Description);
            field.Type = NormalizeCharacterSheetType(field.Type);
            field.Options = NormalizeCharacterSheetOptions(field.Options);
            field.BindTo = TrimOrEmpty(field.BindTo);
            field.Visibility = NormalizeCharacterSheetVisibility(field.Visibility);
            field.EditorRows = field.EditorRows < 0 ? 0 : field.EditorRows;
            field.LayoutSection = ResolveCharacterSheetLayoutSection(field);
            field.Width = CharacterSheetFieldWidths.Normalize(field.Width);
        }

        private static string TrimOrEmpty(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string NormalizeCharacterSheetLabel(string id, string label)
        {
            var normalized = TrimOrEmpty(label);
            return string.IsNullOrWhiteSpace(normalized) ? id : normalized;
        }

        private static string NormalizeCharacterSheetType(string type)
        {
            var normalized = TrimOrEmpty(type);
            return string.IsNullOrWhiteSpace(normalized)
                ? CharacterSheetFieldTypes.String
                : normalized.ToLowerInvariant();
        }

        private static IList<string> NormalizeCharacterSheetOptions(IEnumerable<string> options)
        {
            return options?
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option.Trim())
                .ToList() ?? new List<string>();
        }

        private static string NormalizeCharacterSheetVisibility(string visibility)
        {
            var normalized = TrimOrEmpty(visibility);
            return string.IsNullOrWhiteSpace(normalized)
                ? CharacterSheetFieldVisibilities.Public
                : normalized.ToLowerInvariant();
        }

        private static string ResolveCharacterSheetLayoutSection(CharacterSheetFieldDefinition field)
        {
            var bindTo = field.BindTo?.Trim();
            if (string.IsNullOrWhiteSpace(field.LayoutSection) &&
                (string.Equals(bindTo, "thebasics.fullName", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(bindTo, "thebasics.nickname", StringComparison.OrdinalIgnoreCase)))
            {
                return CharacterSheetLayoutSections.HeaderSide;
            }

            return CharacterSheetLayoutSections.Normalize(field.LayoutSection);
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
                    Visibility = CharacterSheetFieldVisibilities.Public,
                    LayoutSection = CharacterSheetLayoutSections.HeaderSide
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "nickname",
                    Label = "Nickname",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    BindTo = "thebasics.nickname",
                    MaxLength = 100,
                    Visibility = CharacterSheetFieldVisibilities.Public,
                    LayoutSection = CharacterSheetLayoutSections.HeaderSide
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "pronouns",
                    Label = "Pronouns",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 64,
                    Visibility = CharacterSheetFieldVisibilities.Public,
                    LayoutSection = CharacterSheetLayoutSections.HeaderSide
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "species",
                    Label = "Species / Heritage",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 100,
                    Visibility = CharacterSheetFieldVisibilities.Public,
                    LayoutSection = CharacterSheetLayoutSections.HeaderSide
                },
                new CharacterSheetFieldDefinition
                {
                    Id = "age",
                    Label = "Age",
                    Type = CharacterSheetFieldTypes.String,
                    Optional = true,
                    MaxLength = 64,
                    Visibility = CharacterSheetFieldVisibilities.Public,
                    LayoutSection = CharacterSheetLayoutSections.HeaderSide
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

        /// <summary>
        /// Verbs used when a message reads as a question. Falls back to <see cref="ProximityChatModeVerbs"/>
        /// when a mode has no question verbs configured.
        /// </summary>
        [ProtoMember(147)]
        public IDictionary<ProximityChatMode, string[]> ProximityChatModeQuestionVerbs { get; set; }

        /// <summary>
        /// Experimental, off by default. When set for a mode, speech in that mode only reaches
        /// players the speaker has an unobstructed <em>sound</em> path to. Glass and water block it;
        /// foliage does not.
        ///
        /// Named for sound rather than sight deliberately. It was once RequireLineOfSightForSpeech,
        /// which described neither the filter it uses nor the behaviour it produces: a sealed glass
        /// window blocks speech while a hedge does not.
        /// </summary>
        [ProtoMember(148)]
        public IDictionary<ProximityChatMode, bool> RequireClearSoundPathForSpeech { get; set; }

        [ProtoIgnore]
        [JsonProperty("RequireLineOfSightForSpeech", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Use RequireClearSoundPathForSpeech. The setting models sound, not sight.")]
        public IDictionary<ProximityChatMode, bool> RequireLineOfSightForSpeechLegacy
        {
            get => null;
            set
            {
                // Only adopt the old key when the new one is absent, so a config carrying both does
                // not have its current setting overwritten by the stale one.
                if (value != null && RequireClearSoundPathForSpeech == null)
                {
                    RequireClearSoundPathForSpeech = value;
                }
            }
        }

        /// <summary>
        /// Experimental, off by default (0). Blocks of effective distance added per sound-occluding
        /// block between speaker and listener, so walls muffle speech toward unintelligibility and
        /// then out of range instead of cutting it off outright.
        /// </summary>
        [ProtoMember(149)]
        public int SpeechOcclusionWallPenaltyBlocks { get; set; }

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

        /// <summary>
        /// Whether local OOC from an active spectator uses their RP nickname. Disabled by default
        /// so an invisible speaker is attributed to an unambiguous account name.
        /// </summary>
        [ProtoMember(150)]
        public bool UseNicknameInSpectatorOOC { get; set; } = false;

        /// <summary>
        /// Whether active spectators may deliberately place world-positioned environmental text
        /// with !! or /envhere. This does not permit passive above-head spectator bubbles.
        /// </summary>
        [ProtoMember(151)]
        [DefaultValue(true)]
        public bool AllowSpectatorPlacedEnvironmentalMessages { get; set; } = true;

        /// <summary>
        /// Protects an active spectator from accidentally publishing embodied roleplay while
        /// invisible. Plain or explicit speech, signing, and name-led emotes are refused, requiring
        /// the spectator to deliberately choose OOC or narration. Disable this to retain the normal
        /// RP chat pipeline for spectators.
        /// </summary>
        [ProtoMember(152)]
        [DefaultValue(true)]
        public bool ProtectSpectatorRoleplayChat { get; set; } = true;

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

        // Headshots / character portraits attached to bios.
        [ProtoMember(103)]
        public bool EnableCharacterHeadshots { get; set; } = true;

        // Maximum accepted size of an uploaded image (post-normalization PNG, in kilobytes).
        [ProtoMember(104)]
        public int HeadshotMaxKb { get; set; } = 256;

        // Square dimension that uploads are resized to (server-side normalization). Output is always square.
        [ProtoMember(105)]
        public int HeadshotMaxDimension { get; set; } = 256;

        // Per-player upload cooldown in seconds (anti-spam). Admins bypass this.
        [ProtoMember(106)]
        public int HeadshotUploadCooldownSec { get; set; } = 60;

        // Allow clients to fetch a headshot from a URL via /setbiourl. Disable to require drag-and-drop only.
        [ProtoMember(107)]
        public bool HeadshotUrlAllowed { get; set; } = true;

        // Maximum bytes the client will download from a URL before bailing (kilobytes, pre-normalization).
        [ProtoMember(108)]
        public int HeadshotUrlMaxDownloadKb { get; set; } = 4096;

        // Maximum decoded dimension on either axis the server will accept before rejecting (decompression-bomb guard).
        [ProtoMember(109)]
        public int HeadshotMaxDecodedDimension { get; set; } = 4096;

        // Renders a player's headshot inline inside their floating nametag when one is set. Cosmetic only.
        [ProtoMember(110)]
        public bool ShowHeadshotInNametag { get; set; } = true;

        // Cairo-pixel render size for the inline headshot in the nametag (square). VS's distance
        // scaling shrinks the on-screen size from there. ~100 is a balanced MMO-portrait size.
        [ProtoMember(111)]
        public int NametagInlineImagePixelSize { get; set; } = 100;

        // Use our patched nametag texture renderer (VTML + inline image). Disable to fall back to the
        // vanilla plain-text nametag renderer.
        [ProtoMember(112)]
        public bool UseCustomNametagRenderer { get; set; } = true;

        [ProtoMember(113)]
        public bool EnableAdminNotes { get; set; } = true;

        [ProtoMember(114)]
        public bool EnableStructuredAdminNotes { get; set; } = true;

        [ProtoMember(115)]
        public bool EnableAdminNoteLedger { get; set; } = true;

        [ProtoMember(116)]
        public string AdminNotesPermission { get; set; } = "commandplayer";

        [ProtoMember(117)]
        public bool EnablePlayerNotes { get; set; } = true;

        [ProtoMember(118)]
        public string PlayerNotesPermission { get; set; } = "chat";

        [ProtoMember(119)]
        public int MaxNoteLength { get; set; } = 2000;

        [ProtoMember(120)]
        public int MaxFreeformNoteLength { get; set; } = 20000;

        [ProtoMember(121)]
        public int MaxAdminNotesPerTarget { get; set; } = 100;

        [ProtoMember(122)]
        public int MaxPlayerNotesPerAuthor { get; set; } = 200;

        // Opt-in because relaying proximity chat to Discord makes local RP chat globally visible.
        [ProtoMember(123)]
        public bool EnableTh3EssentialsDiscordRelay { get; set; } = false;

        [ProtoMember(124)]
        public bool EnableChatHistory { get; set; } = true;

        [ProtoMember(125)]
        public bool ChatHistoryCaptureNonBasicChat { get; set; } = true;

        [ProtoMember(126)]
        public string ChatHistoryPermission { get; set; } = "commandplayer";

        [ProtoMember(127)]
        public string ChatHistoryManagePermission { get; set; } = "commandplayer";

        // 0 means keep forever by age.
        [ProtoMember(128)]
        public int ChatHistoryRetentionDays { get; set; }

        // 0 means keep unlimited entries by count.
        [ProtoMember(129)]
        public int ChatHistoryMaxEntries { get; set; }

        [ProtoMember(130)]
        public int ChatHistorySearchMaxResults { get; set; } = 100;

        [ProtoMember(131)]
        public int ChatHistoryFlushIntervalMilliseconds { get; set; } = 1000;

        // Vanilla lifecycle messages are always suppressed from proximity chat; this controls
        // whether death messages are intentionally re-sent to nearby players afterward.
        [ProtoMember(132)]
        public bool EnableNearbyDeathMessagesInProximityChat { get; set; } = true;

        [ProtoMember(133)]
        public string HomeCommandPrivilege { get; set; } = "chat";

        [ProtoMember(134)]
        public string SetHomeCommandPrivilege { get; set; } = "chat";

        [ProtoMember(135)]
        public string SpawnCommandPrivilege { get; set; } = "chat";

        [ProtoMember(136)]
        public string SetSpawnCommandPrivilege { get; set; } = "commandplayer";

        [ProtoMember(137)]
        public bool HomeSpawnRequireTemporalGear { get; set; } = false;

        [ProtoMember(138)]
        public TeleportationConfig Teleportation { get; set; } = new();

        // Opt-in wrapper for vanilla player map marker world config. When enabled, The BASICs
        // also forces mapShowGroupPlayers=false because the proximity chat channel is a group.
        [ProtoMember(139)]
        public bool ManageMapPlayerVisibility { get; set; } = false;

        [ProtoMember(140)]
        public bool MapHideOtherPlayers { get; set; } = false;

        // Vanilla treats negative render distance as unlimited; The BASICs normalizes any negative
        // value to -1 before writing the world config.
        [ProtoMember(141)]
        public int MapPlayerRenderDistance { get; set; } = 1000;

        // Optional #RRGGBB or #RRGGBBAA colors for the custom nametag text bubble. Empty keeps
        // the active Vintage Story UI theme colors.
        [ProtoMember(142)]
        public string NametagBackgroundColor { get; set; } = string.Empty;

        [ProtoMember(143)]
        public string NametagBorderColor { get; set; } = string.Empty;

        [ProtoMember(144)]
        public bool AllowPlayersToChangeNametagColors { get; set; } = true;

        [ProtoMember(145)]
        public string ChangeNametagColorPermission { get; set; } = "chat";

        [ProtoMember(146)]
        public SemanticLanguageLearningConfig SemanticLanguageLearning { get; set; } = new SemanticLanguageLearningConfig();
    }
}
