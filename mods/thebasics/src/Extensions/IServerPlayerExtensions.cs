using thebasics.Models;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Extensions
{
    public static class IServerPlayerExtensions
    {
        private const string MODDATA_NICKNAME = "BASIC_NICKNAME";
        private const string MODDATA_CHATMODE = "BASIC_CHATMODE";
        private const string MODDATA_EMOTEMODE = "BASIC_EMOTEMODE";
        private const string MODDATA_RPTEXTENABLED = "BASIC_RPTEXTENABLED";
        
        public static T GetModdata<T> (this IServerPlayer player, string key, T defaultValue)
        {
            return SerializerUtil.Deserialize<T>(player.GetModdata(key), defaultValue);
        }

        public static void SetModdata<T>(this IServerPlayer player, string key, T value)
        {
            player.SetModdata(key, SerializerUtil.Serialize<T>(value));
        }

        public static string GetNickname(this IServerPlayer player)
        {
            return $"<strong>{player.GetModdata(MODDATA_NICKNAME, player.PlayerName)}</strong>";
        }

        public static void SetNickname(this IServerPlayer player, string nickname)
        {
            player.SetModdata(MODDATA_NICKNAME, nickname);
        }
        
        public static bool HasNickname(this IServerPlayer player)
        {
            return player.GetModdata<string>(MODDATA_NICKNAME, null) != null;
        }
        
        public static void ClearNickname(this IServerPlayer player)
        {
            player.RemoveModdata(MODDATA_NICKNAME);
        }

        public static ProximityChatMode GetChatMode(this IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return tempMode ?? player.GetModdata(MODDATA_CHATMODE, ProximityChatMode.NORMAL);
        }

        public static void SetChatMode(this IServerPlayer player, ProximityChatMode mode)
        {
            player.SetModdata(MODDATA_CHATMODE, mode);
        }

        public static void SetEmoteMode(this IServerPlayer player, bool emoteMode)
        {
            player.SetModdata(MODDATA_EMOTEMODE, emoteMode);
        }

        public static bool GetEmoteMode(this IServerPlayer player)
        {
            return player.GetModdata(MODDATA_EMOTEMODE, false);
        }

        public static void SetRpTextEnabled(this IServerPlayer player, bool enabled)
        {
            player.SetModdata(MODDATA_RPTEXTENABLED, enabled);
        }

        public static bool GetRpTextEnabled(this IServerPlayer player)
        {
            return player.GetModdata(MODDATA_RPTEXTENABLED, true);
        }
    }
}