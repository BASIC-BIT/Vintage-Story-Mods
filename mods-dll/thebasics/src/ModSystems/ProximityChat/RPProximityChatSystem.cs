using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HarmonyLib;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatHistory;
using thebasics.ModSystems.ChatHistory.Models;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.Notes;
using thebasics.ModSystems.Notes.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Semantics;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using thebasics.Utilities.Parsers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace thebasics.ModSystems.ProximityChat;

public class RPProximityChatSystem : BaseBasicModSystem, ITheBasicsProximityChatApi
{
    public int ProximityChatId { get; set; }
    public LanguageSystem LanguageSystem { get; set; }
    public DistanceObfuscationSystem DistanceObfuscationSystem { get; set; }
    private IServerNetworkChannel _serverConfigChannel;
    public ProximityCheckUtils ProximityCheckUtils { get; set; }
    public TransformerSystem TransformerSystem { get; set; }
    private Th3EssentialsDiscordRelay _th3EssentialsDiscordRelay;
    private readonly HashSet<string> _loggedExtensionHandlerFailures = new(StringComparer.Ordinal);
    private Harmony _serverHarmony;

    public event EventHandler<ProximityChatMessageEventArgs> ProximityChatMessageProcessed;

    public int ProximityChatGroupId => ProximityChatId;

    public string SemanticEmbeddingProviderStatus => LanguageSystem?.SemanticEmbeddingProviderStatus ?? "Language system unavailable.";

    // Ephemeral state; do not persist.
    private readonly System.Collections.Generic.Dictionary<long, ChatTypingIndicatorState> _typingStatesByEntityId = new();
    private readonly Dictionary<string, string> _languageRenameMapForJoiningPlayers = new(StringComparer.OrdinalIgnoreCase);
    private ChatPreferencesCommandHandler _chatPreferencesCommandHandler;

    protected override void BasicStartServerSide()
    {
        HookEvents();
        RegisterCommands();
        SetupProximityGroup();
        ApplyServerPatches();

        LanguageSystem = new LanguageSystem(this, API, Config);
        DistanceObfuscationSystem = new DistanceObfuscationSystem(this, API, Config);
        ProximityCheckUtils = new ProximityCheckUtils(this, API, Config);
        TransformerSystem = new TransformerSystem(this, LanguageSystem, DistanceObfuscationSystem, ProximityCheckUtils);
        _th3EssentialsDiscordRelay = new Th3EssentialsDiscordRelay(API);
        ProximityChatMessageProcessed += RelayProcessedMessageToTh3Essentials;
        ApplyManagedMapPlayerVisibilityConfig();
        RefreshAllNameTags();
    }

    internal void PublishProximityChatMessageProcessed(MessageContext context, string renderedMessage)
    {
        var handlers = ProximityChatMessageProcessed;
        if (handlers == null)
        {
            return;
        }

        var args = ProximityChatMessageEventArgs.FromContext(context, renderedMessage);
        foreach (var extensionHandler in handlers.GetInvocationList())
        {
            try
            {
                var handler = (EventHandler<ProximityChatMessageEventArgs>)extensionHandler;
                handler(this, args);
            }
            catch (Exception ex)
            {
                LogExtensionHandlerFailure(extensionHandler, ex);
            }
        }
    }

    private void RelayProcessedMessageToTh3Essentials(object sender, ProximityChatMessageEventArgs args)
    {
        _th3EssentialsDiscordRelay?.Relay(Config, args.RenderedMessage);
    }

    private void ApplyServerPatches()
    {
        _serverHarmony ??= new Harmony($"{Mod.Info.ModID}.server.proximitychat");
        ProximityLifecycleMessageFilter.Apply(_serverHarmony, API, Config, ProximityChatId);
    }

    public override void Dispose()
    {
        LanguageSystem?.DisposeLanguageServices();
        ProximityLifecycleMessageFilter.Unpatch(_serverHarmony);
        _serverHarmony = null;
        base.Dispose();
    }

    public bool RegisterSemanticEmbeddingProvider(ITheBasicsSemanticEmbeddingProvider provider)
    {
        return LanguageSystem?.RegisterSemanticEmbeddingProvider(provider) == true;
    }

    private void LogExtensionHandlerFailure(Delegate handler, Exception ex)
    {
        var handlerName = GetExtensionHandlerName(handler);
        var failureKey = $"{handlerName}|{ex.GetType().FullName}";
        if (!_loggedExtensionHandlerFailures.Add(failureKey))
        {
            return;
        }

        API?.Logger.Warning($"[THEBASICS] A proximity chat extension handler failed and was skipped: {handlerName} ({ex.GetType().Name}).");
    }

    private static string GetExtensionHandlerName(Delegate handler)
    {
        var declaringType = handler?.Method?.DeclaringType?.FullName;
        var methodName = handler?.Method?.Name;
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return "unknown handler";
        }

        return string.IsNullOrWhiteSpace(declaringType) ? methodName : $"{declaringType}.{methodName}";
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
                .WithArgs(new PlayerByNameOrNicknameArgParser("target player", API, true, Config),
                    API.ChatCommands.Parsers.OptionalWordRange("force flag", "force"),
                    new StringArgParser("new nickname", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameAdmin);

            API.ChatCommands.GetOrCreate("adminsetnicknamecolor")
                .WithAlias("adminsetnickcolor", "adminsetnickcol")
                .WithDescription(Lang.Get("thebasics:chat-cmd-adminsetnickcolor-desc"))
                .WithArgs(new PlayerByNameOrNicknameArgParser("target player", API, true, Config),
                    new ColorArgParser("new nickname color", false))
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(SetNicknameColorAdmin);
        }

        if (Config.AllowPlayersToChangeNametagColors)
        {
            API.ChatCommands.GetOrCreate("nametagbackgroundcolor")
                .WithAlias("nametagbgcolor", "nameplatebackgroundcolor", "nameplatebgcolor")
                .WithDescription(Lang.Get("thebasics:chat-cmd-nametagbgcolor-desc"))
                .WithArgs(new ColorArgParser("new nametag background color", false))
                .RequiresPrivilege(Config.ChangeNametagColorPermission)
                .RequiresPlayer()
                .HandleWith(HandleNametagBackgroundColor);
            API.ChatCommands.GetOrCreate("clearnametagbackgroundcolor")
                .WithAlias("clearnametagbgcolor", "clearnameplatebackgroundcolor", "clearnameplatebgcolor")
                .WithDescription(Lang.Get("thebasics:chat-cmd-clearnametagbgcolor-desc"))
                .RequiresPrivilege(Config.ChangeNametagColorPermission)
                .RequiresPlayer()
                .HandleWith(ClearNametagBackgroundColor);
            API.ChatCommands.GetOrCreate("nametagbordercolor")
                .WithAlias("nameplatebordercolor")
                .WithDescription(Lang.Get("thebasics:chat-cmd-nametagbordercolor-desc"))
                .WithArgs(new ColorArgParser("new nametag border color", false))
                .RequiresPrivilege(Config.ChangeNametagColorPermission)
                .RequiresPlayer()
                .HandleWith(HandleNametagBorderColor);
            API.ChatCommands.GetOrCreate("clearnametagbordercolor")
                .WithAlias("clearnameplatebordercolor")
                .WithDescription(Lang.Get("thebasics:chat-cmd-clearnametagbordercolor-desc"))
                .RequiresPrivilege(Config.ChangeNametagColorPermission)
                .RequiresPlayer()
                .HandleWith(ClearNametagBorderColor);
        }

        // Skip RP-specific commands if RP chat is disabled
        if (!Config.DisableRPChat)
        {
            API.ChatCommands.GetOrCreate("me")
                .WithAlias("m")
                .WithDescription(Lang.Get("thebasics:chat-cmd-me-desc"))
                .WithArgs(new StringArgParser("emote", false))
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

            API.ChatCommands.GetOrCreate("envhere")
                .WithAlias("dohere", "ithere")
                .WithDescription(Lang.Get("thebasics:chat-cmd-envhere-desc"))
                .WithArgs(new StringArgParser("envMessage", true))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(PlacedEnvironmentMessage);

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
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(SendOOCMessage);

            // Registered even when global OOC is off. An unknown-command error tells the player
            // nothing, and the command's own gate already refuses with a message that explains the
            // state. It also keeps registration consistent when the config is flipped live.
            API.ChatCommands.GetOrCreate("gooc")
                .WithDescription(Lang.Get("thebasics:chat-cmd-gooc-desc"))
                .WithArgs(new StringArgParser("message", false))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(SendGlobalOOCMessage);
        }

        // Chatter opt-out is always available (not gated behind DisableRPChat)
        // so players can toggle it even when RP chat formatting is disabled
        API.ChatCommands.GetOrCreate("chatter")
            .WithDescription(Lang.Get("thebasics:chat-cmd-chatter-desc"))
            .WithArgs(new BoolArgParser("mode", "on", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(ChatterToggle);

        API.ChatCommands.GetOrCreate("chatprefs")
            .WithDescription(Lang.Get("thebasics:chat-cmd-chatprefs-desc"))
            .WithArgs(new WordArgParser("setting", false), new WordArgParser("value", false), new WordArgParser("extra", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(ChatPreferences);

        API.ChatCommands.GetOrCreate("langcolor")
            .WithAlias("languagecolor", "langcolors", "languagecolors")
            .WithDescription(Lang.Get("thebasics:chat-cmd-langcolor-desc"))
            .WithArgs(new BoolArgParser("mode", "on", false))
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer()
            .HandleWith(LanguageColorPreference);

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

        RegisterAdminConfigCommands();
        RegisterForServerSideConfig();
    }

    private void RegisterAdminConfigCommands()
    {
        API.ChatCommands.GetOrCreate("thebasics")
            .WithRootAlias("tb")
            .WithRootAlias("basic")
            .WithDescription(Lang.Get("thebasics:thebasics-cmd-desc"))
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("help")
                .WithAlias("commands")
                .WithDescription(Lang.Get("thebasics:thebasics-cmd-help-desc"))
                .HandleWith(HandleTheBasicsHelpCommand)
            .EndSubCommand()
            .BeginSubCommand("guide")
                .WithAlias("handbook")
                .WithDescription(Lang.Get("thebasics:thebasics-cmd-guide-desc"))
                .HandleWith(HandleTheBasicsGuideCommand)
            .EndSubCommand()
            .BeginSubCommand("config")
                .WithDescription("Open The BASICs config panel")
                .RequiresPrivilege(Privilege.root)
                .RequiresPlayer()
                .BeginSubCommand("languages")
                    .WithAlias("language")
                    .WithDescription("Open The BASICs language config editor")
                    .RequiresPrivilege(Privilege.root)
                    .RequiresPlayer()
                    .HandleWith(HandleOpenLanguageConfigCommand)
                .EndSubCommand()
                .BeginSubCommand("charsheetfields")
                    .WithAlias("sheetfields", "biofields")
                    .WithDescription("Open The BASICs character sheet field editor")
                    .RequiresPrivilege(Privilege.root)
                    .RequiresPlayer()
                    .HandleWith(HandleOpenCharacterSheetFieldConfigCommand)
                .EndSubCommand()
                .HandleWith(HandleOpenConfigCommand)
            .EndSubCommand()
            .BeginSubCommand("charsheetfields")
                .WithAlias("sheetfields", "biofields")
                .WithDescription("Open The BASICs character sheet field editor")
                .RequiresPrivilege(Privilege.root)
                .RequiresPlayer()
                .HandleWith(HandleOpenCharacterSheetFieldConfigCommand)
            .EndSubCommand()
            .BeginSubCommand("reloadconfig")
                .WithDescription("Reload The BASICs config from disk")
                .RequiresPrivilege(Privilege.root)
                .HandleWith(HandleReloadConfigCommand)
            .EndSubCommand()
            .HandleWith(HandleTheBasicsHelpCommand);
    }

    private static TextCommandResult HandleTheBasicsHelpCommand(TextCommandCallingArgs args)
    {
        return TextCommandResult.Success(Lang.Get("thebasics:thebasics-help"));
    }

    private static TextCommandResult HandleTheBasicsGuideCommand(TextCommandCallingArgs args)
    {
        return TextCommandResult.Success(Lang.Get("thebasics:thebasics-guide-link"));
    }

    private TextCommandResult HandleOpenConfigCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("This command can only be used by a player.");
        }

        SendConfigAdminOpen(player, null);
        return TextCommandResult.Success("Opening The BASICs config panel.");
    }

    private TextCommandResult HandleOpenLanguageConfigCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("This command can only be used by a player.");
        }

