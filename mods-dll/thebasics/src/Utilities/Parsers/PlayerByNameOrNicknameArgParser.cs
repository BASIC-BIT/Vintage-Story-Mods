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
    /// Extends PlayersArgParser to add nickname support while maintaining all selector functionality.
    /// </summary>
    public class PlayerByNameOrNicknameArgParser : PlayersArgParser
    {
        public PlayerByNameOrNicknameArgParser(string argName, ICoreAPI api, bool isMandatoryArg)
            : base(argName, api, isMandatoryArg)
        {
        }

        public override string GetSyntaxExplanation(string indent)
        {
            return indent + GetSyntax() + " is the name, nickname, or uid of one player, or a selector in this format: s[] for self, o[] for online players, a[] for all players. Some filters can be specified inside the brackets.";
        }

        // TODO: Add some sort of quoted word parser as a sub-parser, that's used here and elsewhere?
        public override EnumParseResult TryProcess(TextCommandCallingArgs args, Action<AsyncParseResults> onReady = null)
        {
            // First try the standard PlayersArgParser parsing
            var result = base.TryProcess(args, onReady);
            
            // If the result is good, or we're doing async processing, just return
            if (result == EnumParseResult.Good || result == EnumParseResult.Deferred)
            {
                return result;
            }

            // If the standard parsing failed, try to find by nickname
            var text = args.RawArgs.PopWord();
            
            if (text == null || text == "")
            {
                lastErrorMessage = "Missing player name, nickname, or selector";
                return EnumParseResult.Bad;
            }

            // If the nickname is quoted, consume all words until we hit the closing quote
            if(text.StartsWith("\""))
            {
                text = text[1..];
                while(!text.EndsWith("\""))
                {
                    if(args.RawArgs.Length == 0)
                    {
                        lastErrorMessage = "Missing closing quote";
                        return EnumParseResult.Bad;
                    }
                    text += args.RawArgs.PopWord();
                }
                // Remove the closing quote
                text = text[..^1];
            }

            // Check if any online player has this nickname
            List<PlayerUidName> matchedPlayers = new List<PlayerUidName>();
            
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                var nickname = player.GetNickname();
                if (nickname.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPlayers.Add(new PlayerUidName(player.PlayerUID, player.PlayerName));
                }
            }

            // Also check offline players if we haven't found a match yet
            // if (matchedPlayers.Count == 0)
            // {
            //     foreach (IServerPlayerData playerData in api.PlayerData.PlayerDataByUid.Values)
            //     {
            //         var serverPlayer = api.World.PlayerByUid(playerData.PlayerUID) as IServerPlayer;
                    
            //         // Skip if the player is already online (we already checked online players)
            //         if (serverPlayer != null)
            //         {
            //             continue;
            //         }
                    
            //         if (serverPlayer.HasNickname())
            //         {
            //             // Check if this nickname matches
            //             try 
            //             {
            //                 var playerNickname = serverPlayer.GetNickname();
            //                 if (playerNickname.Equals(text, StringComparison.OrdinalIgnoreCase))
            //                 {
            //                     matchedPlayers.Add(new PlayerUidName(playerData.PlayerUID, playerData.LastKnownPlayername));
            //                 }
            //             }
            //             catch (Exception)
            //             {
            //                 // Ignore deserialization errors
            //             }
            //         }
            //     }
            // }
            
            // If we found players matching the nickname, use them
            if (matchedPlayers.Count > 0)
            {
                SetValue(matchedPlayers.ToArray());
                return EnumParseResult.Good;
            }
            
            // If we get here, no player with the given name or nickname was found
            lastErrorMessage = Lang.Get("No player with name, nickname, or uid '{0}' exists", text);
            return EnumParseResult.Bad;
        }
    }
} 