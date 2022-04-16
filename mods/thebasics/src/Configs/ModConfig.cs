using System.Collections.Generic;
using thebasics.Models;

namespace thebasics.Configs
{
    public class ModConfig
    {
        public IDictionary<ProximityChatMode, int> ProximityChatModeDistances = new Dictionary<ProximityChatMode, int>
        {
            {ProximityChatMode.Yell, 90},
            {ProximityChatMode.Normal, 35},
            {ProximityChatMode.Whisper, 5},
            {ProximityChatMode.Sign, 15}
        };

        public bool BoldNicknames = false;

        public IDictionary<ProximityChatMode, string[]> ProximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                {ProximityChatMode.Yell, new[] {"yells", "shouts", "exclaims"}},
                {ProximityChatMode.Normal, new[] {"says", "states", "mentions"}},
                {ProximityChatMode.Whisper, new[] {"whispers", "mumbles", "mutters"}},
                {ProximityChatMode.Sign, new[] {"signs", "gestures", "motions"}}
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModePunctuation =
            new Dictionary<ProximityChatMode, string>
            {
                {ProximityChatMode.Yell, "!"},
                {ProximityChatMode.Normal, "."},
                {ProximityChatMode.Whisper, "."},
                {ProximityChatMode.Sign, "."}
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationStart =
            new Dictionary<ProximityChatMode, string>
            {
                {ProximityChatMode.Yell, "\""},
                {ProximityChatMode.Normal, "\""},
                {ProximityChatMode.Whisper, "\""},
                {ProximityChatMode.Sign, "<i>\'"}
            };

        public IDictionary<ProximityChatMode, string> ProximityChatModeQuotationEnd =
            new Dictionary<ProximityChatMode, string>
            {
                {ProximityChatMode.Yell, "\""},
                {ProximityChatMode.Normal, "\""},
                {ProximityChatMode.Whisper, "\""},
                {ProximityChatMode.Sign, "\'</i>"}
            };

        public bool SendServerSaveAnnouncement = true;
        public bool SendServerSaveFinishedAnnouncement = false;

        public string TEXT_ServerSaveAnnouncement = "Server save has started - expect lag for a few seconds.";
        public string TEXT_ServerSaveFinished = "Server save has finished.";

        public bool PlayerStatSystem = true;
        public bool TrackPlayerDeaths = true;
        public bool TrackPlayerOnPlayerKills = true;
        public bool TrackPlayerOnNpcKills = true;
        // public bool TrackPlayerBlocksBroken = true;

        public bool AllowPlayerTpa = true;
        public double TpaCooldownInGameHours = 1;
        // public double TpaExpirationInGameHours = 1;
        // public bool LogTpaToAdminChat = true;
    }
}