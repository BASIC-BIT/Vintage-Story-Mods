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
using Vintagestory.API.Util;
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

    // Ephemeral state; do not persist.
    private readonly System.Collections.Generic.Dictionary<long, ChatTypingIndicatorState> _typingStatesByEntityId = new();

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
                    .WithDescription(Lang.Get("thebasics:chat-cmd-nickname-desc"))
                    .WithRootAlias("nick")
                    .WithArgs(new StringArgParser("new nickname", false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SetNickname);

                API.ChatCommands.GetOrCreate("clearnick")
                    .WithDescription(Lang.Get("thebasics:chat-cmd-clearnick-desc"))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(ClearNickname);
            }

            if (Config.ProximityChatAllowPlayersToChangeNicknameColors)
            {
                API.ChatCommands.GetOrCreate("nickcolor")
                    .WithAlias("nicknamecolor", "nickcol")
                    .WithDescription(Lang.Get("thebasics:chat-cmd-nickcolor-desc"))
                    .WithArgs(new ColorArgParser("new nickname color", false))
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
                    .HandleWith(HandleNicknameColor);
                API.ChatCommands.GetOrCreate("clearnickcolor")
                    .WithDescription(Lang.Get("thebasics:chat-cmd-clearnickcolor-desc"))
                    .RequiresPrivilege(Config.ChangeNicknameColorPermission)
                    .RequiresPlayer()
                    .HandleWith(ClearNicknameColor);
            }

            API.ChatCommands.GetOrCreate("adminsetnickname")
                .WithAlias("adminsetnick")
                .WithAlias("adminnick")
                .WithAlias("adminnickname")
                .WithDescription(Lang.Get("thebasics:chat-cmd-adminsetnick-desc"))
                .WithRootAlias("adminsetnick")
                .WithArgs(new PlayerByNameOrNicknameArgParser("target player", API, true),
                    API.ChatCommands.Parsers.OptionalWordRange("force flag", "force"),
                    new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);

            API.ChatCommands.GetOrCreate("adminsetnicknamecolor")
                .WithAlias("adminsetnickcolor", "adminsetnickcol")
                .WithDescription(Lang.Get("thebasics:chat-cmd-adminsetnickcolor-desc"))
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
                .WithDescription(Lang.Get("thebasics:chat-cmd-me-desc"))
                .WithArgs(new StringArgParser("emote", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(Emote);

            API.ChatCommands.GetOrCreate("it")
                .WithAlias("do")
                .WithDescription(Lang.Get("thebasics:chat-cmd-it-desc"))
                .WithArgs(new StringArgParser("envMessage", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EnvironmentMessage);

            API.ChatCommands.GetOrCreate("emotemode")
                .WithDescription(Lang.Get("thebasics:chat-cmd-emotemode-desc"))
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(EmoteMode);

            API.ChatCommands.GetOrCreate("rptext")
                .WithDescription(Lang.Get("thebasics:chat-cmd-rptext-desc"))
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Config.RPTextTogglePermission)
                .RequiresPlayer()
                .HandleWith(RpTextEnabled);

            API.ChatCommands.GetOrCreate("oocToggle")
                .WithDescription(Lang.Get("thebasics:chat-cmd-ooctoggle-desc"))
                .WithArgs(new BoolArgParser("mode", "on", false))
                .RequiresPrivilege(Config.OOCTogglePermission)
                .RequiresPlayer()
                .HandleWith(OOCMode);

            API.ChatCommands.GetOrCreate("ooc")
                    .WithDescription(Lang.Get("thebasics:chat-cmd-ooc-desc"))
                .WithArgs(new StringArgParser("message", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(SendOOCMessage);

            if(Config.EnableGlobalOOC)
            {
                API.ChatCommands.GetOrCreate("gooc")
                    .WithDescription(Lang.Get("thebasics:chat-cmd-gooc-desc"))
                    .WithArgs(new StringArgParser("message", true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(SendGlobalOOCMessage);
            }
        }

        // Chatter opt-out is always available (not gated behind DisableRPChat)
        // so players can toggle it even when RP chat formatting is disabled
        API.ChatCommands.GetOrCreate("chatter")
            .WithDescription(Lang.Get("thebasics:chat-cmd-chatter-desc"))
            .WithArgs(new BoolArgParser("mode", "on", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(ChatterToggle);

        // Always register basic chat mode commands
        API.ChatCommands.GetOrCreate("yell")
            .WithAlias("y")
            .WithDescription(Lang.Get("thebasics:chat-cmd-yell-desc"))
            .WithArgs(new StringArgParser("message", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Yell);

        API.ChatCommands.GetOrCreate("say")
            .WithAlias("s", "normal")
            .WithDescription(Lang.Get("thebasics:chat-cmd-say-desc"))
            .WithArgs(new StringArgParser("message", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(Say);

        API.ChatCommands.GetOrCreate("whisper")
            .WithAlias("w")
            .WithDescription(Lang.Get("thebasics:chat-cmd-whisper-desc"))
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
            .RegisterMessageType<ChatTypingStateMessage>()
            .RegisterMessageType<ChatterSoundMessage>()
            .SetMessageHandler<TheBasicsClientReadyMessage>(OnClientReady)
            .SetMessageHandler<ChannelSelectedMessage>(OnChannelSelected)
            .SetMessageHandler<ChatTypingStateMessage>(OnChatTypingStateMessage);
    }

    private void OnChatTypingStateMessage(IServerPlayer player, ChatTypingStateMessage message)
    {
        if (player?.Entity == null || message == null)
        {
            return;
        }

        // If the feature is disabled server-side, ignore.
        if (Config?.EnableTypingIndicator != true)
        {
            return;
        }

        var entityId = player.Entity.EntityId;
        if (entityId == 0)
        {
            return;
        }

        // Prefer the multi-state field; fall back to IsTyping for older clients.
        var state = message.State;
        if (state == ChatTypingIndicatorState.None)
        {
            state = message.IsTyping ? ChatTypingIndicatorState.Typing : ChatTypingIndicatorState.None;
        }

        if (state == ChatTypingIndicatorState.None)
        {
            _typingStatesByEntityId.Remove(entityId);
        }
        else
        {
            _typingStatesByEntityId[entityId] = state;
        }

        // Server is authoritative for EntityId and keeps fields consistent.
        message.EntityId = entityId;
        message.State = state;
        message.IsTyping = state == ChatTypingIndicatorState.Typing;

        // Best-effort broadcast; clients without this message type will silently ignore it.
        _serverConfigChannel?.BroadcastPacket(message, player);
    }

    private void OnChannelSelected(IServerPlayer player, ChannelSelectedMessage message)
    {
        player.SetLastSelectedGroupId(message.GroupId);
    }

    private void OnClientReady(IServerPlayer player, TheBasicsClientReadyMessage message)
    {
        if (Config.DebugMode)
        {
            API.Logger.Debug($"THEBASICS - Received ready message from {player.PlayerName}, sending config");
        }
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
                StatusMessage = Lang.Get("thebasics:chat-error-player-not-found"),
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
                    StatusMessage = Lang.Get("thebasics:chat-nickcolor-admin-none", attemptTarget.PlayerName),
                };
            }

            var color = attemptTarget.GetNicknameColor();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nickcolor-admin-current", attemptTarget.PlayerName, ChatHelper.Color(color, color)),
            };

        }

        var newNicknameColor = (Color)args.Parsers[1].GetValue();
        var newColorHex = ColorTranslator.ToHtml(newNicknameColor);
        if (newColorHex.Contains('<') || newColorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get("thebasics:chat-error-invalid-color"),
            };
        }

        attemptTarget.SetNicknameColor(newColorHex);

        SwapOutNameTag(attemptTarget);
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nickcolor-admin-set", attemptTarget.PlayerName, newColorHex, oldNicknameColor),
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
                    StatusMessage = Lang.Get("thebasics:chat-nickcolor-none"),
                };
            }

            var color = player.GetNicknameColor();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nickcolor-current", ChatHelper.Color(color, color)),
            };
        }

        var newColor = (Color)args.Parsers[0].GetValue();
        var colorHex = ColorTranslator.ToHtml(newColor);
        if (colorHex.Contains('<') || colorHex.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get("thebasics:chat-error-invalid-color"),
            };
        }
        player.SetNicknameColor(colorHex);
        SwapOutNameTag(player);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nickcolor-set", ChatHelper.Color(colorHex, colorHex)),
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

        if (Config.DebugMode)
        {
            API.Logger.Debug($"THEBASICS - Sent complete config to client {byPlayer.PlayerName}");
        }
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

    internal void DispatchChatterForContext(MessageContext context)
    {
        if (_serverConfigChannel == null || !Config.EnableChatter)
        {
            return;
        }

        var isSpeech = context.HasFlag(MessageContext.IS_SPEECH);
        var isEmote = context.HasFlag(MessageContext.IS_EMOTE);

        // Only chatter for speech messages and emotes with quoted speech
        if (!isSpeech && !isEmote)
        {
            return;
        }

        // Sign language is silent — no chatter
        if (context.TryGetMetadata(MessageContext.LANGUAGE, out Language lang) && lang == LanguageSystem.SignLanguage)
        {
            return;
        }

        // Determine the speech length for note count calculation
        int speechLength;
        if (isSpeech)
        {
            // Regular speech — use the full speech text
            if (!context.TryGetSpeechText(out var speechText) || string.IsNullOrWhiteSpace(speechText))
            {
                return;
            }
            speechLength = speechText.Length;
        }
        else
        {
            // Emote — extract quoted speech portions (same split logic as EmoteTransformer)
            var segments = context.Message.Split('"');
            speechLength = 0;
            for (var i = 1; i < segments.Length; i += 2)
            {
                speechLength += segments[i].Length;
            }

            // Pure narration emote (no quoted speech) — no chatter
            if (speechLength == 0)
            {
                return;
            }
        }

        var player = context.SendingPlayer;
        if (player?.Entity == null || context.Recipients == null)
        {
            return;
        }

        var mode = context.GetMetadata(MessageContext.CHAT_MODE, player.GetChatMode());
        var volume = Config.ChatterModeVolume.TryGetValue(mode, out var vol) ? vol : 0.8f;
        var pitch = Config.ChatterModePitch.TryGetValue(mode, out var p) ? p : 1.0f;

        // Use IdleShort for whisper, Idle for normal/yell
        var talkType = mode == ProximityChatMode.Whisper
            ? (int)Vintagestory.API.Util.EnumTalkType.IdleShort
            : (int)Vintagestory.API.Util.EnumTalkType.Idle;

        // Logarithmic scaling (natural log): diminishing returns on longer messages.
        // "hi" (2) -> 6, "hello there" (11) -> 10, full sentence (32) -> 13, novel (150+) -> 18
        var noteCount = 3 + (int)(3.0 * Math.Log(speechLength + 1));

        var message = new ChatterSoundMessage
        {
            EntityId = player.Entity.EntityId,
            TalkType = talkType,
            NoteCount = noteCount,
            Volume = volume,
            Pitch = pitch,
        };

        foreach (var recipient in context.Recipients)
        {
            // Skip recipients who have opted out of chatter
            if (!recipient.GetChatterEnabled())
            {
                continue;
            }

            _serverConfigChannel.SendPacket(message, recipient);
        }
    }

    private void HookEvents()
    {
        API.Event.PlayerChat += Event_PlayerChat;
        API.Event.PlayerJoin += Event_PlayerJoin;
        API.Event.PlayerDisconnect += Event_PlayerDisconnect;
    }

    private void Event_PlayerDisconnect(IServerPlayer player)
    {
        if (player?.Entity == null)
        {
            return;
        }

        var entityId = player.Entity.EntityId;
        if (entityId == 0)
        {
            return;
        }

        if (!_typingStatesByEntityId.TryGetValue(entityId, out var state) || state == ChatTypingIndicatorState.None)
        {
            return;
        }

        _typingStatesByEntityId.Remove(entityId);

        if (Config?.EnableTypingIndicator != true)
        {
            return;
        }

        _serverConfigChannel?.BroadcastPacket(new ChatTypingStateMessage
        {
            EntityId = entityId,
            IsTyping = false,
            State = ChatTypingIndicatorState.None
        });
    }

    private void SetupProximityGroup()
    {
        if (Config.UseGeneralChannelAsProximityChat)
        {
            ProximityChatId = GlobalConstants.GeneralChatGroup;
            RemoveProximityGroupIfExists();

            API.Logger.Notification("THEBASICS: UseGeneralChannelAsProximityChat=true - the General chat tab is now proximity chat. Set it to false to restore global General chat.");
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

        if (behavior == null)
        {
            return;
        }

        // Apply visibility/range settings regardless of whether we're overriding the display name.
        behavior.ShowOnlyWhenTargeted = Config.HideNametagUnlessTargeting;
        behavior.RenderRange = Config.NametagRenderRange;

        // Determine the visible nametag string.
        string displayName;
        if (Config.ShowNicknameInNametag)
        {
            var nickname = player.GetNickname();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                displayName = Config.ShowPlayerNameInNametag ? player.PlayerName : "";
            }
            else
            {
                displayName = Config.ShowPlayerNameInNametag ? $"{nickname} ({player.PlayerName})" : nickname;
            }
        }
        else
        {
            displayName = Config.ShowPlayerNameInNametag ? player.PlayerName : "";
        }

        behavior.SetName(displayName);
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
                    StatusMessage = Lang.Get("thebasics:chat-nick-current", player.GetNicknameWithColor()),
                };
            }
            else
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:chat-nick-none"),
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
                    StatusMessage = Lang.Get("thebasics:chat-nick-conflict", nickname, conflictingPlayer, conflictType),
                };
            }
            
            player.SetNickname(nickname);
            SwapOutNameTag(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nick-set", ChatHelper.Quote(VtmlUtils.EscapeVtml(nickname))),
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
                StatusMessage = Lang.Get("thebasics:chat-error-player-not-found"),
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
                    StatusMessage = Lang.Get("thebasics:chat-nick-admin-no-nick", attemptTarget.PlayerName),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nick-admin-current", attemptTarget.PlayerName, VtmlUtils.EscapeVtml(oldNickname)),
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
                        StatusMessage = Lang.Get("thebasics:chat-nick-admin-conflict-warn", newNickname, conflictingPlayer, conflictType, attemptTarget.PlayerName),
                    };
                }
            }

            attemptTarget.SetNickname(newNickname);
            SwapOutNameTag(attemptTarget);

            string forceMessage = isForced ? Lang.Get("thebasics:chat-nick-admin-forced") : "";
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nick-admin-set", attemptTarget.PlayerName, attemptTarget.GetNicknameWithColor(), VtmlUtils.EscapeVtml(oldNickname), forceMessage),
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
            StatusMessage = Lang.Get("thebasics:chat-nick-cleared"),
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
            StatusMessage = Lang.Get("thebasics:chat-nickcolor-cleared"),
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
            StatusMessage = Lang.Get("thebasics:chat-chatmode-set", mode.ToString().ToLower()),
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
            StatusMessage = Lang.Get("thebasics:chat-emotemode-set", ChatHelper.OnOff(emoteMode)),
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
            StatusMessage = Lang.Get("thebasics:chat-rptext-set", ChatHelper.OnOff(rpTextEnabled)),
        };
    }

    private TextCommandResult OOCMode(TextCommandCallingArgs args)
    {
        if (!Config.AllowOOCToggle)
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get("thebasics:chat-ooc-disabled"),
            };
        }

        var player = (IServerPlayer)args.Caller.Player;
        var newMode = args.Parsers[0].IsMissing ? !player.GetOOCEnabled() : (bool)args.Parsers[0].GetValue();
        player.SetOOCEnabled(newMode);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-ooc-set", newMode ? Lang.Get("thebasics:chat-ooc-enabled") : Lang.Get("thebasics:chat-ooc-disabled-label")),
        };
    }

    private TextCommandResult ChatterToggle(TextCommandCallingArgs args)
    {
        if (!Config.EnableChatter)
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get("thebasics:chat-chatter-disabled"),
            };
        }

        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
        var enabled = args.Parsers[0].IsMissing ? !player.GetChatterEnabled() : (bool)args.Parsers[0].GetValue();
        player.SetChatterEnabled(enabled);
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-chatter-set", ChatHelper.OnOff(enabled)),
        };
    }

    private PlayerGroup GetProximityGroup()
    {
        return API.Groups.GetPlayerGroupByName(Config.ProximityChatName);
    }

    private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
        Vintagestory.API.Datastructures.BoolRef consumed)
    {
        if (byPlayer == null || consumed == null)
        {
            return;
        }

        if(channelId != ProximityChatId)
        {
            return;
        }

        // Short circuit if RP text is disabled
        if(!byPlayer.GetRpTextEnabled())
        {
            return;
        }

        try
        {
            // Extract the content from the full message
            var content = ChatHelper.GetMessage(message);
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            // Only consume after we've validated the message — if our pipeline fails,
            // vanilla chat handles the message instead of silently swallowing it.
            consumed.value = true;

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
            TransformerSystem?.ProcessMessagePipeline(context);
        }
        catch (Exception e)
        {
            // Never crash the server on player chat.
            API.Logger.Error($"THEBASICS - Error processing proxchat message: {e}");
        }
    }
}
