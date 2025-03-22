using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
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
        private const string ModDataNicknameColor = "BASIC_NICKNAME_COLOR";
        private const string ModDataChatMode = "BASIC_CHATMODE";
        private const string ModDataEmoteMode = "BASIC_EMOTEMODE";
        private const string ModDataRpTextEnabled = "BASIC_RPTEXTENABLED";
        private const string ModDataOOCEnabled = "BASIC_OOCENABLED";
        private const string ModDataCharacterSheet = "BASIC_CHARACTER_SHEET";

        private const string ModDataPlayerStatsPrefix = "BASIC_COUNT_";

        private const string ModDataTpaTime = "BASIC_TPA_TIME";
        private const string ModDataTpAllowed = "BASIC_TPA_ALLOWED";
        private const string ModDataTpaRequests = "BASIC_TPA_REQUESTS";


        private const string ModDataLanguages = "BASIC_LANGUAGES";
        private const string ModDataDefaultLanguage = "BASIC_DEFAULT_LANGUAGE";

        private const string TpaPrivilege = "tpa";

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

        #region Nicknames
        public static string GetNickname(this IServerPlayer player)
        {
            return GetModData(player, ModDataNickname, player.PlayerName);
        }
        public static string GetNicknameWithColor(this IServerPlayer player)
        {
            return ChatHelper.Color(GetNickname(player), GetNicknameColor(player));
        }

        public static string GetFormattedName(this IServerPlayer player, bool isIC, ModConfig config)
        {
            string name = isIC ? player.GetNickname() : player.PlayerName;
            
            // Only apply color if configured for this name type
            bool applyColor = isIC ? config.ApplyColorsToNicknames : config.ApplyColorsToPlayerNames;
            if (!applyColor)
                return name;
                
            string color = player.GetNicknameColor();
            return string.IsNullOrEmpty(color) ? name : ChatHelper.Color(name, color);
        }

        public static void SetNickname(this IServerPlayer player, string nickname)
        {
            SetModData(player, ModDataNickname, nickname);
        }

        public static bool HasNickname(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNickname, null) != null;
        }

        public static void ClearNickname(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNickname);
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
        }

        public static bool HasNicknameColor(this IServerPlayer player)
        {
            return GetModData<string>(player, ModDataNicknameColor, null) != null;
        }

        public static void ClearNicknameColor(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNicknameColor);
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
            if(player.GetModdata(ModDataLanguages) == null)
            {
                SetModData(player, ModDataLanguages, config.Languages
                    .Where(lang => lang.Default)
                    .Select(lang => lang.Name)
                    .ToList());
            }
            if(player.GetModdata(ModDataDefaultLanguage) == null)
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
        }
        
        public static void RemoveLanguage(this IServerPlayer player, Language lang)
        {
            var currentLanguages = player.GetLanguages().ToList();
            var newLanguages = currentLanguages.Where(curLang => lang.Name != curLang).ToList();
            SetModData(player, ModDataLanguages, newLanguages);
        }

        public static Language GetDefaultLanguage(this IServerPlayer player, ModConfig config)
        {
            return GetLangFromName(GetModData<string>(player, ModDataDefaultLanguage, null), config, true);
        }

        private static Language GetLangFromName(string langName, ModConfig config, bool allowBabble)
        {
            return GetAllLanguages(config, allowBabble).First((lang) => lang.Name == langName);
        }

        // TODO: Refactor this to use version in LanguageSystem
        private static List<Language> GetAllLanguages(ModConfig config, bool allowBabble)
        {
            List<Language> languages = new();
            languages.AddRange(config.Languages);
            if (allowBabble)
            {
                languages.Add(LanguageSystem.BabbleLang);
            }
            return languages;
        }
        
        public static void SetDefaultLanguage(this IServerPlayer player, Language lang)
        {
            SetModData(player, ModDataDefaultLanguage, lang.Name);
        }
        #endregion


        #region TPA Requests
        private static string SerializeTpaRequests(List<TpaRequest> requests)
        {
            return JsonConvert.SerializeObject(requests);
        }

        private static List<TpaRequest> DeserializeTpaRequests(string data)
        {
            return JsonConvert.DeserializeObject<List<TpaRequest>>(data);
        }

        private static string GetDefaultSerializedTpaRequests()
        {
            return SerializeTpaRequests(new List<TpaRequest>());
        }
        
        public static List<TpaRequest> GetTpaRequests(this IServerPlayer player)
        {
            return DeserializeTpaRequests(GetModData(player, ModDataTpaRequests, GetDefaultSerializedTpaRequests()));
        }

        public static void ClearTpaRequests(this IServerPlayer player)
        {
            SetModData<string>(player, ModDataTpaRequests, null);
        }

        public static void AddTpaRequest(this IServerPlayer player, TpaRequest request)
        {
            var currentRequests = player.GetTpaRequests().ToList();
            currentRequests.Add(request);
            SetModData(player, ModDataTpaRequests, SerializeTpaRequests(currentRequests));
        }

        public static void RemoveTpaRequest(this IServerPlayer player, TpaRequest request)
        {
            var currentRequests = player.GetTpaRequests().ToList();
            currentRequests.Remove(request);
            SetModData(player, ModDataTpaRequests, SerializeTpaRequests(currentRequests));
        }

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
        #endregion

        public static void SetOOCEnabled(this IServerPlayer player, bool enabled)
        {
            SetModData(player, ModDataOOCEnabled, enabled);
        }

        public static bool GetOOCEnabled(this IServerPlayer player)
        {
            return GetModData(player, ModDataOOCEnabled, false);
        }

        #region Character Sheet
        public static CharacterSheetModel GetCharacterSheet(this IServerPlayer player)
        {
            return GetModData(player, ModDataCharacterSheet, new CharacterSheetModel());
        }

        public static void SetCharacterSheet(this IServerPlayer player, CharacterSheetModel sheet)
        {
            SetModData(player, ModDataCharacterSheet, sheet);
        }

        public static void ClearCharacterSheet(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataCharacterSheet);
        }
        #endregion
    }
}