using HarmonyLib;
using thebasics.Models;
using thebasics.Configs;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace thebasics.ModSystems.ChatUiSystem;

[HarmonyPatch]
public class ChatUiSystem : ModSystem
{
    private static ICoreClientAPI _api;
    private Harmony _harmony;
    private static int _proximityGroupId;
    private static bool _preventProximityChannelSwitching;
    private static ModConfig _config = null;
    private static readonly System.Collections.Generic.List<System.Action> _pendingConfigActions = new System.Collections.Generic.List<System.Action>();
    private static string _lastSelectedChannel;
    private static bool _isInitialized;
    private static int _initializationAttempts;
    private const int MAX_INITIALIZATION_ATTEMPTS = 3;
    private const int INITIALIZATION_RETRY_DELAY_MS = 500;

    private static IClientNetworkChannel _clientConfigChannel;

    // private static IClientNetworkChannel _clientNicknameChannel;
    // private GuiDialogCharacterBase dlg;
    // private GuiDialog.DlgComposers Composers => this.dlg.Composers;

    // private Dictionary<string, string> PlayerNicknames = new Dictionary<string, string>();

    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        _api = api;
        _isInitialized = false;
        _initializationAttempts = 0;
        
        LoadConfig();
        RegisterForServerSideConfig();
        
        // Load last selected channel from client settings
        _lastSelectedChannel = _api.Settings.String["lastSelectedChatChannel"];
        
        // Register event handlers
        _api.Event.PlayerJoin += OnPlayerJoin;
        
