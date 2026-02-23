using System;
using System.Collections.Generic;
using System.Linq;
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

        public PlayerByNameOrNicknameArgParser(string argName, ICoreAPI api, bool isMandatoryArg)
            : base(argName, isMandatoryArg)
        {
            this.api = api as ICoreServerAPI;
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
            var text = args.RawArgs.PopWord();
            
            if (string.IsNullOrEmpty(text))
            {
                lastErrorMessage = Lang.Get("thebasics:parser-error-missing-player");
                
                if (onReady != null)
                {
                    onReady(new AsyncParseResults { Status = EnumParseResultStatus.Error });
                    return EnumParseResult.Deferred;
                }
                
                return EnumParseResult.Bad;
            }
            
            // Process quoted text if present
            if (text.StartsWith("\""))
            {
                text = text[1..]; // Remove opening quote
                
                // If it's already a complete quoted string (e.g. "nickname")
                if (text.EndsWith("\""))
                {
                    text = text[..^1]; // Remove closing quote
                }
                else
                {
                    // Keep consuming words until we find the closing quote
                    while (!text.EndsWith("\"") && args.RawArgs.Length > 0)
                    {
                        text += " " + args.RawArgs.PopWord();
                    }
                    
                    // If we found a closing quote, remove it
                    if (text.EndsWith("\""))
                    {
                        text = text[..^1]; // Remove closing quote
                    }
                    else
                    {
                        lastErrorMessage = Lang.Get("thebasics:parser-error-missing-quote");
                        
                        if (onReady != null)
                        {
                            onReady(new AsyncParseResults { Status = EnumParseResultStatus.Error });
                            return EnumParseResult.Deferred;
                        }
                        
                        return EnumParseResult.Bad;
                    }
                }
            }

            text = text.Trim();

            // First, check if any online player matches by name or UID
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.PlayerName.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                    player.PlayerUID.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    players = new PlayerUidName[] { new PlayerUidName(player.PlayerUID, player.PlayerName) };
                    
                    if (onReady != null)
                    {
                        onReady(new AsyncParseResults
                        {
                            Status = EnumParseResultStatus.Ready,
                            Data = players
                        });
                        return EnumParseResult.Deferred;
                    }
                    
                    return EnumParseResult.Good;
                }
            }
            
            // If no match by name, check by nickname
            var matchedPlayers = new List<PlayerUidName>();
            
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                var playerNickname = player.GetNickname();
                if (playerNickname != null && playerNickname.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPlayers.Add(new PlayerUidName(player.PlayerUID, player.PlayerName));
                }
            }
            
            // If we found players matching the nickname, use them
            if (matchedPlayers.Count > 0)
            {
                players = matchedPlayers.ToArray();
                
                if (onReady != null)
                {
                    onReady(new AsyncParseResults
                    {
                        Status = EnumParseResultStatus.Ready,
                        Data = players
                    });
                    return EnumParseResult.Deferred;
                }
                
                return EnumParseResult.Good;
            }
            
            // If we get here, no player with the given name or nickname was found
            lastErrorMessage = Lang.Get("thebasics:parser-error-player-not-found", text);
            
            if (onReady != null)
            {
                onReady(new AsyncParseResults { Status = EnumParseResultStatus.Error });
                return EnumParseResult.Deferred;
            }
            
            return EnumParseResult.Bad;
        }
    }
} 