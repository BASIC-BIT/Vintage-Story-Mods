using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat;

public class RPProximityChatSystem : BaseBasicModSystem
{
    private int _proximityChatId;
    private LanguageSystem _languageSystem;
    private DistanceObfuscationSystem _distanceObfuscationSystem;
    private IServerNetworkChannel _serverConfigChannel;
    private ProximityCheckUtils _proximityCheckUtils;
    private List<IMessageTransformer> _transformers;
    
    // private IServerNetworkChannel _serverNicknameChannel;

    protected override void BasicStartServerSide()
    {
        HookEvents();
        RegisterCommands();
        SetupProximityGroup();

        _languageSystem = new LanguageSystem(this, API, Config);
        _distanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        _proximityCheckUtils = new ProximityCheckUtils(this, API, Config);
        
        // Initialize transformers
        _transformers = new List<IMessageTransformer>
        {
            new LanguageTransformer(_languageSystem),
            new ObfuscationTransformer(_distanceObfuscationSystem),
            new FormatTransformer(this),
            new EmoteTransformer(this),
            new ChatModeTransformer(this)
        };
    }

    private void RegisterCommands()
    {
        // Skip all nickname-related commands if nicknames are disabled
        if (!Config.DisableNicknames)
        {
            if (Config.ProximityChatAllowPlayersToChangeNicknames)
            {
                API.ChatCommands.GetOrCreate("nickname")
                    .WithAlias("nick", "setnick")
                    .WithDescription("Get or set your nickname")
                    .WithRootAlias("nick")
                    .WithArgs(new StringArgParser("new nickname", false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SetNickname);
                
                API.ChatCommands.GetOrCreate("clearnick")
                    .WithDescription("Clear your nickname")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(ClearNickname);
            }

            if (Config.ProximityChatAllowPlayersToChangeNicknameColors)
            {
                API.ChatCommands.GetOrCreate("nickcolor")
                    .WithAlias("nicknamecolor", "nickcol")
                    .WithDescription("Get or set nickname color")
                    .WithArgs(new ColorArgParser("new nickname color", false))
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
                    .HandleWith(HandleNicknameColor);
                API.ChatCommands.GetOrCreate("clearnickcolor")
                    .WithDescription("Clear your nickname color")
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
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
        }

        // Skip RP-specific commands if RP chat is disabled
        if (!Config.DisableRPChat)
        {
            API.ChatCommands.GetOrCreate("me")
                .WithAlias("m")
                .WithDescription("Send a proximity emote message")
                .WithArgs(new StringArgParser("emote", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(Emote);

            API.ChatCommands.GetOrCreate("it")
                .WithDescription("Send a proximity environment message")
                .WithArgs(new StringArgParser("envMessage", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EnvironmentMessage);

            API.ChatCommands.GetOrCreate("emotemode")
                .WithDescription("Turn Emote-only mode on or off")
                .WithArgs(new BoolArgParser("mode", "on", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EmoteMode);

            API.ChatCommands.GetOrCreate("rptext")
                .WithDescription("Turn the whole RP system on or off for your messages")
                .WithArgs(new BoolArgParser("mode", "on", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(RpTextEnabled);

            API.ChatCommands.GetOrCreate("ooc")
                .WithDescription("Toggle Out-Of-Character chat mode")
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Config.OOCTogglePermission)
                .RequiresPlayer()
                .HandleWith(OOCMode);
        }

        // Always register basic chat mode commands
        API.ChatCommands.GetOrCreate("yell")
            .WithAlias("y")
            .WithDescription("Set your chat mode to Yelling, or yell a single message")
            .WithArgs(new StringArgParser("message", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Yell);

        API.ChatCommands.GetOrCreate("say")
            .WithAlias("s", "normal")
            .WithDescription("Set your chat mode back to normal, or say a single message")
            .WithArgs(new StringArgParser("message", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Say);

        API.ChatCommands.GetOrCreate("whisper")
            .WithAlias("w")
            .WithDescription("Set your chat mode to Whispering, or whisper a single message")
            .WithArgs(new StringArgParser("message", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Whisper);

        RegisterForServerSideConfig();

        // _serverNicknameChannel = API.Network.RegisterChannel("thebasics_nickname")
        //     .RegisterMessageType<TheBasicsPlayerNicknameMessage>();
    }

    private void RegisterForServerSideConfig()
    {
        _serverConfigChannel = API.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .SetMessageHandler<TheBasicsClientReadyMessage>(OnClientReady);
    }

    private void OnClientReady(IServerPlayer player, TheBasicsClientReadyMessage message)
    {
        API.Logger.Debug($"THEBASICS - Received ready message from {player.PlayerName}, sending config");
        SendClientConfig(player);
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
            if (!player.HasNicknameColor())
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You don't have a color set! You can set it with `/nickcolor [color]`",
                };
            }

            var color = player.GetNicknameColor();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Your color is: {ChatHelper.Color(color, color)}",
            };
        }

        var newColor = (Color)args.Parsers[0].GetValue();
        var colorHex = ColorTranslator.ToHtml(newColor);
        player.SetNicknameColor(colorHex);
        SwapOutNameTag(player);
        
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = $"Color set to: {ChatHelper.Color(colorHex, colorHex)}",
        };
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

        // Config will be sent when client indicates it's ready
        SwapOutNameTag(byPlayer);
    }
    
    // TODO: Not sure the client will get this data and sync it up.  Supposedly, behaviors should sync seamlessly with the client but I'm not sure that the UI renderer will refire (just like it does for chat), as the PlayerName never usually changes.  NPC names do though, maybe we can tie it in to that?
    private void SwapOutNameTag(IServerPlayer player)
    {
        var behavior = player.Entity.GetBehavior<EntityBehaviorNameTag>();

        if (Config.ShowNicknameInNametag)
        {
            var nickname = player.GetNickname();

            var displayName = Config.ShowPlayerNameInNametag ? $"{nickname} ({player.PlayerName})" : nickname;
        
            behavior.SetName(displayName);
            
            behavior.ShowOnlyWhenTargeted = Config.HideNametagUnlessTargeting;
            behavior.RenderRange = Config.NametagRenderRange;
            player.Entity.WatchedAttributes.MarkPathDirty("nametag");
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

    private MessageContext ExecuteTransformers(MessageContext context)
    {
        foreach (var transformer in _transformers)
        {
            context = transformer.Transform(context);
            if (context.State != MessageContextState.CONTINUE)
            {
                break;
            }
        }
        return context;
    }

    private string GetPlayerChat(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string message, int groupId)
    {
        // Check if message is OOC format
        if (message.StartsWith("((") && message.EndsWith("))") && sendingPlayer.GetOOCEnabled())
        {
            // Strip the (( and )) and send as OOC message
            message = message.Substring(2, message.Length - 4).Trim();
            return $"{ChatHelper.Color("#808080", $"(OOC) {sendingPlayer.PlayerName}: {message}")}";
        }

        var content = ChatHelper.GetMessage(message);
        var isEmote = content[0] == '*';
        var isGlobalOoc = Config.EnableGlobalOOC && content.StartsWith("((");
        var isOOC = !isGlobalOoc && content[0] == '(';
        var isEnvironmentMessage = content[0] == '!';

        // If Global OOC, let the message be sent out like normal
        if (isGlobalOoc)
        {
            return message;
        }

        var context = new MessageContext
        {
            Message = content,
            SendingPlayer = sendingPlayer,
            ReceivingPlayer = receivingPlayer,
            GroupId = groupId,
            Metadata = new Dictionary<string, object>()
        };

        if (isEmote)
        {
            context.Message = content.Remove(0, 1);
            context.Metadata["isEmote"] = true;
        }
        else if (isOOC)
        {
            if (!message.EndsWith(")"))
            {
                message += ")";
            }
            return message;
        }
        else if (isEnvironmentMessage)
        {
            context.Message = content.Remove(0, 1);
            context.Metadata["isEnvironmental"] = true;
        }

        context.Metadata["chatMode"] = sendingPlayer.GetChatMode();
        return ExecuteTransformers(context).Message;
    }

    private string GetFullRPMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, string content, int groupId)
    {
        var context = new MessageContext
        {
            Message = content,
            SendingPlayer = sendingPlayer,
            ReceivingPlayer = receivingPlayer,
            GroupId = groupId
        };

        return ExecuteTransformers(context).Message;
    }

    public string GetFormattedNickname(IServerPlayer player)
    {
        var name = player.GetFormattedName(true, Config);
        return Config.BoldNicknames ? ChatHelper.Strong(name) : name;
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
    
    private IServerPlayer[] GetNearbyPlayers(IServerPlayer player, ProximityChatMode? tempMode = null)
    {
        var range = GetProximityChatRange(player, tempMode);
        var nearbyPlayers = API.World.AllOnlinePlayers.Where(x =>
            x.Entity.Pos.AsBlockPos.ManhattenDistance(player.Entity.Pos.AsBlockPos) < range
            ).Cast<IServerPlayer>()
            .ToArray();
        
        var message = "";
        var lang = _languageSystem.GetSpeakingLanguage(player, _proximityChatId, ref message);
        if (lang == LanguageSystem.SignLanguage)
        {
            return nearbyPlayers.Where(nearbyPlayer => _proximityCheckUtils.CanSeePlayer(player, nearbyPlayer)).ToArray();
        }

        return nearbyPlayers;
    }

    private void SendLocalChatByPlayer(IServerPlayer byPlayer, System.Func<IServerPlayer, string> messageGenerator,
        ProximityChatMode? tempMode = null,
        EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
    {
        var nearbyPlayers = GetNearbyPlayers(byPlayer, tempMode);
        foreach (var player in nearbyPlayers)
        {
            var serverPlayer = player as IServerPlayer;
            var context = new MessageContext
            {
                SendingPlayer = byPlayer,
                ReceivingPlayer = serverPlayer,
                Message = messageGenerator(serverPlayer),
                Metadata = { ["chatMode"] = tempMode ?? byPlayer.GetChatMode() }
            };

            context = ExecuteTransformers(context);
            serverPlayer.SendMessage(_proximityChatId, context.Message, chatType, data);
        }
    }

    private int GetProximityChatRange(IServerPlayer player, ProximityChatMode? tempMode = null)
    {
        var message = "";
        var lang = _languageSystem.GetSpeakingLanguage(player, _proximityChatId, ref message);
        if (lang == LanguageSystem.SignLanguage)
        {
            return Config.SignLanguageRange;
        }

        var mode = tempMode ?? player.GetChatMode();
        return mode switch
        {
            ProximityChatMode.Normal => Config.ProximityChatModeDistances[ProximityChatMode.Normal],
            ProximityChatMode.Whisper => Config.ProximityChatModeDistances[ProximityChatMode.Whisper],
            ProximityChatMode.Yell => Config.ProximityChatModeDistances[ProximityChatMode.Yell],
            _ => Config.ProximityChatModeDistances[ProximityChatMode.Normal]
        };
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
        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = sendingPlayer,
            ReceivingPlayer = receivingPlayer,
            Metadata = { ["isEmote"] = true }
        };

        return ExecuteTransformers(context).Message;
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

    public TextCommandResult HandleEmoteCommand(TextCommandCallingArgs args, bool isEmote)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
        if (isEmote && !player.HasNickname())
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = "You need a nickname to use emotes!  You can set it with `/nick MyName`"
            };
        }

        var context = new MessageContext
        {
            Message = (string)args.Parsers[0].GetValue(),
            SendingPlayer = player,
            Metadata = { [isEmote ? "isEmote" : "isEnvironmental"] = true }
        };

        SendLocalChatByPlayer(player,
            targetPlayer =>
            {
                context.ReceivingPlayer = targetPlayer;
                return ExecuteTransformers(context).Message;
            },
            chatType: isEmote ? EnumChatType.OthersMessage : EnumChatType.Notification);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private TextCommandResult Emote(TextCommandCallingArgs args)
    {
        return HandleEmoteCommand(args, true);
    }

    private TextCommandResult EnvironmentMessage(TextCommandCallingArgs args)
    {
        return HandleEmoteCommand(args, false);
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
            StatusMessage = "Your color has been cleared.",
        };
    }

    public TextCommandResult HandleChatCommand(TextCommandCallingArgs args, ProximityChatMode mode)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
        var message = (string)args.Parsers[0].GetValue();
        var groupId = args.Caller.FromChatGroupId;
        if (!args.Parsers[0].IsMissing)
        {
            var context = new MessageContext
            {
                Message = message,
                SendingPlayer = player,
                GroupId = groupId,
                Metadata = { ["chatMode"] = mode }
            };

            SendLocalChatByPlayer(player,
                targetPlayer =>
                {
                    context.ReceivingPlayer = targetPlayer;
                    return ExecuteTransformers(context).Message;
                },
                mode);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
            };
        }

        player.SetChatMode(mode);
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = $"You are now {mode.ToString().ToLower()}.",
        };
    }

    private TextCommandResult Yell(TextCommandCallingArgs args)
    {
        return HandleChatCommand(args, ProximityChatMode.Yell);
    }

    private TextCommandResult Whisper(TextCommandCallingArgs args)
    {
        return HandleChatCommand(args, ProximityChatMode.Whisper);
    }

    private TextCommandResult Say(TextCommandCallingArgs args)
    {
        return HandleChatCommand(args, ProximityChatMode.Normal);
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

    private TextCommandResult OOCMode(TextCommandCallingArgs args)
    {
        if (!Config.AllowOOCToggle)
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = "OOC chat toggle is disabled on this server.",
            };
        }

        var player = (IServerPlayer)args.Caller.Player;
        var newMode = args.Parsers[0].IsMissing ? !player.GetOOCEnabled() : (bool)args.Parsers[0].GetValue();
        player.SetOOCEnabled(newMode);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = $"OOC chat mode {(newMode ? "enabled" : "disabled")}.",
        };
    }

    private PlayerGroup GetProximityGroup()
    {
        return API.Groups.GetPlayerGroupByName(Config.ProximityChatName);
    }
    
    private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
        Vintagestory.API.Datastructures.BoolRef consumed)
    {
        // Format the player's name regardless of channel
        bool isRoleplay = byPlayer.GetRpTextEnabled() && !Config.DisableRPChat;
        var formattedName = byPlayer.GetFormattedName(false, Config); // Always use real name as base
        if (isRoleplay && channelId == _proximityChatId) // Only use nickname in RP context and proximity chat
        {
            formattedName = byPlayer.GetFormattedName(true, Config);
        }
        message = message.Replace(byPlayer.PlayerName + ">", formattedName + ">");

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

        // If RP chat is disabled or player has disabled RP text, use simple chat
        if (Config.DisableRPChat || !byPlayer.GetRpTextEnabled())
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
}