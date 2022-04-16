using thebasics.Models;
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
        
        private const string ModDataDeathCount = "BASIC_COUNT_DEATHS";
        private const string ModDataPlayerKillCount = "BASIC_COUNT_KILLS_PLAYER";
        private const string ModDataNpcKillCount = "BASIC_COUNT_KILLS_NPC";
        
        private const string ModDataLastTpa = "BASIC_LAST_TPA_PLAYER_ID";

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

        public static int GetDeathCount(this IServerPlayer player)
        {
            return GetModData(player, ModDataDeathCount, 0);
        }
        
        public static void AddDeathCount(this IServerPlayer player)
        {
            AddCount(player, ModDataDeathCount);
        }

        public static int GetNpcKillCount(this IServerPlayer player)
        {
            return GetModData(player, ModDataNpcKillCount, 0);
        }
        
        public static void AddNpcKillCount(this IServerPlayer player)
        {
            AddCount(player, ModDataNpcKillCount);
        }

        public static int GetPlayerKillCount(this IServerPlayer player)
        {
            return GetModData(player, ModDataPlayerKillCount, 0);
        }
        
        public static void AddPlayerKillCount(this IServerPlayer player)
        {
            AddCount(player, ModDataPlayerKillCount);
        }
        
        public static void ClearCounts(this IServerPlayer player)
        {
            SetModData(player, ModDataDeathCount, 0);
            SetModData(player, ModDataNpcKillCount, 0);
            SetModData(player, ModDataPlayerKillCount, 0);
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
    }
}