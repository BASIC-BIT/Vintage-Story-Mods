using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.ModSystems.ProximityChat
{
    public class RPProximityChatSystem : BaseBasicModSystem
    {
        private const string ProximityGroupName = "Proximity";
        private PlayerGroup _proximityGroup;
        private LanguageSystem _languageSystem;
        private DistanceObfuscationSystem _distanceObfuscationSystem;
        private IServerNetworkChannel _serverChannel;

        protected override void BasicStartServerSide()
        {
            HookEvents();
            RegisterCommands();
            SetupProximityGroup();

            _languageSystem = new LanguageSystem(this, API, Config);
            _distanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        }

        private void RegisterCommands()
        {
            // API.RegisterCommand("pmessage", "Sends a message to all players in a specific area", null,
            //     OnPMessageHandler, Privilege.announce);  

            if (Config.ProximityChatAllowPlayersToChangeNicknames)
            {
                API.ChatCommands.GetOrCreate("nickname")
                    .WithAlias("nick", "setnick")
                    .WithDescription("Get or set your nickname")
                    .WithRootAlias("nick")
                    .WithArgs(new StringArgParser("new nickname", false))
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(SetNickname);
            }

            API.ChatCommands.GetOrCreate("adminsetnickname")
                .WithAlias("adminsetnick")
                .WithDescription("Admin: Get or set another player's nickname")
                .WithRootAlias("adminsetnick")
                .WithArgs(new PlayersArgParser("target player", API, true), new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);

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

            API.ChatCommands.GetOrCreate("testmessage")
                .WithDescription("test message")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TestMessage);

            _serverChannel = API.Network.RegisterChannel("thebasics_config")
                .RegisterMessageType<TheBasicsConfigMessage>()
                .RegisterMessageType<TheBasicsPlayerNicknameMessage>();
        }

        private void SendClientConfig(IServerPlayer byPlayer)
        {
            _serverChannel.SendPacket(new TheBasicsConfigMessage
            {
                ProximityGroupId = _proximityGroup.Uid,
                PreventProximityChannelSwitching = Config.PreventProximityChannelSwitching,
            }, byPlayer);
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
            // Console.WriteLine(JsonUtil.ToString(byPlayer.ServerData.PlayerGroupMemberships));

            SendClientConfig(byPlayer);

            SwapOutNameTag(byPlayer);
        }
        
        // TODO: Not sure the client will get this data and sync it up.  Supposedly, behaviors should sync seamlessly with the client but I'm not sure that the UI renderer will refire (just like it does for chat), as the PlayerName never usually changes.  NPC names do though, maybe we can tie it in to that?
        private void SwapOutNameTag(IServerPlayer player)
        {
            if (player.HasNickname())
            {
                return;
            }
            
            var behavior = player.Entity.GetBehavior<EntityBehaviorNameTag>();
            var nickname = player.GetNickname();

            behavior.SetName(nickname);
            
            // Broadcast player's name to all clients (except player)
            _serverChannel.BroadcastPacket(new TheBasicsPlayerNicknameMessage
            {
                PlayerUID = player.PlayerUID,
                Nickname = nickname,
            }, player);
            
            // Send player's name specifically to connecting player
            _serverChannel.SendPacket(new TheBasicsPlayerNicknameMessage
            {
                PlayerUID = player.PlayerUID,
                Nickname = nickname,
            }, player);
            
            // Send the player all other nicknames to sync them up
            API.World.AllOnlinePlayers.Foreach((loopPlayer) =>
            {
                _serverChannel.SendPacket(new TheBasicsPlayerNicknameMessage
                {
                    PlayerUID = loopPlayer.PlayerUID,
                    Nickname = (loopPlayer as IServerPlayer).GetNickname(),
                }, player);
            });
        }

        private string GetPlayerChat(IServerPlayer byPlayer, IServerPlayer receivingPlayer, string message, int groupId)
        {
            var content = ChatHelper.GetMessage(message);
            var isEmote = content[0] == '*';
            var isOOC = content[0] == '(';
            var isEnvironmentMessage = content[0] == '!';

            var messageCopy = (string) message.Clone();

            if (isEmote)
            {
                content = content.Remove(0, 1);
                messageCopy = GetFullEmoteMessage(byPlayer, content);
            }
            else if (isOOC)
            {
                if (!messageCopy.EndsWith(")")) // End the messageCopy with ) if it didn't end with it, to close out the (
                {
                    messageCopy += ")";
                }
            }
            else if (isEnvironmentMessage)
            {
            }
            else
            {
                messageCopy = GetFullRPMessage(byPlayer, receivingPlayer, content, groupId);
            }

            return messageCopy;

        }
        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
            Vintagestory.API.Datastructures.BoolRef consumed)
        {
            var proximityGroup = API.Groups.GetPlayerGroupByName(ProximityGroupName);
            if (proximityGroup.Uid != channelId)
            {
                return;
            }
            consumed.value = true;
            if (!byPlayer.GetRpTextEnabled())
            {
                SendLocalChat(byPlayer, message, data: data);
                return;
            }

            if (!byPlayer.HasNickname())
            {
                byPlayer.SendMessage(channelId,
                    "You need a nickname to use proximity chat!  You can set it with `/nick MyName`",
                    EnumChatType.CommandError);
                return;
            }
            
            if (_languageSystem.HandleSwapLanguage(byPlayer, channelId, message))
            {
                consumed.value = true;
                return;
            }

            var messageCopy = (string)message.Clone();
            SendLocalChatByPlayer(byPlayer,
                receivingPlayer => GetPlayerChat(byPlayer, receivingPlayer, messageCopy, channelId), data: data);
        }

        private string GetFullRPMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string content, int groupId, ProximityChatMode? tempMode = null)
        {
            if (sendingPlayer.GetEmoteMode())
            {
                return GetFullEmoteMessage(sendingPlayer, content);
            }

            var lang = _languageSystem.GetSpeakingLanguage(sendingPlayer, groupId, ref content);

            var message = new StringBuilder();
            message.Append(GetFormattedNickname(sendingPlayer));
            message.Append(" ");
            message.Append(GetProximityChatVerb(sendingPlayer, tempMode));
            message.Append(" ");
            message.Append($"<font color=\"{lang.Color}\">");
            message.Append(Config.ProximityChatModeQuotationStart[sendingPlayer.GetChatMode(tempMode)]);

            var chatContent = AddAutoCapitalizationAndPunctuation(sendingPlayer, content, tempMode);
            chatContent = ProcessAccents(chatContent);

            _languageSystem.ProcessMessage(sendingPlayer, receivingPlayer, groupId, ref chatContent, lang);
            
            _distanceObfuscationSystem.ObfuscateMessage(sendingPlayer, receivingPlayer, ref chatContent,
                tempMode);

            message.Append(chatContent);
            message.Append(Config.ProximityChatModeQuotationEnd[sendingPlayer.GetChatMode(tempMode)]);

            message.Append("</font>");

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

        private string AddAutoCapitalizationAndPunctuation(IServerPlayer player, string message, ProximityChatMode? tempMode = null)
        {
            var autoCapitalizationRegex = new Regex(@"^([\s+|]*)(.)(.*)$");
            var autoPunctuationRegex = new Regex(@"^(.*?)(.)([\s+|]*)$");

            message = autoPunctuationRegex.Replace(message, match =>
            {
                var possiblePunctuation = match.Groups[2].Value[0];
                return
                    $"{match.Groups[1].Value}{possiblePunctuation}{(ChatHelper.IsPunctuation(possiblePunctuation) ? "" : GetProximityChatPunctuation(player, tempMode))}{match.Groups[3].Value}";
            });

            message = autoCapitalizationRegex.Replace(message, match =>
            {
                var firstLetter = match.Groups[2].Value;
                return
                    $"{match.Groups[1].Value}{firstLetter.ToUpper()}{match.Groups[3].Value}";
            });

            return message;
        }

        private string GetEmoteMessage(string message)
        {
            var builder = new StringBuilder();

            builder.Append(message.Trim());
            if (ChatHelper.DoesMessageNeedPunctuation(message))
            {
                builder.Append(".");
            }

            var output = builder.ToString();

            output = ProcessAccents(output);

            return output;
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
            
            // TODO: Broadcast command to set nickname on clients
        }

        private string ProcessAccents(string message)
        {

            message = Regex.Replace(message, @"\|(.*?)\|", "<i>$1</i>");
            message = Regex.Replace(message, @"\|(.*)$", "<i>$1</i>");
            message = Regex.Replace(message, @"\+(.*?)\+", "<strong>$1</strong>");
            message = Regex.Replace(message, @"\+(.*)$", "<strong>$1</strong>");
            // message = Regex.Replace(message, @"=(.*?)=", "<font size=\"50\">$1</font>");

            return message;
        }
        private TextCommandResult SetNicknameAdmin(TextCommandCallingArgs fullArgs)
        {
            var attemptTarget = API.GetPlayerByUID(((PlayerUidName[])fullArgs.Parsers[0].GetValue())[0].Uid);
            if (attemptTarget == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            var oldNickname = attemptTarget.GetNickname();
            
            if (fullArgs.Parsers[1].IsMissing)
            {
                if (!attemptTarget.HasNickname())
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"Player {attemptTarget.PlayerName} does not have a nickname!  You can set it with `/adminsetnick {attemptTarget.PlayerName} NewName`",
                    };
                }

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = $"Player {attemptTarget.PlayerName} nickname is: {attemptTarget.GetNickname()}",
                };

            }
            
            var newNickname = (string)fullArgs.Parsers[1].GetValue();
            
            attemptTarget.SetNickname(newNickname);
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {attemptTarget.PlayerName} nickname has been set to: {newNickname}.  Old Nickname: {oldNickname}",
            };
            
            // TODO: Broadcast command to set nickname on clients
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
                ChatHelper.Wrap(AddAutoCapitalizationAndPunctuation(player, (string)args.Parsers[0].GetValue(), ProximityChatMode.Normal),
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
            // TODO: Send broadcast to clear nickname from clients
        }

        private TextCommandResult TestMessage(TextCommandCallingArgs args)
        {
            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, "TestMessage", EnumChatType.OthersMessage);

            return new TextCommandResult()
            {
                Status = EnumCommandStatus.Success,
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

            player.SetChatMode(ProximityChatMode.Yell);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You are now yelling.",
            };
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

            player.SetChatMode(ProximityChatMode.Sign);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You are now signing.",
            };
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

            player.SetChatMode(ProximityChatMode.Whisper);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You are now whispering.",
            };
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

            player.SetChatMode(ProximityChatMode.Normal);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You are now talking normally.",
            };
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
    }
}