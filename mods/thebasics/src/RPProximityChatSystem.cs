using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics
{
    public class ProximityChatSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private ModConfig config;
        private const string PROXIMITYGROUPNAME = "Proximity";
        private const string CONFIGNAME = "the_basics.json";
        private IDictionary<ProximityChatMode, int> proximityChatModeDistances;

        private IDictionary<ProximityChatMode, string[]> proximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.YELL, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.NORMAL, new[] { "says" } },
                { ProximityChatMode.WHISPER, new[] { "whispers" } }
            };
        private IDictionary<ProximityChatMode, string> proximityChatModePunctuation = 
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.YELL, "!" },
                { ProximityChatMode.NORMAL, "." },
                { ProximityChatMode.WHISPER, "." }
            };

        private PlayerGroup proximityGroup = null;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            try
            {
                this.config = api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception e)
            {
                api.Server.LogError("proximitychat: Failed to load mod config!");
                return;
            }

            if (this.config == null)
            {
                api.Server.LogNotification($"proximitychat: non-existant modconfig at 'ModConfig/{CONFIGNAME}', creating default...");
                this.config = new ModConfig();
                api.StoreModConfig(this.config, CONFIGNAME);
            }
            else if (this.config.ProximityChatNormalBlockRange <= 0 || this.config.ProximityChatWhisperBlockRange <= 0 || this.config.ProximityChatYellBlockRange <= 0)
            {
                api.Server.LogError($"proximitychat: invalid modconfig at 'ModConfig/{CONFIGNAME}'!");
                return;
            }

            sapi.Event.PlayerChat += Event_PlayerChat;
            sapi.Event.PlayerJoin += Event_PlayerJoin;

            sapi.RegisterCommand("pmessage", "Sends a message to all players in a specific area", null, OnPMessageHandler, Privilege.announce);
            api.RegisterCommand("nick", "Get or set your nickname", "", SetNickname);
            api.RegisterCommand("setnick", "Get or set your nickname", "", SetNickname);
            api.RegisterCommand("nickname", "Get or set your nickname", "", SetNickname);
            api.RegisterCommand("clearnick", "Clear your nickname", "", ClearNickname);
            api.RegisterCommand("me", "Send a proximity emote message", "", Emote);
            api.RegisterCommand("m", "Send a proximity emote message", "", Emote);
            api.RegisterCommand("it", "Send a proximity environment message", "", EnvironmentMessage);
            api.RegisterCommand("yell", "Set your chat mode to Yelling, or yell a single message", "", Yell);
            api.RegisterCommand("y", "Set your chat mode to Yelling, or yell a single message", "", Yell);
            api.RegisterCommand("whisper", "Set your chat mode to Whispering, or whisper a single message", "", Whisper);
            api.RegisterCommand("w", "Set your chat mode to Whispering, or whisper a single message", "", Whisper);
            api.RegisterCommand("say", "Set your chat mode back to normal, or say a single message", "", Say);
            api.RegisterCommand("normal", "Set your chat mode back to normal, or say a single message", "", Say);
            api.RegisterCommand("s", "Set your chat mode back to normal, or say a single message", "", Say);
            api.RegisterCommand("emotemode", "Turn Emote-only mode on or off", "/emotemode [on|off]", EmoteMode);
            api.RegisterCommand("rptext", "Turn the whole RP system on or off for your messages", "/rptext [on|off]", RpTextEnabled);
            
            this.proximityGroup = sapi.Groups.GetPlayerGroupByName(PROXIMITYGROUPNAME);
            if (this.proximityGroup == null)
            {
                this.proximityGroup = new PlayerGroup()
                {
                    Name = PROXIMITYGROUPNAME,
                    OwnerUID = null
                };
                sapi.Groups.AddPlayerGroup(this.proximityGroup);
                this.proximityGroup.Md5Identifier = GameMath.Md5Hash(this.proximityGroup.Uid.ToString() + "null");
            }

            this.proximityChatModeDistances = new Dictionary<ProximityChatMode, int>
            {
                { ProximityChatMode.YELL, config.ProximityChatYellBlockRange },
                { ProximityChatMode.NORMAL, config.ProximityChatNormalBlockRange },
                { ProximityChatMode.WHISPER, config.ProximityChatWhisperBlockRange }
            };
        }

        private void OnPMessageHandler(IServerPlayer player, int groupId, CmdArgs args)
        {
            Vec3d spawnpos = sapi.World.DefaultSpawnPosition.XYZ;
            spawnpos.Y = 0;
            Vec3d targetpos = null;
            if( player.Entity == null )
            {
                targetpos = args.PopFlexiblePos(spawnpos, spawnpos);
            }
            else
            {
                targetpos = args.PopFlexiblePos(player.Entity.Pos.XYZ, spawnpos);
            }

            if (targetpos == null)
            {
                player.SendMessage(groupId, @"Invalid position supplied. Syntax: [coord] [coord] [coord] whereas
                                             [coord] may be ~[decimal] or =[decimal] or [decimal]. 
                                             ~ denotes a position relative to the player 
                                             = denotes an absolute position 
                                             no prefix denotes a position relative to the map middle", EnumChatType.CommandError);
                return;
            }

            var blockRadius = args.PopInt();
            if (!blockRadius.HasValue)
            {
                player.SendMessage(groupId, "Invalid radius supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius]", EnumChatType.CommandError);
                return;
            }

            var message = args.PopAll();
            if(string.IsNullOrEmpty(message))
            {
                player.SendMessage(groupId, "Invalid message supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius] [message]", EnumChatType.CommandError);
                return;
            }

            foreach (var nearbyPlayerData in this.sapi.World.AllOnlinePlayers.Select(x => new { Position = x.Entity.ServerPos, Player = (IServerPlayer) x }).Where(x => Math.Abs( x.Position.DistanceTo(targetpos) ) < blockRadius))
            {
                nearbyPlayerData.Player.SendMessage(this.proximityGroup.Uid, message, EnumChatType.CommandSuccess);
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            var proximityGroup = sapi.Groups.GetPlayerGroupByName(PROXIMITYGROUPNAME);
            var playerProximityGroup = byPlayer.GetGroup(proximityGroup.Uid);
            if (playerProximityGroup == null)
            {
                var newMembership = new PlayerGroupMembership()
                {
                    GroupName = proximityGroup.Name,
                    GroupUid = proximityGroup.Uid,
                    Level = EnumPlayerGroupMemberShip.Member
                };
                byPlayer.ServerData.PlayerGroupMemberShips.Add(proximityGroup.Uid, newMembership);
                proximityGroup.OnlinePlayers.Add(byPlayer);
            }
        }

        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data, Vintagestory.API.Datastructures.BoolRef consumed)
        {
            var proximityGroup = sapi.Groups.GetPlayerGroupByName(PROXIMITYGROUPNAME);
            if (proximityGroup.Uid == channelId)
            {
                if (byPlayer.GetRpTextEnabled())
                {
                    if (byPlayer.HasNickname())
                    {
                        var content = getMessage(message);
                        var isEmote = content[0] == '*';
                        var isOOC = content[0] == '(';
                        if (isEmote)
                        {
                            content = content.Remove(0, 1);
                            message = GetFullEmoteMessage(byPlayer, content);
                        }
                        else if (isOOC)
                        {
                        }
                        else
                        {
                            message = GetFullRPMessage(byPlayer, content);
                        }

                        SendLocalChat(byPlayer, message, data: data);
                    }
                    else
                    {
                        byPlayer.SendMessage(channelId, $"You need a nickname to use proximity chat!  You can set it with `/nick MyName`", EnumChatType.CommandError);
                    }
                }
                else
                {
                    SendLocalChat(byPlayer, message, data: data);
                }

                consumed.value = true;
            }
        }

        private string GetFullRPMessage(IServerPlayer player, string content, ProximityChatMode? tempMode = null)
        {
            if (player.GetEmoteMode())
            {
                return GetFullEmoteMessage(player, content, tempMode);
            }
            return $"{player.GetNickname()}{GetProximityChatVerb(player, tempMode)} \"{GetRPMessage(player, content, tempMode)}\"";
        }

        private string GetFullEmoteMessage(IServerPlayer player, string content, ProximityChatMode? tempMode = null)
        {
            return $"{player.GetNickname()}{GetEmoteMessage(content)}";
        }
        private void SendLocalChat(IServerPlayer byPlayer, string message, ProximityChatMode? tempMode = null, EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = sapi.Groups.GetPlayerGroupByName(PROXIMITYGROUPNAME);
            foreach (var player in this.sapi.World.AllOnlinePlayers.Where(x => x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) < GetProximityChatRange(byPlayer, tempMode)))
            {
                var serverPlayer = player as IServerPlayer;
                
                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }

        private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return proximityChatModeDistances[player.GetChatMode(tempMode)];
        }

        private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return proximityChatModeVerbs[player.GetChatMode(tempMode)].GetRandomElement();
        }
        private string GetProximityChatPunctuation(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return proximityChatModePunctuation[player.GetChatMode(tempMode)];
        }

        private string GetRPMessage(IServerPlayer player, string message, ProximityChatMode? tempMode = null)
        {
            message = message.Substring(0, 1).ToUpper() + message.Substring(1, message.Length - 1);

            var lastCharacter = message[message.Length - 1];

            if (!isPunctuation(lastCharacter))
            {
                message = message + GetProximityChatPunctuation(player, tempMode);
            }

            return message;
        }

        private string GetEmoteMessage(string message)
        {
            var lastCharacter = message[message.Length - 1];

            if (!isPunctuation(lastCharacter))
            {
                message = message + ".";
            }

            return message;
        }

        private bool isPunctuation(char character)
        {
            return character == '.' || character == '!' || character == '?' || character == '~' || character == '-' || character == ';' || character == ':' || character == '/' || character == ',' || character == '"' || character == '\'';
        }
        
        private string getMessage(string message)
        {
            var foundText = new Regex(@".*?> (.+)$").Match(message);
            
            return foundText.Groups[1].Value;
        }

        private void SetNickname(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length == 0)
            {
                if (player.HasNickname())
                {
                    player.SendMessage(groupId, $"Your nickname is: {player.GetNickname()}", EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(groupId, $"You don't have a nickname!  You can set it with `/nick MyName`", EnumChatType.Notification);
                }
            }
            else
            {
                var nickname = args.PopAll();
                player.SetNickname(nickname);
                player.SendMessage(groupId, $"Okay, your nickname is set to \"{nickname}\".", EnumChatType.Notification);
            }
        }

        private void Emote(IServerPlayer byPlayer, int groupId, CmdArgs args)
        {
            if (byPlayer.HasNickname())
            {
                SendLocalChat(byPlayer, GetFullEmoteMessage(byPlayer, args.PopAll()));
            }
            else
            {
                byPlayer.SendMessage(groupId, $"You need a nickname to use emotes!  You can set it with `/nick MyName`", EnumChatType.CommandError);
            }
        }

        private void EnvironmentMessage(IServerPlayer byPlayer, int groupId, CmdArgs args)
        {
            SendLocalChat(byPlayer, $"*{GetRPMessage(byPlayer, args.PopAll(), ProximityChatMode.NORMAL)}*", chatType: EnumChatType.Notification);
        }
        
        private void ClearNickname(IServerPlayer player, int groupId, CmdArgs args)
        {
            player.ClearNickname();
            player.SendMessage(groupId, "Your nickname has been cleared.", EnumChatType.Notification);
        }
        private void Yell(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.YELL), ProximityChatMode.YELL);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.YELL);
                player.SendMessage(groupId, "You are now yelling.", EnumChatType.Notification);
            }
        }
        private void Whisper(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.WHISPER), ProximityChatMode.WHISPER);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.WHISPER);
                player.SendMessage(groupId, "You are now whispering.", EnumChatType.Notification);
            }
        }
        private void Say(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.NORMAL), ProximityChatMode.NORMAL);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.NORMAL);
                player.SendMessage(groupId, "You are now talking normally.", EnumChatType.Notification);
            }
        }
        private void EmoteMode(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length != 1)
            {
                player.SendMessage(groupId,"Usage: /emotemode [on|off]", EnumChatType.CommandError);
                return;
            }

            var value = args[0].ToLower();
            
            if (value != "on" && value != "off")
            {
                player.SendMessage(groupId,"Usage: /emotemode [on|off]", EnumChatType.CommandError);
                return;
            }

            var emoteMode = value == "on";
            
            player.SetEmoteMode(emoteMode);
            player.SendMessage(groupId, $"Emote mode is now {value}.", EnumChatType.Notification);
        }

        private void RpTextEnabled(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length != 1)
            {
                player.SendMessage(groupId,"Usage: /rptext [on|off]", EnumChatType.CommandError);
                return;
            }

            var value = args[0].ToLower();
            
            if (value != "on" && value != "off")
            {
                player.SendMessage(groupId,"Usage: /rptext [on|off]", EnumChatType.CommandError);
                return;
            }

            var rpTextEnabled = value == "on";
            
            player.SetRpTextEnabled(rpTextEnabled);
            player.SendMessage(groupId, $"RP Text is now {value} for your messages.", EnumChatType.Notification);
        }
    }
}
