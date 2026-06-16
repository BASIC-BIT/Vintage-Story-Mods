using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Semantics;
using thebasics.ModSystems.TPA;
using thebasics.ModSystems.TPA.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Extensions
{
    public static class IServerPlayerExtensions
    {
        private const string ModDataNickname = "BASIC_NICKNAME";
        private const string ModDataCharacterSheet = "BASIC_CHARACTER_SHEET";
        private const string ModDataActiveCharacterId = "BASIC_ACTIVE_CHARACTER_ID";
        private const string CharacterSheetFullNameBind = "thebasics.fullName";
        private const string CharacterSheetFullNameField = "fullName";
        private const string CharacterSheetNicknameBind = "thebasics.nickname";
        private const string CharacterSheetNicknameField = "nickname";
        private const string ModDataNicknameColor = "BASIC_NICKNAME_COLOR";
        private const string ModDataNametagBackgroundColor = "BASIC_NAMETAG_BACKGROUND_COLOR";
        private const string ModDataNametagBorderColor = "BASIC_NAMETAG_BORDER_COLOR";
        private const string ModDataChatMode = "BASIC_CHATMODE";
        private const string ModDataEmoteMode = "BASIC_EMOTEMODE";
        private const string ModDataRpTextEnabled = "BASIC_RPTEXTENABLED";
        private const string ModDataOOCEnabled = "BASIC_OOCENABLED";

        private const string ModDataPlayerStatsPrefix = "BASIC_COUNT_";

        private const string ModDataTpaTime = "BASIC_TPA_TIME";
        private const string ModDataTpAllowed = "BASIC_TPA_ALLOWED";
        private const string ModDataOutgoingTpaRequest = "BASIC_OUTGOING_TPA_REQUEST";


        private const string ModDataLanguages = "BASIC_LANGUAGES";
        private const string ModDataDefaultLanguage = "BASIC_DEFAULT_LANGUAGE";
        private const string ModDataLanguageSkills = "BASIC_LANGUAGE_SKILLS";
        private const string ModDataSemanticLanguageMemory = "BASIC_LANGUAGE_SEMANTIC_MEMORY";

        private const string ModDataLastSelectedGroupId = "BASIC_LAST_SELECTED_GROUP_ID";
        private const string ModDataChatterEnabled = "BASIC_CHATTER_ENABLED";
        private const string ModDataChatVisualPreferences = "BASIC_CHAT_VISUAL_PREFERENCES";
        private const string ModDataChatLanguageColorsEnabled = "BASIC_CHAT_LANGUAGE_COLORS_ENABLED";
        private const string ModDataChatLanguageLabelsEnabled = "BASIC_CHAT_LANGUAGE_LABELS_ENABLED";
        private const string ModDataChatNicknameColorsEnabled = "BASIC_CHAT_NICKNAME_COLORS_ENABLED";

        public static T GetModData<T>(this IServerPlayer player, string key, T defaultValue)
        {
            try
            {
                return SerializerUtil.Deserialize(player.GetModdata(key), defaultValue);
            }
            catch (Exception e)
            {
                player.Entity.Api.Logger.Error("THEBASICS: Failed to get mod data for key " + key + " for player " + player.PlayerName + ", clearing mod data: " + e.Message);
                try
                {
                    player.SetModdata(key, null);
                }
                catch (Exception e2)
                {
                    player.Entity.Api.Logger.Error("THEBASICS: Failed to clear mod data for key " + key + " for player " + player.PlayerName + ": " + e2.Message);
                }
            }

            return defaultValue;
        }

        public static void SetModData<T>(this IServerPlayer player, string key, T value)
        {
            player.SetModdata(key, SerializerUtil.Serialize<T>(value));
        }

        /// <summary>
        /// Returns the player's active RP character id, or empty string when RP characters are disabled
        /// or no character has been selected. Mirrors the value <see cref="ModSystems.RpCharacters.RpCharacterService"/>
        /// stores under the same key.
        /// </summary>
        public static string GetActiveRpCharacterId(this IServerPlayer player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            try
            {
                return GetModData<string>(player, ModDataActiveCharacterId, string.Empty) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #region Nicknames
        public static string GetNickname(this IServerPlayer player)
        {
            return GetLegacyNickname(player) ?? player.PlayerName;
        }

        public static string GetNickname(this IServerPlayer player, ModConfig config)
        {
            var nickname = GetCharacterSheetBoundValue(player, config, CharacterSheetNicknameBind, CharacterSheetNicknameField);
            return string.IsNullOrWhiteSpace(nickname) ? GetNickname(player) : nickname;
        }

        public static string GetNicknameWithColor(this IServerPlayer player)
        {
            // Escape the nickname first to prevent VTML injection, then apply color
            var escapedNickname = VtmlUtils.EscapeVtml(GetNickname(player));
            return ChatHelper.Color(escapedNickname, GetNicknameColor(player));
        }

        public static string GetNicknameWithColor(this IServerPlayer player, ModConfig config)
        {
            // Escape the nickname first to prevent VTML injection, then apply color
            var escapedNickname = VtmlUtils.EscapeVtml(GetNickname(player, config));
            return ChatHelper.Color(escapedNickname, GetNicknameColor(player));
        }

        public static void SetNickname(this IServerPlayer player, string nickname)
        {
            SetModData(player, ModDataNickname, nickname);
        }

        public static void SetNickname(this IServerPlayer player, string nickname, ModConfig config)
        {
            if (config?.EnableCharacterSheets == true)
            {
                SetCharacterSheetBoundValue(player, config, CharacterSheetNicknameBind, CharacterSheetNicknameField, nickname);
                player.RemoveModdata(ModDataNickname);
                return;
            }

            SetNickname(player, nickname);
        }

        public static bool HasNickname(this IServerPlayer player)
        {
            return GetLegacyNickname(player) != null;
        }

        public static bool HasNickname(this IServerPlayer player, ModConfig config)
        {
            return !string.IsNullOrWhiteSpace(GetCharacterSheetBoundValue(player, config, CharacterSheetNicknameBind, CharacterSheetNicknameField)) || HasNickname(player);
        }

        public static string GetCharacterSheetFullName(this IServerPlayer player, ModConfig config)
        {
            return GetCharacterSheetBoundValue(player, config, CharacterSheetFullNameBind, CharacterSheetFullNameField);
        }

        public static void ClearNickname(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNickname);
        }

        public static void ClearNickname(this IServerPlayer player, ModConfig config)
        {
            if (config?.EnableCharacterSheets == true)
            {
                ClearCharacterSheetBoundValue(player, config, CharacterSheetNicknameBind, CharacterSheetNicknameField);
            }

            player.RemoveModdata(ModDataNickname);
        }

        private static string GetLegacyNickname(IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNickname, null);
        }

        private static string GetCharacterSheetBoundValue(IServerPlayer player, ModConfig config, string bindTo, string fallbackFieldId)
        {
            if (config?.EnableCharacterSheets != true)
            {
                return null;
            }

            var data = GetModData(player, ModDataCharacterSheet, new CharacterSheetData());
            var fieldId = GetCharacterSheetBoundFieldId(config, bindTo, fallbackFieldId);
            return data?.Fields?.FirstOrDefault(field => field.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private static void SetCharacterSheetBoundValue(IServerPlayer player, ModConfig config, string bindTo, string fallbackFieldId, string value)
        {
            var fieldId = GetCharacterSheetBoundFieldId(config, bindTo, fallbackFieldId);
            var data = GetModData(player, ModDataCharacterSheet, new CharacterSheetData()) ?? new CharacterSheetData();
            data.Fields ??= new List<CharacterSheetStoredField>();

            var field = data.Fields.FirstOrDefault(field => field.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                data.Fields.Add(new CharacterSheetStoredField { FieldId = fieldId, Value = value ?? string.Empty });
            }
            else
            {
                field.Value = value ?? string.Empty;
            }

            SetModData(player, ModDataCharacterSheet, data);
        }

        private static void ClearCharacterSheetBoundValue(IServerPlayer player, ModConfig config, string bindTo, string fallbackFieldId)
        {
            var fieldId = GetCharacterSheetBoundFieldId(config, bindTo, fallbackFieldId);
            var data = GetModData(player, ModDataCharacterSheet, new CharacterSheetData()) ?? new CharacterSheetData();
            data.Fields ??= new List<CharacterSheetStoredField>();

            for (var index = data.Fields.Count - 1; index >= 0; index--)
            {
                if (data.Fields[index].FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase))
                {
                    data.Fields.RemoveAt(index);
                }
            }

            SetModData(player, ModDataCharacterSheet, data);
        }

        private static string GetCharacterSheetBoundFieldId(ModConfig config, string bindTo, string fallbackFieldId)
        {
            var fieldId = config?.CharacterSheetFields?
                .FirstOrDefault(field => field.BindTo?.Equals(bindTo, StringComparison.OrdinalIgnoreCase) == true)
                ?.Id;

            return string.IsNullOrWhiteSpace(fieldId) ? fallbackFieldId : fieldId;
        }
        #endregion

        #region Last Selected Group ID
        public static int? GetLastSelectedGroupId(this IServerPlayer player)
        {
            return GetModData<int?>(player, ModDataLastSelectedGroupId, null);
        }

        public static void SetLastSelectedGroupId(this IServerPlayer player, int? groupId)
        {
            SetModData(player, ModDataLastSelectedGroupId, groupId);
        }
        #endregion

        #region Nickname Colors
        public static string GetNicknameColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNicknameColor, null);
        }

        public static void SetNicknameColor(this IServerPlayer player, string nickname)
        {
            SetModData(player, ModDataNicknameColor, nickname);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NicknameColorAttrKey, nickname ?? string.Empty);
        }

        public static bool HasNicknameColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNicknameColor, null) != null;
        }

        public static void ClearNicknameColor(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNicknameColor);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NicknameColorAttrKey, string.Empty);
        }

        public static string GetNametagBackgroundColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNametagBackgroundColor, null);
        }

        public static void SetNametagBackgroundColor(this IServerPlayer player, string color)
        {
            SetModData(player, ModDataNametagBackgroundColor, color);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NametagBackgroundColorAttrKey, color ?? string.Empty);
        }

        public static bool HasNametagBackgroundColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNametagBackgroundColor, null) != null;
        }

        public static void ClearNametagBackgroundColor(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNametagBackgroundColor);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NametagBackgroundColorAttrKey, string.Empty);
        }

        public static string GetNametagBorderColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNametagBorderColor, null);
        }

        public static void SetNametagBorderColor(this IServerPlayer player, string color)
        {
            SetModData(player, ModDataNametagBorderColor, color);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NametagBorderColorAttrKey, color ?? string.Empty);
        }

        public static bool HasNametagBorderColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNametagBorderColor, null) != null;
        }

        public static void ClearNametagBorderColor(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNametagBorderColor);
            SyncNametagSubAttribute(player, ModSystems.CharacterSheets.CharacterSheetSystem.NametagBorderColorAttrKey, string.Empty);
        }

        /// <summary>
        /// Writes a value into the entity's "nametag" tree-attribute and marks it dirty so vanilla's
        /// <c>EntityBehaviorNameTag.OnNameChanged</c> listener fires on the client. Used for the
        /// custom sub-attributes that drive our patched nametag renderer.
        /// </summary>
        private static void SyncNametagSubAttribute(IServerPlayer player, string subKey, string value)
        {
            if (player?.Entity?.WatchedAttributes is not { } attrs) return;
            var tree = ModSystems.CharacterSheets.CharacterSheetSystem.NametagAttrTree;
            var nametag = attrs.GetTreeAttribute(tree);
            if (nametag == null) return;
            if (nametag.GetString(subKey) == value) return;
            nametag.SetString(subKey, value);
            attrs.MarkPathDirty(tree);
        }
        #endregion

        public static ProximityChatMode GetChatMode(this IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return tempMode ?? GetModData(player, ModDataChatMode, ProximityChatMode.Normal);
        }

        public static void SetChatMode(this IServerPlayer player, ProximityChatMode mode)
        {
            SetModData(player, ModDataChatMode, mode);
        }

        public static void SetEmoteMode(this IServerPlayer player, bool emoteMode)
        {
            SetModData(player, ModDataEmoteMode, emoteMode);
        }

        public static bool GetEmoteMode(this IServerPlayer player)
        {
            return GetModData(player, ModDataEmoteMode, false);
        }

        public static void SetRpTextEnabled(this IServerPlayer player, bool enabled)
        {
            SetModData(player, ModDataRpTextEnabled, enabled);
        }

        public static bool GetRpTextEnabled(this IServerPlayer player)
        {
            return GetModData(player, ModDataRpTextEnabled, true);
        }

        #region Chat Visual Preferences
        public static ChatVisualPreferences GetChatVisualPreferences(this IServerPlayer player)
        {
            var preferences = GetModData(player, ModDataChatVisualPreferences, new ChatVisualPreferences());
            preferences ??= new ChatVisualPreferences();
            preferences.LanguageColorOverrides ??= new List<ColorOverrideEntry>();
            preferences.ColorPreset = ChatVisualPreferenceResolver.NormalizePreset(preferences.ColorPreset);
            preferences.LanguageColorsEnabled = player.GetChatLanguageColorsEnabled(preferences.LanguageColorsEnabled);
            preferences.ShowLanguageLabels = player.GetChatLanguageLabelsEnabled(preferences.ShowLanguageLabels);
            preferences.NicknameColorsEnabled = player.GetChatNicknameColorsEnabled(preferences.NicknameColorsEnabled);
            return preferences;
        }

        public static bool GetChatLanguageColorsEnabled(this IServerPlayer player, bool defaultValue = true)
        {
            return GetModData(player, ModDataChatLanguageColorsEnabled, defaultValue);
        }

        public static bool GetChatLanguageLabelsEnabled(this IServerPlayer player, bool defaultValue = false)
        {
            return GetModData(player, ModDataChatLanguageLabelsEnabled, defaultValue);
        }

        public static bool GetChatNicknameColorsEnabled(this IServerPlayer player, bool defaultValue = true)
        {
            return GetModData(player, ModDataChatNicknameColorsEnabled, defaultValue);
        }

        public static void SetChatVisualPreferences(this IServerPlayer player, ChatVisualPreferences preferences)
        {
            preferences ??= new ChatVisualPreferences();
            preferences.LanguageColorOverrides ??= new List<ColorOverrideEntry>();
            preferences.ColorPreset = ChatVisualPreferenceResolver.NormalizePreset(preferences.ColorPreset);
            SetModData(player, ModDataChatVisualPreferences, preferences);
            SetModData(player, ModDataChatLanguageColorsEnabled, preferences.LanguageColorsEnabled);
            SetModData(player, ModDataChatLanguageLabelsEnabled, preferences.ShowLanguageLabels);
            SetModData(player, ModDataChatNicknameColorsEnabled, preferences.NicknameColorsEnabled);
        }

        public static void ClearChatVisualPreferences(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataChatVisualPreferences);
            player.RemoveModdata(ModDataChatLanguageColorsEnabled);
            player.RemoveModdata(ModDataChatLanguageLabelsEnabled);
            player.RemoveModdata(ModDataChatNicknameColorsEnabled);
        }
        #endregion

        #region Player Stats
        private static string GetPlayerStatID(PlayerStatType type)
        {
            return ModDataPlayerStatsPrefix + StatTypes.Types[type].ID;
        }

        public static int GetPlayerStat(this IServerPlayer player, PlayerStatType type, int defaultValue = 0)
        {
            return GetModData(player, GetPlayerStatID(type), defaultValue);
        }

        public static void AddPlayerStat(this IServerPlayer player, PlayerStatType type, int value = 1)
        {
            AddCount(player, GetPlayerStatID(type), value);
        }

        public static void ClearPlayerStats(this IServerPlayer player)
        {
            StatTypes.Types.Keys.ToList().ForEach(type =>
                SetModData(player, GetPlayerStatID(type), 0));
        }

        public static void ClearPlayerStat(this IServerPlayer player, PlayerStatType stat)
        {
            SetModData(player, GetPlayerStatID(stat), 0);
        }

        private static void AddCount(IServerPlayer player, string key, int val = 1)
        {
            var previousCount = GetModData(player, key, 0);
            SetModData(player, key, previousCount + val);
        }
        #endregion

        #region Languages
        public static void InstantiateLanguagesIfNotExist(this IServerPlayer player, ModConfig config)
        {
            if (player.GetModdata(ModDataLanguages) == null)
            {
                SetModData(player, ModDataLanguages, config.Languages
                    .Where(lang => lang.Default)
                    .Select(lang => lang.Name)
                    .ToList());
            }
            if (player.GetModdata(ModDataDefaultLanguage) == null)
            {
                var defaultLang = config.Languages
                    .Where(lang => lang.Default)
                    .Select(lang => lang.Name)
                    .FirstOrDefault(LanguageSystem.BabbleLang.Name);
                SetModData(player, ModDataDefaultLanguage, defaultLang);
            }
        }

        public static List<string> GetLanguages(this IServerPlayer player)
        {
            return GetModData(player, ModDataLanguages, new List<string>());
        }

        public static List<string> GetLanguages(this IWorldPlayerData playerData)
        {
            return playerData?.GetModData(ModDataLanguages, new List<string>()) ?? new List<string>();
        }

        public static void SetLanguages(this IServerPlayer player, IEnumerable<string> languages)
        {
            SetModData(player, ModDataLanguages, languages.ToList());
        }

        public static void SetLanguages(this IWorldPlayerData playerData, IEnumerable<string> languages)
        {
            playerData?.SetModData(ModDataLanguages, languages?.ToList() ?? new List<string>());
        }

        public static Dictionary<string, int> GetLanguageSkills(this IServerPlayer player)
        {
            return NormalizeLanguageSkills(GetModData(player, ModDataLanguageSkills, new Dictionary<string, int>()));
        }

        public static Dictionary<string, int> GetLanguageSkills(this IWorldPlayerData playerData)
        {
            return NormalizeLanguageSkills(playerData?.GetModData(ModDataLanguageSkills, new Dictionary<string, int>()) ?? new Dictionary<string, int>());
        }

        public static void SetLanguageSkills(this IServerPlayer player, IDictionary<string, int> languageSkills)
        {
            SetModData(player, ModDataLanguageSkills, NormalizeLanguageSkills(languageSkills));
        }

        public static void SetLanguageSkills(this IWorldPlayerData playerData, IDictionary<string, int> languageSkills)
        {
            playerData?.SetModData(ModDataLanguageSkills, NormalizeLanguageSkills(languageSkills));
        }

        public static SemanticLanguageMemoryStore GetSemanticLanguageMemory(this IServerPlayer player)
        {
            return NormalizeSemanticLanguageMemory(GetModData(player, ModDataSemanticLanguageMemory, new SemanticLanguageMemoryStore()));
        }

        public static SemanticLanguageMemoryStore GetSemanticLanguageMemory(this IWorldPlayerData playerData)
        {
            return NormalizeSemanticLanguageMemory(playerData?.GetModData(ModDataSemanticLanguageMemory, new SemanticLanguageMemoryStore()) ?? new SemanticLanguageMemoryStore());
        }

        public static void SetSemanticLanguageMemory(this IServerPlayer player, SemanticLanguageMemoryStore memory)
        {
            SetModData(player, ModDataSemanticLanguageMemory, NormalizeSemanticLanguageMemory(memory));
        }

        public static void SetSemanticLanguageMemory(this IWorldPlayerData playerData, SemanticLanguageMemoryStore memory)
        {
            playerData?.SetModData(ModDataSemanticLanguageMemory, NormalizeSemanticLanguageMemory(memory));
        }

        public static void ClearSemanticLanguageMemory(this IServerPlayer player, Language lang)
        {
            if (lang == null)
            {
                return;
            }

            var memory = player.GetSemanticLanguageMemory();
            var removed = memory.Languages.RemoveAll(entry => string.Equals(entry.LanguageName, lang.Name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                player.SetSemanticLanguageMemory(memory);
            }
        }

        public static SemanticLanguageMemoryStore NormalizeSemanticLanguageMemory(SemanticLanguageMemoryStore memory, ISet<string> alreadyKnownLanguages = null)
        {
            var normalized = new SemanticLanguageMemoryStore();
            if (memory?.Languages == null)
            {
                return normalized;
            }

            var languages = new Dictionary<string, SemanticLanguageMemory>(StringComparer.OrdinalIgnoreCase);
            foreach (var languageMemory in memory.Languages)
            {
                var languageName = GetSemanticMemoryLanguageName(languageMemory, alreadyKnownLanguages);
                if (languageName == null)
                {
                    continue;
                }

                if (!languages.TryGetValue(languageName, out var target))
                {
                    target = new SemanticLanguageMemory { LanguageName = languageName };
                    languages[languageName] = target;
                }

                target.LastLearningObservationUnixSeconds = Math.Max(
                    target.LastLearningObservationUnixSeconds,
                    Math.Max(0, languageMemory.LastLearningObservationUnixSeconds));
                AddNormalizedSemanticAtlasBuckets(target, languageMemory.AtlasBuckets);
            }

            normalized.Languages = languages.Values
                .Where(languageMemory => languageMemory.AtlasBuckets.Count > 0)
                .OrderBy(languageMemory => languageMemory.LanguageName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return normalized;
        }

        private static string GetSemanticMemoryLanguageName(SemanticLanguageMemory languageMemory, ISet<string> alreadyKnownLanguages)
        {
            if (languageMemory == null || string.IsNullOrWhiteSpace(languageMemory.LanguageName))
            {
                return null;
            }

            var languageName = languageMemory.LanguageName.Trim();
            return alreadyKnownLanguages?.Contains(languageName) == true ? null : languageName;
        }

        private static void AddNormalizedSemanticAtlasBuckets(SemanticLanguageMemory target, IEnumerable<SemanticLanguageAtlasBucketCoverage> buckets)
        {
            if (buckets == null)
            {
                return;
            }

            var byId = new Dictionary<string, SemanticLanguageAtlasBucketCoverage>(StringComparer.OrdinalIgnoreCase);
            foreach (var bucket in buckets)
            {
                if (!TryCreateNormalizedSemanticAtlasBucket(bucket, out var normalizedBucket))
                {
                    continue;
                }

                if (!byId.TryGetValue(normalizedBucket.BucketId, out var existing) || normalizedBucket.Confidence > existing.Confidence)
                {
                    byId[normalizedBucket.BucketId] = normalizedBucket;
                }
            }

            target.AtlasBuckets.AddRange(byId.Values
                .OrderByDescending(bucket => bucket.Confidence)
                .ThenBy(bucket => bucket.BucketId, StringComparer.OrdinalIgnoreCase)
                .Take(4096));
        }

        private static bool TryCreateNormalizedSemanticAtlasBucket(SemanticLanguageAtlasBucketCoverage bucket, out SemanticLanguageAtlasBucketCoverage normalizedBucket)
        {
            normalizedBucket = null;
            var bucketId = SemanticLanguageAtlasCatalog.NormalizeIdentifier(bucket?.BucketId);
            if (string.IsNullOrWhiteSpace(bucketId) || bucket.Confidence <= 0)
            {
                return false;
            }

            normalizedBucket = new SemanticLanguageAtlasBucketCoverage
            {
                BucketId = bucketId,
                Confidence = Math.Max(0, Math.Min(100, bucket.Confidence)),
                ExposureCount = Math.Max(0, bucket.ExposureCount),
                LastUpdatedUnixSeconds = Math.Max(0, bucket.LastUpdatedUnixSeconds),
                LearnedAtUnixSeconds = Math.Max(0, bucket.LearnedAtUnixSeconds)
            };
            return true;
        }

        public static int GetLanguageSkill(this IServerPlayer player, Language lang)
        {
            if (lang == null)
            {
                return 0;
            }

            if (player.KnowsLanguage(lang))
            {
                return 100;
            }

            return player.GetLanguageSkills().TryGetValue(lang.Name, out var skill)
                ? ClampLanguageSkill(skill)
                : 0;
        }

        public static void SetLanguageSkill(this IServerPlayer player, Language lang, int skill)
        {
            if (lang == null)
            {
                return;
            }

            var skills = player.GetLanguageSkills();
            var clampedSkill = ClampLanguageSkill(skill);
            if (clampedSkill <= 0 || player.KnowsLanguage(lang))
            {
                skills.Remove(lang.Name);
            }
            else
            {
                skills[lang.Name] = clampedSkill;
            }

            SetModData(player, ModDataLanguageSkills, skills);
        }

        private static Dictionary<string, int> NormalizeLanguageSkills(IDictionary<string, int> languageSkills)
        {
            var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (languageSkills == null)
            {
                return normalized;
            }

            foreach (var entry in languageSkills)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                var skill = ClampLanguageSkill(entry.Value);
                if (skill > 0)
                {
                    normalized[entry.Key.Trim()] = skill;
                }
            }

            return normalized;
        }

        private static int ClampLanguageSkill(int skill)
        {
            return Math.Max(0, Math.Min(100, skill));
        }

        public static bool KnowsLanguage(this IServerPlayer player, Language lang)
        {
            return GetLanguages(player).Any(langName => langName == lang.Name);
        }

        public static void AddLanguage(this IServerPlayer player, Language lang)
        {
            var currentLanguages = player.GetLanguages().ToList();
            if (player.GetLanguages().All(curLang => curLang != lang.Name))
            {
                currentLanguages.Add(lang.Name);
            }
            SetModData(player, ModDataLanguages, currentLanguages);
            ClearLanguageSkill(player, lang);
            ClearSemanticLanguageMemory(player, lang);
        }

        public static void RemoveLanguage(this IServerPlayer player, Language lang)
        {
            var currentLanguages = player.GetLanguages().ToList();
            var newLanguages = currentLanguages.Where(curLang => lang.Name != curLang).ToList();
            SetModData(player, ModDataLanguages, newLanguages);
            ClearLanguageSkill(player, lang);
            ClearSemanticLanguageMemory(player, lang);
        }

        private static void ClearLanguageSkill(IServerPlayer player, Language lang)
        {
            var skills = player.GetLanguageSkills();
            if (skills.Remove(lang.Name))
            {
                SetModData(player, ModDataLanguageSkills, skills);
            }
        }

        public static Language GetDefaultLanguage(this IServerPlayer player, ModConfig config)
        {
            return GetLangFromName(GetModData<string>(player, ModDataDefaultLanguage, null), config, true, true);
        }

        public static string GetDefaultLanguageName(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataDefaultLanguage, null);
        }

        public static string GetDefaultLanguageName(this IWorldPlayerData playerData)
        {
            return playerData?.GetModData<string>(ModDataDefaultLanguage, null);
        }

        private static Language GetLangFromName(string langName, ModConfig config, bool allowBabble, bool allowSignLanguage)
        {
            return GetAllLanguages(config, allowBabble, allowSignLanguage).First((lang) => lang.Name == langName);
        }

        // TODO: Refactor this to use version in LanguageSystem
        private static List<Language> GetAllLanguages(ModConfig config, bool allowBabble, bool allowSignLanguage = false)
        {
            List<Language> languages = [.. config.Languages];
            if (allowBabble)
            {
                languages.Add(LanguageSystem.BabbleLang);
            }
            if (allowSignLanguage)
            {
                languages.Add(LanguageSystem.SignLanguage);
            }
            return languages;
        }

        public static void SetDefaultLanguage(this IServerPlayer player, Language lang)
        {
            SetModData(player, ModDataDefaultLanguage, lang.Name);
        }

        public static void SetDefaultLanguage(this IWorldPlayerData playerData, Language lang)
        {
            playerData?.SetModData(ModDataDefaultLanguage, lang?.Name);
        }
        #endregion


        #region TPA Requests

        public static bool CanTpa(this IServerPlayer player, IGameCalendar cal, ModConfig config)
        {
            if (!config.TpaUseCooldown)
            {
                return true;
            }

            var prevHours = GetModData(player, ModDataTpaTime, Double.MinValue);
            var curHours = cal.TotalHours;

            var diff = curHours - prevHours;

            return diff > config.TpaCooldownInGameHours;
        }

        public static void SetTpaTime(this IServerPlayer player, IGameCalendar cal)
        {
            SetModData(player, ModDataTpaTime, cal.TotalHours);
        }

        public static void SetTpAllowed(this IServerPlayer player, bool allowed)
        {
            SetModData(player, ModDataTpAllowed, allowed);
        }

        public static bool GetTpAllowed(this IServerPlayer player)
        {
            return GetModData(player, ModDataTpAllowed, true);
        }

        public static TpaRequest GetOutgoingTpaRequest(this IServerPlayer player)
        {
            var data = GetModData<string>(player, ModDataOutgoingTpaRequest, null);
            if (string.IsNullOrEmpty(data)) return null;

            try
            {
                return JsonConvert.DeserializeObject<TpaRequest>(data);
            }
            catch
            {
                // If deserialization fails, clear the corrupted data
                player.ClearOutgoingTpaRequest();
                return null;
            }
        }

        public static void SetOutgoingTpaRequest(this IServerPlayer player, TpaRequest request)
        {
            var data = request == null ? null : JsonConvert.SerializeObject(request);
            SetModData(player, ModDataOutgoingTpaRequest, data);
        }

        public static void ClearOutgoingTpaRequest(this IServerPlayer player)
        {
            SetModData<string>(player, ModDataOutgoingTpaRequest, null);
        }


        public static List<(IServerPlayer requester, TpaRequest request)> FindAllIncomingTpaRequests(this IServerPlayer targetPlayer, TpaSystem tpaSystem)
        {
            var incomingRequests = new List<(IServerPlayer, TpaRequest)>();

            // Find all online players who have outgoing requests targeting this player
            foreach (var player in tpaSystem.API.World.AllOnlinePlayers)
            {
                var serverPlayer = player as IServerPlayer;
                if (serverPlayer == null) continue;

                var outgoingRequest = serverPlayer.GetOutgoingTpaRequest();
                if (outgoingRequest != null &&
                    outgoingRequest.TargetPlayerUID == targetPlayer.PlayerUID &&
                    !tpaSystem.IsRequestExpired(outgoingRequest))
                {
                    incomingRequests.Add((serverPlayer, outgoingRequest));
                }
            }

            return incomingRequests;
        }

        public static TpaRequest FindIncomingTpaRequestFrom(this IServerPlayer targetPlayer, string requesterUID, TpaSystem tpaSystem)
        {
            // Find a specific player's outgoing request targeting this player
            var requester = tpaSystem.API.GetPlayerByUID(requesterUID);
            if (requester == null) return null;

            var outgoingRequest = requester.GetOutgoingTpaRequest();
            if (outgoingRequest != null &&
                outgoingRequest.TargetPlayerUID == targetPlayer.PlayerUID &&
                !tpaSystem.IsRequestExpired(outgoingRequest))
            {
                return outgoingRequest;
            }

            return null;
        }
        #endregion

        public static void SetOOCEnabled(this IServerPlayer player, bool enabled)
        {
            SetModData(player, ModDataOOCEnabled, enabled);
        }

        public static bool GetOOCEnabled(this IServerPlayer player)
        {
            return GetModData(player, ModDataOOCEnabled, false);
        }

        public static void SetChatterEnabled(this IServerPlayer player, bool enabled)
        {
            player.SetModdata(ModDataChatterEnabled, new[] { enabled ? (byte)1 : (byte)0 });
        }

        public static bool GetChatterEnabled(this IServerPlayer player)
        {
            var data = player.GetModdata(ModDataChatterEnabled);
            if (data == null)
            {
                return true;
            }

            // Legacy protobuf serialized bool false as an empty byte array.
            if (data.Length == 0)
            {
                return false;
            }

            if (data.Length == 1 && data[0] <= 1)
            {
                return data[0] == 1;
            }

            try
            {
                return SerializerUtil.Deserialize(data, true);
            }
            catch
            {
                player.RemoveModdata(ModDataChatterEnabled);
                return true;
            }
        }

        public static double GetDistance(this IServerPlayer sendingPlayer, IServerPlayer receivingPlayer) =>
            sendingPlayer.Entity.Pos.DistanceTo(receivingPlayer.Entity.Pos);
    }
}
