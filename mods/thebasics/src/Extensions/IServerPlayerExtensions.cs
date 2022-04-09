using thebasics.Models;
using thebasics.Utilities;
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
            return ChatHelper.Strong(player.GetModdata(ModDataNickname, player.PlayerName));
        }

        public static void SetNickname(this IServerPlayer player, string nickname)
        {
            player.SetModdata(ModDataNickname, nickname);
        }
        
        public static bool HasNickname(this IServerPlayer player)
        {
            return player.GetModdata<string>(ModDataNickname, null) != null;
        }
        
        public static void ClearNickname(this IServerPlayer player)
        {
            player.RemoveModdata(ModDataNickname);
        }

        public static ProximityChatMode GetChatMode(this IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return tempMode ?? player.GetModdata(ModDataChatMode, ProximityChatMode.Normal);
        }

        public static void SetChatMode(this IServerPlayer player, ProximityChatMode mode)
        {
            player.SetModdata(ModDataChatMode, mode);
        }

        public static void SetEmoteMode(this IServerPlayer player, bool emoteMode)
        {
            player.SetModdata(ModDataEmoteMode, emoteMode);
        }

        public static bool GetEmoteMode(this IServerPlayer player)
        {
            return player.GetModdata(ModDataEmoteMode, false);
        }

        public static void SetRpTextEnabled(this IServerPlayer player, bool enabled)
        {
            player.SetModdata(ModDataRpTextEnabled, enabled);
        }

        public static bool GetRpTextEnabled(this IServerPlayer player)
        {
            return player.GetModdata(ModDataRpTextEnabled, true);
        }
    }
}