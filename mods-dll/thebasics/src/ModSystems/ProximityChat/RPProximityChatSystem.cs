using System;
using System.Drawing;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using thebasics.Utilities.Parsers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ProximityChat;

public class RPProximityChatSystem : BaseBasicModSystem
{
    public int ProximityChatId;
    public LanguageSystem LanguageSystem;
    public DistanceObfuscationSystem DistanceObfuscationSystem;
    private IServerNetworkChannel _serverConfigChannel;
    public ProximityCheckUtils ProximityCheckUtils;
    public TransformerSystem TransformerSystem;

    protected override void BasicStartServerSide()
    {
        HookEvents();
        RegisterCommands();
        SetupProximityGroup();

        LanguageSystem = new LanguageSystem(this, API, Config);
        DistanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        ProximityCheckUtils = new ProximityCheckUtils(this, API, Config);
        TransformerSystem = new TransformerSystem(this, LanguageSystem, DistanceObfuscationSystem, ProximityCheckUtils);
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
                .WithAlias("adminnick")
                .WithAlias("adminnickname")
                .WithDescription("Admin: Get or set another player's nickname. Use 'force' before the nickname to bypass conflict warnings.")
                .WithRootAlias("adminsetnick")
                .WithArgs(new PlayerByNameOrNicknameArgParser("target player", API, true),
                    API.ChatCommands.Parsers.OptionalWordRange("force flag", "force"),
                    new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);

            API.ChatCommands.GetOrCreate("adminsetnicknamecolor")
                .WithAlias("adminsetnickcolor", "adminsetnickcol")
                .WithDescription("Admin: Get or set another player's nickname color")
                .WithArgs(new PlayerByNameOrNicknameArgParser("target player", API, true),
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
                .WithAlias("do")
                .WithDescription("Send a proximity environment message")
                .WithArgs(new StringArgParser("envMessage", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EnvironmentMessage);

            API.ChatCommands.GetOrCreate("emotemode")
                .WithDescription("Turn Emote-only mode on or off")
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EmoteMode);

            API.ChatCommands.GetOrCreate("rptext")
                .WithDescription("Turn the whole RP system on or off for your messages")
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(RpTextEnabled);

            API.ChatCommands.GetOrCreate("oocToggle")
                .WithDescription("Toggle Out-Of-Character chat mode")
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Config.OOCTogglePermission)
                .RequiresPlayer()
                .HandleWith(OOCMode);

            API.ChatCommands.GetOrCreate("ooc")
                .WithDescription("Send an Out-Of-Character message")
                .WithArgs(new StringArgParser("message", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(SendOOCMessage);

            if(Config.EnableGlobalOOC)
            {
                API.ChatCommands.GetOrCreate("gooc")
                    .WithDescription("Send a Global Out-Of-Character message")
                    .WithArgs(new StringArgParser("message", true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SendGlobalOOCMessage);
            }
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

    private TextCommandResult SendGlobalOOCMessage(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var message = (string)args.Parsers[0].GetValue();
        var groupId = args.Caller.FromChatGroupId;

        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player,
            GroupId = groupId,
            Flags = { 
                [MessageContext.IS_GLOBAL_OOC] = true,
                [MessageContext.IS_FROM_COMMAND] = true
            }
        };
        
        TransformerSystem.ProcessMessagePipeline(context, EnumChatType.OthersMessage);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private TextCommandResult SendOOCMessage(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var message = (string)args.Parsers[0].GetValue();
        var groupId = args.Caller.FromChatGroupId;

        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player,
            GroupId = groupId,
            Flags = { 
                [MessageContext.IS_OOC] = true,
                [MessageContext.IS_FROM_COMMAND] = true
            },
        };
        
        TransformerSystem.ProcessMessagePipeline(context, EnumChatType.OthersMessage);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private void RegisterForServerSideConfig()
    {
        _serverConfigChannel = API.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .RegisterMessageType<ChannelSelectedMessage>()
            .RegisterMessageType<ProximitySpeechMessage>()
            .SetMessageHandler<TheBasicsClientReadyMessage>(OnClientReady)
            .SetMessageHandler<ChannelSelectedMessage>(OnChannelSelected);
    }

    private void OnChannelSelected(IServerPlayer player, ChannelSelectedMessage message)
    {
        player.SetLastSelectedGroupId(message.GroupId);
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
        if (newColorHex.Contains('<') || newColorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = "Invalid color.",
            };
        }

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
        if (colorHex.Contains('<') || colorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = "Invalid color.",
            };
        }
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
            ProximityGroupId = ProximityChatId,
            Config = Config,
            LastSelectedGroupId = byPlayer.GetLastSelectedGroupId()
        }, byPlayer);
        
        API.Logger.Debug($"THEBASICS - Sent complete config to client {byPlayer.PlayerName}");
    }

    internal void DispatchSpeechForContext(MessageContext context)
    {
        if (_serverConfigChannel == null || !context.HasFlag(MessageContext.IS_SPEECH))
        {
            return;
        }

        if (!context.TryGetSpeechText(out var speechText) || string.IsNullOrWhiteSpace(speechText))
        {
            return;
        }

        if (context.TryGetMetadata(MessageContext.LANGUAGE, out Language lang) && lang == LanguageSystem.SignLanguage)
        {
            return;
        }

        var player = context.SendingPlayer;
        if (player == null)
        {
            return;
        }

        var (gain, falloff) = CalculateSpeechAudioParameters(context);
        _serverConfigChannel.SendPacket(new ProximitySpeechMessage
        {
            Text = speechText,
            Gain = gain,
            Falloff = falloff
        }, player);
    }