        SendLanguageConfigOpen(player, null);
        return TextCommandResult.Success("Opening The BASICs language config editor.");
    }

    private TextCommandResult HandleOpenCharacterSheetFieldConfigCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("This command can only be used by a player.");
        }

        SendCharacterSheetFieldConfigOpen(player, null);
        return TextCommandResult.Success("Opening The BASICs character sheet field editor.");
    }

    private TextCommandResult HandleReloadConfigCommand(TextCommandCallingArgs args)
    {
        var before = CloneConfig(Config);

        // Refuse rather than apply defaults over a running server's settings. The file is
        // unparseable, so "reloading" it would mean discarding every live setting and telling the
        // admin it succeeded.
        if (!TryReloadSharedConfigFromDisk(API, out _))
        {
            return TextCommandResult.Error(Lang.Get("thebasics:config-reload-failed"));
        }

        var changedKeys = GetChangedConfigKeys(before, Config);
        ApplyConfigChangeSideEffects(changedKeys);
        BroadcastClientConfigs();

        if (args.Caller.Player is IServerPlayer player)
        {
            SendConfigAdminOpen(player, $"Reloaded The BASICs config from disk. Changed settings: {changedKeys.Count}.");
        }

        return TextCommandResult.Success($"Reloaded The BASICs config from disk. Changed settings: {changedKeys.Count}.");
    }

    private TextCommandResult SendGlobalOOCMessage(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;

        if (args.Parsers[0].IsMissing)
        {
            return HandleOverrideModeCommand(player, ChatOverrideMode.GlobalOoc, "gooc");
        }

        // The command stays registered when the config is flipped live, so the one-off form has to
        // ask the same predicate every other global OOC path asks. Checking EnableGlobalOOC directly
        // here would miss DisableRPChat and drift from the sticky and prefix paths again.
        if (!CanEnterOverrideMode(player, ChatOverrideMode.GlobalOoc, out var goocRefusal))
        {
            // Command-level counterpart to the failure recorded inside the gate, so a refused
            // /gooc still appears in command analytics rather than vanishing.
            AnalyticsService.TrackCommandUsed("gooc", false, "blocked", AnalyticsService.ChatProperties("gooc"));

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = goocRefusal,
            };
        }

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

        SendThroughPipeline(context, "gooc", "global_ooc", "send");

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private static TextCommandResult LanguageColorPreference(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var preferences = player.GetChatVisualPreferences();
        if (args.Parsers[0].IsMissing)
        {
            return Success(Lang.Get("thebasics:chatprefs-langcolor-status", ChatHelper.OnOff(preferences.LanguageColorsEnabled)));
        }

        preferences.LanguageColorsEnabled = (bool)args.Parsers[0].GetValue();
        player.SetChatVisualPreferences(preferences);
        AnalyticsService.TrackCommandUsed("langcolor", true);
        AnalyticsService.TrackFeatureUsed("language_colors", preferences.LanguageColorsEnabled ? "enable" : "disable");
        return Success(Lang.Get("thebasics:chatprefs-langcolor-set", ChatHelper.OnOff(preferences.LanguageColorsEnabled)));
    }

    private TextCommandResult ChatPreferences(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        _chatPreferencesCommandHandler ??= new ChatPreferencesCommandHandler(
            Config,
            identifier => LanguageSystem?.GetLangFromText(identifier, true, allowHidden: true));
        return _chatPreferencesCommandHandler.Handle(player, GetOptionalWord(args, 0), GetOptionalWord(args, 1), GetOptionalWord(args, 2));
    }

    private static string GetOptionalWord(TextCommandCallingArgs args, int index)
    {
        return args.Parsers.Count > index && !args.Parsers[index].IsMissing
            ? (string)args.Parsers[index].GetValue()
            : null;
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = message };
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private TextCommandResult SendOOCMessage(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;

        if (args.Parsers[0].IsMissing)
        {
            return HandleOverrideModeCommand(player, ChatOverrideMode.Ooc, "ooc");
        }

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

        SendThroughPipeline(context, "ooc", "ooc", "send");

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private void RegisterForServerSideConfig()
    {
        _serverConfigChannel = API.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsConfigAdminOpenMessage>()
            .RegisterMessageType<TheBasicsConfigAdminSaveMessage>()
            .RegisterMessageType<TheBasicsConfigAdminResultMessage>()
            .RegisterMessageType<TheBasicsLanguageConfigOpenRequest>()
            .RegisterMessageType<TheBasicsLanguageConfigOpenMessage>()
            .RegisterMessageType<TheBasicsLanguageConfigSaveMessage>()
            .RegisterMessageType<TheBasicsLanguageConfigResultMessage>()
            .RegisterMessageType<TheBasicsCharacterSheetFieldConfigOpenRequest>()
            .RegisterMessageType<TheBasicsCharacterSheetFieldConfigOpenMessage>()
            .RegisterMessageType<TheBasicsCharacterSheetFieldConfigSaveMessage>()
            .RegisterMessageType<TheBasicsCharacterSheetFieldConfigResultMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .RegisterMessageType<ChannelSelectedMessage>()
            .RegisterMessageType<ProximitySpeechMessage>()
            .RegisterMessageType<ChatTypingStateMessage>()
            .RegisterMessageType<ChatterSoundMessage>()
            .RegisterMessageType<PlacedEnvironmentMessage>()
            .RegisterMessageType<CharacterSheetOpenRequest>()
            .RegisterMessageType<CharacterSheetSaveRequest>()
            .RegisterMessageType<CharacterSheetViewMessage>()
            .RegisterMessageType<HeadshotUploadRequest>()
            .RegisterMessageType<HeadshotUploadResult>()
            .RegisterMessageType<HeadshotFetchRequest>()
            .RegisterMessageType<HeadshotFetchResult>()
            .RegisterMessageType<HeadshotClearRequest>()
            .RegisterMessageType<TheBasicsNotesOpenRequest>()
            .RegisterMessageType<TheBasicsNotesSaveMessage>()
            .RegisterMessageType<TheBasicsNotesViewMessage>()
            .RegisterMessageType<TheBasicsChatHistoryQueryRequest>()
            .RegisterMessageType<TheBasicsChatHistoryResultMessage>()
            .SetMessageHandler<TheBasicsClientReadyMessage>(OnClientReady)
            .SetMessageHandler<ChannelSelectedMessage>(OnChannelSelected)
            .SetMessageHandler<ChatTypingStateMessage>(OnChatTypingStateMessage)
            .SetMessageHandler<CharacterSheetOpenRequest>(OnCharacterSheetOpenRequest)
            .SetMessageHandler<CharacterSheetSaveRequest>(OnCharacterSheetSaveRequest)
            .SetMessageHandler<HeadshotUploadRequest>(OnHeadshotUploadRequest)
            .SetMessageHandler<HeadshotFetchRequest>(OnHeadshotFetchRequest)
            .SetMessageHandler<HeadshotClearRequest>(OnHeadshotClearRequest)
            .SetMessageHandler<TheBasicsLanguageConfigOpenRequest>(OnLanguageConfigOpenRequest)
            .SetMessageHandler<TheBasicsLanguageConfigSaveMessage>(OnLanguageConfigSaveMessage)
            .SetMessageHandler<TheBasicsCharacterSheetFieldConfigOpenRequest>(OnCharacterSheetFieldConfigOpenRequest)
            .SetMessageHandler<TheBasicsCharacterSheetFieldConfigSaveMessage>(OnCharacterSheetFieldConfigSaveMessage)
            .SetMessageHandler<TheBasicsNotesOpenRequest>(OnNotesOpenRequest)
            .SetMessageHandler<TheBasicsNotesSaveMessage>(OnNotesSaveMessage)
            .SetMessageHandler<TheBasicsChatHistoryQueryRequest>(OnChatHistoryQueryRequest)
            .SetMessageHandler<TheBasicsConfigAdminSaveMessage>(OnConfigAdminSaveMessage);
    }

    /// <summary>
    /// Public push: lets <c>CharacterSheetSystem</c> open the GUI bio dialog on a player from
    /// server-side chat commands without owning the network channel itself.
    /// </summary>
    public void PushCharacterSheetView(IServerPlayer viewer, CharacterSheetViewMessage view)
    {
        if (viewer == null || view == null || _serverConfigChannel == null) return;
        _serverConfigChannel.SendPacket(view, viewer);
    }

    public void PushNotesView(IServerPlayer viewer, TheBasicsNotesViewMessage view)
    {
        if (viewer == null || view == null || _serverConfigChannel == null) return;
        _serverConfigChannel.SendPacket(view, viewer);
    }

    public void PushChatHistoryResult(IServerPlayer viewer, TheBasicsChatHistoryResultMessage view)
    {
        if (viewer == null || view == null || _serverConfigChannel == null) return;
        _serverConfigChannel.SendPacket(view, viewer);
    }

    public void RecordChatHistory(MessageContext context, string formattedMessage)
    {
        API.ModLoader.GetModSystem<ChatHistorySystem>()?.RecordBasicChat(context, formattedMessage);
    }

    private void OnNotesOpenRequest(IServerPlayer player, TheBasicsNotesOpenRequest message)
    {
        var notesSystem = API.ModLoader.GetModSystem<PlayerNotesSystem>();
        var response = notesSystem?.HandleNotesOpenRequest(player, message) ?? new TheBasicsNotesViewMessage
        {
            Success = false,
            Message = Lang.Get("thebasics:notes-error-disabled")
        };
        _serverConfigChannel.SendPacket(response, player);
    }

    private void OnNotesSaveMessage(IServerPlayer player, TheBasicsNotesSaveMessage message)
    {
        var notesSystem = API.ModLoader.GetModSystem<PlayerNotesSystem>();
        var response = notesSystem?.HandleNotesSaveRequest(player, message) ?? new TheBasicsNotesViewMessage
        {
            Success = false,
            Message = Lang.Get("thebasics:notes-error-disabled")
        };
        _serverConfigChannel.SendPacket(response, player);
    }

    private void OnChatHistoryQueryRequest(IServerPlayer player, TheBasicsChatHistoryQueryRequest message)
    {
        var historySystem = API.ModLoader.GetModSystem<ChatHistorySystem>();
        var response = historySystem?.HandleGuiRequest(player, message) ?? new TheBasicsChatHistoryResultMessage
        {
            Success = false,
            Message = Lang.Get("thebasics:chat-history-disabled")
        };
        _serverConfigChannel.SendPacket(response, player);
    }

    private void OnHeadshotUploadRequest(IServerPlayer player, HeadshotUploadRequest message)
    {
        var sheetSystem = API.ModLoader.GetModSystem<thebasics.ModSystems.CharacterSheets.CharacterSheetSystem>();
        var result = sheetSystem?.HandleHeadshotUpload(player, message) ?? new HeadshotUploadResult
        {
            Success = false,
            Message = Lang.Get("thebasics:charsheet-gui-disabled"),
            TargetPlayerUid = player?.PlayerUID ?? string.Empty
        };
        _serverConfigChannel.SendPacket(result, player);
    }

    private void OnHeadshotFetchRequest(IServerPlayer player, HeadshotFetchRequest message)
    {
        var sheetSystem = API.ModLoader.GetModSystem<thebasics.ModSystems.CharacterSheets.CharacterSheetSystem>();
        var result = sheetSystem?.HandleHeadshotFetch(player, message) ?? new HeadshotFetchResult
        {
            TargetPlayerUid = message?.TargetPlayerUid ?? string.Empty
        };
        _serverConfigChannel.SendPacket(result, player);
    }

    private void OnHeadshotClearRequest(IServerPlayer player, HeadshotClearRequest message)
    {
        var sheetSystem = API.ModLoader.GetModSystem<thebasics.ModSystems.CharacterSheets.CharacterSheetSystem>();
        var result = sheetSystem?.HandleHeadshotClear(player, message) ?? new HeadshotUploadResult
        {
            Success = false,
            Message = Lang.Get("thebasics:charsheet-gui-disabled"),
            TargetPlayerUid = player?.PlayerUID ?? string.Empty
        };
        _serverConfigChannel.SendPacket(result, player);
    }

    private void OnLanguageConfigOpenRequest(IServerPlayer player, TheBasicsLanguageConfigOpenRequest message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendLanguageConfigResult(player, false, "You do not have permission to edit The BASICs languages.", LanguageConfigAdmin.BuildEntries(Config));
            return;
        }

        SendLanguageConfigOpen(player, null);
    }

    private void OnCharacterSheetFieldConfigOpenRequest(IServerPlayer player, TheBasicsCharacterSheetFieldConfigOpenRequest message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendCharacterSheetFieldConfigResult(player, false, "You do not have permission to edit The BASICs character sheet fields.", CharacterSheetFieldConfigAdmin.BuildEntries(Config));
            return;
        }

        SendCharacterSheetFieldConfigOpen(player, null);
    }

    private void OnCharacterSheetOpenRequest(IServerPlayer player, CharacterSheetOpenRequest message)
    {
        var sheetSystem = API.ModLoader.GetModSystem<CharacterSheetSystem>();
        var response = sheetSystem?.BuildClientView(player, message) ?? new CharacterSheetViewMessage
        {
            Success = false,
            Message = Lang.Get("thebasics:charsheet-gui-disabled")
        };
        _serverConfigChannel.SendPacket(response, player);
    }

    private void OnCharacterSheetSaveRequest(IServerPlayer player, CharacterSheetSaveRequest message)
    {
        var sheetSystem = API.ModLoader.GetModSystem<CharacterSheetSystem>();
        var response = sheetSystem?.SaveClientFields(player, message) ?? new CharacterSheetViewMessage
        {
            Success = false,
            Message = Lang.Get("thebasics:charsheet-gui-disabled")
        };
        _serverConfigChannel.SendPacket(response, player);
    }

    private void OnConfigAdminSaveMessage(IServerPlayer player, TheBasicsConfigAdminSaveMessage message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendConfigAdminResult(player, false, "You do not have permission to edit The BASICs config.", Array.Empty<string>());
            return;
        }

        if (TryHandleConfigAdminReload(player, message))
        {
            return;
        }

        if (!TryBuildConfigAdminDraft(message, out var draft, out var errors))
        {
            TrackConfigEditorFailure("config_admin", "save", errors.Count);
            SendConfigAdminResult(player, false, string.Join("\n", errors), Array.Empty<string>());
            return;
        }

        SaveConfigAdminDraft(player, draft);
    }

    private bool TryHandleConfigAdminReload(IServerPlayer player, TheBasicsConfigAdminSaveMessage message)
    {
        if (message?.ReloadFromDisk != true)
        {
            return false;
        }

        if (!TryReloadConfigAndGetChangedKeys(out var reloadChangedKeys))
        {
            AnalyticsService.TrackFailure("config_admin", "reload", "error", "config_unreadable");
            SendConfigAdminResult(player, false, Lang.Get("thebasics:config-reload-failed"), []);
            return true;
        }

        AnalyticsService.TrackFeatureUsed("config_admin", "reload");
        SendConfigAdminResult(player, true, $"Reloaded config from disk. Changed settings: {reloadChangedKeys.Count}.", reloadChangedKeys);
        return true;
    }

    private bool TryBuildConfigAdminDraft(TheBasicsConfigAdminSaveMessage message, out ModConfig draft, out List<string> errors)
    {
        draft = CloneConfig(Config);
        errors = ConfigAdminSaveWorkflow.ApplyValues(draft, message?.Values);
        if (errors.Count > 0)
        {
            return false;
        }

        if (message?.MarkReviewedKeys != null && message.MarkReviewedKeys.Count > 0)
        {
            ConfigAdminSaveWorkflow.MarkReviewedKeys(draft, message.MarkReviewedKeys);
        }

        errors.AddRange(ConfigAdminSettingRegistry.ValidateConfig(draft));
        return errors.Count == 0;
    }

    private void SaveConfigAdminDraft(IServerPlayer player, ModConfig draft)
    {
        var changedKeys = GetChangedConfigKeys(Config, draft);
        CopyConfigValues(draft, Config);
        SaveSharedConfig(API);
        ApplyConfigChangeSideEffects(changedKeys);
        BroadcastClientConfigs();

        var restartRequired = GetRestartRequiredKeys(changedKeys);
        AnalyticsService.TrackFeatureUsed("config_admin", "save", properties: new Dictionary<string, object>
        {
            ["changed_settings_bucket"] = AnalyticsBuckets.Count(changedKeys.Count),
            ["restart_required_settings_bucket"] = AnalyticsBuckets.Count(restartRequired.Count)
        });
        SendConfigAdminResult(player, true, ConfigAdminSaveWorkflow.BuildConfigSaveMessage(changedKeys, restartRequired), changedKeys);
    }

    private void OnLanguageConfigSaveMessage(IServerPlayer player, TheBasicsLanguageConfigSaveMessage message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendLanguageConfigResult(player, false, "You do not have permission to edit The BASICs languages.", message?.Languages ?? LanguageConfigAdmin.BuildEntries(Config));
            return;
        }

        if (TryHandleLanguageConfigReload(player, message))
        {
            return;
        }

        var submittedLanguages = message?.Languages ?? new List<LanguageConfigEntryMessage>();
        if (!TryBuildLanguageConfigDraft(submittedLanguages, out var draft, out var errors))
        {
            TrackConfigEditorFailure("language_config", "save", errors.Count);
            SendLanguageConfigResult(player, false, string.Join("\n", errors), submittedLanguages);
            return;
        }

        SaveLanguageConfigDraft(player, draft, submittedLanguages);
    }

    private bool TryHandleLanguageConfigReload(IServerPlayer player, TheBasicsLanguageConfigSaveMessage message)
    {
        if (message?.ReloadFromDisk != true)
        {
            return false;
        }

        if (!TryReloadConfigAndGetChangedKeys(out var changedKeys))
        {
            AnalyticsService.TrackFailure("language_config", "reload", "error", "config_unreadable");
            SendLanguageConfigResult(player, false, Lang.Get("thebasics:config-reload-failed"), LanguageConfigAdmin.BuildEntries(Config));
            return true;
        }

        AnalyticsService.TrackFeatureUsed("language_config", "reload");
        SendLanguageConfigResult(player, true, $"Reloaded language config from disk. Changed settings: {changedKeys.Count}.", LanguageConfigAdmin.BuildEntries(Config));
        return true;
    }

    private bool TryBuildLanguageConfigDraft(List<LanguageConfigEntryMessage> submittedLanguages, out ModConfig draft, out List<string> errors)
    {
        draft = CloneConfig(Config);
        if (!LanguageConfigAdmin.TryApplyEntries(draft, submittedLanguages, out errors))
        {
            return false;
        }

        errors.AddRange(ConfigAdminSettingRegistry.ValidateConfig(draft));
        return errors.Count == 0;
    }

    private void SaveLanguageConfigDraft(IServerPlayer player, ModConfig draft, List<LanguageConfigEntryMessage> submittedLanguages)
    {
        var renameMap = LanguageConfigAdmin.BuildRenameMap(submittedLanguages);
        TrackLanguageRenamesForJoiningPlayers(renameMap);
        CopyConfigValues(draft, Config);
        SaveSharedConfig(API);

        var changedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(Config.Languages) };
        ReconcileOnlinePlayerLanguages(renameMap);
        ReconcileStoredPlayerLanguages(renameMap);
        ApplyConfigChangeSideEffects(changedKeys);
        BroadcastClientConfigs();

        AnalyticsService.TrackFeatureUsed("language_config", "save", properties: new Dictionary<string, object>
        {
            ["language_count_bucket"] = AnalyticsBuckets.Count(Config.Languages?.Count ?? 0)
        });
        SendLanguageConfigResult(
            player,
            true,
            "Saved The BASICs language config. Online players were reconciled; renamed languages also reconcile for later joins until the next server restart.",
            LanguageConfigAdmin.BuildEntries(Config));
    }

    private void OnCharacterSheetFieldConfigSaveMessage(IServerPlayer player, TheBasicsCharacterSheetFieldConfigSaveMessage message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendCharacterSheetFieldConfigResult(player, false, "You do not have permission to edit The BASICs character sheet fields.", message?.Fields ?? CharacterSheetFieldConfigAdmin.BuildEntries(Config));
            return;
        }

        if (TryHandleCharacterSheetFieldConfigReload(player, message))
        {
            return;
        }

        var submittedFields = message?.Fields ?? new List<CharacterSheetFieldConfigEntryMessage>();
        if (!TryBuildCharacterSheetFieldConfigDraft(submittedFields, out var draft, out var errors))
        {
            TrackConfigEditorFailure("character_sheet_fields", "save", errors.Count);
            SendCharacterSheetFieldConfigResult(player, false, string.Join("\n", errors), submittedFields);
            return;
        }

        SaveCharacterSheetFieldConfigDraft(player, draft);
    }

    private bool TryHandleCharacterSheetFieldConfigReload(IServerPlayer player, TheBasicsCharacterSheetFieldConfigSaveMessage message)
    {
        if (message?.ReloadFromDisk != true)
        {
            return false;
        }

        if (!TryReloadConfigAndGetChangedKeys(out var changedKeys))
        {
            AnalyticsService.TrackFailure("character_sheet_fields", "reload", "error", "config_unreadable");
            SendCharacterSheetFieldConfigResult(player, false, Lang.Get("thebasics:config-reload-failed"), CharacterSheetFieldConfigAdmin.BuildEntries(Config));
            return true;
        }

        AnalyticsService.TrackFeatureUsed("character_sheet_fields", "reload");
        SendCharacterSheetFieldConfigResult(player, true, $"Reloaded character sheet fields from disk. Changed settings: {changedKeys.Count}.", CharacterSheetFieldConfigAdmin.BuildEntries(Config));
        return true;
    }

    private bool TryBuildCharacterSheetFieldConfigDraft(List<CharacterSheetFieldConfigEntryMessage> submittedFields, out ModConfig draft, out List<string> errors)
    {
        draft = CloneConfig(Config);
        if (!CharacterSheetFieldConfigAdmin.TryApplyEntries(draft, submittedFields, out errors))
        {
            return false;
        }

        errors.AddRange(ConfigAdminSettingRegistry.ValidateConfig(draft));
        return errors.Count == 0;
    }

    private void SaveCharacterSheetFieldConfigDraft(IServerPlayer player, ModConfig draft)
    {
        CopyConfigValues(draft, Config);
        SaveSharedConfig(API);

        var changedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(Config.CharacterSheetFields) };
        ApplyConfigChangeSideEffects(changedKeys);
        BroadcastClientConfigs();

        AnalyticsService.TrackFeatureUsed("character_sheet_fields", "save", properties: new Dictionary<string, object>
        {
            ["field_count_bucket"] = AnalyticsBuckets.Count(Config.CharacterSheetFields?.Count ?? 0)
        });
        SendCharacterSheetFieldConfigResult(player, true, "Saved The BASICs character sheet field config.", CharacterSheetFieldConfigAdmin.BuildEntries(Config));
    }

    private static void TrackConfigEditorFailure(string featureName, string action, int errorCount)
    {
        var properties = new Dictionary<string, object>
        {
            ["error_count_bucket"] = AnalyticsBuckets.Count(errorCount)
        };

        AnalyticsService.TrackFeatureUsed(featureName, action, false, "validation_failed", properties);
        AnalyticsService.TrackFailure(featureName, action, "warning", "validation_failed", properties: properties);
    }

    /// <summary>
    /// False when the file on disk could not be read. A Try shape rather than an empty set, because
    /// "no settings changed" is a legitimate successful reload and must stay distinguishable from
    /// "nothing was reloaded"; conflating them is what let the editors report success on a parse
    /// failure.
    /// </summary>
    private bool TryReloadConfigAndGetChangedKeys(out HashSet<string> changedKeys)
    {
        var before = CloneConfig(Config);
        if (!TryReloadSharedConfigFromDisk(API, out _))
        {
            changedKeys = [];
            return false;
        }

        changedKeys = GetChangedConfigKeys(before, Config);
        ApplyConfigChangeSideEffects(changedKeys);
        BroadcastClientConfigs();
        return true;
    }

    private void OnChatTypingStateMessage(IServerPlayer player, ChatTypingStateMessage message)
    {
        if (!TryPrepareTypingStateBroadcast(player, message, out var entityId, out var state))
        {
            return;
        }

        UpdateTypingState(entityId, state);
        BroadcastTypingState(player, message, entityId, state);
    }

    private bool TryPrepareTypingStateBroadcast(IServerPlayer player, ChatTypingStateMessage message, out long entityId, out ChatTypingIndicatorState state)
    {
        entityId = player?.Entity?.EntityId ?? 0;
        state = ChatTypingIndicatorState.None;

        if (message == null || entityId == 0 || Config?.EnableTypingIndicator != true)
        {
            return false;
        }

        state = NormalizeTypingIndicatorState(message.State, message.IsTyping);
        return true;
    }

    internal static ChatTypingIndicatorState NormalizeTypingIndicatorState(ChatTypingIndicatorState state, bool isTyping)
    {
        return state == ChatTypingIndicatorState.None && isTyping
            ? ChatTypingIndicatorState.Typing
            : state;
    }

    private void UpdateTypingState(long entityId, ChatTypingIndicatorState state)
    {
        if (state == ChatTypingIndicatorState.None)
        {
            _typingStatesByEntityId.Remove(entityId);
            return;
        }

        _typingStatesByEntityId[entityId] = state;
    }

    private void BroadcastTypingState(IServerPlayer player, ChatTypingStateMessage message, long entityId, ChatTypingIndicatorState state)
    {
        // Server is authoritative for EntityId and keeps fields consistent.
        message.EntityId = entityId;
        message.State = state;
        message.IsTyping = state == ChatTypingIndicatorState.Typing;

        // Best-effort broadcast; clients without this message type will silently ignore it.
        _serverConfigChannel?.BroadcastPacket(message, player);
    }

    private static void OnChannelSelected(IServerPlayer player, ChannelSelectedMessage message)
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
        var newColorHex = ColorToHex(newNicknameColor);
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
        AnalyticsService.TrackCommandUsed("adminsetnicknamecolor", true);
        AnalyticsService.TrackFeatureUsed("nickname_color", "admin_set");
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
        var colorHex = ColorToHex(newColor);
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
        AnalyticsService.TrackCommandUsed("nickcolor", true);
        AnalyticsService.TrackFeatureUsed("nickname_color", "set");

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nickcolor-set", ChatHelper.Color(colorHex, colorHex)),
        };
    }

    private TextCommandResult HandleNametagBackgroundColor(TextCommandCallingArgs args)
    {
        return HandleNametagColor(args, isBackground: true);
    }

    private TextCommandResult HandleNametagBorderColor(TextCommandCallingArgs args)
    {
        return HandleNametagColor(args, isBackground: false);
    }

    private TextCommandResult HandleNametagColor(TextCommandCallingArgs args, bool isBackground)
    {
        var player = (IServerPlayer)args.Caller.Player;
        if (args.Parsers[0].IsMissing)
        {
            return GetNametagColorStatus(player, isBackground);
        }

        var color = ColorToHex((Color)args.Parsers[0].GetValue());
        if (color.Contains('<') || color.Contains('>'))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get("thebasics:chat-error-invalid-color"),
            };
        }

        if (isBackground)
        {
            player.SetNametagBackgroundColor(color);
        }
        else
        {
            player.SetNametagBorderColor(color);
        }

        SwapOutNameTag(player);
        AnalyticsService.TrackCommandUsed(isBackground ? "nametagbackgroundcolor" : "nametagbordercolor", true);
        AnalyticsService.TrackFeatureUsed("nametag_style", isBackground ? "set_background" : "set_border");

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get(isBackground ? "thebasics:chat-nametag-bgcolor-set" : "thebasics:chat-nametag-bordercolor-set", ChatHelper.Color(color, color)),
        };
    }

    private static TextCommandResult GetNametagColorStatus(IServerPlayer player, bool isBackground)
    {
        var color = isBackground ? player.GetNametagBackgroundColor() : player.GetNametagBorderColor();
        if (string.IsNullOrWhiteSpace(color))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get(isBackground ? "thebasics:chat-nametag-bgcolor-none" : "thebasics:chat-nametag-bordercolor-none"),
            };
        }

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get(isBackground ? "thebasics:chat-nametag-bgcolor-current" : "thebasics:chat-nametag-bordercolor-current", ChatHelper.Color(color, color)),
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

    private void BroadcastClientConfigs()
    {
        foreach (var onlinePlayer in API.World.AllOnlinePlayers)
        {
            if (onlinePlayer is IServerPlayer serverPlayer)
            {
                SendClientConfig(serverPlayer);
            }
        }
    }

    private void SendConfigAdminOpen(IServerPlayer player, string statusMessage)
    {
        _serverConfigChannel?.SendPacket(new TheBasicsConfigAdminOpenMessage
        {
            Config = Config,
            Values = GetConfigAdminValues(Config),
            ReviewedKeys = (Config.ReviewedConfigSettingKeys ?? Array.Empty<string>()).ToList(),
            StatusMessage = statusMessage
        }, player);
    }

    private void SendLanguageConfigOpen(IServerPlayer player, string message)
    {
        _serverConfigChannel?.SendPacket(new TheBasicsLanguageConfigOpenMessage
        {
            Success = true,
            Message = message,
            Languages = LanguageConfigAdmin.BuildEntries(Config)
        }, player);
    }

    private void SendCharacterSheetFieldConfigOpen(IServerPlayer player, string message)
    {
        _serverConfigChannel?.SendPacket(new TheBasicsCharacterSheetFieldConfigOpenMessage
        {
            Success = true,
            Message = message,
            Fields = CharacterSheetFieldConfigAdmin.BuildEntries(Config)
        }, player);
    }

    private void SendLanguageConfigResult(IServerPlayer player, bool success, string message, IEnumerable<LanguageConfigEntryMessage> languages)
    {
        if (player == null)
        {
            return;
        }

        _serverConfigChannel?.SendPacket(new TheBasicsLanguageConfigResultMessage
        {
            Success = success,
            Message = message,
            Languages = (languages ?? LanguageConfigAdmin.BuildEntries(Config)).ToList()
        }, player);
    }

    private void SendCharacterSheetFieldConfigResult(IServerPlayer player, bool success, string message, IEnumerable<CharacterSheetFieldConfigEntryMessage> fields)
    {
        if (player == null)
        {
            return;
        }

        _serverConfigChannel?.SendPacket(new TheBasicsCharacterSheetFieldConfigResultMessage
        {
            Success = success,
            Message = message,
            Fields = (fields ?? CharacterSheetFieldConfigAdmin.BuildEntries(Config)).ToList()
        }, player);
    }

    private void SendConfigAdminResult(IServerPlayer player, bool success, string message, IReadOnlyCollection<string> changedKeys)
    {
        if (player == null)
        {
            return;
        }

        changedKeys ??= [];
        var restartRequired = GetRestartRequiredKeys(changedKeys);
        var liveApplied = changedKeys.Where(key => !restartRequired.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();

        _serverConfigChannel?.SendPacket(new TheBasicsConfigAdminResultMessage
        {
            Success = success,
            Message = message,
            Config = Config,
            Values = GetConfigAdminValues(Config),
            ReviewedKeys = (Config.ReviewedConfigSettingKeys ?? Array.Empty<string>()).ToList(),
            LiveAppliedKeys = liveApplied,
            RestartRequiredKeys = restartRequired
        }, player);
    }

    private static List<ConfigAdminSettingValue> GetConfigAdminValues(ModConfig config)
    {
        return ConfigAdminSettingRegistry.Settings
            .Select(setting => new ConfigAdminSettingValue
            {
                Key = setting.Key,
                Value = setting.GetValue(config)
            })
            .ToList();
    }

    private static HashSet<string> GetChangedConfigKeys(ModConfig before, ModConfig after)
    {
        return ConfigAdminSettingRegistry.Settings
            .Where(setting => !string.Equals(setting.GetValue(before), setting.GetValue(after), StringComparison.Ordinal))
            .Select(setting => setting.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> GetRestartRequiredKeys(IEnumerable<string> changedKeys)
    {
        return changedKeys
            .Where(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) && setting.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override void OnConfigReloaded(IReadOnlySet<string> changedKeys)
    {
        if (changedKeys.Contains(nameof(Config.ChangeNicknameColorPermission)) ||
            changedKeys.Contains(nameof(Config.ChangeNametagColorPermission)) ||
            changedKeys.Contains(nameof(Config.RPTextTogglePermission)) ||
            changedKeys.Contains(nameof(Config.OOCTogglePermission)) ||
            changedKeys.Contains(nameof(Config.ChangeOwnLanguagePermission)) ||
            changedKeys.Contains(nameof(Config.ChangeOtherLanguagePermission)))
        {
            RefreshCommandPrivileges();
        }
    }

    private void RefreshCommandPrivileges()
    {
        SetCommandPrivilege("nickcolor", Config.ChangeNicknameColorPermission);
        SetCommandPrivilege("clearnickcolor", Config.ChangeNicknameColorPermission);
        SetCommandPrivilege("nametagbackgroundcolor", Config.ChangeNametagColorPermission);
        SetCommandPrivilege("clearnametagbackgroundcolor", Config.ChangeNametagColorPermission);
        SetCommandPrivilege("nametagbordercolor", Config.ChangeNametagColorPermission);
        SetCommandPrivilege("clearnametagbordercolor", Config.ChangeNametagColorPermission);
        SetCommandPrivilege("rptext", Config.RPTextTogglePermission);
        SetCommandPrivilege("oocToggle", Config.OOCTogglePermission);

        SetCommandPrivilege("addlang", Config.ChangeOwnLanguagePermission);
        SetCommandPrivilege("removelang", Config.ChangeOwnLanguagePermission);
        SetCommandPrivilege("adminaddlang", Config.ChangeOtherLanguagePermission);
        SetCommandPrivilege("adminremovelang", Config.ChangeOtherLanguagePermission);
        SetCommandPrivilege("adminlistlang", Config.ChangeOtherLanguagePermission);
    }

    private void SetCommandPrivilege(string commandName, string privilege)
    {
        API.ChatCommands.Get(commandName)?.RequiresPrivilege(privilege);
    }

    private void ApplyConfigChangeSideEffects(IReadOnlySet<string> changedKeys)
    {
        NotifyConfigReloaded(changedKeys);

        if (IsMapVisibilityConfigChange(changedKeys))
        {
            ApplyManagedMapPlayerVisibilityConfig();
        }

        if (IsNametagConfigChange(changedKeys))
        {
            RefreshAllNameTags();
        }

        if (changedKeys.Contains(nameof(Config.EnableTypingIndicator)) && !Config.EnableTypingIndicator)
        {
            ClearTypingIndicators();
        }
    }

    private static bool IsMapVisibilityConfigChange(IReadOnlySet<string> changedKeys)
    {
        return changedKeys.Contains(nameof(Config.ManageMapPlayerVisibility)) ||
               changedKeys.Contains(nameof(Config.MapHideOtherPlayers)) ||
               changedKeys.Contains(nameof(Config.MapPlayerRenderDistance));
    }

    private static bool IsNametagConfigChange(IReadOnlySet<string> changedKeys)
    {
        return changedKeys.Contains(nameof(Config.ShowNicknameInNametag)) ||
               changedKeys.Contains(nameof(Config.ShowPlayerNameInNametag)) ||
               changedKeys.Contains(nameof(Config.HideNametagUnlessTargeting)) ||
               changedKeys.Contains(nameof(Config.NametagRenderRange)) ||
               changedKeys.Contains(nameof(Config.NametagBackgroundColor)) ||
               changedKeys.Contains(nameof(Config.NametagBorderColor)) ||
               changedKeys.Contains(nameof(Config.CharacterSheetFields));
    }

    private void ApplyManagedMapPlayerVisibilityConfig()
    {
        MapPlayerVisibilityConfigApplier.Apply(Config, API?.WorldManager?.SaveGame?.WorldConfiguration ?? API?.World?.Config);
    }

    private void ReconcileOnlinePlayerLanguages(IReadOnlyDictionary<string, string> renameMap)
    {
        foreach (var onlinePlayer in API.World.AllOnlinePlayers)
        {
            if (onlinePlayer is not IServerPlayer serverPlayer)
            {
                continue;
            }

            ReconcilePlayerLanguages(serverPlayer, Config, renameMap);
        }
    }

    private void ReconcileStoredPlayerLanguages(IReadOnlyDictionary<string, string> renameMap)
    {
        if (API.PlayerData is not PlayerDataManager playerDataManager)
        {
            return;
        }

        var onlinePlayerIds = new HashSet<string>(API.World.AllOnlinePlayers.Select(player => player.PlayerUID), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in playerDataManager.WorldDataByUID)
        {
            var playerData = entry.Value;
            if (playerData == null || onlinePlayerIds.Contains(entry.Key))
            {
                continue;
            }

            ReconcileStoredPlayerLanguages(playerData, Config, renameMap);
        }
    }

    internal static void ReconcileStoredPlayerLanguages(IWorldPlayerData playerData, ModConfig config, IReadOnlyDictionary<string, string> renameMap)
    {
        playerData.InstantiateLanguagesIfNotExist(config);
        var languagesByName = LanguageCatalog.GetReconciliationMap(config);
        var currentNames = playerData.GetLanguages();
        var reconciledNames = ReconcileLanguageNames(currentNames, renameMap, languagesByName);

        if (!currentNames.SequenceEqual(reconciledNames, StringComparer.OrdinalIgnoreCase))
        {
            playerData.SetLanguages(reconciledNames);
        }

        ReconcileStoredLanguageSkills(playerData, renameMap, languagesByName, reconciledNames);
        ReconcileStoredSemanticLanguageMemory(playerData, renameMap, languagesByName, reconciledNames);
        playerData.SetDefaultLanguage(ResolveReconciledDefaultLanguage(
            playerData.GetDefaultLanguageName(),
            renameMap,
            languagesByName,
            reconciledNames));
    }

    private static void ReconcileStoredLanguageSkills(IWorldPlayerData playerData, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var currentSkills = playerData.GetLanguageSkills();
        var reconciledSkills = ReconcileLanguageSkills(currentSkills, renameMap, languagesByName, knownNames);
        if (!LanguageSkillsEqual(currentSkills, reconciledSkills))
        {
            playerData.SetLanguageSkills(reconciledSkills);
        }
    }

    internal static void ReconcilePlayerLanguages(IServerPlayer serverPlayer, ModConfig config, IReadOnlyDictionary<string, string> renameMap)
    {
        serverPlayer.InstantiateLanguagesIfNotExist(config);
        var languagesByName = LanguageCatalog.GetReconciliationMap(config);
        var currentNames = serverPlayer.GetLanguages();
        var reconciledNames = ReconcileLanguageNames(currentNames, renameMap, languagesByName);

        if (!currentNames.SequenceEqual(reconciledNames, StringComparer.OrdinalIgnoreCase))
        {
            serverPlayer.SetLanguages(reconciledNames);
        }

        ReconcilePlayerLanguageSkills(serverPlayer, renameMap, languagesByName, reconciledNames);
        ReconcilePlayerSemanticLanguageMemory(serverPlayer, renameMap, languagesByName, reconciledNames);
        serverPlayer.SetDefaultLanguage(ResolveReconciledDefaultLanguage(
            serverPlayer.GetDefaultLanguageName(),
            renameMap,
            languagesByName,
            reconciledNames));
    }

    internal static List<string> ReconcileLanguageNames(
        IEnumerable<string> currentNames,
        IReadOnlyDictionary<string, string> renameMap,
        IReadOnlyDictionary<string, Language> languagesByName)
    {
        var reconciledNames = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var currentName in currentNames ?? Array.Empty<string>())
        {
            var candidate = ResolveLanguageCandidate(currentName, renameMap, languagesByName);
            if (string.IsNullOrWhiteSpace(candidate) ||
                !languagesByName.TryGetValue(candidate, out var language) ||
                !seenNames.Add(language.Name))
            {
                continue;
            }

            reconciledNames.Add(language.Name);
        }

        return reconciledNames;
    }

    internal static string ResolveLanguageCandidate(
        string currentName,
        IReadOnlyDictionary<string, string> renameMap,
        IReadOnlyDictionary<string, Language> languagesByName)
    {
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return null;
        }

        if (string.Equals(currentName, LanguageSystem.SignLanguage.Name, StringComparison.OrdinalIgnoreCase))
        {
            return LanguageSystem.SignLanguage.Name;
        }

        if (renameMap != null && renameMap.TryGetValue(currentName, out var renamed))
        {
            return renamed;
        }

        return languagesByName.TryGetValue(currentName, out var currentLanguage)
            ? currentLanguage.Name
            : currentName;
    }

    internal static Language ResolveReconciledDefaultLanguage(
        string defaultLanguageName,
        IReadOnlyDictionary<string, string> renameMap,
        IReadOnlyDictionary<string, Language> languagesByName,
        IReadOnlyList<string> reconciledNames)
    {
        if (string.Equals(defaultLanguageName, LanguageSystem.BabbleLang.Name, StringComparison.OrdinalIgnoreCase))
        {
            return LanguageSystem.BabbleLang;
        }

        var knownNames = new HashSet<string>(reconciledNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var candidate = ResolveLanguageCandidate(defaultLanguageName, renameMap, languagesByName);
        if (!string.IsNullOrWhiteSpace(candidate) &&
            languagesByName.TryGetValue(candidate, out var defaultLanguage) &&
            knownNames.Contains(defaultLanguage.Name))
        {
            return defaultLanguage;
        }

        foreach (var knownName in reconciledNames ?? Array.Empty<string>())
        {
            if (languagesByName.TryGetValue(knownName, out var fallbackLanguage))
            {
                return fallbackLanguage;
            }
        }

        return LanguageSystem.BabbleLang;
    }

    private static void ReconcileStoredSemanticLanguageMemory(IWorldPlayerData playerData, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var currentMemory = playerData.GetSemanticLanguageMemory();
        var reconciledMemory = ReconcileSemanticLanguageMemory(currentMemory, renameMap, languagesByName, knownNames);
        playerData.SetSemanticLanguageMemory(reconciledMemory);
    }

    private static void ReconcilePlayerSemanticLanguageMemory(IServerPlayer serverPlayer, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var currentMemory = serverPlayer.GetSemanticLanguageMemory();
        var reconciledMemory = ReconcileSemanticLanguageMemory(currentMemory, renameMap, languagesByName, knownNames);
        serverPlayer.SetSemanticLanguageMemory(reconciledMemory);
    }

    internal static SemanticLanguageMemoryStore ReconcileSemanticLanguageMemory(SemanticLanguageMemoryStore currentMemory, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var reconciled = new SemanticLanguageMemoryStore();
        foreach (var languageMemory in currentMemory?.Languages ?? new List<SemanticLanguageMemory>())
        {
            if (string.IsNullOrWhiteSpace(languageMemory.LanguageName))
            {
                continue;
            }

            var candidate = ResolveLanguageCandidate(languageMemory.LanguageName, renameMap, languagesByName);
            if (!languagesByName.TryGetValue(candidate, out var language))
            {
                continue;
            }

            reconciled.Languages.Add(new SemanticLanguageMemory
            {
                LanguageName = language.Name,
                LastLearningObservationUnixSeconds = languageMemory.LastLearningObservationUnixSeconds,
                AtlasBuckets = languageMemory.AtlasBuckets ?? new List<SemanticLanguageAtlasBucketCoverage>()
            });
        }

        return IServerPlayerExtensions.NormalizeSemanticLanguageMemory(reconciled, new HashSet<string>(knownNames, StringComparer.OrdinalIgnoreCase));
    }

    private static void ReconcilePlayerLanguageSkills(IServerPlayer serverPlayer, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var currentSkills = serverPlayer.GetLanguageSkills();
        var reconciledSkills = ReconcileLanguageSkills(currentSkills, renameMap, languagesByName, knownNames);
        if (!LanguageSkillsEqual(currentSkills, reconciledSkills))
        {
            serverPlayer.SetLanguageSkills(reconciledSkills);
        }
    }

    private static Dictionary<string, int> ReconcileLanguageSkills(IDictionary<string, int> currentSkills, IReadOnlyDictionary<string, string> renameMap, IReadOnlyDictionary<string, Language> languagesByName, IReadOnlyList<string> knownNames)
    {
        var known = new HashSet<string>(knownNames, StringComparer.OrdinalIgnoreCase);
        var reconciled = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in currentSkills ?? new Dictionary<string, int>())
        {
            var candidate = ResolveLanguageCandidate(entry.Key, renameMap, languagesByName);
            if (!languagesByName.TryGetValue(candidate, out var language) || known.Contains(language.Name))
            {
                continue;
            }

            var skill = Math.Max(0, Math.Min(100, entry.Value));
            if (skill <= 0)
            {
                continue;
            }

            reconciled[language.Name] = reconciled.TryGetValue(language.Name, out var existingSkill)
                ? Math.Max(existingSkill, skill)
                : skill;
        }

        return reconciled;
    }

    private static bool LanguageSkillsEqual(IReadOnlyDictionary<string, int> currentSkills, IReadOnlyDictionary<string, int> reconciledSkills)
    {
        if ((currentSkills?.Count ?? 0) != (reconciledSkills?.Count ?? 0))
        {
            return false;
        }

        foreach (var entry in reconciledSkills)
        {
            if (currentSkills == null || !currentSkills.TryGetValue(entry.Key, out var currentSkill) || currentSkill != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    private void TrackLanguageRenamesForJoiningPlayers(IReadOnlyDictionary<string, string> renameMap)
    {
        if (renameMap.Count == 0)
        {
            return;
        }

        foreach (var key in _languageRenameMapForJoiningPlayers.Keys.ToArray())
        {
            if (renameMap.TryGetValue(_languageRenameMapForJoiningPlayers[key], out var renamedAgain))
            {
                _languageRenameMapForJoiningPlayers[key] = renamedAgain;
            }
        }

        foreach (var rename in renameMap)
        {
            if (string.Equals(rename.Key, rename.Value, StringComparison.OrdinalIgnoreCase))
            {
                _languageRenameMapForJoiningPlayers.Remove(rename.Key);
                continue;
            }

            _languageRenameMapForJoiningPlayers[rename.Key] = rename.Value;
        }
    }

    private void ClearTypingIndicators()
    {
        foreach (var entityId in _typingStatesByEntityId.Keys.ToArray())
        {
            _serverConfigChannel?.BroadcastPacket(new ChatTypingStateMessage
            {
                EntityId = entityId,
                IsTyping = false,
                State = ChatTypingIndicatorState.None
            });
        }

        _typingStatesByEntityId.Clear();
    }

    private void ClearTypingIndicator(IServerPlayer player)
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
        var gain = Config.GetRpttsGain(mode);
        var falloff = Config.GetRpttsFalloff(mode);

        return (gain, falloff);
    }

    internal void DispatchChatterForContext(MessageContext context)
    {
        if (_serverConfigChannel == null || !Config.EnableChatter)
        {
            return;
        }

        var player = context.SendingPlayer;
        if (player?.Entity == null || context.Recipients == null)
        {
            return;
        }

        if (!ChatterMessagePlanner.TryGetSpeechLength(context, Config, out var speechLength))
        {
            return;
        }

        SendChatterToRecipients(context, player, ChatterMessagePlanner.CreateBaseMessage(player, context, Config, speechLength));
    }

    private void SendChatterToRecipients(MessageContext context, IServerPlayer player, ChatterSoundMessage message)
    {
        foreach (var recipient in context.Recipients.Where(recipient => recipient.GetChatterEnabled()))
        {
            var recipientMessage = ChatterMessagePlanner.ForRecipient(message, recipient.PlayerUID == player.PlayerUID, Config.ChatterSelfVolumeMultiplier);
            _serverConfigChannel?.SendPacket(recipientMessage, recipient);
        }
    }

    internal static int CountQuotedSpeechLength(string message)
    {
        return ChatterMessagePlanner.CountQuotedSpeechLength(message);
    }

    private void HookEvents()
    {
        API.Event.PlayerChat += Event_PlayerChat;
        API.Event.PlayerJoin += Event_PlayerJoin;
        API.Event.PlayerDisconnect += Event_PlayerDisconnect;
    }

    private void Event_PlayerDisconnect(IServerPlayer player)
    {
        ClearTypingIndicator(player);
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
                    Name = GetConfiguredProximityChatName(),
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
        ReconcileChatTypeOnJoin(byPlayer);

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
            }
            else if (playerProximityGroup.Level == EnumPlayerGroupMemberShip.None)
            {
                playerProximityGroup.Level = EnumPlayerGroupMemberShip.Member;
            }
        }

        // Handle nickname conflicts when player joins - always enforced
        var resetPlayers = NicknameValidationUtils.HandleNicknameConflictsOnJoin(byPlayer, API, Config);
        if (resetPlayers.Count > 0)
        {
            // Log the conflicts that were resolved
            API.Logger.Notification($"THEBASICS: Player '{byPlayer.PlayerName}' joined and caused {resetPlayers.Count} nickname conflicts to be reset: {string.Join(", ", resetPlayers)}");
        }

        // Config will be sent when client indicates it's ready.
        ReconcilePlayerLanguages(byPlayer, Config, _languageRenameMapForJoiningPlayers);
        SwapOutNameTag(byPlayer);
    }

    private void RefreshAllNameTags()
    {
        foreach (var onlinePlayer in API.World.AllOnlinePlayers)
        {
            if (onlinePlayer is IServerPlayer serverPlayer)
            {
                SwapOutNameTag(serverPlayer);
            }
        }
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

        behavior.SetName(CharacterSheetSystem.BuildNametagDisplayName(player, Config));
        CharacterSheetSystem.SyncNametagVisualAttrs(player);
    }

    internal void RefreshPlayerIdentityState(IServerPlayer player)
    {
        if (player?.Entity == null)
        {
            return;
        }

        ClearTypingIndicator(player);
        SwapOutNameTag(player);
        SendOwnCharacterSheetView(player);
    }

    private void SendOwnCharacterSheetView(IServerPlayer player)
    {
        if (_serverConfigChannel == null || Config?.EnableCharacterSheets != true)
        {
            return;
        }

        var sheetSystem = API.ModLoader.GetModSystem<CharacterSheetSystem>();
        if (sheetSystem == null)
        {
            return;
        }

        var view = sheetSystem.BuildClientView(player, new CharacterSheetOpenRequest
        {
            Mode = CharacterSheetOpenRequest.ModeOwn
        });
        view.SuppressDialogOpen = true;
        _serverConfigChannel.SendPacket(view, player);
    }

    private TextCommandResult SetNickname(TextCommandCallingArgs fullArgs)
    {
        var player = (IServerPlayer)fullArgs.Caller.Player;
        if (fullArgs.Parsers[0].IsMissing)
        {
            if (player.HasNickname(Config))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get("thebasics:chat-nick-current", player.GetNicknameWithColor(Config)),
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
            if (!NicknameValidationUtils.ValidateNickname(player, nickname, API, Config, out string conflictingPlayer, out string conflictType))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:chat-nick-conflict", nickname, conflictingPlayer, conflictType),
                };
            }

            player.SetNickname(nickname, Config);
            SwapOutNameTag(player);
            AnalyticsService.TrackCommandUsed("nickname", true);
            AnalyticsService.TrackFeatureUsed("nickname", "set");
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
        var oldNickname = attemptTarget.GetNicknameWithColor(Config);

        // Check if we have a force flag (parser[1])
        bool isForced = !fullArgs.Parsers[1].IsMissing && ((string)fullArgs.Parsers[1].GetValue())?.ToLowerInvariant() == "force";

        // If nickname argument is missing (parser[2])
        if (fullArgs.Parsers[2].IsMissing)
        {
            if (!attemptTarget.HasNickname(Config))
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
            if (!isForced &&
                !NicknameValidationUtils.ValidateNickname(attemptTarget, newNickname, API, Config, out string conflictingPlayer, out string conflictType))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:chat-nick-admin-conflict-warn", newNickname, conflictingPlayer, conflictType, attemptTarget.PlayerName),
                };
            }

            attemptTarget.SetNickname(newNickname, Config);
            SwapOutNameTag(attemptTarget);
            AnalyticsService.TrackCommandUsed("adminsetnickname", true);
            AnalyticsService.TrackFeatureUsed("nickname", "admin_set");

            string forceMessage = isForced ? Lang.Get("thebasics:chat-nick-admin-forced") : "";
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:chat-nick-admin-set", attemptTarget.PlayerName, attemptTarget.GetNicknameWithColor(Config), VtmlUtils.EscapeVtml(oldNickname), forceMessage),
            };
        }
    }

    private TextCommandResult Emote(TextCommandCallingArgs args)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

        if (args.Parsers[0].IsMissing)
        {
            return HandleOverrideModeCommand(player, ChatOverrideMode.Emote, "me");
        }

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
        SendThroughPipeline(context, "me", "proximity_emote", "send");

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
        SendThroughPipeline(context, "it", "environment_message", "send", EnumChatType.Notification);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private TextCommandResult PlacedEnvironmentMessage(TextCommandCallingArgs args)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

        var context = new MessageContext
        {
            Message = (string)args.Parsers[0].GetValue(),
            SendingPlayer = player,
            GroupId = ProximityChatId,
            Flags =
            {
                [MessageContext.IS_ENVIRONMENTAL] = true,
                [MessageContext.IS_PLACED_ENVIRONMENTAL] = true,
                [MessageContext.IS_FROM_COMMAND] = true
            }
        };

        // Process the entire pipeline — PlacedEnvironmentTransformer will raycast and
        // either store the position or fall back to standard env.
        SendThroughPipeline(context, "envhere", "environment_message", "place", EnumChatType.Notification);

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
        };
    }

    private TextCommandResult ClearNickname(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        player.ClearNickname(Config);
        SwapOutNameTag(player);
        AnalyticsService.TrackCommandUsed("clearnick", true);
        AnalyticsService.TrackFeatureUsed("nickname", "clear");
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
        AnalyticsService.TrackCommandUsed("clearnickcolor", true);
        AnalyticsService.TrackFeatureUsed("nickname_color", "clear");
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nickcolor-cleared"),
        };
    }

    private TextCommandResult ClearNametagBackgroundColor(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        player.ClearNametagBackgroundColor();
        SwapOutNameTag(player);
        AnalyticsService.TrackCommandUsed("clearnametagbackgroundcolor", true);
        AnalyticsService.TrackFeatureUsed("nametag_style", "clear_background");
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nametag-bgcolor-cleared"),
        };
    }

    private TextCommandResult ClearNametagBorderColor(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        player.ClearNametagBorderColor();
        SwapOutNameTag(player);
        AnalyticsService.TrackCommandUsed("clearnametagbordercolor", true);
        AnalyticsService.TrackFeatureUsed("nametag_style", "clear_border");
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-nametag-bordercolor-cleared"),
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
                    [MessageContext.IS_FROM_COMMAND] = true,
                    [MessageContext.IS_EXPLICIT_RANGE_COMMAND] = true
                }
            };

            // Process the entire pipeline
            var modeName = mode.ToString().ToLowerInvariant();
            SendThroughPipeline(context, modeName, "proximity_chat", "send_" + modeName);

            // Status stays Success even on a refusal: the pipeline has already sent the player a
            // specific explanation, and an empty command error on top would just add noise.
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
            };
        }

        // If no message provided, just set the player's chat mode
        player.SetChatMode(mode);
        AnalyticsService.TrackFeatureUsed("chat_mode", "set_" + mode.ToString().ToLowerInvariant(), properties: AnalyticsService.ChatProperties(mode.ToString().ToLowerInvariant()));
        return ChatModeStatus(player, ChatAxis.Mode);
    }

    /// <summary>
    /// Toggles a sticky override mode. Running the command for the mode you are already in clears it,
    /// and running a different one replaces it, so the three override commands are mutually exclusive.
    /// </summary>
    /// <summary>
    /// Bare <c>/me</c>, <c>/ooc</c> and <c>/gooc</c>. Takes the command name because this returns
    /// before each caller's own analytics, so without it both successful toggles and gate-refused
    /// attempts would be missing from command analytics for exactly the three commands whose
    /// disabled-demand this branch set out to measure.
    /// </summary>
    private TextCommandResult HandleOverrideModeCommand(IServerPlayer player, ChatOverrideMode mode, string commandName)
    {
        // Raw value, not the effective one: the effective getter clears and explains a mode the
        // player may no longer hold, which for a player trying to LEAVE that mode would emit the
        // refusal twice and never confirm the change. Leaving is always allowed, and /emotemode
        // and /oocToggle already read the raw value for the same reason.
        var result = SetOverrideMode(player, mode, enabled: player.GetChatOverrideMode() != mode);

        var succeeded = result.Status == EnumCommandStatus.Success;
        AnalyticsService.TrackCommandUsed(
            commandName,
            succeeded,
            succeeded ? null : "blocked",
            AnalyticsService.ChatProperties(commandName));

        return result;
    }

    /// <summary>
    /// The single gated way any command changes the override axis. <c>/me</c>, <c>/ooc</c>,
    /// <c>/gooc</c>, <c>/emotemode</c> and <c>/oocToggle</c> all route through here, so a gate added
    /// in one place cannot be bypassed by a sibling command writing the same state directly.
    /// Leaving a mode is always allowed; nobody should be trapped in one.
    /// </summary>
    private TextCommandResult SetOverrideMode(IServerPlayer player, ChatOverrideMode mode, bool enabled)
    {
        if (!enabled)
        {
            if (player.GetChatOverrideMode() == mode)
            {
                player.SetChatOverrideMode(ChatOverrideMode.None);
            }

            return ChatModeStatus(player, ChatAxis.Type);
        }

        if (!CanEnterOverrideMode(player, mode, out var refusal))
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = refusal,
            };
        }

        player.SetChatOverrideMode(mode);

        AnalyticsService.TrackFeatureUsed("chat_override_mode", "set_" + mode.ToString().ToLowerInvariant());

        return ChatModeStatus(player, ChatAxis.Type);
    }

    /// <summary>
    /// Whether the player may park themselves in an override mode. Sending a one-off OOC line only
    /// needs chat privilege, but staying in OOC is what <c>/oocToggle</c> grants, so it answers to
    /// the same server switch and privilege rather than being reachable by a bare <c>/ooc</c>.
    /// </summary>
    private bool CanEnterOverrideMode(IServerPlayer player, ChatOverrideMode mode, out string refusal)
    {
        var allowed = IsOverrideModeAvailable(player, mode, out var refusalLangKey);
        refusal = refusalLangKey == null ? null : Lang.Get(refusalLangKey);

        if (!allowed)
        {
            // Measuring these is the whole reason the commands stay registered when a feature is off:
            // a command that does not exist produces no signal that anyone wanted it. Recorded here
            // because this is the one seam every command-layer gate check passes through, whereas
            // IsOverrideModeAvailable is also consulted on join and on delivery, which are not
            // player-initiated attempts and would pollute the count.
            AnalyticsService.TrackFailure(
                "chat_override_mode",
                "enter_" + mode.ToString().ToLowerInvariant(),
                "blocked",
                refusalLangKey ?? "unknown");
        }

        return allowed;
    }

    /// <summary>
    /// The single answer to "may this player be in this override mode right now?".
    ///
    /// Entry, delivery, and status all consult this. They each used to carry their own copy, and
    /// every time the copies drifted, a setting silently stopped applying to players already holding
    /// the mode. Every gate here is live-reloadable or role-dependent, so none can be checked only
    /// at entry time.
    /// </summary>
    internal bool IsOverrideModeAvailable(IServerPlayer player, ChatOverrideMode mode, out string refusalLangKey)
    {
        refusalLangKey = null;

        if (mode == ChatOverrideMode.None)
        {
            return true;
        }

        // The override commands are not registered when RP chat is off, but the config can be
        // flipped live, so guard the capability rather than relying on registration.
        if (Config.DisableRPChat)
        {
            refusalLangKey = "thebasics:chat-override-rp-disabled";
            return false;
        }

        // With RP text off for this player, plain lines bypass the pipeline entirely and go out as
        // vanilla chat, so a stored type does nothing. Storing one anyway would leave it waiting to
        // spring back to life the moment they re-enable RP text. One-off /ooc and /me still work,
        // because those run the pipeline directly rather than through the chat event.
        if (!player.GetRpTextEnabled())
        {
            refusalLangKey = "thebasics:chat-type-rptext-disabled";
            return false;
        }

        // Global OOC answers only to its own switch. AllowOOCToggle and OOCTogglePermission govern
        // local OOC; gating global OOC behind them would let an unrelated setting remove a working
        // feature, and refuse it with a message naming the wrong one.
        if (mode == ChatOverrideMode.GlobalOoc)
        {
            if (Config.EnableGlobalOOC)
            {
                return true;
            }

            refusalLangKey = "thebasics:chat-gooc-disabled";
            return false;
        }

        if (mode != ChatOverrideMode.Ooc)
        {
            return true;
        }

        if (!Config.AllowOOCToggle)
        {
            refusalLangKey = "thebasics:chat-ooc-disabled";
            return false;
        }

        // Matches how /oocToggle is registered, so the two routes into the same state agree. Roles
        // change at runtime with no config edit at all, so this must be re-checked on delivery.
        if (!player.HasPrivilege(Config.OOCTogglePermission))
        {
            refusalLangKey = "thebasics:chat-ooc-mode-no-privilege";
            return false;
        }

        return true;
    }

    /// <summary>
    /// A player parked in global OOC when an admin disables the feature would otherwise be stuck:
    /// <c>/gooc</c> is no longer registered to toggle it back off. Clears the stale value outright
    /// rather than masking it, so re-enabling global OOC later cannot silently drop the player back
    /// into a server-wide channel they never asked to rejoin.
    /// </summary>
    private ChatOverrideMode GetEffectiveOverrideMode(IServerPlayer player)
    {
        var mode = player.GetChatOverrideMode();
        if (IsOverrideModeAvailable(player, mode, out var refusalLangKey))
        {
            return mode;
        }

        player.SetChatOverrideMode(ChatOverrideMode.None);

        // Say why. This runs on status-adjacent commands like a bare /w, so dropping the mode
        // quietly would leave the player reading "Chat mode: whisper" with the override clause
        // simply gone, and their next line published in character.
        player.SendMessage(ProximityChatId, Lang.Get(refusalLangKey), EnumChatType.CommandError);

        return ChatOverrideMode.None;
    }

    /// <summary>
    /// Reports both axes at once. Range alone is ambiguous now that a player can be whispering in
    /// character or whispering out of character.
    /// </summary>
    /// <summary>
    /// Puts a player back into ordinary chat when the type they were parked in is no longer allowed,
    /// so they never start a session in a state the server will refuse.
    ///
    /// The delivery path clears stale types too, but only once the player has already tried to speak,
    /// which costs them a message and reads as being stuck. Doing it at join makes that the rare
    /// live-config-flip case rather than the normal one.
    /// </summary>
    private void ReconcileChatTypeOnJoin(IServerPlayer byPlayer)
    {
        var mode = byPlayer.GetChatOverrideMode();
        if (mode == ChatOverrideMode.None || IsOverrideModeAvailable(byPlayer, mode, out var refusalLangKey))
        {
            return;
        }

        byPlayer.SetChatOverrideMode(ChatOverrideMode.None);

        // Say why, and say what they are now. A silent change would leave them believing their next
        // line goes somewhere it does not.
        byPlayer.SendMessage(
            ProximityChatId,
            Lang.Get("thebasics:chat-type-reset-on-join", Lang.Get(refusalLangKey)),
            EnumChatType.Notification);
    }

    /// <summary>
    /// Which of the two axes the player just changed. The confirmation leads with that one and
    /// parenthesises the other, so a single line teaches that both exist without either looking
    /// like the only setting.
    /// </summary>
    private enum ChatAxis
    {
        Mode,
        Type
    }

    private TextCommandResult ChatModeStatus(IServerPlayer player, ChatAxis changed)
    {
        var mode = player.GetChatMode().ToString().ToLowerInvariant();
        var type = Lang.Get(ChatTypeLangKey(GetEffectiveOverrideMode(player)));

        string message;
        if (changed == ChatAxis.Type)
        {
            // Always show both here: the player just discovered the type axis exists, so naming the
            // mode alongside it is the cheapest place to teach that they are independent.
            message = Lang.Get("thebasics:chat-chattype-set", type, mode);
        }
        else
        {
            // Only mention the type when one is actually set; otherwise this is the plain, familiar
            // confirmation that shipped before the type axis existed.
            message = GetEffectiveOverrideMode(player) == ChatOverrideMode.None
                ? Lang.Get("thebasics:chat-chatmode-set", mode)
                : Lang.Get("thebasics:chat-chatmode-set-with-type", mode, type);
        }

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = message,
        };
    }

    private static string ChatTypeLangKey(ChatOverrideMode mode) => mode switch
    {
        ChatOverrideMode.Emote => "thebasics:chat-type-emote",
        ChatOverrideMode.Ooc => "thebasics:chat-type-ooc",
        ChatOverrideMode.GlobalOoc => "thebasics:chat-type-gooc",
        _ => "thebasics:chat-type-none"
    };

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

        // Track after the gate has had its say. Emitting the success events up front recorded
        // refused attempts as completed mode changes.
        var result = SetOverrideMode(player, ChatOverrideMode.Emote, emoteMode);
        var succeeded = result.Status == EnumCommandStatus.Success;
        AnalyticsService.TrackCommandUsed("emotemode", succeeded);
        if (succeeded)
        {
            AnalyticsService.TrackFeatureUsed("emote_mode", emoteMode ? "enable" : "disable");
        }

        return result;
    }

    private TextCommandResult RpTextEnabled(TextCommandCallingArgs args)
    {
        var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
        // If no argument provided, toggle the current state
        var rpTextEnabled = args.Parsers[0].IsMissing ? !player.GetRpTextEnabled() : (bool)args.Parsers[0].GetValue();
        player.SetRpTextEnabled(rpTextEnabled);

        // With RP text off the pipeline hands the line to vanilla chat untouched, so a lingering
        // override would never be applied and never be cleared: the player's next line would go out
        // verbatim to the whole group while they still believed they were in OOC.
        if (!rpTextEnabled)
        {
            player.SetChatOverrideMode(ChatOverrideMode.None);
        }

        AnalyticsService.TrackCommandUsed("rptext", true);
        AnalyticsService.TrackFeatureUsed("rp_text", rpTextEnabled ? "enable" : "disable");
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-rptext-set", ChatHelper.OnOff(rpTextEnabled)),
        };
    }

    private TextCommandResult OOCMode(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var newMode = args.Parsers[0].IsMissing ? !player.GetOOCEnabled() : (bool)args.Parsers[0].GetValue();

        // Track after the gate has had its say. Emitting the success events up front recorded
        // refused attempts as completed mode changes.
        var result = SetOverrideMode(player, ChatOverrideMode.Ooc, newMode);
        var succeeded = result.Status == EnumCommandStatus.Success;
        AnalyticsService.TrackCommandUsed("ooctoggle", succeeded);
        if (succeeded)
        {
            AnalyticsService.TrackFeatureUsed("ooc_mode", newMode ? "enable" : "disable");
        }

        return result;
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
        AnalyticsService.TrackCommandUsed("chatter", true);
        AnalyticsService.TrackFeatureUsed("chatter", enabled ? "enable" : "disable");
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:chat-chatter-set", ChatHelper.OnOff(enabled)),
        };
    }

    private PlayerGroup GetProximityGroup()
    {
        return API.Groups.GetPlayerGroupByName(GetConfiguredProximityChatName());
    }

    private string GetConfiguredProximityChatName()
    {
        return string.IsNullOrWhiteSpace(Config.ProximityChatName) ? "Proximity" : Config.ProximityChatName;
    }

    /// <summary>
    /// Runs a message through the pipeline and records what actually happened to it.
    /// </summary>
    /// <remarks>
    /// The pipeline can refuse a line without throwing — a cleared stale chat type, a disabled
    /// surface — and it tells the player itself when it does. Every send reports through this one
    /// exit so that no caller can drift back to assuming its own send landed; that assumption was
    /// separately true of all seven call sites, which would have reported a server silently dropping
    /// chat as one delivering every line.
    /// </remarks>
    private void SendThroughPipeline(MessageContext context, string surface, string featureName, string featureAction,
        EnumChatType defaultChatType = EnumChatType.OthersMessage)
    {
        if (TransformerSystem == null)
        {
            // A missing pipeline is a dropped line, not a delivered one.
            AnalyticsService.TrackFailure(featureName, surface, "error", "no_transformer_system");
            return;
        }

        TransformerSystem.ProcessMessagePipeline(context, defaultChatType);

        var delivered = context.State == MessageContextState.CONTINUE;
        var chatProperties = AnalyticsService.ChatProperties(surface);

        if (context.HasFlag(MessageContext.IS_FROM_COMMAND))
        {
            AnalyticsService.TrackCommandUsed(surface, delivered, delivered ? null : "rejected", chatProperties);
        }
        else if (!delivered)
        {
            // Chat-tab lines have no command event to carry the refusal, so record it as a failure.
            AnalyticsService.TrackFailure(featureName, surface, "rejected", "pipeline_stopped");
        }

        if (delivered)
        {
            AnalyticsService.TrackFeatureUsed(featureName, featureAction, properties: chatProperties);
        }
    }

    private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data,
        Vintagestory.API.Datastructures.BoolRef consumed)
    {
        if (byPlayer == null || consumed == null)
        {
            return;
        }

        if (channelId != ProximityChatId)
        {
            return;
        }

        // Short circuit if RP text is disabled
        if (!byPlayer.GetRpTextEnabled())
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

            // Consumed before processing: once we take the message, vanilla will not deliver it.
            // That makes an unhandled pipeline exception a silent drop, so the call below is
            // wrapped rather than left to propagate.
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

            // The message is already consumed, so an exception here would drop the player's line with
            // no delivery and no feedback. Tell them and log it rather than losing it silently.
            try
            {
                SendThroughPipeline(context, "chat_tab", "proximity_chat", "send_chat_tab");
            }
            catch (Exception ex)
            {
                API.Logger.Error("THEBASICS: proximity chat pipeline threw for player {0}: {1}", byPlayer?.PlayerName, ex);
                byPlayer?.SendMessage(ProximityChatId, Lang.Get("thebasics:chat-pipeline-failed"), EnumChatType.CommandError);

                // Report it here too. Handling the throw locally means it never reaches the outer
                // catch, so without this a server whose pipeline is failing would report 100% chat
                // success and zero failures, hiding the very problem this handler exists to surface.
                AnalyticsService.TrackFailure("proximity_chat", "chat_tab_pipeline", "error", "pipeline_exception", ex);
                return;
            }
        }
        catch (Exception e)
        {
            // Never crash the server on player chat.
            API.Logger.Error($"THEBASICS - Error processing proxchat message: {e}");
            AnalyticsService.TrackFailure("proximity_chat", "chat_tab_pipeline", "error", "pipeline_exception", e);
        }
    }

    /// <summary>
    /// Sends a <see cref="PlacedEnvironmentMessage"/> packet to a specific recipient.
    /// Called by the transformer pipeline for placed environmental messages.
    /// </summary>
    public void SendPlacedEnvironmentPacket(IServerPlayer recipient, Vec3d position, string bubbleText)
    {
        _serverConfigChannel?.SendPacket(new PlacedEnvironmentMessage
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            BubbleText = bubbleText,
        }, recipient);
    }
}
