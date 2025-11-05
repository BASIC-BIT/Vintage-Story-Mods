using HarmonyLib;
using thebasics.Models;
using thebasics.Configs;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

[HarmonyPatch]
public class ChatUiSystem : ModSystem
{
    private static ICoreClientAPI _api;
    private Harmony _harmony;
    private static int? _proximityGroupId = null;
    private static int? _lastSelectedGroupId = null;
    private static ModConfig _config = null;
    private static readonly System.Collections.Generic.List<System.Action> _pendingConfigActions = new System.Collections.Generic.List<System.Action>();

    private static IClientNetworkChannel _clientConfigChannel;
    private static SafeClientNetworkChannel _safeNetworkChannel;
    private static bool _usingRptts = false;
    private static dynamic _rpttsApi = null;
    private static dynamic _rpttsChatSystem = null;
    private const int RpttsInitMaxAttempts = 3;
    private static int _rpttsInitAttempts = 0;
    private static bool _rpttsInitScheduled = false;
    private static bool _rpttsExplicitModeApplied = false;

    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        _api = api;
        // _isInitialized = false;
        // _initializationAttempts = 0;

        _rpttsApi = null;
        _rpttsChatSystem = null;
        _usingRptts = false;
        _rpttsInitAttempts = 0;
        _rpttsInitScheduled = false;
        _rpttsExplicitModeApplied = false;
        ScheduleRpttsInitialization();
        
        RegisterForServerSideConfig();

        
        // Register event handlers
        _api.Event.PlayerJoin += OnPlayerJoin;
        // _api.Event.PlayerLeave += OnPlayerLeave;
        
