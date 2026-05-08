using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

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
    private static bool _lastClientChannelConnected;
    private static bool _usingRptts = false;
    private static dynamic _rpttsApi = null;
    private static dynamic _rpttsChatSystem = null;
    private static CharacterSheetDialog _characterSheetDialog;
    private static CharacterSheetMessageDialog _characterSheetMessageDialog;
    private static bool _pendingCharacterSheetSave;
    private static bool _pendingCharacterSheetOpenFromCharacterDialog;
    private static bool _suppressNextCharacterDialogSheetOpen;
    private static bool _characterSheetOpenedFromCharacterDialog;
    private static CharacterSheetViewMessage _lastOwnCharacterSheetView;
    private static string _characterDialogTitleOverride;
    private static GuiDialogCharacterBase _characterDialog;
    private static bool _characterDialogHooked;
    private const int RpttsInitMaxAttempts = 3;
    private static int _rpttsInitAttempts = 0;
    private static bool _rpttsInitScheduled = false;
    private static bool _rpttsExplicitModeApplied = false;

    private static TypingIndicatorRenderer _typingIndicatorRenderer;
    private static PlacedBubbleRenderer _placedBubbleRenderer;
    private static readonly Dictionary<long, ChatTypingIndicatorState> _typingStatesByEntityId = new Dictionary<long, ChatTypingIndicatorState>();
    private static ChatTypingIndicatorState? _lastSentTypingState;
    private static string _lastChatInputText;
    private static long _lastChatInputChangeMs;
    private static GuiJsonDialog _configAdminDialog;
    private static Dictionary<string, string> _configAdminDraft = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _configAdminReviewedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string _configAdminStatusMessage;
    private static string _configAdminSelectedGroup;

    private static void DebugLog(string message)
    {
        if (_config?.DebugMode == true)
        {
            _api?.Logger.Debug(message);
        }
    }

    internal static bool IsDebugModeEnabled()
    {
        return _config?.DebugMode == true;
    }

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
        RegisterCharacterSheetUi(api);

        // Renderer is cheap when disabled; keep it always registered.
        _typingIndicatorRenderer = new TypingIndicatorRenderer(api);
        api.Event.RegisterRenderer(_typingIndicatorRenderer, EnumRenderStage.Ortho, "thebasics-typingindicator");

        _placedBubbleRenderer = new PlacedBubbleRenderer(api);
        api.Event.RegisterRenderer(_placedBubbleRenderer, EnumRenderStage.Ortho, "thebasics-placedbubbles");

        // Register event handlers
        _api.Event.PlayerJoin += OnPlayerJoin;
        _api.Event.PlayerEntitySpawn += OnPlayerEntitySpawn;
        // _api.Event.PlayerLeave += OnPlayerLeave;

        // Initialize Harmony patches if needed
        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            DebugLog("[THEBASICS] Applying Harmony patches");
            _harmony = new Harmony(Mod.Info.ModID);
            _harmony.PatchAll();
        }

    }

    private static void RegisterCharacterSheetUi(ICoreClientAPI api)
    {
        api.Input.RegisterHotKey("thebasicscharsheet", Lang.Get("thebasics:charsheet-gui-title"), GlKeys.B, HotkeyType.HelpAndOverlays, altPressed: false, ctrlPressed: true, shiftPressed: true);
        api.Input.SetHotKeyHandler("thebasicscharsheet", OnCharacterSheetHotkey);
        HookCharacterDialog(api);
    }

    private static bool OnCharacterSheetHotkey(KeyCombination combination)
    {
        if (_characterSheetDialog?.IsOpened() == true)
        {
            _characterSheetDialog.TryClose();
            return true;
        }

        RequestOwnCharacterSheet();
        return true;
    }

    private static void HookCharacterDialog(ICoreClientAPI api)
    {
        if (_characterDialogHooked)
        {
            return;
        }

        _characterDialog = api.Gui.LoadedGuis.Find(dialog => dialog is GuiDialogCharacterBase) as GuiDialogCharacterBase;
        if (_characterDialog == null)
        {
            api.Event.RegisterCallback(_ => HookCharacterDialog(api), 1000);
            return;
        }

        _characterDialog.ComposeExtraGuis += OnCharacterDialogComposed;
        _characterDialogHooked = true;
    }

    private static void OnCharacterDialogComposed()
    {
        if (_suppressNextCharacterDialogSheetOpen)
        {
            _suppressNextCharacterDialogSheetOpen = false;
            return;
        }

        if (_config == null || !_config.EnableCharacterSheets || _characterSheetDialog?.IsOpened() == true || _pendingCharacterSheetOpenFromCharacterDialog)
        {
            return;
        }

        _characterSheetOpenedFromCharacterDialog = true;
        if (_lastOwnCharacterSheetView != null)
        {
            OpenCharacterSheetDialog(_lastOwnCharacterSheetView);
        }

        _pendingCharacterSheetOpenFromCharacterDialog = true;
        RequestOwnCharacterSheet();
    }

    private static void RequestOwnCharacterSheet()
    {
        SendCharacterSheetRequest(new CharacterSheetOpenRequest { Mode = CharacterSheetOpenRequest.ModeOwn });
    }

    private static void SendCharacterSheetRequest(CharacterSheetOpenRequest request)
    {
        _pendingCharacterSheetSave = false;
        _safeNetworkChannel?.SendPacketSafely(request);
    }

    private static void SendCharacterSheetSaveRequest(CharacterSheetSaveRequest request)
    {
        _pendingCharacterSheetSave = true;
        _safeNetworkChannel?.SendPacketSafely(request);
    }

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
            DebugLog("[THEBASICS] Action queued until config is received");
        }
    }

    // Process all queued actions
    private static void ProcessConfigActionQueue()
    {
        DebugLog($"[THEBASICS] Processing {_pendingConfigActions.Count} queued actions");

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
            DebugLog("THEBASICS - Local player joined, attempting to send ready message to server");

            // Use safe packet sending with connection checking and retry mechanism
            // The server will only send config after receiving this ready message
            _safeNetworkChannel?.SendPacketSafely(new TheBasicsClientReadyMessage());
        }
    }

    private static void OnPlayerEntitySpawn(IClientPlayer byPlayer)
    {
        ApplyClientNametagSettings(byPlayer?.Entity);
    }

    private void RegisterForServerSideConfig()
    {
        _clientConfigChannel = _api.Network.RegisterChannel("thebasics")
            .RegisterMessageType<TheBasicsConfigMessage>()
            .RegisterMessageType<TheBasicsConfigAdminOpenMessage>()
            .RegisterMessageType<TheBasicsConfigAdminSaveMessage>()
            .RegisterMessageType<TheBasicsConfigAdminResultMessage>()
            .RegisterMessageType<TheBasicsClientReadyMessage>()
            .RegisterMessageType<ChannelSelectedMessage>()
            .RegisterMessageType<ProximitySpeechMessage>()
            .RegisterMessageType<ChatTypingStateMessage>()
            .RegisterMessageType<ChatterSoundMessage>()
            .RegisterMessageType<PlacedEnvironmentMessage>()
            .RegisterMessageType<CharacterSheetOpenRequest>()
            .RegisterMessageType<CharacterSheetSaveRequest>()
            .RegisterMessageType<CharacterSheetViewMessage>()
            .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage)
            .SetMessageHandler<TheBasicsConfigAdminOpenMessage>(OnConfigAdminOpenMessage)
            .SetMessageHandler<TheBasicsConfigAdminResultMessage>(OnConfigAdminResultMessage)
            .SetMessageHandler<ProximitySpeechMessage>(OnProximitySpeechMessage)
            .SetMessageHandler<ChatTypingStateMessage>(OnChatTypingStateMessage)
            .SetMessageHandler<ChatterSoundMessage>(OnChatterSoundMessage)
            .SetMessageHandler<PlacedEnvironmentMessage>(OnPlacedEnvironmentMessage)
            .SetMessageHandler<CharacterSheetViewMessage>(OnCharacterSheetViewMessage);

        // Initialize the safe network channel wrapper
        var config = new SafeClientNetworkChannel.SafeNetworkChannelConfig
        {
            LogPrefix = "[THEBASICS]",
            EnableDebugLogging = false,
            RetryDelayMs = 2000,
            MaxRetries = 10
        };
        _safeNetworkChannel = new SafeClientNetworkChannel(_clientConfigChannel, _api, config);
    }

    private static void OnCharacterSheetViewMessage(CharacterSheetViewMessage message)
    {
        if (message == null)
        {
            _pendingCharacterSheetOpenFromCharacterDialog = false;
            return;
        }

        UpdateLocalCharacterDisplayName(message);
        CacheOwnCharacterSheetView(message);

        if (!message.Success || message.IsErrorResponse)
        {
            _pendingCharacterSheetSave = false;
            _pendingCharacterSheetOpenFromCharacterDialog = false;
            _characterSheetMessageDialog?.TryClose();
            _api.TriggerIngameError(_api.World, "charsheet", message.Message);
            return;
        }

        if (message.SuppressDialogOpen)
        {
            _pendingCharacterSheetSave = false;
            _pendingCharacterSheetOpenFromCharacterDialog = false;
            RefreshCharacterDialogTitle();
            if (_characterSheetDialog?.IsOpened() == true)
            {
                _characterSheetDialog.SetView(message);
            }

            return;
        }

        if (message.IsSaveResponse || _pendingCharacterSheetSave)
        {
            _pendingCharacterSheetSave = false;
            _pendingCharacterSheetOpenFromCharacterDialog = false;
            RefreshCharacterDialogTitle();
            var saveMessage = message.Message;
            message.Message = string.Empty;

            OpenCharacterSheetDialog(message);

            ShowCharacterSheetMessage(saveMessage);
            return;
        }

        _pendingCharacterSheetSave = false;
        _pendingCharacterSheetOpenFromCharacterDialog = false;

        OpenCharacterSheetDialog(message);
    }

    private static void OpenCharacterSheetDialog(CharacterSheetViewMessage message)
    {
        if (_characterSheetDialog == null)
        {
            _characterSheetDialog = new CharacterSheetDialog(_api, message, SendCharacterSheetSaveRequest);
        }
        else
        {
            _characterSheetDialog.SetView(message);
        }

        if (!_characterSheetDialog.IsOpened())
        {
            _characterSheetDialog.TryOpen();
        }
    }

    private static void CacheOwnCharacterSheetView(CharacterSheetViewMessage message)
    {
        if (_api?.World?.Player == null || message.TargetPlayerUid != _api.World.Player.PlayerUID || !message.Success)
        {
            return;
        }

        _lastOwnCharacterSheetView = message;
    }

    private static void UpdateLocalCharacterDisplayName(CharacterSheetViewMessage message)
    {
        if (_api?.World?.Player == null || message.TargetPlayerUid != _api.World.Player.PlayerUID || string.IsNullOrWhiteSpace(message.DisplayName))
        {
            return;
        }

        _characterDialogTitleOverride = message.DisplayName;
    }

    private static void RefreshCharacterDialogTitle()
    {
        if (_characterDialog?.IsOpened() != true)
        {
            return;
        }

        _suppressNextCharacterDialogSheetOpen = true;
        _characterDialog.OnGuiOpened();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Vintagestory.API.Client.GuiComposerHelpers), "AddDialogTitleBar")]
    public static void GuiComposerHelpers_AddDialogTitleBar_Prefix(GuiComposer composer, ref string text)
    {
        if (composer?.DialogName != "playercharacter" || string.IsNullOrWhiteSpace(_characterDialogTitleOverride))
        {
            return;
        }

        var characterClass = _api?.World?.Player?.Entity?.WatchedAttributes?.GetString("characterClass");
        if (!string.IsNullOrWhiteSpace(characterClass) && Lang.HasTranslation("characterclass-" + characterClass))
        {
            text = Lang.Get("characterdialog-title-nameandclass", _characterDialogTitleOverride, Lang.Get("characterclass-" + characterClass));
            return;
        }

        text = _characterDialogTitleOverride;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogCharacter), "OnGuiClosed")]
    public static void GuiDialogCharacter_OnGuiClosed_Postfix()
    {
        _pendingCharacterSheetOpenFromCharacterDialog = false;
        if (_characterSheetOpenedFromCharacterDialog && _characterSheetDialog?.IsOpened() == true)
        {
            _characterSheetDialog.TryClose();
        }

        _characterSheetOpenedFromCharacterDialog = false;
    }

    private static void ShowCharacterSheetMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _characterSheetMessageDialog?.TryClose();
        _characterSheetMessageDialog = new CharacterSheetMessageDialog(_api, message);
        _characterSheetMessageDialog.TryOpen();
    }

    private void OnConfigAdminOpenMessage(TheBasicsConfigAdminOpenMessage message)
    {
        if (message?.Config != null)
        {
            _config = message.Config;
            _safeNetworkChannel?.SetEnableDebugLogging(_config.DebugMode);
        }

        UpdateConfigAdminDraft(message?.Values, message?.ReviewedKeys, message?.StatusMessage);
        OpenConfigAdminDialog();
    }

    private void OnConfigAdminResultMessage(TheBasicsConfigAdminResultMessage message)
    {
        if (message?.Config != null)
        {
            _config = message.Config;
            _safeNetworkChannel?.SetEnableDebugLogging(_config.DebugMode);
        }

        if (!string.IsNullOrWhiteSpace(message?.Message))
        {
            _api.ShowChatMessage(message.Message);
        }

        UpdateConfigAdminDraft(message?.Values, message?.ReviewedKeys, message?.Message);
        OpenConfigAdminDialog();
    }

    private static void UpdateConfigAdminDraft(IEnumerable<ConfigAdminSettingValue> values, IEnumerable<string> reviewedKeys, string statusMessage)
    {
        _configAdminDraft = ConfigAdminSettingRegistry.Settings.ToDictionary(
            setting => setting.Key,
            setting => _config == null ? string.Empty : setting.GetValue(_config),
            StringComparer.OrdinalIgnoreCase);

        if (values != null)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value?.Key))
                {
                    _configAdminDraft[value.Key] = value.Value ?? string.Empty;
                }
            }
        }

        _configAdminReviewedKeys = new HashSet<string>(reviewedKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _configAdminStatusMessage = statusMessage;
    }

    private static void OpenConfigAdminDialog()
    {
        if (_api == null)
        {
            return;
        }

        _configAdminDialog?.TryClose();
        _configAdminDialog = new GuiJsonDialog(BuildConfigAdminDialogSettings(), _api, focusFirstElement: false);
        _configAdminDialog.TryOpen();
    }

    private static JsonDialogSettings BuildConfigAdminDialogSettings()
    {
        var rows = new List<DialogRow>
        {
            new(new DialogElement
            {
                Code = "title",
                Type = EnumDialogElementType.Text,
                Text = Lang.Get("thebasics:config-admin-title"),
                Width = 720,
                Height = 32,
                FontSize = 18
            })
        };

        if (!string.IsNullOrWhiteSpace(_configAdminStatusMessage))
        {
            rows.Add(new DialogRow(new DialogElement
            {
                Code = "status",
                Type = EnumDialogElementType.Text,
                Text = _configAdminStatusMessage,
                Width = 720,
                Height = 42,
                FontSize = 13
            }));
        }

        var groups = GetConfigAdminGroups();
        _configAdminSelectedGroup = NormalizeConfigAdminGroup(_configAdminSelectedGroup, groups);
        rows.Add(new DialogRow(new DialogElement
        {
            Code = "group-select",
            Label = "thebasics:config-admin-group",
            Tooltip = "thebasics:config-admin-group-tooltip",
            Type = EnumDialogElementType.Select,
            Mode = EnumDialogElementMode.DropDown,
            Values = groups.ToArray(),
            Names = groups.ToArray(),
            Width = 720,
            Height = 28
        })
        {
            TopPadding = 8,
            BottomPadding = 6
        });

        foreach (var group in ConfigAdminSettingRegistry.Settings.Where(setting => string.Equals(setting.Group, _configAdminSelectedGroup, StringComparison.OrdinalIgnoreCase)).GroupBy(setting => setting.Group))
        {
            rows.Add(new DialogRow(new DialogElement
            {
                Code = "group-" + group.Key,
                Type = EnumDialogElementType.Text,
                Text = group.Key,
                Width = 720,
                Height = 26,
                FontSize = 15
            })
            {
                TopPadding = 8,
                BottomPadding = 2
            });

            foreach (var setting in group.OrderBy(setting => _configAdminReviewedKeys.Contains(setting.Key) ? 1 : 0).ThenBy(setting => setting.Label))
            {
                rows.Add(new DialogRow(CreateConfigAdminElement(setting))
                {
                    BottomPadding = 4
                });
            }
        }

        rows.Add(new DialogRow(
            CreateButton("save", Lang.Get("thebasics:config-admin-save"), Lang.Get("thebasics:config-admin-save-tooltip")),
            CreateButton("mark-reviewed", Lang.Get("thebasics:config-admin-mark-reviewed"), Lang.Get("thebasics:config-admin-mark-reviewed-tooltip")),
            CreateButton("reload", Lang.Get("thebasics:config-admin-reload"), Lang.Get("thebasics:config-admin-reload-tooltip")),
            CreateButton("close", Lang.Get("thebasics:config-admin-close"), Lang.Get("thebasics:config-admin-close-tooltip")))
        {
            TopPadding = 12
        });

        return new JsonDialogSettings
        {
            Code = "thebasics-config-admin",
            Alignment = EnumDialogArea.CenterMiddle,
            Rows = rows.ToArray(),
            SizeMultiplier = 0.9,
            Padding = 16,
            DisableWorldInteract = true,
            OnGet = GetConfigAdminValue,
            OnSet = SetConfigAdminValue
        };
    }

    private static List<string> GetConfigAdminGroups()
    {
        return ConfigAdminSettingRegistry.Settings
            .Select(setting => setting.Group)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeConfigAdminGroup(string group, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0)
        {
            return string.Empty;
        }

        return groups.FirstOrDefault(candidate => string.Equals(candidate, group, StringComparison.OrdinalIgnoreCase)) ?? groups[0];
    }

    private static DialogElement CreateConfigAdminElement(ConfigAdminSettingDefinition setting)
    {
        var label = _configAdminReviewedKeys.Contains(setting.Key) ? setting.Label : Lang.Get("thebasics:config-admin-new-prefix", setting.Label);
        var tooltip = setting.ReloadBehavior == ConfigAdminReloadBehavior.Live
            ? Lang.Get("thebasics:config-admin-live-tooltip", setting.Description)
            : Lang.Get("thebasics:config-admin-restart-tooltip", setting.Description);

        var element = new DialogElement
        {
            Code = setting.Key,
            Label = label,
            Tooltip = tooltip,
            Width = 720,
            Height = 28
        };

        switch (setting.Kind)
        {
            case ConfigAdminSettingKind.Boolean:
                element.Type = EnumDialogElementType.Switch;
                break;
            case ConfigAdminSettingKind.Integer:
            case ConfigAdminSettingKind.Decimal:
                element.Type = EnumDialogElementType.NumberInput;
                break;
            case ConfigAdminSettingKind.Select:
                element.Type = EnumDialogElementType.Select;
                element.Mode = EnumDialogElementMode.DropDown;
                element.Values = setting.Options.ToArray();
                element.Names = setting.OptionNames.ToArray();
                break;
            default:
                element.Type = EnumDialogElementType.Input;
                break;
        }

        return element;
    }

    private static DialogElement CreateButton(string code, string text, string tooltip)
    {
        return new DialogElement
        {
            Code = code,
            Type = EnumDialogElementType.Button,
            Text = text,
            Tooltip = tooltip,
            Width = 150,
            Height = 34,
            FontSize = 13
        };
    }

    private static string GetConfigAdminValue(string code)
    {
        if (code == "group-select")
        {
            return _configAdminSelectedGroup ?? string.Empty;
        }

        return _configAdminDraft.TryGetValue(code, out var value) ? value : string.Empty;
    }

    private static void SetConfigAdminValue(string code, string value)
    {
        switch (code)
        {
            case "save":
                SendConfigAdminSave();
                break;
            case "mark-reviewed":
                SendConfigAdminMarkReviewed();
                break;
            case "reload":
                SendConfigAdminReload();
                break;
            case "close":
                _configAdminDialog?.TryClose();
                break;
            case "group-select":
                _configAdminSelectedGroup = NormalizeConfigAdminGroup(value, GetConfigAdminGroups());
                OpenConfigAdminDialog();
                break;
            default:
                if (ConfigAdminSettingRegistry.TryGet(code, out _))
                {
                    _configAdminDraft[code] = value ?? string.Empty;
                }
                break;
        }
    }

    private static void SendConfigAdminSave()
    {
        _safeNetworkChannel?.SendPacketSafely(new TheBasicsConfigAdminSaveMessage
        {
            Values = _configAdminDraft
                .Select(kvp => new ConfigAdminSettingValue { Key = kvp.Key, Value = kvp.Value })
                .ToList()
        });
    }

    private static void SendConfigAdminMarkReviewed()
    {
        _safeNetworkChannel?.SendPacketSafely(new TheBasicsConfigAdminSaveMessage
        {
            MarkReviewedKeys = ConfigAdminSettingRegistry.Settings.Select(setting => setting.Key).ToList()
        });
    }

    private static void SendConfigAdminReload()
    {
        _safeNetworkChannel?.SendPacketSafely(new TheBasicsConfigAdminSaveMessage
        {
            ReloadFromDisk = true
        });
    }

    private static void OnChatTypingStateMessage(ChatTypingStateMessage message)
    {
        if (message == null || message.EntityId == 0)
        {
            return;
        }

        var state = message.State;
        if (state == ChatTypingIndicatorState.None)
        {
            // Backwards compatibility: older clients/servers only set IsTyping.
            state = message.IsTyping ? ChatTypingIndicatorState.Typing : ChatTypingIndicatorState.None;
        }

        if (state == ChatTypingIndicatorState.None)
        {
            _typingStatesByEntityId.Remove(message.EntityId);
        }
        else
        {
            _typingStatesByEntityId[message.EntityId] = state;
        }
    }

    private static void OnPlacedEnvironmentMessage(PlacedEnvironmentMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.BubbleText))
        {
            return;
        }

        var worldPos = new Vintagestory.API.MathTools.Vec3d(message.X, message.Y, message.Z);
        _placedBubbleRenderer?.AddBubble(worldPos, message.BubbleText);
    }

    internal static ChatTypingIndicatorState GetEntityTypingIndicatorState(long entityId)
    {
        return _typingStatesByEntityId.TryGetValue(entityId, out var state) ? state : ChatTypingIndicatorState.None;
    }

    internal static bool IsEntityTyping(long entityId)
    {
        return GetEntityTypingIndicatorState(entityId) == ChatTypingIndicatorState.Typing;
    }

    internal static bool IsTypingIndicatorEnabled()
    {
        return _config?.EnableTypingIndicator == true;
    }

    internal static int GetTypingIndicatorRange()
    {
        return GetEffectiveTypingIndicatorRange(_config);
    }

    internal static int GetEffectiveTypingIndicatorRange(ModConfig config)
    {
        if (config == null)
        {
            return 0;
        }

        return Math.Max(0, config.TypingIndicatorMaxRange);
    }

    internal static bool DoNametagsRequireLineOfSight()
    {
        return _config?.NametagRequiresLineOfSight == true;
    }

    internal static bool IsSpeechBubbleVtmlEnabled()
    {
        return _config != null &&
               !_config.DisableRPChat &&
               OverheadChatBubbleModes.Normalize(_config.OverheadChatBubbleMode, _config.DisableRpOverheadBubbles) == OverheadChatBubbleModes.RpText;
    }

    internal static int GetSpeechBubbleMinimumDisplayMilliseconds()
    {
        return Math.Max(0, _config?.SpeechBubbleMinimumDisplayMilliseconds ?? 3500);
    }

    internal static Models.TypingIndicatorDisplayMode GetTypingIndicatorDisplayMode()
    {
        return _config?.TypingIndicatorDisplayMode ?? Models.TypingIndicatorDisplayMode.Icon;
    }

    internal static string GetTypingIndicatorText()
    {
        var overrideText = _config?.TypingIndicatorTextOverride;
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            return overrideText;
        }

        return Lang.Get("thebasics:typingindicator-typing-text");
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

            // Apply debug mode to networking helpers now that we have config.
            _safeNetworkChannel?.SetEnableDebugLogging(_config.DebugMode);

            _proximityGroupId = configMessage.ProximityGroupId;
            _lastSelectedGroupId = configMessage.LastSelectedGroupId;

            DebugLog($"[THEBASICS] Received server config: PreventProximityChannelSwitching={_config.PreventProximityChannelSwitching}, ProximityId={_proximityGroupId}, LastSelectedGroupId={_lastSelectedGroupId}");
            DebugLog($"[THEBASICS] Full config received from server with settings: ProximityChatName={_config.ProximityChatName}, UseGeneralChannelAsProximityChat={_config.UseGeneralChannelAsProximityChat}, PreserveDefaultChatChoice={_config.PreserveDefaultChatChoice}, ProximityChatAsDefault={_config.ProximityChatAsDefault}");
            NameTagRenderRangePatches.ClearCache();

            // Process any actions that were waiting for config
            if (_pendingConfigActions.Count > 0)
            {
                ProcessConfigActionQueue();
            }

            ApplyClientNametagSettingsToLoadedPlayers();
        }
        catch (System.Exception e)
        {
            _api.Logger.Error($"[THEBASICS] Error processing server config message: {e}");
        }
    }

    private static void ApplyClientNametagSettingsToLoadedPlayers()
    {
        if (_api?.World?.LoadedEntities == null || _config == null)
        {
            return;
        }

        foreach (var entity in _api.World.LoadedEntities.Values)
        {
            ApplyClientNametagSettings(entity);
        }
    }

    private static void ApplyClientNametagSettings(Entity entity)
    {
        if (_config == null || entity is not EntityPlayer)
        {
            return;
        }

        NameTagRenderRangePatches.ApplyConfiguredNametagSettings(
            entity,
            _config.HideNametagUnlessTargeting,
            _config.NametagRenderRange);
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

    // Cached reflection fields for EntityTalkUtil (protected fields we need to override)
    private static FieldInfo _lettersLeftToTalkField;
    private static FieldInfo _totalLettersToTalkField;
    private static bool _talkUtilFieldsResolved;

    private static void ResolveTalkUtilFields()
    {
        if (_talkUtilFieldsResolved) return;

        try
        {
            var talkUtilType = typeof(EntityTalkUtil);
            _lettersLeftToTalkField = talkUtilType.GetField("lettersLeftToTalk",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            _totalLettersToTalkField = talkUtilType.GetField("totalLettersToTalk",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            if (_lettersLeftToTalkField == null || _totalLettersToTalkField == null)
            {
                _api?.Logger.Warning("[THEBASICS] Could not resolve EntityTalkUtil fields for chatter note count override. Chatter will use default note counts.");
            }

            // Only mark resolved after successful attempt (even if fields were not found)
            // so that exceptions during GetField allow a retry on the next message
            _talkUtilFieldsResolved = true;
        }
        catch (Exception ex)
        {
            _api?.Logger.Warning($"[THEBASICS] Failed to resolve EntityTalkUtil fields: {ex.Message}");
        }
    }

    private static void OnChatterSoundMessage(ChatterSoundMessage message)
    {
        if (message == null || _api == null)
        {
            return;
        }

        try
        {
            var entity = _api.World.GetEntityById(message.EntityId);
            if (entity == null)
            {
                return;
            }

            // NPCs expose TalkUtil via ITalkUtil; player entities expose the same utility directly.
            var talkUtilHolder = entity as ITalkUtil;
            var talkUtil = talkUtilHolder?.TalkUtil;
            if (talkUtil == null && entity is EntityPlayer entityPlayer)
            {
                talkUtil = entityPlayer.talkUtil;
            }
            if (talkUtil == null)
            {
                return;
            }

            // Apply volume and pitch modifiers for this chatter event
            talkUtil.SetModifiers(1f, message.Pitch, message.Volume);

            // Trigger the chatter — this sets lettersLeftToTalk internally.
            // VS defers note processing to subsequent game-loop ticks, so the reflection
            // override below is guaranteed to take effect before any note plays.
            var talkType = (EnumTalkType)message.TalkType;
            talkUtil.Talk(talkType);

            // Override the randomized note count with our logarithmically-scaled value.
            // These fields are protected, so we use cached reflection.
            ResolveTalkUtilFields();
            if (_lettersLeftToTalkField != null && _totalLettersToTalkField != null)
            {
                try
                {
                    _lettersLeftToTalkField.SetValue(talkUtil, message.NoteCount);
                    _totalLettersToTalkField.SetValue(talkUtil, message.NoteCount);
                }
                catch (Exception setEx)
                {
                    _api?.Logger.Warning($"[THEBASICS] Note count override failed (TalkUtil may not be EntityTalkUtil): {setEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[THEBASICS] Failed to play chatter sound: {ex}");
        }
    }

    /*
     * Prevent automatic chat channel switching when user is in proximity tab
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "OnNewServerToClientChatLine")]
    public static void OnNewServerToClientChatLine_PreventAutoSwitch(ref long __state)
    {
        __state = 0;
        if (IsDebugModeEnabled())
        {
            __state = PerfStats.Timestamp();
        }

        if (_config?.PreventProximityChannelSwitching == true && _proximityGroupId.HasValue)
        {
            var game = (ClientMain)_api.World;

            DebugLog($"[THEBASICS] OnNewServerToClientChatLine - Current group: {game.currentGroupid}, Proximity group: {_proximityGroupId.Value}");
            if (game.currentGroupid == _proximityGroupId.Value)
            {
                // User is in proximity tab - temporarily disable the client setting that causes auto-switching
                _originalAutoChatOpenSelected = ClientSettings.AutoChatOpenSelected;
                ClientSettings.AutoChatOpenSelected = false; // This prevents the auto-switching logic

                DebugLog($"[THEBASICS] Preventing auto-switch - saved original AutoChatOpenSelected: {_originalAutoChatOpenSelected}");
            }
        }
    }

    /*
     * Restore the original AutoChatOpenSelected setting after message processing
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnNewServerToClientChatLine")]
    public static void OnNewServerToClientChatLine_RestoreAutoSwitch(long __state, int groupId, string message, EnumChatType chattype)
    {
        if (_config?.PreventProximityChannelSwitching == true && _originalAutoChatOpenSelected.HasValue)
        {
            // Restore the original setting
            ClientSettings.AutoChatOpenSelected = _originalAutoChatOpenSelected.Value;

            DebugLog($"[THEBASICS] Restored AutoChatOpenSelected to: {_originalAutoChatOpenSelected.Value}");
            _originalAutoChatOpenSelected = null;
        }

        if (__state != 0)
        {
            PerfStats.Record(_api, "HudDialogChat.OnNewServerToClientChatLine", __state, PerfStats.Timestamp(),
                $"group={groupId}, type={chattype}, len={(message?.Length ?? 0)}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudDialogChat), "UpdateText")]
    public static void HudDialogChat_UpdateText_Prefix(ref long __state)
    {
        __state = 0;
        if (IsDebugModeEnabled())
        {
            __state = PerfStats.Timestamp();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "UpdateText")]
    public static void HudDialogChat_UpdateText_Postfix(long __state)
    {
        if (__state != 0)
        {
            PerfStats.Record(_api, "HudDialogChat.UpdateText", __state, PerfStats.Timestamp());
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
            DebugLog("[THEBASICS] OnGuiOpened queued until config is received");
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
                    DebugLog($"[THEBASICS] Set active tab to index {tabIndex} for group {targetGroupId}");
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
                _api.Event.PlayerEntitySpawn -= OnPlayerEntitySpawn;
                _typingIndicatorRenderer?.Dispose();
                _typingIndicatorRenderer = null;
                _placedBubbleRenderer?.Dispose();
                _placedBubbleRenderer = null;
                _api = null;
            }
            _harmony?.UnpatchAll(Mod.Info.ModID);
            _config = null;
            _lastSelectedGroupId = null;
            _pendingConfigActions.Clear();
            _safeNetworkChannel?.Dispose();
            _safeNetworkChannel = null;
            _configAdminDialog?.TryClose();
            _configAdminDialog = null;
            _configAdminDraft.Clear();
            _configAdminReviewedKeys.Clear();
            _configAdminSelectedGroup = null;
            _configAdminStatusMessage = null;

            // Clear static typing indicator state to prevent stale data on reconnect/world reload.
            _typingStatesByEntityId.Clear();
            _lastSentTypingState = null;
            _lastChatInputText = null;
            _lastChatInputChangeMs = 0;
            _lastClientChannelConnected = false;
            NameTagRenderRangePatches.ClearCache();
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
    public static void OnFinalizeFrame_RestoreAutoSwitch(HudDialogChat __instance, float dt)
    {
        if (_config?.PreventProximityChannelSwitching == true && _originalAutoChatOpenSelectedFinalize.HasValue)
        {
            // Restore the original setting
            ClientSettings.AutoChatOpenSelected = _originalAutoChatOpenSelectedFinalize.Value;
            _originalAutoChatOpenSelectedFinalize = null;
        }

        UpdateLocalTypingState(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudDialogChat), "OnGuiClosed")]
    public static void OnGuiClosed_TypingIndicator(HudDialogChat __instance)
    {
        // Ensure we don't leave stale typing state on the server.
        ForceLocalTypingState(ChatTypingIndicatorState.None);

        // Reset local tracking so reopening chat does not immediately report Typing
        // based on stale text/timestamps.
        _lastChatInputText = null;
        _lastChatInputChangeMs = 0;
    }

    private static void UpdateLocalTypingState(HudDialogChat chatDialog)
    {
        if (_api == null)
        {
            return;
        }

        if (_config?.EnableTypingIndicator != true)
        {
            // Feature disabled (or config not received yet) - ensure local state is off.
            ForceLocalTypingState(ChatTypingIndicatorState.None);
            return;
        }

        if (chatDialog == null)
        {
            ForceLocalTypingState(ChatTypingIndicatorState.None);
            return;
        }

        try
        {
            if (chatDialog.Composers == null || !chatDialog.Composers.ContainsKey("chat"))
            {
                ForceLocalTypingState(ChatTypingIndicatorState.None);
                return;
            }

            var chatInput = chatDialog.Composers["chat"].GetChatInput("chatinput");
            if (chatInput == null)
            {
                ForceLocalTypingState(ChatTypingIndicatorState.None);
                return;
            }

            var hasFocus = chatInput.HasFocus;
            var text = chatInput.GetText() ?? "";
            var nowMs = _api.ElapsedMilliseconds;

            if (text != _lastChatInputText)
            {
                _lastChatInputText = text;
                _lastChatInputChangeMs = nowMs;
            }

            if (!hasFocus)
            {
                ForceLocalTypingState(ChatTypingIndicatorState.None);
                return;
            }

            // Unified UX:
            // - Chat open, empty input => subtle indicator
            // - Chat open, has text => composing indicator
            // - Recent text changes => typing indicator
            if (text.Length == 0)
            {
                ForceLocalTypingState(ChatTypingIndicatorState.ChatOpenEmpty);
                return;
            }

            float timeoutSeconds = _config.TypingIndicatorTimeoutSeconds;
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = 5f;
            }

            bool isTyping = (nowMs - _lastChatInputChangeMs) <= (long)(timeoutSeconds * 1000f);
            ForceLocalTypingState(isTyping ? ChatTypingIndicatorState.Typing : ChatTypingIndicatorState.ChatOpenComposing);
        }
        catch
        {
            // Chat UI is a fragile surface area across VS versions.
            // Fail closed - never crash the client for a cosmetic feature.
            ForceLocalTypingState(ChatTypingIndicatorState.None);
        }
    }

    private static void ForceLocalTypingState(ChatTypingIndicatorState state)
    {
        // Best-effort: do not throw or spam retries for an ephemeral state.
        var connected = _clientConfigChannel?.Connected ?? false;
        if (!connected)
        {
            _lastClientChannelConnected = false;
            return;
        }

        if (!_lastClientChannelConnected)
        {
            // Channel just became connected - force a resend even if the state did not change.
            _lastClientChannelConnected = true;
            _lastSentTypingState = null;
        }

        if (_lastSentTypingState.HasValue && _lastSentTypingState.Value == state)
        {
            return;
        }

        try
        {
            _clientConfigChannel.SendPacket(new ChatTypingStateMessage
            {
                IsTyping = state == ChatTypingIndicatorState.Typing,
                State = state
            });

            _lastSentTypingState = state;
        }
        catch
        {
            // Ignore.
        }
    }
}
