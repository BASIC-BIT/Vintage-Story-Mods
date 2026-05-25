using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.Utilities.Parsers
{
    /// <summary>
    /// Argument parser that allows selecting players by either their player name or nickname.
    /// Focuses only on currently online players.
    /// </summary>
    public class PlayerByNameOrNicknameArgParser : ArgumentParserBase
    {
        protected ICoreServerAPI api;
        private PlayerUidName[] players;
        private readonly ModConfig config;

        public PlayerByNameOrNicknameArgParser(string argName, ICoreAPI api, bool isMandatoryArg)
            : this(argName, api, isMandatoryArg, null)
        {
        }

        public PlayerByNameOrNicknameArgParser(string argName, ICoreAPI api, bool isMandatoryArg, ModConfig config)
            : base(argName, isMandatoryArg)
        {
            this.api = api as ICoreServerAPI;
            this.config = config;
            if (api.Side != EnumAppSide.Server)
            {
                throw new InvalidOperationException("PlayerByNameOrNicknameArgParser is only available server side");
            }
        }

        public override string GetSyntaxExplanation(string indent)
        {
            return Lang.Get("thebasics:parser-syntax-explanation", indent + GetSyntax());
        }

        public override object GetValue()
        {
            return players;
        }

        public override void SetValue(object data)
        {
            players = (PlayerUidName[])data;
        }

        public override EnumParseResult TryProcess(TextCommandCallingArgs args, Action<AsyncParseResults> onReady = null)
        {
            if (!TryReadTargetText(args, out var text, out var errorMessage))
            {
                lastErrorMessage = errorMessage;
                return CompleteError(onReady);
            }

            text = text.Trim();

            // First, check if any online player matches by name or UID
            var exactMatch = FindExactPlayer(text);

            if (exactMatch != null)
            {
                players = new PlayerUidName[] { new PlayerUidName(exactMatch.PlayerUID, exactMatch.PlayerName) };
                return CompleteReady(onReady, players);
            }

            // If no match by name, check by nickname
            var matchedPlayers = FindPlayersByNickname(text);

            // If we found players matching the nickname, use them
            if (matchedPlayers.Count > 0)
            {
                players = matchedPlayers.ToArray();
                return CompleteReady(onReady, players);
            }

            // If we get here, no player with the given name or nickname was found
            lastErrorMessage = Lang.Get("thebasics:parser-error-player-not-found", text);
            return CompleteError(onReady);
        }

        private static bool TryReadTargetText(TextCommandCallingArgs args, out string text, out string errorMessage)
        {
            text = args.RawArgs.PopWord();
            errorMessage = null;

            if (string.IsNullOrEmpty(text))
            {
                errorMessage = Lang.Get("thebasics:parser-error-missing-player");
                return false;
            }

            if (!text.StartsWith('"'))
            {
                return true;
            }

            text = text[1..]; // Remove opening quote
            if (text.EndsWith('"'))
            {
                text = text[..^1]; // Remove closing quote
                return true;
            }

            var quotedText = new StringBuilder(text);
            while (!quotedText.ToString().EndsWith('"') && args.RawArgs.Length > 0)
            {
                quotedText.Append(' ').Append(args.RawArgs.PopWord());
            }

            text = quotedText.ToString();
            if (text.EndsWith('"'))
            {
                text = text[..^1]; // Remove closing quote
                return true;
            }

            errorMessage = Lang.Get("thebasics:parser-error-missing-quote");
            return false;
        }

        private IServerPlayer FindExactPlayer(string text)
        {
            return api.World.AllOnlinePlayers
                .OfType<IServerPlayer>()
                .FirstOrDefault(player => player.PlayerName.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                                          player.PlayerUID.Equals(text, StringComparison.OrdinalIgnoreCase));
        }

        private List<PlayerUidName> FindPlayersByNickname(string text)
        {
            var matchedPlayers = new List<PlayerUidName>();

            foreach (IServerPlayer player in api.World.AllOnlinePlayers.OfType<IServerPlayer>())
            {
                var playerNickname = config == null ? player.GetNickname() : player.GetNickname(config);
                var hasNickname = config == null ? player.HasNickname() : player.HasNickname(config);
                if (hasNickname && playerNickname != null && playerNickname.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPlayers.Add(new PlayerUidName(player.PlayerUID, player.PlayerName));
                }
            }

            return matchedPlayers;
        }

        private static EnumParseResult CompleteReady(Action<AsyncParseResults> onReady, PlayerUidName[] players = null)
        {
            if (onReady == null)
            {
                return EnumParseResult.Good;
            }

            onReady(new AsyncParseResults
            {
                Status = EnumParseResultStatus.Ready,
                Data = players
            });
            return EnumParseResult.Deferred;
        }

        private static EnumParseResult CompleteError(Action<AsyncParseResults> onReady)
        {
            if (onReady == null)
            {
                return EnumParseResult.Bad;
            }

            onReady(new AsyncParseResults { Status = EnumParseResultStatus.Error });
            return EnumParseResult.Deferred;
        }
    }
}
