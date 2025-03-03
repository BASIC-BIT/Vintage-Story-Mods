using System.Drawing;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using thebasics.src.ModSystems.ProximityChat.Transformers;

namespace thebasics.ModSystems.ProximityChat;

public class RPProximityChatSystem : BaseBasicModSystem
{
    private int _proximityChatId;
    private LanguageSystem _languageSystem;
    private DistanceObfuscationSystem _distanceObfuscationSystem;
    private IServerNetworkChannel _serverConfigChannel;
    private ProximityCheckUtils _proximityCheckUtils;
    private List<IMessageTransformer> _senderPhaseTransformers;
    private List<IMessageTransformer> _recipientPhaseTransformers;

    protected override void BasicStartServerSide()
    {
        HookEvents();
        RegisterCommands();
        SetupProximityGroup();

        _languageSystem = new LanguageSystem(this, API, Config);
        _distanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        _proximityCheckUtils = new ProximityCheckUtils(this, API, Config);

        // Initialize transformers for the sender phase (validation and recipient determination)
        _senderPhaseTransformers = new List<IMessageTransformer>
        {
            // Validation transformers
            new RoleplayTransformer(this), // Add roleplay metadata
            new NicknameRequirementTransformer(this), // Require nickname if we're in RP chat
            new PlayerChatTransformer(this), // If player chat, process special modifiers
            new BabbleWarningTransformer(this, _languageSystem), // Warn if babbling

            new OOCTransformer(this),
            new EnvironmentMessageTransformer(this),
            new FormatTransformer(this),
            new EmoteTransformer(this),
            new ChatModeTransformer(this),
            new ChatTypeTransformer(this),

            // Recipient determination runs last in the sender phase
            new RecipientDeterminationTransformer(this, _languageSystem, _proximityCheckUtils),
        };

        // Initialize transformers for the recipient phase (content transformation for each recipient)
        _recipientPhaseTransformers = new List<IMessageTransformer>
        {
            // Keep only transformers that need recipient-specific processing
            new LanguageTransformer(_languageSystem),
            new ObfuscationTransformer(_distanceObfuscationSystem)
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
    }

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
    }

    private MessageContext ExecuteTransformers(MessageContext context, List<IMessageTransformer> transformers)
    {
        foreach (var transformer in transformers)
        {
            context = transformer.Transform(context);
            if (context.State != MessageContextState.CONTINUE)
            {
                break;
            }
        }
        return context;
    }

    /// <summary>
    /// Processes a message through the two-phase pipeline: sender validation followed by per-recipient processing
    /// </summary>
    public void ProcessMessagePipeline(MessageContext initialContext, EnumChatType defaultChatType = EnumChatType.OthersMessage)
    {
        // ----- PHASE 1: Process sender context (validation and recipient determination) -----
        var context = ExecuteTransformers(initialContext, _senderPhaseTransformers);

        // If processing was stopped or no recipients were determined, we're done
        if (context.State != MessageContextState.CONTINUE || context.Recipients == null || context.Recipients.Count == 0)
        {
            return;
        }

        // ----- PHASE 2: Process for each recipient (content transformation) -----
        foreach (var recipient in context.Recipients)
        {
            // Create a fresh context for this recipient
            var recipientContext = new MessageContext
            {
                Message = context.Message,
                SendingPlayer = context.SendingPlayer,
                ReceivingPlayer = recipient,
                GroupId = context.GroupId,
                Metadata = new Dictionary<string, object>(context.Metadata)
            };

            // Process only the recipient-phase transformers
            recipientContext = ExecuteTransformers(recipientContext, _recipientPhaseTransformers);

            // Skip sending if processing was stopped for this recipient
            if (recipientContext.State != MessageContextState.CONTINUE)
            {
                continue;
            }

            // Get the chat type from the context metadata or use the provided default
            var chatType = recipientContext.Metadata.ContainsKey("chatType")
                ? (EnumChatType)recipientContext.Metadata["chatType"]
                : defaultChatType;

            // Get client data if available
            string data = null;
            if (recipientContext.Metadata.ContainsKey("clientData") && recipientContext.Metadata["clientData"] is string clientData)
            {
                data = clientData;
            }

            // Send the message to this recipient
            recipient.SendMessage(GetProximityChatGroupId(), recipientContext.Message, chatType, data);
        }
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

        var context = new MessageContext
        {
            Message = (string)args.Parsers[0].GetValue(),
            SendingPlayer = player,
            ReceivingPlayer = player, // Start with sender context for validation
            GroupId = GetProximityChatGroupId(),
            Metadata = { ["isEmote"] = true }
        };

        // Process the entire pipeline
        ProcessMessagePipeline(context, EnumChatType.OthersMessage);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private TextCommandResult EnvironmentMessage(TextCommandCallingArgs args)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

        var context = new MessageContext
        {
            Message = (string)args.Parsers[0].GetValue(),
            SendingPlayer = player,
            ReceivingPlayer = player, // Start with sender context for validation
            GroupId = GetProximityChatGroupId(),
            Metadata = { ["isEnvironmental"] = true }
        };

        // Process the entire pipeline
        ProcessMessagePipeline(context, EnumChatType.Notification);

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
            // Create a context for this chat message with the specified chat mode
            var context = new MessageContext
            {
                Message = message,
                SendingPlayer = player,
                ReceivingPlayer = player, // Start with sender for validation
                GroupId = groupId,
                Metadata =
                {
                    ["chatMode"] = mode,
                    ["isPlayerChat"] = true // Mark as player chat so it goes through player transformers
                }
            };

            // Process the entire pipeline
            ProcessMessagePipeline(context);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
            };
        }

        // If no message provided, just set the player's chat mode
        player.SetChatMode(mode);
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = $"Chat mode set to: {mode.ToString().ToLower()}",
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
        if(channelId != _proximityChatId)
        {
            return;
        }

        consumed.value = true;

        // Extract the content from the full message
        var content = ChatHelper.GetMessage(message);

        // Check for global OOC without creating a new transformer
        if (PlayerChatTransformer.IsGlobalOOC(content, Config))
        {
            consumed.value = false; // Let the server handle global OOC
            return;
        }

        // Create a player chat context
        var context = new MessageContext
        {
            Message = content,
            SendingPlayer = byPlayer,
            ReceivingPlayer = byPlayer, // Start with sender for validation
            GroupId = channelId,
            Metadata =
            {
                ["isPlayerChat"] = true,
                ["chatMode"] = byPlayer.GetChatMode()
            }
        };

        if (data != null)
        {
            context.Metadata["clientData"] = data;
        }

        // Process the message through the pipeline
        ProcessMessagePipeline(context);
    }

    // Add this method to provide access to the config
    public ModConfig GetModConfig()
    {
        return Config;
    }

    // Add this method to provide access to the proximity chat group ID
    public int GetProximityChatGroupId()
    {
        return _proximityChatId;
    }

    // Add this method to provide access to the API
    public ICoreServerAPI GetAPI()
    {
        return API as ICoreServerAPI;
    }
}