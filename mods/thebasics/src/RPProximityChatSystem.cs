using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics
{
    public class ProximityChatSystem : ModSystem
    {
        private ICoreServerAPI _sapi;
        private ModConfig _config;
        private const string ProximityGroupName = "Proximity";
        private const string ConfigName = "the_basics.json";
        private IDictionary<ProximityChatMode, int> _proximityChatModeDistances;

        private readonly IDictionary<ProximityChatMode, string[]> _proximityChatModeVerbs =
            new Dictionary<ProximityChatMode, string[]>
            {
                { ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" } },
                { ProximityChatMode.Normal, new[] { "says", "states", "mentions" } },
                { ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" } },
                { ProximityChatMode.Sign, new[] { "signs", "gestures", "motions" } }
            };
        private readonly IDictionary<ProximityChatMode, string> _proximityChatModePunctuation = 
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "!" },
                { ProximityChatMode.Normal, "." },
                { ProximityChatMode.Whisper, "." },
                { ProximityChatMode.Sign, "." }
            };
        private readonly IDictionary<ProximityChatMode, string> _proximityChatModeQuotationStart = 
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" },
                { ProximityChatMode.Sign, "<i>\'" }
            };
        private readonly IDictionary<ProximityChatMode, string> _proximityChatModeQuotationEnd = 
            new Dictionary<ProximityChatMode, string>
            {
                { ProximityChatMode.Yell, "\"" },
                { ProximityChatMode.Normal, "\"" },
                { ProximityChatMode.Whisper, "\"" },
                { ProximityChatMode.Sign, "\'</i>" }
            };

        private PlayerGroup _proximityGroup;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this._sapi = api;

            try
            {
                this._config = api.LoadModConfig<ModConfig>(ConfigName);
            }
            catch (Exception)
            {
                api.Server.LogError("proximitychat: Failed to load mod config!");
                return;
            }

            if (_config == null)
            {
                api.Server.LogNotification("proximitychat: non-existant modconfig at 'ModConfig/" + ConfigName + "', creating default...");
                _config = new ModConfig();
                api.StoreModConfig(this._config, ConfigName);
            }
            else if (_config.ProximityChatNormalBlockRange <= 0 || _config.ProximityChatWhisperBlockRange <= 0 || _config.ProximityChatYellBlockRange <= 0)
            {
                api.Server.LogError("proximitychat: invalid modconfig at 'ModConfig/" + ConfigName + "'!");
                return;
            }

            _sapi.Event.PlayerChat += Event_PlayerChat;
            _sapi.Event.PlayerJoin += Event_PlayerJoin;

            _sapi.RegisterCommand("pmessage", "Sends a message to all players in a specific area", null, OnPMessageHandler, Privilege.announce);
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
            api.RegisterCommand("hands", "Set your chat mode to Sign Language, or sign a single message", "", Sign);
            api.RegisterCommand("h", "Set your chat mode to Sign Language, or sign a single message", "", Sign);
            api.RegisterCommand("emotemode", "Turn Emote-only mode on or off", "/emotemode [on|off]", EmoteMode);
            api.RegisterCommand("rptext", "Turn the whole RP system on or off for your messages", "/rptext [on|off]", RpTextEnabled);
            
            _proximityGroup = _sapi.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (_proximityGroup == null)
            {
                _proximityGroup = new PlayerGroup()
                {
                    Name = ProximityGroupName,
                    OwnerUID = null
                };
                _sapi.Groups.AddPlayerGroup(_proximityGroup);
                _proximityGroup.Md5Identifier = GameMath.Md5Hash(_proximityGroup.Uid + "null");
            }

            _proximityChatModeDistances = new Dictionary<ProximityChatMode, int>
            {
                { ProximityChatMode.Yell, _config.ProximityChatYellBlockRange },
                { ProximityChatMode.Normal, _config.ProximityChatNormalBlockRange },
                { ProximityChatMode.Whisper, _config.ProximityChatWhisperBlockRange },
                { ProximityChatMode.Sign, _config.ProximityChatSignBlockRange }
            };
        }

        private void OnPMessageHandler(IServerPlayer player, int groupId, CmdArgs args)
        {
            Vec3d spawnpos = _sapi.World.DefaultSpawnPosition.XYZ;
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

            foreach (var nearbyPlayerData in this._sapi.World.AllOnlinePlayers.Select(x => new { Position = x.Entity.ServerPos, Player = (IServerPlayer) x }).Where(x => Math.Abs( x.Position.DistanceTo(targetpos) ) < blockRadius))
            {
                nearbyPlayerData.Player.SendMessage(this._proximityGroup.Uid, message, EnumChatType.CommandSuccess);
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            var proximityGroup = _sapi.Groups.GetPlayerGroupByName(ProximityGroupName);
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
            var proximityGroup = _sapi.Groups.GetPlayerGroupByName(ProximityGroupName);
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
                        byPlayer.SendMessage(channelId, "You need a nickname to use proximity chat!  You can set it with `/nick MyName`", EnumChatType.CommandError);
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

            var message = new StringBuilder();
            message.Append(player.GetNickname());
            message.Append(" ");
            message.Append(GetProximityChatVerb(player, tempMode));
            message.Append(" ");
            message.Append(_proximityChatModeQuotationStart[player.GetChatMode(tempMode)]);
            message.Append(GetRPMessage(player, content, tempMode));
            message.Append(_proximityChatModeQuotationEnd[player.GetChatMode(tempMode)]);

            return message.ToString();
        }

        private string GetFullEmoteMessage(IServerPlayer player, string content, ProximityChatMode? tempMode = null)
        {
            return player.GetNickname() + GetEmoteMessage(content);
        }
        private void SendLocalChat(IServerPlayer byPlayer, string message, ProximityChatMode? tempMode = null, EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = _sapi.Groups.GetPlayerGroupByName(ProximityGroupName);
            foreach (var player in this._sapi.World.AllOnlinePlayers.Where(x => x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) < GetProximityChatRange(byPlayer, tempMode)))
            {
                var serverPlayer = player as IServerPlayer;
                
                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }

        private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return _proximityChatModeDistances[player.GetChatMode(tempMode)];
        }

        private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return _proximityChatModeVerbs[player.GetChatMode(tempMode)].GetRandomElement();
        }
        private string GetProximityChatPunctuation(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return _proximityChatModePunctuation[player.GetChatMode(tempMode)];
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
                    player.SendMessage(groupId, ChatHelper.Build("Your nickname is: ", player.GetNickname()), EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(groupId, "You don't have a nickname!  You can set it with `/nick MyName`", EnumChatType.Notification);
                }
            }
            else
            {
                var nickname = args.PopAll();
                player.SetNickname(nickname);
                player.SendMessage(groupId, ChatHelper.Build("Okay, your nickname is set to ", ChatHelper.Quote(nickname)), EnumChatType.Notification);
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
                byPlayer.SendMessage(groupId, "You need a nickname to use emotes!  You can set it with `/nick MyName`", EnumChatType.CommandError);
            }
        }

        private void EnvironmentMessage(IServerPlayer byPlayer, int groupId, CmdArgs args)
        {
            SendLocalChat(byPlayer, ChatHelper.Wrap(GetRPMessage(byPlayer, args.PopAll(), ProximityChatMode.Normal), "*"), chatType: EnumChatType.Notification);
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
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Yell), ProximityChatMode.Yell);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Yell);
                player.SendMessage(groupId, "You are now yelling.", EnumChatType.Notification);
            }
        }
        private void Sign(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Sign), ProximityChatMode.Sign);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Sign);
                player.SendMessage(groupId, "You are now signing.", EnumChatType.Notification);
            }
        }
        private void Whisper(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Whisper), ProximityChatMode.Whisper);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Whisper);
                player.SendMessage(groupId, "You are now whispering.", EnumChatType.Notification);
            }
        }
        private void Say(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                SendLocalChat(player, GetFullRPMessage(player, args.PopAll(), ProximityChatMode.Normal), ProximityChatMode.Normal);
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Normal);
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
            player.SendMessage(groupId, ChatHelper.Build("Emote mode is now ", value), EnumChatType.Notification);
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
            player.SendMessage(groupId, ChatHelper.Build("RP Text is now ", value, " for your messages."), EnumChatType.Notification);
        }
    }
}
