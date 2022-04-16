using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Extensions
{
    public static class IServerPlayerExtensions
    {
        private const string ModDataNickname = "BASIC_NICKNAME";
        private const string ModDataChatMode = "BASIC_CHATMODE";
        private const string ModDataEmoteMode = "BASIC_EMOTEMODE";
        private const string ModDataRpTextEnabled = "BASIC_RPTEXTENABLED";

        private const string ModDataPlayerStatsPrefix = "BASIC_COUNT_";
        
        private const string ModDataLastTpa = "BASIC_LAST_TPA_PLAYER_ID";
        private const string ModDataTpaTime = "BASIC_TPA_TIME";
        private const string ModDataTpAllowed = "BASIC_TPA_ALLOWED";

        public static T GetModData<T>(this IServerPlayer player, string key, T defaultValue)
        {
            return SerializerUtil.Deserialize(player.GetModdata(key), defaultValue);
        }

        public static void SetModData<T>(this IServerPlayer player, string key, T value)
        {
            player.SetModdata(key, SerializerUtil.Serialize<T>(value));
        }

        public static string GetNickname(this IServerPlayer player)
        {
            return GetModData(player, ModDataNickname, player.PlayerName);
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
        
        private static string GetPlayerStatID(PlayerStatType type)
        {
            return ModDataPlayerStatsPrefix + StatTypes.Types[type].ID;
        }

        public static int GetPlayerStat(this IServerPlayer player, PlayerStatType type, int defaultValue = 0)
        {
            return GetModData(player, GetPlayerStatID(type), defaultValue);
        }
        
        public static void AddPlayerStat(this IServerPlayer player, PlayerStatType type)
        {
            AddCount(player, GetPlayerStatID(type));
        }

        public static void ClearPlayerStats(this IServerPlayer player)
        {
            StatTypes.Types.Keys.ToList().ForEach(type =>
                SetModData(player, GetPlayerStatID(type), 0));
        }

        private static void AddCount(IServerPlayer player, string key)
        {
            var previousCount = GetModData(player, key, 0);
            SetModData(player, key, previousCount + 1);
        }

        public static IServerPlayer GetLastTpa(this IServerPlayer player)
        {
            var uid = GetModData<string>(player, ModDataLastTpa, null);

            if (uid == null)
            {
                return null;
            }

            return player.Entity.Api.World.PlayerByUid(uid) as IServerPlayer;
        }

        public static void SetLastTpa(this IServerPlayer player, IServerPlayer requestPlayer)
        {
            SetModData(player, ModDataLastTpa, requestPlayer.PlayerUID);
        }

        public static void ClearLastTpa(this IServerPlayer player)
        {
            SetModData<string>(player, ModDataLastTpa, null);
        }

        public static bool CanTpa(this IServerPlayer player, IGameCalendar cal, ModConfig config)
        {
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
    }
}