    private (float gain, float falloff) CalculateSpeechAudioParameters(MessageContext context)
    {
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        var gain = Config.RPTTS_ModeGain[mode];
        var falloff = Config.RPTTS_ModeFalloff[mode];

        return (gain, falloff);
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
            ProximityChatId = GlobalConstants.GeneralChatGroup;
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

            ProximityChatId = proximityGroup.Uid;
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

        // Handle nickname conflicts when player joins - always enforced
        var resetPlayers = NicknameValidationUtils.HandleNicknameConflictsOnJoin(byPlayer, API);
        if (resetPlayers.Count > 0)
        {
            // Log the conflicts that were resolved
            API.Logger.Notification($"THEBASICS: Player '{byPlayer.PlayerName}' joined and caused {resetPlayers.Count} nickname conflicts to be reset: {string.Join(", ", resetPlayers)}");
        }

        // Config will be sent when client indicates it's ready
        SwapOutNameTag(byPlayer);
    }

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
            
            // Validate nickname against conflicts - always enforced
            if (!NicknameValidationUtils.ValidateNickname(player, nickname, API, out string conflictingPlayer, out string conflictType))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"The nickname '{nickname}' is already taken by {conflictingPlayer} (as their {conflictType}). Please choose a different nickname.",
                };
            }
            
            player.SetNickname(nickname);
            SwapOutNameTag(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Okay, your nickname is set to {ChatHelper.Quote(VtmlUtils.EscapeVtml(nickname))}",
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

        // Check if we have a force flag (parser[1])
        bool isForced = !fullArgs.Parsers[1].IsMissing && ((string)fullArgs.Parsers[1].GetValue())?.ToLowerInvariant() == "force";
        
        // If nickname argument is missing (parser[2])
        if (fullArgs.Parsers[2].IsMissing)
        {
            if (!attemptTarget.HasNickname())
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Player {attemptTarget.PlayerName} does not have a nickname! You can set it with `/adminsetnick {attemptTarget.PlayerName} NewNickname`",
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {attemptTarget.PlayerName} nickname is: {VtmlUtils.EscapeVtml(oldNickname)}",
            };
        }
        else
        {
            var newNickname = (string)fullArgs.Parsers[2].GetValue();
            
            // Validate nickname against conflicts and show warning to admin - always enforced unless forced
            if (!isForced)
            {
                if (!NicknameValidationUtils.ValidateNickname(attemptTarget, newNickname, API, out string conflictingPlayer, out string conflictType))
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"WARNING: The nickname '{newNickname}' conflicts with {conflictingPlayer} (as their {conflictType}). " +
                                       $"This can cause confusion and weird behavior. Consider using a different nickname, or " +
                                       $"if you really want to proceed, use `/adminsetnick {attemptTarget.PlayerName} force {newNickname}` to override this warning.",
                    };
                }
            }

            attemptTarget.SetNickname(newNickname);
            SwapOutNameTag(attemptTarget);

            string forceMessage = isForced ? " (FORCED - bypassed conflict warnings)" : "";
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {attemptTarget.PlayerName} nickname has been set to: {attemptTarget.GetNicknameWithColor()}.  Old Nickname: {VtmlUtils.EscapeVtml(oldNickname)}{forceMessage}",
            };
        }
    }

    private TextCommandResult Emote(TextCommandCallingArgs args)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

        var context = new MessageContext
        {
            Message = (string)args.Parsers[0].GetValue(),
            SendingPlayer = player,
            GroupId = ProximityChatId,
            Flags = { 
                [MessageContext.IS_EMOTE] = true,
                [MessageContext.IS_FROM_COMMAND] = true
            }
        };

        // Process the entire pipeline
        TransformerSystem.ProcessMessagePipeline(context, EnumChatType.OthersMessage);

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
            GroupId = ProximityChatId,
            Flags = { 
                [MessageContext.IS_ENVIRONMENTAL] = true,
                [MessageContext.IS_FROM_COMMAND] = true
            }
        };

        // Process the entire pipeline
        TransformerSystem.ProcessMessagePipeline(context, EnumChatType.Notification);

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
                GroupId = groupId,
                Metadata =
                {
                    [MessageContext.CHAT_MODE] = mode,
                },
                Flags =
                {
                    [MessageContext.IS_PLAYER_CHAT] = true, // Mark as player chat so it goes through player transformers
                    [MessageContext.IS_FROM_COMMAND] = true
                }
            };

            // Process the entire pipeline
            TransformerSystem.ProcessMessagePipeline(context);

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
        // If no argument provided, toggle the current state
        var emoteMode = args.Parsers[0].IsMissing ? !player.GetEmoteMode() : (bool)args.Parsers[0].GetValue();
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
        // If no argument provided, toggle the current state
        var rpTextEnabled = args.Parsers[0].IsMissing ? !player.GetRpTextEnabled() : (bool)args.Parsers[0].GetValue();
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
        if(channelId != ProximityChatId)
        {
            return;
        }

        // Short circuit if RP text is disabled
        if(!byPlayer.GetRpTextEnabled())
        {
            return;
        }

        consumed.value = true;

        // Extract the content from the full message
        var content = ChatHelper.GetMessage(message);

        // Create a player chat context
        var context = new MessageContext
        {
            Message = content,
            SendingPlayer = byPlayer,
            GroupId = channelId,
            Metadata =
            {
                ["clientData"] = data,
            },
            Flags =
            {
                [MessageContext.IS_PLAYER_CHAT] = true,
            }
        };

        // Process the message through the pipeline
        TransformerSystem.ProcessMessagePipeline(context);
    }
}
