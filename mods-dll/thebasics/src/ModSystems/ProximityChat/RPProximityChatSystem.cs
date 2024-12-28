using System.Drawing;
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
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ProximityChat
{
    public class RPProximityChatSystem : BaseBasicModSystem
    {
        private int _proximityChatId;
        private LanguageSystem _languageSystem;
        private DistanceObfuscationSystem _distanceObfuscationSystem;
        private IServerNetworkChannel _serverConfigChannel;
        private ProximityCheckUtils _proximityCheckUtils;
        
        // private IServerNetworkChannel _serverNicknameChannel;

        protected override void BasicStartServerSide()
        {
            HookEvents();
            RegisterCommands();
            SetupProximityGroup();

            _languageSystem = new LanguageSystem(this, API, Config);
            _distanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
            _proximityCheckUtils = new ProximityCheckUtils(this, API, Config);
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
                
                API.ChatCommands.GetOrCreate("nickcolor")
                    .WithAlias("nicknamecolor", "nickcol")
                    .WithDescription("Get or set nickname color")
                    .WithArgs(new ColorArgParser("new nickname color", false))
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(HandleNicknameColor);

                API.ChatCommands.GetOrCreate("clearnick")
                    .WithDescription("Clear your nickname")
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(ClearNickname);
            
                API.ChatCommands.GetOrCreate("clearnickcolor")
                    .WithDescription("Clear your nickname color")
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(ClearNicknameColor);
            }

            API.ChatCommands.GetOrCreate("adminsetnickname")
                .WithAlias("adminsetnick")
                .WithDescription("Admin: Get or set another player's nickname")
                .WithRootAlias("adminsetnick")
                .WithArgs(new PlayersArgParser("target player", API, true),
                    new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);
            
            API.ChatCommands.GetOrCreate("adminsetnicknamecolor")
                .WithAlias("adminsetnickcolor", "adminsetnickcol")
                .WithDescription("Admin: Get or set another player's nickname color")
                .WithArgs(new PlayersArgParser("target player", API, true),
                    new ColorArgParser("new nickname color", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameColorAdmin);

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
                .WithAlias("h", "sign")
                .WithDescription("Set your chat mode to Sign Language, or sign a single message")
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Sign);

            _serverConfigChannel = API.Network.RegisterChannel("thebasics")
                .RegisterMessageType<TheBasicsConfigMessage>();
            // .RegisterMessageType<TheBasicsChatTypingMessage>()
            // .SetMessageHandler<TheBasicsChatTypingMessage>((player, packet) =>
            // {
            //     
            // });

            // _serverNicknameChannel = API.Network.RegisterChannel("thebasics_nickname")
            //     .RegisterMessageType<TheBasicsPlayerNicknameMessage>();
        }

        private TextCommandResult SetNicknameColorAdmin(TextCommandCallingArgs args)
        {
            var attemptTarget = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            if (attemptTarget == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            var oldNicknameColor = attemptTarget.GetNicknameColor();
            
            if (args.Parsers[1].IsMissing)
            {
                if (!attemptTarget.HasNicknameColor())
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"Player {attemptTarget.PlayerName} does not have a nickname color!  You can set it with `/adminsetnickcolor {attemptTarget.PlayerName} NewColor`",
                    };
                }

                var color = attemptTarget.GetNicknameColor();
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = $"Player {attemptTarget.PlayerName} nickname color is: {ChatHelper.Color(color, color)}",
                };

            }
            
            var newNicknameColor = (Color)args.Parsers[1].GetValue();
            var newColorHex = ColorTranslator.ToHtml(newNicknameColor);
            
            attemptTarget.SetNicknameColor(newColorHex);
            
            SwapOutNameTag(attemptTarget);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {attemptTarget.PlayerName} nickname color has been set to: {newColorHex}.  Old Nickname Color: {oldNicknameColor}",
            };
        }

        private TextCommandResult HandleNicknameColor(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            if (args.Parsers[0].IsMissing)
            {
                if (player.HasNicknameColor())
                {
                    var color = player.GetNicknameColor();
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Success,
                        StatusMessage = $"Your nickname color is: {ChatHelper.Color(color, color)}",
                    };
                }
                else
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = "You don't have a nickname color!  You can set it with `/nickcolor [color]`",
                    };
                }
            }
            else
            {
                var nicknameColor = (Color)args.Parsers[0].GetValue();
                var colorHex = ColorTranslator.ToHtml(nicknameColor);
                player.SetNicknameColor(colorHex);
                SwapOutNameTag(player);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = $"Okay, your nickname color is set to {ChatHelper.Color(colorHex, colorHex)}",
                };
            }
        }

        private void SendClientConfig(IServerPlayer byPlayer)
        {
            _serverConfigChannel.SendPacket(new TheBasicsConfigMessage
            {
                ProximityGroupId = _proximityChatId,
                PreventProximityChannelSwitching = Config.PreventProximityChannelSwitching,
            }, byPlayer);
        }

        private void HookEvents()
        {
            API.Event.PlayerChat += Event_PlayerChat;
            API.Event.PlayerJoin += Event_PlayerJoin;
            // API.Event.OnEntitySpawn += SetPlayerRenderer;
        }

        // private void SetPlayerRenderer(Entity entity)
        // {
        //     if (entity is EntityPlayer entityPlayer)
        //     {
        //         API.Logger.Debug($"THEBASICS - Loading Player - {entity.Properties.Client.RendererName}");
        //         entity.Properties.Client.RendererName = "TestPlayerShape";
        //     }
        // }

        private void SetupProximityGroup()
        {
            if (Config.UseGeneralChannelAsProximityChat)
            {
                _proximityChatId = GlobalConstants.GeneralChatGroup;
                RemoveProximityGroupIfExists();
            }
            if (!Config.UseGeneralChannelAsProximityChat)
            {
                var proximityGroup = GetProximityGroup();
                if (proximityGroup == null)
                {
                    proximityGroup = new PlayerGroup()
                    {
                        Name = Config.ProximityChatName,
                        OwnerUID = null
                    };
                    API.Groups.AddPlayerGroup(proximityGroup);
                    proximityGroup.Md5Identifier = GameMath.Md5Hash(proximityGroup.Uid + "null");
                }

                _proximityChatId = proximityGroup.Uid;
            }
        }

        private void RemoveProximityGroupIfExists()
        {
            
            var proximityGroup = GetProximityGroup();
            if (proximityGroup != null)
            {
                API.Groups.RemovePlayerGroup(proximityGroup);
            }
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            if (!Config.UseGeneralChannelAsProximityChat)
            {
                var proximityGroup = GetProximityGroup();
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
                } else if (playerProximityGroup.Level == EnumPlayerGroupMemberShip.None)
                {
                    playerProximityGroup.Level = EnumPlayerGroupMemberShip.Member;
                }
            }

            // TODO: Wait until client has requested config to send it? Commonly done in other mods
            SendClientConfig(byPlayer);

            SwapOutNameTag(byPlayer);
        }
        
        // TODO: Not sure the client will get this data and sync it up.  Supposedly, behaviors should sync seamlessly with the client but I'm not sure that the UI renderer will refire (just like it does for chat), as the PlayerName never usually changes.  NPC names do though, maybe we can tie it in to that?
        private void SwapOutNameTag(IServerPlayer player)
        {
            var behavior = player.Entity.GetBehavior<EntityBehaviorNameTag>();
            behavior.ShowOnlyWhenTargeted = Config.HideNametagUnlessTargeting;
            behavior.RenderRange = Config.NametagRenderRange;

            if (Config.ShowNicknameInNametag)
            {
                var nickname = player.GetNickname();

                var displayName = Config.ShowPlayerNameInNametag ? $"{nickname} ({player.PlayerName})" : nickname;
            
                behavior.SetName(displayName);
            }
            
            // Broadcast player's name to all clients (except player)
            // _serverNicknameChannel.BroadcastPacket(new TheBasicsPlayerNicknameMessage
            // {
            //     PlayerUID = player.PlayerUID,
            //     Nickname = nickname,
            // }, player);
            
            // Send player's name specifically to connecting player
            // _serverNicknameChannel.SendPacket(new TheBasicsPlayerNicknameMessage
            // {
            //     PlayerUID = player.PlayerUID,
            //     Nickname = nickname,
            // }, player);
            
            // Send the player all other nicknames to sync them up
            // API.World.AllOnlinePlayers.Foreach((loopPlayer) =>
            // {
            //     _serverNicknameChannel.SendPacket(new TheBasicsPlayerNicknameMessage
            //     {
            //         PlayerUID = loopPlayer.PlayerUID,
            //         Nickname = (loopPlayer as IServerPlayer).GetNickname(),
            //     }, player);
            // });
        }

        private string GetPlayerChat(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string message, int groupId)
        {
            var content = ChatHelper.GetMessage(message);
            var isEmote = content[0] == '*';
            var isGlobalOoc = Config.EnableGlobalOOC && content.StartsWith("((");
            var isOOC = !isGlobalOoc && content[0] == '(';
            var isEnvironmentMessage = content[0] == '!';

            var messageCopy = (string) message.Clone();

            if (isEmote)
            {
                content = content.Remove(0, 1);
                messageCopy = GetFullEmoteMessage(sendingPlayer, receivingPlayer, content);
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
                content = content.Remove(0, 1);
                messageCopy = ChatHelper.Wrap(
                    AddAutoCapitalizationAndPunctuation(sendingPlayer, content, ProximityChatMode.Normal),
                    "*");
            }
            else
            {
                messageCopy = GetFullRPMessage(sendingPlayer, receivingPlayer, content, groupId);
            }

            return messageCopy;

        }

        private PlayerGroup GetProximityGroup()
        {
            return API.Groups.GetPlayerGroupByName(Config.ProximityChatName);
        }
        
        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
            Vintagestory.API.Datastructures.BoolRef consumed)
        {
            // Handle cases of incorrect channel
            if(Config.UseGeneralChannelAsProximityChat && channelId != GlobalConstants.GeneralChatGroup)
            {
                return;
            }
            if (!Config.UseGeneralChannelAsProximityChat)
            {
                var proximityGroup = GetProximityGroup();
                if (proximityGroup.Uid != channelId)
                {
                    return;
                }
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
            
            // TODO: Fix this hack - using a check here to check for environment messages to apply specific message type
            // I'm checking the content twice, both here and in GetPlayerChat. Should be cleaned up
            var content = ChatHelper.GetMessage(message);
            var isEnvironmentMessage = content[0] == '!';
            var isGlobalOOC = Config.EnableGlobalOOC && content.StartsWith("((");

            if (isGlobalOOC)
            {
                // If Global OOC, let the message be sent out like normal just like we do with other channels
                consumed.value = false;
                return;
            }
            
            EnumChatType chatType = isEnvironmentMessage ? EnumChatType.Notification : EnumChatType.OthersMessage;
            var messageCopy = (string)message.Clone();
            SendLocalChatByPlayer(byPlayer,
                receivingPlayer => GetPlayerChat(byPlayer, receivingPlayer, messageCopy, channelId), chatType: chatType, data: data);
            
            // TODO: Only send this message if the player tried to speak in babble accidentally
            if (byPlayer.GetDefaultLanguage(Config) == LanguageSystem.BabbleLang)
            {
                byPlayer.SendMessage(GlobalConstants.CurrentChatGroup, "You are speaking in babble.  Add a language via /addlang or set your default lang with a language identifier, ex. \":c\".  Use /listlang to see all available languages", EnumChatType.CommandError);
            }
        }

        private string GetFullRPMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string content, int groupId, ProximityChatMode? tempMode = null)
        {
            if (sendingPlayer.GetEmoteMode())
            {
                return GetFullEmoteMessage(sendingPlayer, receivingPlayer, content);
            }

            var lang = _languageSystem.GetSpeakingLanguage(sendingPlayer, groupId, ref content);
            
            var message = new StringBuilder();
            if (Config.EnableDistanceFontSizeSystem)
            {
                message.Append($"<font size=\"{_distanceObfuscationSystem.GetFontSize(sendingPlayer, receivingPlayer, tempMode)}\">");
            }
            message.Append(GetFormattedNickname(sendingPlayer));
            message.Append(" ");
            message.Append(GetProximityChatVerb(sendingPlayer, lang, tempMode));
            message.Append(" ");
            message.Append($"<font color=\"{lang.Color}\">");
            message.Append(Config.ProximityChatModeQuotationStart[sendingPlayer.GetChatMode(tempMode)]);

            var chatContent = AddAutoCapitalizationAndPunctuation(sendingPlayer, content, tempMode);
            chatContent = ProcessAccents(chatContent);

            _languageSystem.ProcessMessage(receivingPlayer, ref chatContent, lang);
            
            _distanceObfuscationSystem.ObfuscateMessage(sendingPlayer, receivingPlayer, ref chatContent,
                tempMode);

            message.Append(chatContent);
            message.Append(Config.ProximityChatModeQuotationEnd[sendingPlayer.GetChatMode(tempMode)]);

            message.Append("</font>");
            if (Config.EnableDistanceFontSizeSystem)
            {
                message.Append("</font>");
            }

            return message.ToString();
        }

        private string GetFormattedNickname(IServerPlayer player)
        {
            var nick = player.GetNicknameWithColor();
            return Config.BoldNicknames ? ChatHelper.Strong(nick) : nick;
        }

        private string GetFullEmoteMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string content)
        {
            return ChatHelper.Build(GetFormattedNickname(sendingPlayer), " ", GetEmoteMessage(sendingPlayer, receivingPlayer, content));
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
            foreach (var player in API.World.AllOnlinePlayers.Where(x =>
                         x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) <
                         GetProximityChatRange(byPlayer, tempMode)))
            {
                var serverPlayer = player as IServerPlayer;

                serverPlayer.SendMessage(_proximityChatId, messageGenerator(serverPlayer), chatType, data);
            }
        }

        private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
        {
            return Config.ProximityChatModeDistances[player.GetChatMode(tempMode)];
        }

        private string GetProximityChatVerb(IServerPlayer player, Language lang, ProximityChatMode? tempMode = null)
        {
            if(lang == LanguageSystem.BabbleLang)
            {
                return Config.ProximityChatModeBabbleVerb;
            }
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

        private string GetEmoteMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string message)
        {
            var builder = new StringBuilder();

            var trimmedMessage = message.Trim();
            
            // Separate trimmed message by quotes, applying the language system to every other part
            // TODO: Allow for multiple languages in a single message
            var splitMessage = trimmedMessage.Split('"');
            for (var i = 0; i < splitMessage.Length; i++)
            {
                if (i % 2 == 0)
                {
                    builder.Append(splitMessage[i]);
                }
                else
                {
                    // TODO: apply obfuscation system here too!
                    var lang = sendingPlayer.GetDefaultLanguage(Config);
                    _languageSystem.ProcessMessage(receivingPlayer, ref splitMessage[i], lang);
                    _distanceObfuscationSystem.ObfuscateMessage(sendingPlayer, receivingPlayer, ref splitMessage[i]);
                    
                    // TODO: Should emotes accept a temporary mode or temporary language? Probably not, too complicated 
                    // TODO: Font size in just the text of an emote seems... weird.  Maybe it should be consistent across the whole message
                    var fontSize = _distanceObfuscationSystem.GetFontSize(sendingPlayer, receivingPlayer);
                    
                    builder.Append($"<font color=\"{lang.Color}\" size=\"{fontSize}\">");
                    builder.Append("\"");
                    builder.Append(splitMessage[i]);
                    builder.Append("\"");
                    builder.Append("</font>");
                }
            }
            
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
                        StatusMessage = $"Your nickname is: {player.GetNicknameWithColor()}",
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
                SwapOutNameTag(player);
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = $"Okay, your nickname is set to {ChatHelper.Quote(nickname)}",
                };
            }
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
            var oldNickname = attemptTarget.GetNicknameWithColor();
            
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
                    StatusMessage = $"Player {attemptTarget.PlayerName} nickname is: {attemptTarget.GetNicknameWithColor()}",
                };

            }
            
            var newNickname = (string)fullArgs.Parsers[1].GetValue();
            
            attemptTarget.SetNickname(newNickname);
            
            SwapOutNameTag(attemptTarget);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {attemptTarget.PlayerName} nickname has been set to: {newNickname}.  Old Nickname: {oldNickname}",
            };
        }

        private TextCommandResult Emote(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            if (player.HasNickname())
            {
                SendLocalChatByPlayer(player,
                    targetPlayer => GetFullEmoteMessage(player, targetPlayer, 
                        (string)args.Parsers[0].GetValue()));
                
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
            var player = (IServerPlayer)args.Caller.Player;
            player.ClearNickname();
            SwapOutNameTag(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Your nickname has been cleared.",
            };
        }
        
        private TextCommandResult ClearNicknameColor(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            player.ClearNicknameColor();
            SwapOutNameTag(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Your nickname color has been cleared.",
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