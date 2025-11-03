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

    private static string L(string key, params object[] args) => Lang.Get($"thebasics:{key}", args);
    private static string OnOffText(bool value) => L(value ? "common.on" : "common.off");
    private static string EnabledDisabledText(bool value) => L(value ? "common.enabled" : "common.disabled");
    private static string DescribeChatMode(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Yell => L("rp.chatMode.yell"),
        ProximityChatMode.Whisper => L("rp.chatMode.whisper"),
        _ => L("rp.chatMode.normal"),
    };

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
                    .WithDescription(L("rp.command.nickname.description"))
                    .WithRootAlias("nick")
                    .WithArgs(new StringArgParser(L("rp.command.nickname.arg.new"), false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SetNickname);

                API.ChatCommands.GetOrCreate("clearnick")
                    .WithDescription(L("rp.command.clearnick.description"))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(ClearNickname);
            }

            if (Config.ProximityChatAllowPlayersToChangeNicknameColors)
            {
                API.ChatCommands.GetOrCreate("nickcolor")
                    .WithAlias("nicknamecolor", "nickcol")
                    .WithDescription(L("rp.command.nickcolor.description"))
                    .WithArgs(new ColorArgParser(L("rp.command.nickcolor.arg.new"), false))
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
                    .HandleWith(HandleNicknameColor);
                API.ChatCommands.GetOrCreate("clearnickcolor")
                    .WithDescription(L("rp.command.clearnickcolor.description"))
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
                    .HandleWith(ClearNicknameColor);
            }

            API.ChatCommands.GetOrCreate("adminsetnickname")
                .WithAlias("adminsetnick")
                .WithAlias("adminnick")
                .WithAlias("adminnickname")
                .WithDescription(L("rp.command.adminsetnickname.description"))
                .WithRootAlias("adminsetnick")
                .WithArgs(new PlayerByNameOrNicknameArgParser(L("rp.command.adminsetnickname.arg.target"), API, true),
                    API.ChatCommands.Parsers.OptionalWordRange(L("rp.command.adminsetnickname.arg.force"), "force"),
                    new StringArgParser(L("rp.command.adminsetnickname.arg.new"), false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);

            API.ChatCommands.GetOrCreate("adminsetnicknamecolor")
                .WithAlias("adminsetnickcolor", "adminsetnickcol")
                .WithDescription(L("rp.command.adminsetnickcolor.description"))
                .WithArgs(new PlayerByNameOrNicknameArgParser(L("rp.command.adminsetnickcolor.arg.target"), API, true),
                    new ColorArgParser(L("rp.command.adminsetnickcolor.arg.new"), false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameColorAdmin);
        }

        // Skip RP-specific commands if RP chat is disabled
        if (!Config.DisableRPChat)
        {
            API.ChatCommands.GetOrCreate("me")
                .WithAlias("m")
                .WithDescription(L("rp.command.me.description"))
                .WithArgs(new StringArgParser(L("rp.command.me.arg"), true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(Emote);

            API.ChatCommands.GetOrCreate("it")
                .WithAlias("do")
                .WithDescription(L("rp.command.it.description"))
                .WithArgs(new StringArgParser(L("rp.command.it.arg"), true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EnvironmentMessage);

            API.ChatCommands.GetOrCreate("emotemode")
                .WithDescription(L("rp.command.emotemode.description"))
                .WithArgs(new BoolArgParser(L("rp.command.common.arg.mode"), Lang.Get("command.arg.on"), false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EmoteMode);

            API.ChatCommands.GetOrCreate("rptext")
                .WithDescription(L("rp.command.rptext.description"))
                .WithArgs(new BoolArgParser(L("rp.command.common.arg.mode"), Lang.Get("command.arg.on"), false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(RpTextEnabled);

            API.ChatCommands.GetOrCreate("oocToggle")
                .WithDescription(L("rp.command.oocToggle.description"))
                .WithArgs(new BoolArgParser(L("rp.command.common.arg.mode"), Lang.Get("command.arg.on"), false))
                .RequiresPrivilege(Config.OOCTogglePermission)
                .RequiresPlayer()
                .HandleWith(OOCMode);

            API.ChatCommands.GetOrCreate("ooc")
                .WithDescription(L("rp.command.ooc.description"))
                .WithArgs(new StringArgParser(L("rp.command.common.arg.message"), true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(SendOOCMessage);

            if(Config.EnableGlobalOOC)
            {
                API.ChatCommands.GetOrCreate("gooc")
                    .WithDescription(L("rp.command.gooc.description"))
                    .WithArgs(new StringArgParser(L("rp.command.common.arg.message"), true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SendGlobalOOCMessage);
            }
        }

        // Always register basic chat mode commands
        API.ChatCommands.GetOrCreate("yell")
            .WithAlias("y")
            .WithDescription(L("rp.command.yell.description"))
            .WithArgs(new StringArgParser(L("rp.command.common.arg.message"), false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Yell);

        API.ChatCommands.GetOrCreate("say")
            .WithAlias("s", "normal")
            .WithDescription(L("rp.command.say.description"))
            .WithArgs(new StringArgParser(L("rp.command.common.arg.message"), false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Say);

        API.ChatCommands.GetOrCreate("whisper")
            .WithAlias("w")
            .WithDescription(L("rp.command.whisper.description"))
            .WithArgs(new StringArgParser(L("rp.command.common.arg.message"), false))
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
                StatusMessage = L("common.error.playerNotFound"),
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
                    StatusMessage = L("rp.nickcolor.admin.noColor", attemptTarget.PlayerName),
                };
            }

            var color = attemptTarget.GetNicknameColor();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("rp.nickcolor.admin.current", attemptTarget.PlayerName, ChatHelper.Color(color, color)),
            };

        }

        var newNicknameColor = (Color)args.Parsers[1].GetValue();
        var newColorHex = ColorTranslator.ToHtml(newNicknameColor);
        if (newColorHex.Contains('<') || newColorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = L("rp.nickcolor.error.invalid"),
            };
        }

        attemptTarget.SetNicknameColor(newColorHex);

        SwapOutNameTag(attemptTarget);
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = L("rp.nickcolor.admin.set", attemptTarget.PlayerName, newColorHex, oldNicknameColor ?? L("rp.nickcolor.none")),
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
                    StatusMessage = L("rp.nickcolor.self.noColor"),
                };
            }

            var color = player.GetNicknameColor();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("rp.nickcolor.self.current", ChatHelper.Color(color, color)),
            };
        }

        var newColor = (Color)args.Parsers[0].GetValue();
        var colorHex = ColorTranslator.ToHtml(newColor);
        if (colorHex.Contains('<') || colorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = L("rp.nickcolor.error.invalid"),
            };
        }
        player.SetNicknameColor(colorHex);
        SwapOutNameTag(player);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = L("rp.nickcolor.self.set", ChatHelper.Color(colorHex, colorHex)),
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
                    StatusMessage = L("rp.nickname.current", player.GetNicknameWithColor()),
                };
            }
            else
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = L("rp.nickname.error.none"),
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
                    StatusMessage = L("rp.nickname.error.conflict", nickname, conflictingPlayer, conflictType),
                };
            }
            
            player.SetNickname(nickname);
            SwapOutNameTag(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("rp.nickname.success.selfSet", ChatHelper.Quote(VtmlUtils.EscapeVtml(nickname))),
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
                StatusMessage = L("common.error.playerNotFound"),
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
                    StatusMessage = L("rp.nickname.target.noNickname", attemptTarget.PlayerName),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("rp.nickname.target.current", attemptTarget.PlayerName, VtmlUtils.EscapeVtml(oldNickname)),
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
                        StatusMessage = L("rp.nickname.warning.conflict", newNickname, conflictingPlayer, conflictType, attemptTarget.PlayerName, newNickname),
                    };
                }
            }

            attemptTarget.SetNickname(newNickname);
            SwapOutNameTag(attemptTarget);

            string forceMessage = isForced ? L("rp.nickname.forceSuffix") : string.Empty;
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("rp.nickname.adminSet", attemptTarget.PlayerName, attemptTarget.GetNicknameWithColor(), VtmlUtils.EscapeVtml(oldNickname), forceMessage),
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
            StatusMessage = L("rp.nickname.success.cleared"),
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
            StatusMessage = L("rp.nickcolor.success.cleared"),
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
            StatusMessage = L("rp.chatMode.set", DescribeChatMode(mode)),
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
            StatusMessage = L("rp.emotemode.status", OnOffText(emoteMode)),
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
            StatusMessage = L("rp.rptext.status", OnOffText(rpTextEnabled)),
        };
    }

    private TextCommandResult OOCMode(TextCommandCallingArgs args)
    {
        if (!Config.AllowOOCToggle)
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = L("rp.oocToggle.error.disabled"),
            };
        }

        var player = (IServerPlayer)args.Caller.Player;
        var newMode = args.Parsers[0].IsMissing ? !player.GetOOCEnabled() : (bool)args.Parsers[0].GetValue();
        player.SetOOCEnabled(newMode);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = L("rp.oocToggle.status", EnabledDisabledText(newMode)),
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
