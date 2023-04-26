using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat
{
    public class RPProximityChatSystem : BaseBasicModSystem
    {
        private const string ProximityGroupName = "Proximity";
        private PlayerGroup _proximityGroup;
        // private LanguageSystem _languageSystem;
        // private DistanceObfuscationSystem _distanceObfuscationSystem;

        protected override void BasicStartServerSide()
        {
            HookEvents();
            RegisterCommands();
            SetupProximityGroup();

            // _languageSystem = new LanguageSystem(this, API, Config);
            // _distanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        }

        private void RegisterCommands()
        {
            // API.RegisterCommand("pmessage", "Sends a message to all players in a specific area", null,
            //     OnPMessageHandler, Privilege.announce);  

            API.ChatCommands.GetOrCreate("nickname")
                .WithAlias("nick", "setnick")
                .WithDescription("Get or set your nickname")
                .WithRootAlias("nick")
                .WithArgs(new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(SetNickname);

            API.ChatCommands.GetOrCreate("me")
                .WithAlias("m")
                .WithDescription("Send a proximity emote message")
                .WithArgs(new StringArgParser("emote", true))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Emote);

            API.ChatCommands.GetOrCreate("it")
                .WithDescription("Send a proximity environment message")
                .WithArgs(new StringArgParser("envMessage", true))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(EnvironmentMessage);

            API.ChatCommands.GetOrCreate("yell")
                .WithAlias("y")
                .WithDescription("Set your chat mode to Yelling, or yell a single message")
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Yell);

            API.ChatCommands.GetOrCreate("say")
                .WithAlias("s", "normal")
                .WithDescription("Set your chat mode back to normal, or say a single message")
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Say);

            API.ChatCommands.GetOrCreate("whisper")
                .WithAlias("w")
                .WithDescription("Set your chat mode to Whispering, or whisper a single message")
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Whisper);

            API.ChatCommands.GetOrCreate("emotemode")
                .WithDescription("Turn Emote-only mode on or off")
                .WithArgs(new BoolArgParser("mode", "on", true))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(EmoteMode);

            API.ChatCommands.GetOrCreate("rptext")
                .WithDescription("Turn the whole RP system on or off for your messages")
                .WithArgs(new BoolArgParser("mode", "on", true))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(RpTextEnabled);

            API.ChatCommands.GetOrCreate("hands")
                .WithAlias("h")
                .WithDescription("Set your chat mode to Sign Language, or sign a single message")
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Sign);

            API.ChatCommands.GetOrCreate("clearnick")
                .WithDescription("Clear your nickname")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ClearNickname);
        }

        private void HookEvents()
        {
            API.Event.PlayerChat += Event_PlayerChat;
            API.Event.PlayerJoin += Event_PlayerJoin;
        }

        private void SetupProximityGroup()
        {
            _proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (_proximityGroup == null)
            {
                _proximityGroup = new PlayerGroup()
                {
                    Name = ProximityGroupName,
                    OwnerUID = null
                };
                API.Groups.AddPlayerGroup(_proximityGroup);
                _proximityGroup.Md5Identifier = GameMath.Md5Hash(_proximityGroup.Uid + "null");
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            var playerProximityGroup = byPlayer.GetGroup(proximityGroup.Uid);
            if (playerProximityGroup == null)
            {
                var newMembership = new PlayerGroupMembership()
                {
                    GroupName = proximityGroup.Name,
                    GroupUid = proximityGroup.Uid,
                    Level = EnumPlayerGroupMemberShip.Member
                };
                byPlayer.ServerData.PlayerGroupMemberships.Add(proximityGroup.Uid, newMembership);
                proximityGroup.OnlinePlayers.Add(byPlayer);
                foreach (var serverDataPlayerGroupMembership in byPlayer.ServerData.PlayerGroupMemberships)
                {
                    byPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                        serverDataPlayerGroupMembership.Value.GroupName, EnumChatType.Notification);
                }
            }
        }

        public delegate string TransformMessage(IServerPlayer byPlayer, ref string message, Vintagestory.API.Datastructures.BoolRef consumed);

        public TransformMessage GetLanguage()
        {
            return (IServerPlayer byPlayer, ref string message, Vintagestory.API.Datastructures.BoolRef consumed) =>
            {
                return message;
            };
        }
        
        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
            Vintagestory.API.Datastructures.BoolRef consumed)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (proximityGroup.Uid == channelId)
            {
                if (byPlayer.GetRpTextEnabled())
                {
                    // if (_languageSystem.HandleSwapLanguage(byPlayer, channelId, message))
                    // {
                    //     consumed.value = true;
                    //     return;
                    // }
                    //
                    //
                    // if (_languageSystem.HandleSwapLanguage(byPlayer, channelId, message))
                    // {
                    //     
                    // }

                    if (byPlayer.HasNickname())
                    {
                        var content = ChatHelper.GetMessage(message);
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
                            message = GetFullRPMessage(byPlayer, byPlayer, content, channelId);
                        }

                        SendLocalChatByPlayer(byPlayer,
                            targetPlayer => GetFullRPMessage(byPlayer, targetPlayer, content, channelId), data: data);
                    }
                    else
                    {
                        byPlayer.SendMessage(channelId,
                            "You need a nickname to use proximity chat!  You can set it with `/nick MyName`",
                            EnumChatType.CommandError);
                    }
                }
                else
                {
                    SendLocalChat(byPlayer, message, data: data);
                }

                consumed.value = true;
            }
        }

        private string GetFullRPMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string content,
            int groupId, ProximityChatMode? tempMode = null)
        {
            if (sendingPlayer.GetEmoteMode())
            {
                return GetFullEmoteMessage(sendingPlayer, content);
            }

            // var lang = _languageSystem.GetSpeakingLanguage(sendingPlayer, groupId, ref content);

            var message = new StringBuilder();
            message.Append(GetFormattedNickname(sendingPlayer));
            message.Append(" ");
            message.Append(GetProximityChatVerb(sendingPlayer, tempMode));
            message.Append(" ");
            // message.Append($"<font color=\"{lang.Color}\">");
            message.Append(Config.ProximityChatModeQuotationStart[sendingPlayer.GetChatMode(tempMode)]);

            var chatContent = GetRPMessage(sendingPlayer, content, tempMode);

            // _languageSystem.ProcessMessage(sendingPlayer, receivingPlayer, groupId, ref chatContent, lang);
            //
            // _distanceObfuscationSystem.ObfuscateMessage(sendingPlayer, receivingPlayer, ref chatContent,
            //     tempMode);

            message.Append(chatContent);
            message.Append(Config.ProximityChatModeQuotationEnd[sendingPlayer.GetChatMode(tempMode)]);

            // message.Append("</font>");

            return message.ToString();
        }

        private string GetFormattedNickname(IServerPlayer player)
        {
            var nick = player.GetNickname();
            return Config.BoldNicknames ? ChatHelper.Strong(nick) : nick;
        }

        private string GetFullEmoteMessage(IServerPlayer player, string content)
        {
            return ChatHelper.Build(GetFormattedNickname(player), " ", GetEmoteMessage(content));
        }

        private void SendLocalChat(IServerPlayer byPlayer, string message, ProximityChatMode? tempMode = null,
            EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            SendLocalChatByPlayer(byPlayer,
                _ => message, tempMode,
                chatType, data);
        }

        private void SendLocalChatByPlayer(IServerPlayer byPlayer, System.Func<IServerPlayer, string> messageGenerator,
            ProximityChatMode? tempMode = null,
            EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            foreach (var player in API.World.AllOnlinePlayers.Where(x =>
                         x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) <
                         GetProximityChatRange(byPlayer, tempMode)))
            {
                var serverPlayer = player as IServerPlayer;

                serverPlayer.SendMessage(proximityGroup.Uid, messageGenerator(serverPlayer), chatType, data);
            }
        }

        private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModeDistances[player.GetChatMode(tempMode)];
        }

        private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModeVerbs[player.GetChatMode(tempMode)].GetRandomElement();
        }

        private string GetProximityChatPunctuation(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModePunctuation[player.GetChatMode(tempMode)];
        }

        private string GetRPMessage(IServerPlayer player, string message, ProximityChatMode? tempMode = null)
        {
            var builder = new StringBuilder();

            builder.Append(message.Substring(0, 1).ToUpper() + message.Substring(1, message.Length - 1));

            if (ChatHelper.DoesMessageNeedPunctuation(message))
            {
                builder.Append(GetProximityChatPunctuation(player, tempMode));
            }

            return builder.ToString();
        }

        private string GetEmoteMessage(string message)
        {
            var builder = new StringBuilder();

            builder.Append(message.Trim());
            if (ChatHelper.DoesMessageNeedPunctuation(message))
            {
                builder.Append(".");
            }

            return builder.ToString();
        }

        private TextCommandResult SetNickname(TextCommandCallingArgs fullArgs)
        {
            var player = (IServerPlayer)fullArgs.Caller.Player;
            if (fullArgs.Parsers[0].IsMissing)
            {
                if (player.HasNickname())
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Success,
                        StatusMessage = $"Your nickname is: {player.GetNickname()}",
                    };
                }
                else
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = "You don't have a nickname!  You can set it with `/nick MyName`",
                    };
                }
            }
            else
            {
                var nickname = (string)fullArgs.Parsers[0].GetValue();
                player.SetNickname(nickname);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = $"Okay, your nickname is set to {ChatHelper.Quote(nickname)}",
                };
            }
        }

        private TextCommandResult Emote(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            if (player.HasNickname())
            {
                SendLocalChat(player, GetFullEmoteMessage(player, (string)args.Parsers[0].GetValue()));
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                };
            }
            else
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You need a nickname to use emotes!  You can set it with `/nick MyName`"
                };
            }
        }

        private TextCommandResult EnvironmentMessage(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            SendLocalChat(player,
                ChatHelper.Wrap(GetRPMessage(player, (string)args.Parsers[0].GetValue(), ProximityChatMode.Normal),
                    "*"),
                chatType: EnumChatType.Notification);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
            };
        }

        private TextCommandResult ClearNickname(TextCommandCallingArgs args)
        {
            ((IServerPlayer)args.Caller.Player).ClearNickname();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Your nickname has been cleared.",
            };
        }

        private TextCommandResult Yell(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var message = (string)args.Parsers[0].GetValue();
            var groupId = args.Caller.FromChatGroupId;
            if (!args.Parsers[0].IsMissing)
            {
                SendLocalChatByPlayer(player,
                    targetPlayer => GetFullRPMessage(player, targetPlayer, message, groupId, ProximityChatMode.Yell),
                    ProximityChatMode.Yell);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                };
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Yell);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "You are now yelling.",
                };
            }
        }

        private TextCommandResult Sign(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var message = (string)args.Parsers[0].GetValue();
            var groupId = args.Caller.FromChatGroupId;
            if (!args.Parsers[0].IsMissing)
            {
                SendLocalChatByPlayer(player,
                    targetPlayer => GetFullRPMessage(player, targetPlayer, message, groupId, ProximityChatMode.Sign),
                    ProximityChatMode.Sign);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                };
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Sign);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "You are now signing.",
                };
            }
        }

        private TextCommandResult Whisper(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var message = (string)args.Parsers[0].GetValue();
            var groupId = args.Caller.FromChatGroupId;
            if (!args.Parsers[0].IsMissing)
            {
                SendLocalChatByPlayer(player,
                    targetPlayer => GetFullRPMessage(player, targetPlayer, message, groupId, ProximityChatMode.Whisper),
                    ProximityChatMode.Whisper);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                };
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Whisper);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "You are now whispering.",
                };
            }
        }

        private TextCommandResult Say(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var message = (string)args.Parsers[0].GetValue();
            var groupId = args.Caller.FromChatGroupId;
            if (!args.Parsers[0].IsMissing)
            {
                SendLocalChatByPlayer(player,
                    targetPlayer => GetFullRPMessage(player, targetPlayer, message, groupId, ProximityChatMode.Normal),
                    ProximityChatMode.Normal);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                };
            }
            else
            {
                player.SetChatMode(ProximityChatMode.Normal);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "You are now talking normally.",
                };
            }
        }

        private TextCommandResult EmoteMode(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var emoteMode = (bool)args.Parsers[0].GetValue();
            player.SetEmoteMode(emoteMode);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Emote mode is now {ChatHelper.OnOff(emoteMode)}",
            };
        }

        private TextCommandResult RpTextEnabled(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var rpTextEnabled = (bool)args.Parsers[0].GetValue();
            player.SetRpTextEnabled(rpTextEnabled);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"RP Text is now {ChatHelper.OnOff(rpTextEnabled)} for your messages.",
            };
        }

        private void OnPMessageHandler(IServerPlayer player, int groupId, CmdArgs args)
        {
            Vec3d spawnpos = API.World.DefaultSpawnPosition.XYZ;
            spawnpos.Y = 0;
            Vec3d targetpos = null;
            if (player.Entity == null)
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
                                             no prefix denotes a position relative to the map middle",
                    EnumChatType.CommandError);
                return;
            }

            var blockRadius = args.PopInt();
            if (!blockRadius.HasValue)
            {
                player.SendMessage(groupId,
                    "Invalid radius supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius]",
                    EnumChatType.CommandError);
                return;
            }

            var message = args.PopAll();
            if (string.IsNullOrEmpty(message))
            {
                player.SendMessage(groupId,
                    "Invalid message supplied. Syntax: =[abscoord] =[abscoord] =[abscoord] [radius] [message]",
                    EnumChatType.CommandError);
                return;
            }

            foreach (var nearbyPlayerData in API.World.AllOnlinePlayers
                         .Select(x => new { Position = x.Entity.ServerPos, Player = (IServerPlayer)x })
                         .Where(x => Math.Abs(x.Position.DistanceTo(targetpos)) < blockRadius))
            {
                nearbyPlayerData.Player.SendMessage(this._proximityGroup.Uid, message, EnumChatType.CommandSuccess);
            }
        }
    }
}