        // Initialize Harmony patches if needed
        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            _api.Logger.Debug("[THEBASICS] Applying Harmony patches");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }
        
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

    private void OnPlayerJoin(IClientPlayer byPlayer)
    {
        // Only send ready message when the local player joins, not when any player joins
        if (byPlayer.PlayerUID == _api.World.Player.PlayerUID)
        {
            _api.Logger.Debug("THEBASICS - Local player joined, attempting to send ready message to server");
            
            // Use safe packet sending with connection checking and retry mechanism
            // The server will only send config after receiving this ready message
            _safeNetworkChannel?.SendPacketSafely(new TheBasicsClientReadyMessage());
        }
    }

    private void RegisterForServerSideConfig()
    {
        _clientConfigChannel = _api.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .RegisterMessageType<ChannelSelectedMessage>()
            .RegisterMessageType<ProximitySpeechMessage>()
            .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage)
            .SetMessageHandler<ProximitySpeechMessage>(OnProximitySpeechMessage);
        // .RegisterMessageType<TheBasicsChatTypingMessage>();

        // Initialize the safe network channel wrapper
        var config = new SafeClientNetworkChannel.SafeNetworkChannelConfig
        {
            LogPrefix = "[THEBASICS]",
            EnableDebugLogging = true,
            RetryDelayMs = 2000,
            MaxRetries = 10
        };
        _safeNetworkChannel = new SafeClientNetworkChannel(_clientConfigChannel, _api, config);
    }

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
            
            _proximityGroupId = configMessage.ProximityGroupId;
            _lastSelectedGroupId = configMessage.LastSelectedGroupId;
            
            _api.Logger.Debug($"[THEBASICS] Received server config: PreventProximityChannelSwitching={_config.PreventProximityChannelSwitching}, ProximityId={_proximityGroupId}, LastSelectedGroupId={_lastSelectedGroupId}");
            _api.Logger.Debug($"[THEBASICS] Full config received from server with settings: ProximityChatName={_config.ProximityChatName}, UseGeneralChannelAsProximityChat={_config.UseGeneralChannelAsProximityChat}, PreserveDefaultChatChoice={_config.PreserveDefaultChatChoice}, ProximityChatAsDefault={_config.ProximityChatAsDefault}");
            
            // Process any actions that were waiting for config
            if (_pendingConfigActions.Count > 0)
            {
                ProcessConfigActionQueue();
            }
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error processing server config message: {e}");
        }
    }

    private static void ScheduleRpttsInitialization()
    {
        if (_api == null || _usingRptts || _rpttsInitAttempts >= RpttsInitMaxAttempts || _rpttsInitScheduled)
        {
            return;
        }

        _rpttsInitScheduled = true;
        _api.Event.RegisterCallback(_ =>
        {
            _rpttsInitScheduled = false;
            _rpttsInitAttempts++;
            var initialized = TryInitializeRpttsIntegration();
            if (!initialized && _rpttsInitAttempts < RpttsInitMaxAttempts)
            {
                ScheduleRpttsInitialization();
            }
        }, 1000);
    }

    private static bool TryInitializeRpttsIntegration()
    {
        if (_api == null)
        {
            return false;
        }

        if (_usingRptts)
        {
            return true;
        }

        try
        {
        if (!_api.ModLoader.IsModSystemEnabled("RPTTS.RPTTSAPI"))
        {
            return false;
        }

        var detectedApi = _api.ModLoader.GetModSystem("RPTTS.RPTTSAPI");
        if (detectedApi == null)
        {
            _api.Logger.Warning("[THEBASICS] RPTTS reported enabled but API instance unavailable after delayed initialization.");
            return false;
        }

        _rpttsApi = detectedApi;
        _rpttsChatSystem = _api.ModLoader.GetModSystem("RPTTS.TTSChatSystem");
        _usingRptts = true;
        ApplyRpttsExplicitMode();
        return true;
        }
        catch (System.Exception ex)
        {
            _api.Logger.Warning($"[THEBASICS] Failed to initialize RPTTS integration: {ex}");
            return false;
        }
    }

    private static void ApplyRpttsExplicitMode()
    {
        if (_rpttsExplicitModeApplied || _rpttsChatSystem == null)
        {
            return;
        }

        try
        {
            ((dynamic)_rpttsChatSystem).OverwriteChatSubscription(false);
            _rpttsExplicitModeApplied = true;
        }
        catch (System.Exception ex)
        {
            _api?.Logger.Warning($"[THEBASICS] Failed to disable RPTTS chat subscription: {ex}");
        }
    }

    private static void OnProximitySpeechMessage(ProximitySpeechMessage speechMessage)
    {
        if (!_usingRptts || _rpttsApi == null || speechMessage == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(speechMessage.Text))
        {
            return;
        }

        try
        {
            _rpttsApi?.APIChatMessage(speechMessage.Text, (float?)speechMessage.Gain, (float?)speechMessage.Falloff);
        }
        catch (System.Exception ex)
        {
            _api.Logger.Warning($"[THEBASICS] Failed to dispatch RPTTS speech: {ex}");
        }
    }

    /*
     * Prevent automatic chat channel switching when user is in proximity tab
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "OnNewServerToClientChatLine")]
    public static void OnNewServerToClientChatLine_PreventAutoSwitch()
    {
        if (_config?.PreventProximityChannelSwitching == true && _proximityGroupId.HasValue)
        {
            var game = (ClientMain)_api.World;
            _api.Logger.Debug($"[THEBASICS] OnNewServerToClientChatLine - Current group: {game.currentGroupid}, Proximity group: {_proximityGroupId.Value}");
            
            if (game.currentGroupid == _proximityGroupId.Value)
            {
                // User is in proximity tab - temporarily disable the client setting that causes auto-switching
                _originalAutoChatOpenSelected = ClientSettings.AutoChatOpenSelected;
                ClientSettings.AutoChatOpenSelected = false; // This prevents the auto-switching logic
                _api.Logger.Debug($"[THEBASICS] Preventing auto-switch - saved original AutoChatOpenSelected: {_originalAutoChatOpenSelected}");
            }
        }
        else
        {
            _api.Logger.Debug($"[THEBASICS] OnNewServerToClientChatLine - Not preventing: config={_config?.PreventProximityChannelSwitching}, proximityId={_proximityGroupId}");
        }
    }

    /*
     * Restore the original AutoChatOpenSelected setting after message processing
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnNewServerToClientChatLine")]
    public static void OnNewServerToClientChatLine_RestoreAutoSwitch()
    {
        if (_config?.PreventProximityChannelSwitching == true && _originalAutoChatOpenSelected.HasValue)
        {
            // Restore the original setting
            ClientSettings.AutoChatOpenSelected = _originalAutoChatOpenSelected.Value;
            _api.Logger.Debug($"[THEBASICS] Restored AutoChatOpenSelected to: {_originalAutoChatOpenSelected.Value}");
            _originalAutoChatOpenSelected = null;
        }
    }

    private static bool? _originalAutoChatOpenSelected = null;

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
            int targetGroupId = (_config.PreserveDefaultChatChoice && _lastSelectedGroupId != null) ? _lastSelectedGroupId.Value :
            (_config.ProximityChatAsDefault && _proximityGroupId != null) ? _proximityGroupId.Value : GlobalConstants.GeneralChatGroup;

            // Get the tab index for the target group
            System.Reflection.MethodInfo tabIndexMethod = typeof(HudDialogChat).GetMethod("tabIndexByGroupId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (tabIndexMethod != null)
            {
                int tabIndex = (int)tabIndexMethod.Invoke(__instance, [targetGroupId]);
                if (tabIndex >= 0)
                {
                    // Set the visual tab state - this will trigger OnTabClicked automatically
                    __instance.Composers["chat"].GetHorizontalTabs("tabs").SetValue(tabIndex, callhandler: true);
                    _api.Logger.Debug($"[THEBASICS] Set active tab to index {tabIndex} for group {targetGroupId}");
                }
                else
                {
                    _api.Logger.Warning($"[THEBASICS] Could not find tab index for group {targetGroupId}");
                }
            }
            else
            {
                _api.Logger.Error("[THEBASICS] Could not find tabIndexByGroupId method via reflection");
            }
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error in PostfixOnGuiOpened: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnTabClicked")]
    public static void PostfixOnTabClicked(int groupId)
    {
        if (_config == null)
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
            _lastSelectedGroupId = groupId;
            
            // Use safe packet sending with connection checking and retry mechanism
            _safeNetworkChannel?.SendPacketSafely(new ChannelSelectedMessage() { GroupId = _lastSelectedGroupId });
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
            _lastSelectedGroupId = null;
            _pendingConfigActions.Clear();
            _safeNetworkChannel?.Dispose();
            _safeNetworkChannel = null;
        }
        catch (System.Exception e)
        {
            if (_api != null)
            {
                _api.Logger.Error($"[THEBASICS] Error in Dispose: {e}");
            }
        }
    }

    private static bool? _originalAutoChatOpenSelectedFinalize = null;

    /*
     * Prevent automatic chat channel switching in OnFinalizeFrame when user is in proximity tab
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "OnFinalizeFrame")]
    public static void OnFinalizeFrame_PreventAutoSwitch()
    {
        if (_config?.PreventProximityChannelSwitching == true && _proximityGroupId.HasValue)
        {
            var game = (ClientMain)_api.World;
            if (game.currentGroupid == _proximityGroupId.Value)
            {
                // User is in proximity tab - temporarily disable the client setting that causes auto-switching
                _originalAutoChatOpenSelectedFinalize = ClientSettings.AutoChatOpenSelected;
                ClientSettings.AutoChatOpenSelected = false; // This prevents the auto-switching logic in OnFinalizeFrame
            }
        }
    }

    /*
     * Restore the original AutoChatOpenSelected setting after OnFinalizeFrame processing
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnFinalizeFrame")]
    public static void OnFinalizeFrame_RestoreAutoSwitch()
    {
        if (_config?.PreventProximityChannelSwitching == true && _originalAutoChatOpenSelectedFinalize.HasValue)
        {
            // Restore the original setting
            ClientSettings.AutoChatOpenSelected = _originalAutoChatOpenSelectedFinalize.Value;
            _originalAutoChatOpenSelectedFinalize = null;
        }
    }
}