        // Initialize Harmony patches if needed
        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            _api.Logger.Debug("[THEBASICS] Applying Harmony patches");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }
        
        // Schedule initialization check
        _api.Event.RegisterCallback(dt => InitializeIfNeeded(), 1000);
    }

    // private void Dlg_ComposeExtraGuis()
    // {
    //     ComposeCharacterSheetGui();
    // }
    //
    // private void ComposeCharacterSheetGui()
    // {
    //
    //     var pcDialogPadding = 400;
    //     var dialogWidth = 400;
    //     var rowPadding = 20;
    //     ElementBounds statsBounds = this.Composers["playercharacter"].Bounds;
    //     _api.Logger.Debug($"THEBASICS - test bounds {statsBounds.absX + statsBounds.OuterWidth + pcDialogPadding}, {statsBounds.absY}");
    //     ElementBounds dialogBounds = new ElementBounds()
    //         .WithSizing(ElementSizing.Fixed)
    //         .WithFixedPosition(statsBounds.absX + statsBounds.OuterWidth + pcDialogPadding, statsBounds.absY)
    //         .WithFixedSize(dialogWidth, statsBounds.OuterHeight);
    //
    //     ElementBounds bgBounds = ElementBounds.Fill
    //         .WithFixedPadding(GuiStyle.ElementToDialogPadding)
    //         .WithSizing(ElementSizing.FitToChildren)
    //         .WithChildren();
    //         
    //     this.Composers["charsheet"] = _api.Gui
    //         .CreateCompo("charsheet", dialogBounds)
    //         .AddShadedDialogBG(bgBounds)
    //         .AddDialogTitleBar(Lang.Get("Character Sheet"), () => dlg.OnTitleBarClose())
    //         .BeginChildElements()
    //         // .AddInset()
    //             .AddDynamicText("Testing!", CairoFont.WhiteSmallText(), ElementStdBounds.Rowed(0, rowPadding))
    //         .EndChildElements()
    //         .Compose();
    // }

    // private void ApplyRenderer(Entity entity)
    // {
    //     if (entity is EntityPlayer entityPlayer)
    //     {
    //         var oldRenderer = entity.Properties.Client.Renderer;
    //         var newRenderer = new RpTextEntityPlayerShapeRenderer(entity,_api);
    //         entity.Properties.Client.Renderer = newRenderer;
    //         entity.Properties.Client.RendererName = "TestPlayerShape";
    //
    //         oldRenderer.Dispose();
    //         newRenderer.OnEntityLoaded();
    //     }
    // }

    // private bool OnPlayerReady(ref EnumHandling handling)
    // {
    //     handling = EnumHandling.PassThrough;
    //
    //     if (_api.World.Player != null && _api.World.Player.Entity != null)
    //     {
    //         _api.Logger.Debug($"THEBASICS - OnPlayerReady, Setting my player nickname!");
    //         ApplyNickname(_api.World.Player.Entity);
    //     }
    //     else
    //     {
    //         _api.Logger.Debug($"THEBASICS - OnPlayerReady, Player or Entity was null!");
    //     }
    //
    //     return true;
    // }

    // private void ApplyNickname(Entity entity)
    // {
    //     if (entity is EntityPlayer entityPlayer)
    //     {
    //         if (PlayerNicknames.ContainsKey(entityPlayer.PlayerUID))
    //         {
    //             SetPlayerNickname(entityPlayer, PlayerNicknames[entityPlayer.PlayerUID]);
    //         }
    //     }
    // }

    // Config is loaded from server
    private void LoadConfig()
    {
        // Initialize config to null until we receive it from the server
        _config = null;
        _api.Logger.Debug("[THEBASICS] Waiting for server config");
    }

    // Queue an action to be executed once we have the config from server
    private static void QueueConfigAction(System.Action action)
    {
        if (_config != null)
        {
            // If we already have the config, execute immediately
            action();
        }
        else
        {
            // Otherwise queue for later execution
            _pendingConfigActions.Add(action);
            _api.Logger.Debug("[THEBASICS] Action queued until config is received");
        }
    }

    // Process all queued actions
    private static void ProcessConfigActionQueue()
    {
        _api.Logger.Debug($"[THEBASICS] Processing {_pendingConfigActions.Count} queued actions");
        
        foreach (var action in _pendingConfigActions)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                _api.Logger.Error($"[THEBASICS] Error executing queued action: {e}");
            }
        }
        
        _pendingConfigActions.Clear();
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized || _initializationAttempts >= MAX_INITIALIZATION_ATTEMPTS) return;

        try
        {
            var chatDialog = _api.Gui.LoadedGuis.Find(dlg => dlg is HudDialogChat) as HudDialogChat;
            if (chatDialog != null)
            {
                var chatTabs = Traverse.Create(chatDialog).Field("chatTabs").GetValue<GuiTab[]>();
                if (chatTabs != null && chatTabs.Length > 0)
                {
                    _isInitialized = true;
                    _api.Logger.Debug("[THEBASICS] Chat system successfully initialized");
                    return;
                }
            }

            _initializationAttempts++;
            if (_initializationAttempts < MAX_INITIALIZATION_ATTEMPTS)
            {
                _api.Logger.Debug($"[THEBASICS] Initialization attempt {_initializationAttempts} failed, retrying in {INITIALIZATION_RETRY_DELAY_MS}ms");
                _api.Event.RegisterCallback(dt => InitializeIfNeeded(), INITIALIZATION_RETRY_DELAY_MS);
            }
            else
            {
                _api.Logger.Warning("[THEBASICS] Failed to initialize chat system after maximum attempts");
            }
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error during initialization: {e}");
        }
    }

    private void OnPlayerJoin(IClientPlayer byPlayer)
    {
        try
        {
            _api.Logger.Debug("THEBASICS - Player joined, sending ready message to server");
            _clientConfigChannel.SendPacket(new TheBasicsClientReadyMessage());
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"THEBASICS - Error in OnPlayerJoin: {e}");
        }
    }

    private void RegisterForServerSideConfig()
    {
        _clientConfigChannel = _api.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage);
        // .RegisterMessageType<TheBasicsChatTypingMessage>();

        // _clientNicknameChannel = _api.Network.RegisterChannel("thebasics_nickname")
        //     .RegisterMessageType<TheBasicsPlayerNicknameMessage>()
        //     .SetMessageHandler<TheBasicsPlayerNicknameMessage>(OnServerPlayerNicknameMessage);
    }

    // private void OnServerPlayerNicknameMessage(TheBasicsPlayerNicknameMessage packet)
    // {
    //     PlayerNicknames[packet.PlayerUID] = packet.Nickname;
    //
    //     var player = _api.World.PlayerByUid(packet.PlayerUID);
    //
    //     if (player == null)
    //     {
    //         _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find player by UUID");
    //         return;
    //     }
    //
    //     var entity = player.Entity;
    //     
    //     if (entity == null)
    //     {
    //         _api.Logger.Debug($"Got nickname {packet.PlayerUID} {packet.Nickname} but couldn't find entity for player");
    //         return;
    //     }
    //     
    //     SetPlayerNickname(entity, packet.Nickname);
    //
    //     // TODO: Check if player is currently being rendered to update nickname
    // }

    // private void SetPlayerNickname(EntityPlayer entityPlayer, string name)
    // {
    //     _api.Logger.Debug($"THEBASICS - Setting player ${entityPlayer.Player.PlayerName} nametag to nickname ${name}");
    //     var nametag = entityPlayer.GetBehavior<EntityBehaviorNameTag>();
    //
    //     nametag.SetName(name);
    // }

    private void OnServerConfigMessage(TheBasicsConfigMessage configMessage)
    {
        try
        {
            // Store the received config
            _config = configMessage.Config;
            
            if (_config == null)
            {
                _api.Logger.Error("[THEBASICS] Received null config from server!");
                return;
            }
            
            // Set the proximity group ID
            _proximityGroupId = configMessage.ProximityGroupId;
            
            // Extract specific values needed for other functionality
            _preventProximityChannelSwitching = _config.PreventProximityChannelSwitching;
            
            _api.Logger.Debug($"[THEBASICS] Received server config: Prevention={_preventProximityChannelSwitching}, ProximityId={_proximityGroupId}");
            _api.Logger.Debug($"[THEBASICS] Full config received from server with settings: ProximityChatName={_config.ProximityChatName}, UseGeneralChannelAsProximityChat={_config.UseGeneralChannelAsProximityChat}");
            
            // Process any actions that were waiting for config
            if (_pendingConfigActions.Count > 0)
            {
                _api.Logger.Debug($"[THEBASICS] Processing {_pendingConfigActions.Count} queued actions");
                ProcessConfigActionQueue();
            }
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error processing server config message: {e}");
        }
    }

    /*
     * Prevent chat switching by preventing original method from executing if user is currently viewing the proximity tab and server config is set to prevent switching
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "HandleGotoGroupPacket")]
    public static bool HandleGotoGroupPacket(HudDialogChat __instance, Packet_Server packet)
    {
        if (_config == null)
        {
            _api.Logger.Debug("[THEBASICS] Config not yet received, queuing channel switch check");
            // We can't make a decision yet, so allow the packet for now
            return true;
        }
        
        var game = (ClientMain)_api.World;
        int gotoGroupId = packet.GotoGroup.GroupId;
        _api.Logger.Debug(
            $"[THEBASICS] HandleGotoGroupPacket ~ Prevention: {_preventProximityChannelSwitching}, Current: {game.currentGroupid}, Target: {gotoGroupId}, Proximity: {_proximityGroupId}");
        
        if (_preventProximityChannelSwitching && game.currentGroupid == _proximityGroupId)
        {
            _api.Logger.Debug("[THEBASICS] Denying GotoGroupPacket - proximity channel switching prevented");
            return false;
        }

        _api.Logger.Debug("[THEBASICS] Allowing GotoGroupPacket");
        return true;
    }

    /*
     * Set default chat channel and handle channel persistence
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnGuiOpened")]
    public static void PostfixOnGuiOpened(HudDialogChat __instance)
    {
        if (_api == null) return;
        
        if (_config == null)
        {
            // Queue this action for when config is received
            QueueConfigAction(() => PostfixOnGuiOpened(__instance));
            _api.Logger.Debug("[THEBASICS] OnGuiOpened queued until config is received");
            return;
        }

        try
        {
            if (!_isInitialized)
            {
                _api.Logger.Debug("[THEBASICS] Chat system not fully initialized, deferring tab modification");
                _api.Event.RegisterCallback(dt => PostfixOnGuiOpened(__instance), INITIALIZATION_RETRY_DELAY_MS);
                return;
            }

            // Don't modify chat tabs if using general channel as proximity chat
            if (_config.UseGeneralChannelAsProximityChat)
            {
                _api.Logger.Debug("[THEBASICS] UseGeneralChannelAsProximityChat is enabled, skipping default channel modification");
                return;
            }

            // Get the chat tabs
            var chatTabs = Traverse.Create(__instance).Field("chatTabs").GetValue<GuiTab[]>();
            if (chatTabs == null || chatTabs.Length == 0)
            {
                _api.Logger.Warning("[THEBASICS] Failed to get chat tabs, they may not be initialized yet");
                return;
            }

            // Find the Proximity and General chat tabs
            GuiTab proximityTab = null;
            GuiTab generalTab = null;
            
            for (int i = 0; i < chatTabs.Length; i++)
            {
                if (chatTabs[i].Name == _config.ProximityChatName)
                {
                    proximityTab = chatTabs[i];
                }
                else if (chatTabs[i].Name == "general")
                {
                    generalTab = chatTabs[i];
                }
            }

            if (proximityTab == null)
            {
                _api.Logger.Warning($"[THEBASICS] Proximity chat tab '{_config.ProximityChatName}' not found");
                return;
            }
            if (generalTab == null)
            {
                _api.Logger.Warning("[THEBASICS] General chat tab not found");
                return;
            }

            // Validate stored channel name if preservation is enabled
            if (_config.PreserveDefaultChatChoice && !string.IsNullOrEmpty(_lastSelectedChannel))
            {
                bool validChannel = false;
                for (int i = 0; i < chatTabs.Length; i++)
                {
                    if (chatTabs[i].Name == _lastSelectedChannel)
                    {
                        validChannel = true;
                        break;
                    }
                }
                if (!validChannel)
                {
                    _api.Logger.Warning($"[THEBASICS] Stored channel '{_lastSelectedChannel}' is no longer valid, reverting to default");
                    _lastSelectedChannel = null;
                }
            }

            // Determine which tab should be active
            GuiTab targetTab = null;

            if (_config.PreserveDefaultChatChoice && !string.IsNullOrEmpty(_lastSelectedChannel))
            {
                // Use last selected channel if preservation is enabled
                targetTab = _lastSelectedChannel == _config.ProximityChatName ? proximityTab : generalTab;
            }
            else
            {
                // Use configured default
                targetTab = _config.ProximityChatAsDefault ? proximityTab : generalTab;
            }

            // Set the active tab
            if (targetTab != null)
            {
                try
                {
                    Traverse.Create(__instance).Field("activeTab").SetValue(targetTab);
                    Traverse.Create(__instance).Field("activeTabName").SetValue(targetTab.Name);
                    _api.Logger.Debug($"[THEBASICS] Set active chat tab to {targetTab.Name}");
                }
                catch (System.Exception e)
                {
                    _api.Logger.Error($"[THEBASICS] Failed to set active chat tab: {e.Message}");
                }
            }
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error in PostfixOnGuiOpened: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnTabClicked")]
    public static void PostfixOnTabClicked(GuiTab tab)
    {
        if (_config == null || tab == null)
        {
            // Can't process this yet, but it's not important enough to queue
            return;
        }
        
        if (!_config.PreserveDefaultChatChoice) return;

        try
        {
            // Don't store channel preference if using general channel as proximity chat
            if (_config.UseGeneralChannelAsProximityChat) return;

            // Store the selected channel
            _lastSelectedChannel = tab.Name;
            _api.Settings.String["lastSelectedChatChannel"] = _lastSelectedChannel;
            _api.Logger.Debug($"[THEBASICS] Stored last selected channel: {_lastSelectedChannel}");
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error in PostfixOnTabClicked: {e}");
        }
    }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(EntityShapeRenderer), "OnChatMessage")]
    // protected static bool OnChatMessage(EntityPlayerShapeRenderer __instance, int groupId, string message,
    //     EnumChatType chattype, string data)
    // {
    //     if (!(__instance.entity is EntityPlayer))
    //     {
    //         return true;
    //     }
    //
    //     if (data == null || !data.Contains("from:") ||
    //         __instance.entity.Pos.SquareDistanceTo(__instance.capi.World.Player.Entity.Pos.XYZ) >= 400.0 ||
    //         message.Length <= 0)
    //         return false;
    //     string[] strArray1 = data.Split(new char[1] { ',' }, 2);
    //     if (strArray1.Length < 2)
    //         return false;
    //     string[] strArray2 = strArray1[0].Split(new char[1]
    //     {
    //         ':'
    //     }, 2);
    //     string[] strArray3 = strArray1[1].Split(new char[1]
    //     {
    //         ':'
    //     }, 2);
    //     if (strArray2[0] != "from")
    //         return false;
    //     int result;
    //     int.TryParse(strArray2[1], out result);
    //     if (__instance.entity.EntityId != (long)result)
    //         return false;
    //     message = strArray3[1];
    //     message = message.Replace("&lt;", "<").Replace("&gt;", ">");
    //     LoadedTexture loadedTexture = __instance.capi.Gui.TextTexture.GenTextTexture(message,
    //         new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble), 350, new TextBackground()
    //         {
    //             FillColor = GuiStyle.DialogLightBgColor,
    //             Padding = 100,
    //             Radius = GuiStyle.ElementBGRadius
    //         }, EnumTextOrientation.Center);
    //     var messageTexturesField = __instance.GetType()
    //         .GetField("messageTextures", BindingFlags.NonPublic | BindingFlags.Instance);
    //     var messageTextures = (List<MessageTexture>)messageTexturesField.GetValue(__instance);
    //     messageTextures.Insert(0, new MessageTexture()
    //     {
    //         tex = loadedTexture,
    //         message = message,
    //         receivedTime = __instance.capi.World.ElapsedMilliseconds
    //     });
    //
    //     return false;
    // }

    public override void Dispose()
    {
        try
        {
            if (_api != null)
            {
                _api.Event.PlayerJoin -= OnPlayerJoin;
                _api = null;
            }
            _harmony?.UnpatchAll(Mod.Info.ModID);
            _config = null;
            _lastSelectedChannel = null;
            _isInitialized = false;
            _initializationAttempts = 0;
        }
        catch (System.Exception e)
        {
            if (_api != null)
            {
                _api.Logger.Error($"[THEBASICS] Error in Dispose: {e}");
            }
        }
    }
}