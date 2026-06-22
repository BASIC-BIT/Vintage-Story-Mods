using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BasicConfig;
using DimensionLib.Api;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

internal enum PocketElevatorLandingMode
{
    RequireElevatorBlock,
    ClearHeadroomOnly,
    AutoPlaceElevatorIfMissing,
}

internal enum PocketCapabilityMode
{
    Disabled,
    Privilege,
    OwnerOrPrivilege,
    OwnerMemberOrPrivilege,
    Public,
}

/// <summary>
/// Small DimensionLib consumer mod. Keep this file readable enough to copy from as an integration example.
/// </summary>
public sealed class PocketDimensionModSystem : ModSystem, IDimensionPolicyProvider, IPocketWaystoneService
{
    private const string ModId = "pocketdimensions";
    private const string ApiCacheKey = "dimensionlib:api";
    private const string WaystoneServiceCacheKey = "pocketdimensions:waystone-service";
    private const string ConfigName = "pocket_dimensions.json";
    private const string PocketFloorBlockCode = "pocketdimensions:pocketfloor";
    private const string PocketWaystoneBlockCode = "pocketdimensions:pocketwaystone";
    private const string PocketReturnPedestalBlockCode = "pocketdimensions:pocketreturnpedestal";
    private const string PocketElevatorBlockCode = "pocketdimensions:pocketelevator";
    private const string DirectoryActionEnter = "enter";
    private const string DirectoryActionCreatePocket = "createpocket";
    private const string DirectoryActionCreateLayer = "createlayer";
    private const string DirectoryActionEditLayer = "editlayer";
    private const float PocketMinimumSceneLight = 0.18f;
    private const int HudUpdateTickMs = 250;
    private const int MaxDisplayNameLength = 48;

    private ICoreServerAPI _api;
    private ICoreClientAPI _clientApi;
    private IDimensionLibApi _dimensionLib;
    private PocketLinkStore _linkStore;
    private BasicConfigStore<PocketDimensionsConfig> _configStore;
    private BasicConfigServerController<PocketDimensionsConfig> _configController;
    private BasicConfigClientController _clientConfigController;
    private PocketDimensionsConfig _config = new PocketDimensionsConfig();
    private IServerNetworkChannel _serverChannel;
    private IClientNetworkChannel _clientChannel;
    private PocketCoordinatesHud _coordinatesHud;
    private PocketDirectoryDialog _directoryDialog;
    private long _hudUpdateListenerId;
    private readonly Dictionary<string, PocketWaystoneLink> _linksByEndpointId = new Dictionary<string, PocketWaystoneLink>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _activeIngressByPlayer = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, DimensionLocation>> _unanchoredReturnsByPlayer = new Dictionary<string, Dictionary<string, DimensionLocation>>(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _pendingTransfersByPlayer = new Dictionary<string, long>(StringComparer.Ordinal);
    private readonly Dictionary<string, PocketLayerStack> _layerStacksById = new Dictionary<string, PocketLayerStack>(StringComparer.Ordinal);
    private readonly HashSet<string> _playersWithPocketHud = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _layersNeedingInitialSend = new HashSet<string>(StringComparer.Ordinal);
    private long _nextPendingTransferId;

    public override double ExecuteOrder()
    {
        return 1.1;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("PocketWaystoneBlock", typeof(PocketWaystoneBlock));
        api.RegisterBlockClass("PocketReturnPedestalBlock", typeof(PocketReturnPedestalBlock));
        api.RegisterBlockClass("PocketElevatorBlock", typeof(PocketElevatorBlock));
        api.RegisterBlockEntityClass("PocketWaystone", typeof(PocketWaystoneBlockEntity));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _api = api;
        if (!api.ObjectCache.TryGetValue(ApiCacheKey, out var cachedApi) || cachedApi is not IDimensionLibApi dimensionLib)
        {
            api.Logger.Warning("[PocketDimensions] DimensionLib API was not available; pocket commands were not registered.");
            return;
        }

        _dimensionLib = dimensionLib;
        _serverChannel = api.Network.RegisterChannel(ModId)
            .RegisterMessageType<PocketElevatorTravelRequest>()
            .RegisterMessageType<PocketLayerCreationPrompt>()
            .RegisterMessageType<PocketLayerCreationResponse>()
            .RegisterMessageType<PocketElevatorPlacementPrompt>()
            .RegisterMessageType<PocketElevatorPlacementResponse>()
            .RegisterMessageType<PocketHudStateMessage>()
            .RegisterMessageType<PocketDirectoryRequest>()
            .RegisterMessageType<PocketDirectoryActionRequest>()
            .RegisterMessageType<PocketDirectoryStateMessage>()
            .RegisterMessageType<PocketDirectoryStackMessage>()
            .RegisterMessageType<PocketDirectoryLayerMessage>()
            .RegisterMessageType<BasicConfigOpenMessage>()
            .RegisterMessageType<BasicConfigSaveMessage>()
            .RegisterMessageType<BasicConfigResultMessage>()
            .SetMessageHandler<PocketElevatorTravelRequest>(OnElevatorTravelRequest)
            .SetMessageHandler<PocketLayerCreationResponse>(OnLayerCreationResponse)
            .SetMessageHandler<PocketElevatorPlacementResponse>(OnElevatorPlacementResponse)
            .SetMessageHandler<PocketDirectoryRequest>(OnPocketDirectoryRequest)
            .SetMessageHandler<PocketDirectoryActionRequest>(OnPocketDirectoryActionRequest)
            .SetMessageHandler<BasicConfigSaveMessage>((player, message) => _configController?.OnSaveMessage(player, message));
        _linkStore = new PocketLinkStore(api);
        LoadLinkState();
        api.ObjectCache[WaystoneServiceCacheKey] = this;
        _configStore = new BasicConfigStore<PocketDimensionsConfig>(api, ConfigName, "Pocket Dimensions", config => config.Normalize());
        _config = _configStore.GetOrLoad();
        _configController = new BasicConfigServerController<PocketDimensionsConfig>(new BasicConfigServerControllerOptions<PocketDimensionsConfig>
        {
            ConfigId = PocketDimensionsConfigSchema.ConfigId,
            DisplayName = "Pocket Dimensions",
            Store = _configStore,
            Schema = PocketDimensionsConfigSchema.Build(),
            Channel = _serverChannel,
            CanEdit = CanEditConfig,
            GetReviewedKeys = config => config.ReviewedConfigSettingKeys,
            SetReviewedKeys = (config, keys) => config.ReviewedConfigSettingKeys = keys,
            AfterChanged = _ => _config = _configStore.GetOrLoad()
        });
        var policyResult = _dimensionLib.RegisterPolicyProvider(ModId, this);
        if (!policyResult.Success)
        {
            api.Logger.Warning("[PocketDimensions] Failed to register DimensionLib policy provider: {0}", policyResult.Message);
        }

        RegisterCommands();
        ApplyRecipeConfig();
        api.Event.GameWorldSave += SaveLinkState;
        _hudUpdateListenerId = api.Event.RegisterGameTickListener(OnHudUpdateTick, HudUpdateTickMs);
        api.Logger.Notification("[PocketDimensions] Registered /pocket commands.");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientChannel = api.Network.RegisterChannel(ModId)
            .RegisterMessageType<PocketElevatorTravelRequest>()
            .RegisterMessageType<PocketLayerCreationPrompt>()
            .RegisterMessageType<PocketLayerCreationResponse>()
            .RegisterMessageType<PocketElevatorPlacementPrompt>()
            .RegisterMessageType<PocketElevatorPlacementResponse>()
            .RegisterMessageType<PocketHudStateMessage>()
            .RegisterMessageType<PocketDirectoryRequest>()
            .RegisterMessageType<PocketDirectoryActionRequest>()
            .RegisterMessageType<PocketDirectoryStateMessage>()
            .RegisterMessageType<PocketDirectoryStackMessage>()
            .RegisterMessageType<PocketDirectoryLayerMessage>()
            .RegisterMessageType<BasicConfigOpenMessage>()
            .RegisterMessageType<BasicConfigSaveMessage>()
            .RegisterMessageType<BasicConfigResultMessage>()
            .SetMessageHandler<PocketLayerCreationPrompt>(OnLayerCreationPrompt)
            .SetMessageHandler<PocketElevatorPlacementPrompt>(OnElevatorPlacementPrompt)
            .SetMessageHandler<PocketHudStateMessage>(OnPocketHudState)
            .SetMessageHandler<PocketDirectoryStateMessage>(OnPocketDirectoryState)
            .SetMessageHandler<BasicConfigOpenMessage>(message => _clientConfigController?.OnOpenMessage(message))
            .SetMessageHandler<BasicConfigResultMessage>(message => _clientConfigController?.OnResultMessage(message));

        _clientConfigController = new BasicConfigClientController(new BasicConfigClientOptions
        {
            ConfigId = PocketDimensionsConfigSchema.ConfigId,
            DisplayName = "Pocket Dimensions",
            Title = "Pocket Dimensions Config",
            DialogCode = "pocketdimensions-basicconfig-admin",
            Api = api,
            Settings = PocketDimensionsConfigSchema.Build().Settings.Cast<IBasicConfigSettingDefinition>().ToList(),
            SendPacket = packet =>
            {
                if (packet is BasicConfigSaveMessage saveMessage && _clientChannel?.Connected == true)
                {
                    _clientChannel.SendPacket(saveMessage);
                }
            }
        });

        api.Input.RegisterHotKey("pocketelevatorup", "Pocket Elevator Up", GlKeys.PageUp, HotkeyType.CharacterControls);
        api.Input.RegisterHotKey("pocketelevatordown", "Pocket Elevator Down", GlKeys.PageDown, HotkeyType.CharacterControls);
        api.Input.RegisterHotKey("pocketdirectory", "Pocket Directory", GlKeys.P, HotkeyType.HelpAndOverlays, ctrlPressed: true, shiftPressed: true);
        api.Input.SetHotKeyHandler("pocketelevatorup", _ => SendElevatorTravelRequest(1));
        api.Input.SetHotKeyHandler("pocketelevatordown", _ => SendElevatorTravelRequest(-1));
        api.Input.SetHotKeyHandler("pocketdirectory", OnPocketDirectoryHotkey);
        _coordinatesHud = new PocketCoordinatesHud(api);
    }

    public override void Dispose()
    {
        if (_api != null)
        {
            _api.Event.GameWorldSave -= SaveLinkState;
            if (_hudUpdateListenerId != 0)
            {
                _api.Event.UnregisterGameTickListener(_hudUpdateListenerId);
                _hudUpdateListenerId = 0;
            }

            SaveLinkState();
        }

        _coordinatesHud?.Dispose();
        _directoryDialog?.Dispose();

        base.Dispose();
    }

    public bool CanEnter(IServerPlayer player, Dimension dimension, out string reason)
    {
        reason = string.Empty;
        return CanAccessPocket(player, dimension, out reason);
    }

    public bool CanUseBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, out string reason)
    {
        return CanUsePocketBlock(player, dimension, out reason);
    }

    public bool CanMutateBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, DimensionBlockMutationKind mutationKind, out string reason)
    {
        reason = string.Empty;
        if (mutationKind == DimensionBlockMutationKind.Break && IsProtectedPocketBlock(blockSelection))
        {
            reason = "Pocket Dimension anchors are indestructible.";
            return false;
        }

        return CanMutatePocketBlock(player, dimension, out reason);
    }

    public string DisplayName(string dimensionId)
    {
        return ShortName(dimensionId);
    }

    private static string LayerDisplayName(PocketLayerStack stack, PocketLayerRef layer, string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(layer?.DisplayName))
        {
            return layer.DisplayName;
        }

        return layer?.Index == 0 && !string.IsNullOrWhiteSpace(stack?.DisplayName)
            ? stack.DisplayName
            : ShortName(dimensionId);
    }

    public DimensionLibResult EnterBoundPocket(IServerPlayer player, BlockSelection blockSelection)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (blockSelection?.Position == null)
        {
            return DimensionLibResult.Fail("This Waystone is not bound to a Pocket Dimension yet.", "pocketwaystone-unbound");
        }

        var blockEntity = _api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as PocketWaystoneBlockEntity;
        if (blockEntity == null || !blockEntity.IsBound)
        {
            return DimensionLibResult.Fail("This Waystone is not bound yet. Look at it and run /pocket bind <name>.", "pocketwaystone-unbound");
        }

        var dimensionId = blockEntity.BoundDimensionId;
        var lookup = _dimensionLib.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail($"Bound pocket '{DisplayName(dimensionId)}' is not available.", lookup.ErrorCode);
        }

        if (!IsOwnedPocket(lookup.Value))
        {
            return DimensionLibResult.Fail($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.", "not-pocket-dimension");
        }

        if (_dimensionLib.IsDimensionOrphaned(dimensionId))
        {
            return DimensionLibResult.Fail($"Bound pocket '{DisplayName(dimensionId)}' is orphaned.", "dimension-orphaned");
        }

        if (!CanUseWaystone(player, lookup.Value))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.UseWaystoneCapabilityMode, _config.UseWaystonePrivilege, "use a Pocket Waystone"), "missing-waystone-use-privilege");
        }

        var linked = RegisterWaystoneLink(blockEntity, blockSelection.Position, lookup.Value, player);
        if (!linked.Success)
        {
            return DimensionLibResult.Fail(linked.Message, linked.ErrorCode);
        }

        if (_config.PrepareChunksDuringTeleportDelay)
        {
            var prepared = EnsurePocketInfrastructure(lookup.Value, player);
            if (!prepared.Success)
            {
                return prepared;
            }
        }

        return BeginDelayedWaystoneEntry(player, blockSelection.Position.Copy(), lookup.Value.DimensionId, linked.Value.EndpointId);
    }

    public DimensionLibResult ReturnFromPocket(IServerPlayer player, BlockSelection blockSelection = null)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (blockSelection != null && !HasPrivilege(player, _config.UseReturnPedestalPrivilege))
        {
            return DimensionLibResult.Fail($"Missing privilege '{_config.UseReturnPedestalPrivilege}'.", "missing-return-pedestal-use-privilege");
        }

        var lookup = _dimensionLib.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        if (!lookup.Success || !IsOwnedPocket(lookup.Value))
        {
            return DimensionLibResult.Fail("You are not inside a Pocket Dimensions pocket.", "not-in-pocket");
        }

        var dimension = lookup.Value;

        if (_config.PrepareChunksDuringTeleportDelay)
        {
            var ensured = EnsurePocketInfrastructure(dimension, player);
            if (!ensured.Success)
            {
                return ensured;
            }
        }

        return BeginDelayedPocketReturn(player, dimension.DimensionId);
    }

    private DimensionLibResult CompletePocketReturn(IServerPlayer player, string dimensionId)
    {
        var lookup = _dimensionLib.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        if (!lookup.Success || !IsOwnedPocket(lookup.Value) || !string.Equals(lookup.Value.DimensionId, dimensionId, StringComparison.Ordinal))
        {
            return DimensionLibResult.Fail("You are no longer inside the pocket where this return started.", "pocket-return-context-changed");
        }

        var dimension = lookup.Value;
        DimensionLibResult<DimensionLocation> returnLocation;
        string successMessage;
        if (TryGetActiveIngressEndpoint(player, dimension.DimensionId, out var endpointId))
        {
            returnLocation = ResolveReturnLocation(dimension, endpointId, player);
            if (!returnLocation.Success)
            {
                return DimensionLibResult.Fail(returnLocation.Message, returnLocation.ErrorCode);
            }

            successMessage = $"Returned from pocket '{DisplayName(dimension.DimensionId)}' via linked Waystone.";
        }
        else if (TryGetUnanchoredReturn(player, dimension.DimensionId, out var unanchoredReturn))
        {
            returnLocation = DimensionLibResult<DimensionLocation>.Ok(unanchoredReturn);
            successMessage = $"Returned from pocket '{DisplayName(dimension.DimensionId)}' to your command entry point.";
        }
        else
        {
            if (!TryGetSingleLinkedEndpoint(dimension.DimensionId, out endpointId, out var linkError))
            {
                successMessage = $"Returned from pocket '{DisplayName(dimension.DimensionId)}'.";
                var fallback = TeleportToOverworldFallback(player, linkError, ref successMessage);
                if (fallback.Success)
                {
                    ClearActiveIngress(player, dimension.DimensionId);
                    ClearUnanchoredReturn(player, dimension.DimensionId);
                }

                return fallback;
            }

            returnLocation = ResolveReturnLocation(dimension, endpointId, player);
            if (!returnLocation.Success)
            {
                return DimensionLibResult.Fail(returnLocation.Message, returnLocation.ErrorCode);
            }

            successMessage = $"Returned from pocket '{DisplayName(dimension.DimensionId)}' via linked Waystone.";
        }

        var returned = TeleportToReturnLocationOrOverworld(player, returnLocation.Value, ref successMessage);
        if (!returned.Success)
        {
            return returned;
        }

        ClearActiveIngress(player, dimension.DimensionId);
        ClearUnanchoredReturn(player, dimension.DimensionId);
        PlayTeleportSound(player, _config.TeleportCompleteSound);
        return DimensionLibResult.Ok(successMessage);
    }

    private DimensionLibResult TeleportToReturnLocationOrOverworld(IServerPlayer player, DimensionLocation location, ref string successMessage)
    {
        if (TryGetUnavailableReturnTargetReason(location, out var unavailableReason))
        {
            return TeleportToOverworldFallback(player, unavailableReason, ref successMessage);
        }

        var returned = _dimensionLib.TeleportToLocation(player, location);
        if (!returned.Success && IsUnavailableReturnTargetError(returned.ErrorCode))
        {
            return TeleportToOverworldFallback(player, returned.Message, ref successMessage);
        }

        return returned;
    }

    private bool TryGetUnavailableReturnTargetReason(DimensionLocation location, out string reason)
    {
        reason = string.Empty;
        if (location == null || string.IsNullOrWhiteSpace(location.DimensionId))
        {
            return false;
        }

        var lookup = _dimensionLib.GetDimension(location.DimensionId);
        if (!lookup.Success)
        {
            reason = $"return target dimension '{DisplayName(location.DimensionId)}' is not registered";
            return true;
        }

        if (_dimensionLib.IsDimensionOrphaned(location.DimensionId))
        {
            reason = $"return target dimension '{DisplayName(location.DimensionId)}' is orphaned";
            return true;
        }

        return false;
    }

    private DimensionLibResult TeleportToOverworldFallback(IServerPlayer player, string unavailableReason, ref string successMessage)
    {
        var fallback = CreateOverworldFallbackLocation(player);
        var returned = _dimensionLib.TeleportToLocation(player, fallback);
        if (!returned.Success)
        {
            return returned;
        }

        successMessage = $"{successMessage} Warning: {unavailableReason}; returned to overworld spawn instead.";
        return DimensionLibResult.Ok(successMessage);
    }

    private DimensionLocation CreateOverworldFallbackLocation(IServerPlayer player)
    {
        var spawn = _api.World.DefaultSpawnPosition;
        return new DimensionLocation
        {
            DimensionPlaneId = 0,
            X = spawn.X,
            Y = spawn.Y,
            Z = spawn.Z,
            Yaw = player?.Entity?.Pos?.Yaw ?? 0,
            Pitch = player?.Entity?.Pos?.Pitch ?? 0,
            Roll = player?.Entity?.Pos?.Roll ?? 0,
        };
    }

    private static bool IsUnavailableReturnTargetError(string errorCode)
    {
        return string.Equals(errorCode, "unknown-location-dimension", StringComparison.Ordinal) ||
            string.Equals(errorCode, "dimension-orphaned", StringComparison.Ordinal);
    }

    private static bool IsMissingTargetElevator(string errorCode)
    {
        return string.Equals(errorCode, "missing-target-pocketelevator", StringComparison.Ordinal);
    }

    private DimensionLibResult BeginDelayedWaystoneEntry(IServerPlayer player, BlockPos waystonePos, string dimensionId, string endpointId)
    {
        if (!TryBeginTransfer(player, out var transferId, out var error))
        {
            return error;
        }

        PlayTeleportSound(player, _config.TeleportStartSound);
        RegisterTransferCallback(() => CompleteDelayedWaystoneEntry(player.PlayerUID, transferId, waystonePos, dimensionId, endpointId));
        return DimensionLibResult.Ok($"Pocket Waystone activates. Transfer in {_config.TeleportDelaySeconds:0.#}s...");
    }

    private DimensionLibResult BeginDelayedPocketReturn(IServerPlayer player, string dimensionId)
    {
        if (!TryBeginTransfer(player, out var transferId, out var error))
        {
            return error;
        }

        PlayTeleportSound(player, _config.TeleportStartSound);
        RegisterTransferCallback(() => CompleteDelayedPocketReturn(player.PlayerUID, transferId, dimensionId));
        return DimensionLibResult.Ok($"Pocket return pedestal activates. Transfer in {_config.TeleportDelaySeconds:0.#}s...");
    }

    private void RegisterTransferCallback(Action action)
    {
        var delayMs = (int)Math.Round(_config.TeleportDelaySeconds * 1000);
        _api.Event.RegisterCallback(_ => action(), Math.Max(0, delayMs));
    }

    private bool TryBeginTransfer(IServerPlayer player, out long transferId, out DimensionLibResult error)
    {
        transferId = 0;
        error = null;
        if (player?.Entity == null)
        {
            error = DimensionLibResult.Fail("Online player is required.", "missing-player");
            return false;
        }

        var playerKey = PlayerKey(player);
        if (_pendingTransfersByPlayer.ContainsKey(playerKey))
        {
            error = DimensionLibResult.Fail("A Pocket Dimensions transfer is already pending.", "pocket-transfer-pending");
            return false;
        }

        transferId = ++_nextPendingTransferId;
        _pendingTransfersByPlayer[playerKey] = transferId;
        return true;
    }

    private bool TryCompleteTransfer(string playerUid, long transferId, out IServerPlayer player)
    {
        player = FindOnlinePlayerByUid(playerUid);
        if (player?.Entity == null)
        {
            _pendingTransfersByPlayer.Remove(playerUid ?? string.Empty);
            return false;
        }

        if (!_pendingTransfersByPlayer.TryGetValue(playerUid, out var pendingTransferId) || pendingTransferId != transferId)
        {
            return false;
        }

        _pendingTransfersByPlayer.Remove(playerUid);
        return true;
    }

    private void CompleteDelayedWaystoneEntry(string playerUid, long transferId, BlockPos waystonePos, string dimensionId, string endpointId)
    {
        if (!TryCompleteTransfer(playerUid, transferId, out var player))
        {
            return;
        }

        NotifyTransferResult(player, CompleteWaystoneEntry(player, waystonePos, dimensionId, endpointId));
    }

    private DimensionLibResult CompleteWaystoneEntry(IServerPlayer player, BlockPos waystonePos, string dimensionId, string endpointId)
    {
        var lookup = _dimensionLib.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        if (!CanUseWaystone(player, lookup.Value))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.UseWaystoneCapabilityMode, _config.UseWaystonePrivilege, "use a Pocket Waystone"), "missing-waystone-use-privilege");
        }

        if (!IsBlockCode(waystonePos, PocketWaystoneBlockCode))
        {
            return DimensionLibResult.Fail("The activated Waystone is missing.", "missing-pocket-waystone-block");
        }

        var blockEntity = _api.World.BlockAccessor.GetBlockEntity(waystonePos) as PocketWaystoneBlockEntity;
        if (blockEntity == null || !string.Equals(blockEntity.EndpointId, endpointId, StringComparison.Ordinal) || !string.Equals(blockEntity.BoundDimensionId, dimensionId, StringComparison.Ordinal))
        {
            return DimensionLibResult.Fail("The activated Waystone binding changed before transfer completed.", "waystone-binding-mismatch");
        }

        if (!IsOwnedPocket(lookup.Value))
        {
            return DimensionLibResult.Fail($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.", "not-pocket-dimension");
        }

        if (_dimensionLib.IsDimensionOrphaned(dimensionId))
        {
            return DimensionLibResult.Fail($"Bound pocket '{DisplayName(dimensionId)}' is orphaned.", "dimension-orphaned");
        }

        var ensured = EnsurePocketInfrastructure(lookup.Value, player);
        if (!ensured.Success)
        {
            return ensured;
        }

        var entered = EnterPocket(player, lookup.Value, endpointId);
        if (entered.Success)
        {
            PlayTeleportSound(player, _config.TeleportCompleteSound);
        }

        return entered;
    }

    private void CompleteDelayedPocketReturn(string playerUid, long transferId, string dimensionId)
    {
        if (!TryCompleteTransfer(playerUid, transferId, out var player))
        {
            return;
        }

        NotifyTransferResult(player, CompletePocketReturn(player, dimensionId));
    }

    private void NotifyTransferResult(IServerPlayer player, DimensionLibResult result)
    {
        if (player == null || result == null)
        {
            return;
        }

        if (result.Success)
        {
            player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
        }
        else
        {
            player.SendIngameError(result.ErrorCode ?? "pocket-transfer-failed", result.Message);
        }
    }

    private void PlayTeleportSound(IServerPlayer player, string sound)
    {
        if (!_config.EnableTeleportSounds || string.IsNullOrWhiteSpace(sound) || player?.Entity == null || _config.TeleportSoundVolume <= 0 || _config.TeleportSoundRange <= 0)
        {
            return;
        }

        _api.World.PlaySoundAt(new AssetLocation(sound), player.Entity, null, randomizePitch: false, _config.TeleportSoundRange, _config.TeleportSoundVolume);
    }

    private IServerPlayer FindOnlinePlayerByUid(string playerUid)
    {
        return _api.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .FirstOrDefault(player => string.Equals(player.PlayerUID, playerUid, StringComparison.Ordinal));
    }

    private void ApplyRecipeConfig()
    {
        if (_config.AllowWaystoneCrafting)
        {
            return;
        }

        var removed = _api.World.GridRecipes.RemoveAll(IsPocketWaystoneRecipe);
        if (removed > 0)
        {
            _api.Logger.Notification("[PocketDimensions] Disabled {0} Pocket Waystone grid recipe(s) by config.", removed);
        }
    }

    private static bool IsPocketWaystoneRecipe(GridRecipe recipe)
    {
        var outputCode = recipe?.Output?.Code?.ToString();
        return string.Equals(outputCode, "pocketdimensions:pocketwaystone", StringComparison.Ordinal)
            || string.Equals(outputCode, "pocketwaystone", StringComparison.Ordinal)
            || string.Equals(recipe?.Name?.ToString(), "pocketdimensions:recipes/grid/pocketwaystone", StringComparison.Ordinal)
            || string.Equals(recipe?.Name?.ToString(), "pocketdimensions:grid/pocketwaystone", StringComparison.Ordinal);
    }

    public void ForgetWaystoneEndpoint(string endpointId)
    {
        RemoveWaystoneLink(endpointId);
    }

    public DimensionLibResult CanPlaceWaystone(IServerPlayer player)
    {
        return HasPrivilege(player, _config.PlaceWaystonePrivilege)
            ? DimensionLibResult.Ok("Waystone placement allowed.")
            : DimensionLibResult.Fail($"Missing privilege '{_config.PlaceWaystonePrivilege}'.", "missing-waystone-place-privilege");
    }

    public DimensionLibResult CanBreakWaystone(IServerPlayer player, BlockPos pos)
    {
        var blockEntity = pos == null ? null : _api?.World.BlockAccessor.GetBlockEntity(pos) as PocketWaystoneBlockEntity;
        if (blockEntity?.IsBound == true)
        {
            return DimensionLibResult.Fail($"This Waystone is bound to '{DisplayName(blockEntity.BoundDimensionId)}'. Run /pocket unbind before breaking it.", "pocketwaystone-bound-break-denied");
        }

        return HasPrivilege(player, _config.BreakWaystonePrivilege)
            ? DimensionLibResult.Ok("Waystone breaking allowed.")
            : DimensionLibResult.Fail($"Missing privilege '{_config.BreakWaystonePrivilege}'.", "missing-waystone-break-privilege");
    }

    public DimensionLibResult TravelElevator(IServerPlayer player, int direction, bool createMissingLayer = false)
    {
        return TravelElevator(player, direction, createMissingLayer, placeMissingElevator: false);
    }

    private DimensionLibResult TravelElevator(IServerPlayer player, int direction, bool createMissingLayer, bool placeMissingElevator)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        direction = NormalizeDirection(direction);
        if (!TryFindStandingElevator(player, out var elevatorPos, out _))
        {
            return DimensionLibResult.Fail("Stand on a Pocket Elevator first.", "not-on-pocket-elevator");
        }

        var dimensionLookup = _dimensionLib.GetDimensionAt(elevatorPos);
        if (!dimensionLookup.Success || !IsOwnedPocket(dimensionLookup.Value))
        {
            return DimensionLibResult.Fail("Pocket Elevators only work inside Pocket Dimensions pockets.", "not-in-pocket");
        }

        var dimension = dimensionLookup.Value;
        var stack = EnsureLayerStack(dimension);
        var layer = FindLayer(stack, dimension.DimensionId);
        if (!CanUseElevator(player, stack))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.UseElevatorCapabilityMode, _config.UseElevatorPrivilege, "use a Pocket Elevator"), "missing-elevator-use-privilege");
        }

        if (layer == null)
        {
            return DimensionLibResult.Fail($"Pocket space metadata for '{DisplayName(dimension.DimensionId)}' is unavailable.", "missing-pocket-layer");
        }

        var targetIndex = layer.Index + direction;
        var target = ResolveElevatorTargetLayer(player, stack, layer, targetIndex, direction, createMissingLayer);
        if (!target.Success)
        {
            return DimensionLibResult.Fail(target.Message, target.ErrorCode);
        }

        if (target.Value.TargetLayer == null)
        {
            return DimensionLibResult.Ok(target.Message);
        }

        layer = FindLayer(stack, dimension.DimensionId);
        var mappingIdResult = ResolveElevatorMapping(stack, layer, targetIndex, direction);
        if (!mappingIdResult.Success)
        {
            return DimensionLibResult.Fail(mappingIdResult.Message, mappingIdResult.ErrorCode);
        }

        var destinations = ResolveElevatorDestinations(player, elevatorPos, dimension.DimensionId, mappingIdResult.Value);
        if (!destinations.Success)
        {
            return DimensionLibResult.Fail(destinations.Message, destinations.ErrorCode);
        }

        var destination = destinations.Value.Destination;
        var targetElevatorPos = destinations.Value.TargetElevatorPos;
        if (placeMissingElevator && !HasPrivilege(player, _config.MutatePocketBlocksPrivilege))
        {
            return DimensionLibResult.Fail($"Missing privilege '{_config.MutatePocketBlocksPrivilege}' to place a Pocket Elevator in the target connected space.", "missing-elevator-place-privilege");
        }

        var landing = EnsureElevatorLanding(targetElevatorPos, target.Value.CreatedTargetLayer || placeMissingElevator);
        if (!landing.Success)
        {
            if (IsMissingTargetElevator(landing.ErrorCode))
            {
                if (!HasPrivilege(player, _config.MutatePocketBlocksPrivilege))
                {
                    return DimensionLibResult.Fail($"Missing privilege '{_config.MutatePocketBlocksPrivilege}' to place a Pocket Elevator in the target connected space.", "missing-elevator-place-privilege");
                }

                PromptElevatorPlacement(player, stack, layer.Index, targetIndex, direction);
                return DimensionLibResult.Ok("Confirm creating a Pocket Elevator at the matching landing to continue.");
            }

            return landing;
        }

        var traveled = _dimensionLib.TeleportToLocation(player, destination);
        if (!traveled.Success)
        {
            return traveled;
        }

        if (placeMissingElevator || target.Value.CreatedTargetLayer)
        {
            var placedAfterTravel = EnsurePlacedElevatorAtLoadedLanding(targetElevatorPos);
            if (!placedAfterTravel.Success)
            {
                return placedAfterTravel;
            }
        }

        return DimensionLibResult.Ok($"Moved within {stack.DisplayName}.");
    }

    private DimensionLibResult<(DimensionLocation Destination, BlockPos TargetElevatorPos)> ResolveElevatorDestinations(
        IServerPlayer player,
        BlockPos elevatorPos,
        string dimensionId,
        string mappingId)
    {
        var options = new DimensionMappingTeleportOptions { RequireCollisionFreeDestination = false };
        var mappedPlayer = _dimensionLib.ResolveMappedLocation(CreateLocation(player, dimensionId), mappingId, options);
        if (!mappedPlayer.Success)
        {
            return DimensionLibResult<(DimensionLocation Destination, BlockPos TargetElevatorPos)>.Fail(mappedPlayer.Message, mappedPlayer.ErrorCode);
        }

        var mappedElevator = _dimensionLib.ResolveMappedLocation(CreateBlockLocation(elevatorPos, dimensionId), mappingId, options);
        return mappedElevator.Success
            ? DimensionLibResult<(DimensionLocation Destination, BlockPos TargetElevatorPos)>.Ok((mappedPlayer.Value.Location, ToBlockPos(mappedElevator.Value.Location)))
            : DimensionLibResult<(DimensionLocation Destination, BlockPos TargetElevatorPos)>.Fail(mappedElevator.Message, mappedElevator.ErrorCode);
    }

    private DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)> ResolveElevatorTargetLayer(
        IServerPlayer player,
        PocketLayerStack stack,
        PocketLayerRef sourceLayer,
        int targetIndex,
        int direction,
        bool createMissingLayer)
    {
        var targetLayer = FindLayer(stack, targetIndex);
        if (targetLayer != null)
        {
            return DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Ok((targetLayer, false));
        }

        if (!CanCreateLayer(player, stack))
        {
            return DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Fail(CapabilityDeniedReason(_config.CreateLayerCapabilityMode, _config.CreatePrivilege, "create a new connected pocket space"), "missing-layer-create-privilege");
        }

        if (!createMissingLayer)
        {
            PromptLayerCreation(player, stack, sourceLayer.Index, targetIndex, direction);
            return DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Ok((null, false), "Confirm creating a connected pocket space to continue.");
        }

        var created = CreateLayer(stack, targetIndex, player);
        return created.Success
            ? DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Ok((created.Value, true))
            : DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Fail(created.Message, created.ErrorCode);
    }

    private DimensionLibResult<string> ResolveElevatorMapping(PocketLayerStack stack, PocketLayerRef layer, int targetIndex, int direction)
    {
        var mappingId = direction > 0 ? layer.UpMappingId : layer.DownMappingId;
        if (!string.IsNullOrWhiteSpace(mappingId))
        {
            return DimensionLibResult<string>.Ok(mappingId);
        }

        var linked = EnsureAdjacentMapping(stack, Math.Min(layer.Index, targetIndex), Math.Max(layer.Index, targetIndex));
        if (!linked.Success)
        {
            return DimensionLibResult<string>.Fail(linked.Message, linked.ErrorCode);
        }

        SaveLinkState();
        mappingId = direction > 0 ? layer.UpMappingId : layer.DownMappingId;
        return DimensionLibResult<string>.Ok(mappingId);
    }

    private static BlockPos ToBlockPos(DimensionLocation destination)
    {
        return new BlockPos(
            (int)Math.Floor(destination.X),
            (int)Math.Floor(destination.Y),
            (int)Math.Floor(destination.Z),
            destination.DimensionPlaneId);
    }

    private void OnElevatorTravelRequest(IServerPlayer player, PocketElevatorTravelRequest request)
    {
        var result = TravelElevator(player, request?.Direction ?? 1);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketelevator-travel-failed", result.Message);
        }
    }

    private void OnLayerCreationResponse(IServerPlayer player, PocketLayerCreationResponse response)
    {
        if (response == null || !response.Create)
        {
            return;
        }

        if (!IsCurrentElevatorPrompt(player, response, out var error))
        {
            player.SendIngameError(error.ErrorCode ?? "pocketlayer-confirmation-stale", error.Message);
            return;
        }

        var result = TravelElevator(player, response.Direction, createMissingLayer: true);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketlayer-create-failed", result.Message);
            return;
        }

        player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
    }

    private void OnElevatorPlacementResponse(IServerPlayer player, PocketElevatorPlacementResponse response)
    {
        if (response == null || !response.Place)
        {
            return;
        }

        if (!IsCurrentElevatorPrompt(player, response, out var error))
        {
            player.SendIngameError(error.ErrorCode ?? "pocketelevator-confirmation-stale", error.Message);
            return;
        }

        var result = TravelElevator(player, response.Direction, createMissingLayer: false, placeMissingElevator: true);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketelevator-place-failed", result.Message);
            return;
        }

        player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
    }

    private void OnPocketDirectoryRequest(IServerPlayer player, PocketDirectoryRequest request)
    {
        SendPocketDirectory(player);
    }

    private void OnPocketDirectoryActionRequest(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        var result = HandlePocketDirectoryAction(player, request);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocket-directory-action-failed", result.Message);
        }

        SendPocketDirectory(player, result.Message, result.Success, DirectorySelectedStack(request, result.Success));
    }

    private DimensionLibResult HandlePocketDirectoryAction(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        return request?.Action?.Trim().ToLowerInvariant() switch
        {
            DirectoryActionEnter => EnterPocketFromDirectory(player, request.DimensionId),
            DirectoryActionCreatePocket => CreatePocketFromDirectory(player, request),
            DirectoryActionCreateLayer => CreateLayerFromDirectory(player, request),
            DirectoryActionEditLayer => EditLayerFromDirectory(player, request),
            _ => DimensionLibResult.Fail("Unknown Pocket Directory action.", "unknown-pocket-directory-action"),
        };
    }

    private DimensionLibResult CreatePocketFromDirectory(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        if (!CanCreatePocket(player))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.CreatePocketCapabilityMode, _config.CreatePrivilege, "create a pocket"), "missing-pocket-create-privilege");
        }

        var result = CreateOrPreparePocket(player, request?.DisplayName, request?.Slug, request?.SizeChunks ?? 0, request?.SpawnY ?? 0);
        return result.Success
            ? DimensionLibResult.Ok(result.Message)
            : DimensionLibResult.Fail(result.Message, result.ErrorCode);
    }

    private DimensionLibResult CreateLayerFromDirectory(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        if (!TryGetDirectoryStack(request?.StackId, out var stack))
        {
            return DimensionLibResult.Fail("Select a pocket stack first.", "missing-pocket-stack");
        }

        if (!CanCreateLayer(player, stack))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.CreateLayerCapabilityMode, _config.CreatePrivilege, "create a connected pocket space"), "missing-layer-create-privilege");
        }

        var targetIndex = request.LayerIndex;
        if (FindLayer(stack, targetIndex) != null)
        {
            return DimensionLibResult.Fail($"That connected pocket space already exists in '{stack.DisplayName}'.", "pocket-layer-exists");
        }

        if (!HasAdjacentLayer(stack, targetIndex))
        {
            return DimensionLibResult.Fail("Choose a connected space next to an existing one so elevator mappings stay connected.", "pocket-layer-not-adjacent");
        }

        var created = CreateLayer(stack, targetIndex, player, request.DisplayName);
        return created.Success
            ? DimensionLibResult.Ok($"Created a connected space in '{stack.DisplayName}'.")
            : DimensionLibResult.Fail(created.Message, created.ErrorCode);
    }

    private DimensionLibResult EditLayerFromDirectory(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        if (!TryGetDirectoryStack(request?.StackId, out var stack))
        {
            return DimensionLibResult.Fail("Select a pocket stack first.", "missing-pocket-stack");
        }

        if (!CanEditLayer(player, stack))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.EditLayerCapabilityMode, _config.CreatePrivilege, "edit connected pocket space metadata"), "missing-layer-edit-privilege");
        }

        var layer = FindLayer(stack, request.LayerIndex);
        if (layer == null)
        {
            return DimensionLibResult.Fail($"That connected pocket space does not exist in '{stack.DisplayName}'.", "missing-pocket-layer");
        }

        ApplyLayerDisplayName(stack, layer, request.DisplayName);
        return DimensionLibResult.Ok($"Updated a connected space in '{stack.DisplayName}'.");
    }

    private bool TryGetDirectoryStack(string stackId, out PocketLayerStack stack)
    {
        stack = null;
        return !string.IsNullOrWhiteSpace(stackId) && _layerStacksById.TryGetValue(stackId.Trim(), out stack);
    }

    private static bool HasAdjacentLayer(PocketLayerStack stack, int targetIndex)
    {
        return FindLayer(stack, targetIndex - 1) != null || FindLayer(stack, targetIndex + 1) != null;
    }

    private DimensionLibResult EnterPocketFromDirectory(IServerPlayer player, string dimensionId)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return DimensionLibResult.Fail("Select a pocket first.", "missing-pocket-selection");
        }

        var lookup = _dimensionLib.GetDimension(dimensionId.Trim());
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var stack = FindStackForDimension(lookup.Value.DimensionId);
        if (!CanTeleportFromDirectory(player, stack, lookup.Value))
        {
            return DimensionLibResult.Fail(CapabilityDeniedReason(_config.DirectoryTeleportCapabilityMode, new[] { _config.EnterPrivilege, _config.UseWaystonePrivilege }, "enter this pocket from the directory"), "missing-pocket-access-privilege");
        }

        if (_dimensionLib.IsDimensionOrphaned(lookup.Value.DimensionId))
        {
            return DimensionLibResult.Fail($"Pocket '{DisplayName(lookup.Value.DimensionId)}' is orphaned.", "dimension-orphaned");
        }

        var ensured = EnsurePocketInfrastructure(lookup.Value, player);
        if (!ensured.Success)
        {
            return ensured;
        }

        var returnLocation = _dimensionLib.CaptureLocation(player);
        return returnLocation.Success
            ? EnterPocket(player, lookup.Value, endpointId: null, unanchoredReturn: returnLocation.Value)
            : DimensionLibResult.Fail(returnLocation.Message, returnLocation.ErrorCode);
    }

    private bool SendElevatorTravelRequest(int direction)
    {
        _clientChannel?.SendPacket(new PocketElevatorTravelRequest { Direction = NormalizeDirection(direction) });
        return true;
    }

    private void OnLayerCreationPrompt(PocketLayerCreationPrompt prompt)
    {
        if (prompt == null || _clientApi == null)
        {
            return;
        }

        var text = $"Create a new connected pocket space {(prompt.Direction > 0 ? "above" : "below")} {prompt.StackName}?";
        new PocketLayerCreationDialog(_clientApi, text, create =>
        {
            _clientChannel?.SendPacket(new PocketLayerCreationResponse
            {
                Direction = prompt.Direction,
                Create = create,
                StackId = prompt.StackId,
                SourceLayerIndex = prompt.SourceLayerIndex,
                TargetLayerIndex = prompt.TargetLayerIndex,
            });
        }).TryOpen();
    }

    private void OnElevatorPlacementPrompt(PocketElevatorPlacementPrompt prompt)
    {
        if (prompt == null || _clientApi == null)
        {
            return;
        }

        var text = $"Create a Pocket Elevator at the matching landing {(prompt.Direction > 0 ? "above" : "below")} {prompt.StackName}?";
        new PocketLayerCreationDialog(_clientApi, "Create Pocket Elevator", text, place =>
        {
            _clientChannel?.SendPacket(new PocketElevatorPlacementResponse
            {
                Direction = prompt.Direction,
                Place = place,
                StackId = prompt.StackId,
                SourceLayerIndex = prompt.SourceLayerIndex,
                TargetLayerIndex = prompt.TargetLayerIndex,
            });
        }).TryOpen();
    }

    private bool IsCurrentElevatorPrompt(IServerPlayer player, PocketLayerCreationResponse response, out DimensionLibResult error)
    {
        return IsCurrentElevatorPrompt(player, response.StackId, response.SourceLayerIndex, response.TargetLayerIndex, response.Direction, out error);
    }

    private bool IsCurrentElevatorPrompt(IServerPlayer player, PocketElevatorPlacementResponse response, out DimensionLibResult error)
    {
        return IsCurrentElevatorPrompt(player, response.StackId, response.SourceLayerIndex, response.TargetLayerIndex, response.Direction, out error);
    }

    private bool IsCurrentElevatorPrompt(IServerPlayer player, string stackId, int sourceLayerIndex, int targetLayerIndex, int responseDirection, out DimensionLibResult error)
    {
        error = DimensionLibResult.Ok();
        var direction = NormalizeDirection(responseDirection);
        if (!TryFindStandingElevator(player, out var elevatorPos, out _))
        {
            error = DimensionLibResult.Fail("Stand on the original Pocket Elevator and confirm again.", "not-on-pocket-elevator");
            return false;
        }

        var dimensionLookup = _dimensionLib.GetDimensionAt(elevatorPos);
        if (!dimensionLookup.Success || !IsOwnedPocket(dimensionLookup.Value))
        {
            error = DimensionLibResult.Fail("Pocket space confirmation is no longer valid.", "pocketlayer-confirmation-stale");
            return false;
        }

        var stack = FindStackForDimension(dimensionLookup.Value.DimensionId);
        var layer = stack == null ? null : FindLayer(stack, dimensionLookup.Value.DimensionId);
        if (stack == null || layer == null ||
            !string.Equals(stack.StackId, stackId, StringComparison.Ordinal) ||
            layer.Index != sourceLayerIndex ||
            targetLayerIndex != layer.Index + direction)
        {
            error = DimensionLibResult.Fail("Pocket space confirmation is stale. Try the elevator again.", "pocketlayer-confirmation-stale");
            return false;
        }

        return true;
    }

    private void OnPocketHudState(PocketHudStateMessage message)
    {
        _coordinatesHud?.SetState(message);
    }

    private bool OnPocketDirectoryHotkey(KeyCombination _)
    {
        if (_directoryDialog?.IsOpened() == true)
        {
            _directoryDialog.TryClose();
            return true;
        }

        _directoryDialog ??= new PocketDirectoryDialog(
            _clientApi,
            RequestPocketDirectory,
            dimensionId => _clientChannel?.SendPacket(new PocketDirectoryActionRequest { Action = DirectoryActionEnter, DimensionId = dimensionId }),
            (displayName, slug, sizeChunks, spawnY) => _clientChannel?.SendPacket(new PocketDirectoryActionRequest
            {
                Action = DirectoryActionCreatePocket,
                DisplayName = displayName,
                Slug = slug,
                SizeChunks = sizeChunks,
                SpawnY = spawnY,
            }));
        _directoryDialog.SetState(new PocketDirectoryStateMessage { Message = "Loading Pocket Directory..." });
        _directoryDialog.TryOpen();
        RequestPocketDirectory();
        return true;
    }

    private void RequestPocketDirectory()
    {
        _clientChannel?.SendPacket(new PocketDirectoryRequest { Refresh = true });
    }

    private void OnPocketDirectoryState(PocketDirectoryStateMessage message)
    {
        _directoryDialog?.SetState(message);
    }

    private void PromptLayerCreation(IServerPlayer player, PocketLayerStack stack, int sourceIndex, int targetIndex, int direction)
    {
        if (player == null || !CanCreateLayer(player, stack))
        {
            return;
        }

        _serverChannel?.SendPacket(new PocketLayerCreationPrompt
        {
            Direction = direction,
            StackName = stack.DisplayName,
            StackId = stack.StackId,
            SourceLayerIndex = sourceIndex,
            TargetLayerIndex = targetIndex,
        }, player);
    }

    private bool PromptElevatorPlacement(IServerPlayer player, PocketLayerStack stack, int sourceIndex, int targetIndex, int direction)
    {
        if (player == null)
        {
            return false;
        }

        _serverChannel?.SendPacket(new PocketElevatorPlacementPrompt
        {
            Direction = direction,
            StackName = stack.DisplayName,
            StackId = stack.StackId,
            SourceLayerIndex = sourceIndex,
            TargetLayerIndex = targetIndex,
        }, player);
        return true;
    }

    private void OnHudUpdateTick(float dt)
    {
        foreach (var player in _api.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            var playerKey = PlayerKey(player);
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                continue;
            }

            var state = BuildHudState(player);
            if (state.InPocket)
            {
                _playersWithPocketHud.Add(playerKey);
                _serverChannel?.SendPacket(state, player);
            }
            else if (_playersWithPocketHud.Remove(playerKey))
            {
                _serverChannel?.SendPacket(state, player);
            }
        }
    }

    private PocketHudStateMessage BuildHudState(IServerPlayer player)
    {
        if (player?.Entity?.Pos == null)
        {
            return new PocketHudStateMessage();
        }

        var lookup = _dimensionLib.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        if (!lookup.Success || !IsOwnedPocket(lookup.Value))
        {
            return new PocketHudStateMessage();
        }

        var dimension = lookup.Value;
        var stack = FindStackForDimension(dimension.DimensionId);
        if (stack == null)
        {
            return new PocketHudStateMessage();
        }

        var layer = FindLayer(stack, dimension.DimensionId);
        if (layer == null)
        {
            return new PocketHudStateMessage();
        }

        var local = _dimensionLib.ResolveLocalPosition(CreateLocation(player, dimension.DimensionId));
        if (!local.Success)
        {
            return new PocketHudStateMessage();
        }

        return new PocketHudStateMessage
        {
            InPocket = true,
            PocketName = stack.DisplayName,
            LayerIndex = layer.Index,
            LocalX = local.Value.BlockX,
            LocalY = local.Value.BlockY,
            LocalZ = local.Value.BlockZ,
        };
    }

    private string DirectorySelectedStack(PocketDirectoryActionRequest request, bool success)
    {
        if (!success || request == null)
        {
            return null;
        }

        return request.Action?.Trim().ToLowerInvariant() switch
        {
            DirectoryActionCreatePocket => ToDimensionId(PocketSlug.Normalize(string.IsNullOrWhiteSpace(request.Slug) ? request.DisplayName : request.Slug)),
            DirectoryActionCreateLayer => request.StackId,
            DirectoryActionEditLayer => request.StackId,
            DirectoryActionEnter => FindStackForDimension(request.DimensionId)?.StackId,
            _ => null,
        };
    }

    private void SendPocketDirectory(IServerPlayer player, string message = null, bool success = true, string selectedStackId = null)
    {
        if (player == null)
        {
            return;
        }

        _serverChannel?.SendPacket(BuildPocketDirectoryState(player, message, success, selectedStackId), player);
    }

    private PocketDirectoryStateMessage BuildPocketDirectoryState(IServerPlayer player, string message, bool success, string selectedStackId)
    {
        var currentDimensionId = CurrentPocketDimensionId(player);
        var currentStack = string.IsNullOrWhiteSpace(currentDimensionId) ? null : FindStackForDimension(currentDimensionId);
        var state = new PocketDirectoryStateMessage
        {
            Message = message,
            Success = success,
            CanCreatePocket = CanCreatePocket(player),
            CanCreateLayer = _layerStacksById.Values.Any(stack => CanCreateLayer(player, stack)),
            SelectedStackId = selectedStackId ?? currentStack?.StackId,
            CurrentLocationText = CurrentLocationText(currentStack, currentDimensionId),
            DefaultSizeChunks = _config.DefaultSizeChunks,
            DefaultSpawnY = ResolveDefaultSpawnY(),
        };

        var emittedDimensions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stack in _layerStacksById.Values.OrderBy(stack => stack.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var entry = BuildDirectoryStack(player, stack, currentDimensionId, emittedDimensions);
            if (entry.Layers.Count > 0)
            {
                state.Stacks.Add(entry);
            }
        }

        foreach (var dimension in _dimensionLib.Dimensions.Where(IsOwnedPocket).OrderBy(dimension => DisplayName(dimension.DimensionId), StringComparer.OrdinalIgnoreCase))
        {
            if (emittedDimensions.Contains(dimension.DimensionId))
            {
                continue;
            }

            var entry = BuildDirectoryStandalonePocket(player, dimension, currentDimensionId);
            if (entry != null)
            {
                state.Stacks.Add(entry);
            }
        }

        return state;
    }

    private string CurrentLocationText(PocketLayerStack stack, string currentDimensionId)
    {
        if (stack == null || string.IsNullOrWhiteSpace(currentDimensionId))
        {
            return "Current location: outside a Pocket Dimensions pocket.";
        }

        var layer = FindLayer(stack, currentDimensionId);
        return layer == null
            ? $"Current location: {stack.DisplayName}."
            : $"Current location: {stack.DisplayName}.";
    }

    private PocketDirectoryStackMessage BuildDirectoryStack(IServerPlayer player, PocketLayerStack stack, string currentDimensionId, HashSet<string> emittedDimensions)
    {
        var entry = new PocketDirectoryStackMessage
        {
            StackId = stack.StackId,
            DisplayName = stack.DisplayName,
            OwnerPlayerUid = stack.OwnerPlayerUid,
            OwnerPlayerName = stack.OwnerPlayerName,
            IsOwner = IsStackOwner(player, stack),
            CanCreateLayer = CanCreateLayer(player, stack),
        };

        foreach (var layer in stack.Layers.OrderBy(layer => layer.Index))
        {
            var dimension = _dimensionLib.GetDimension(layer.DimensionId);
            if (!dimension.Success || !IsOwnedPocket(dimension.Value))
            {
                continue;
            }

            emittedDimensions.Add(dimension.Value.DimensionId);
            var layerEntry = BuildDirectoryLayer(player, stack, layer, dimension.Value, currentDimensionId);
            if (layerEntry != null)
            {
                entry.Layers.Add(layerEntry);
            }
        }

        return entry;
    }

    private PocketDirectoryStackMessage BuildDirectoryStandalonePocket(IServerPlayer player, Dimension dimension, string currentDimensionId)
    {
        var layer = BuildDirectoryLayer(player, null, null, dimension, currentDimensionId);
        if (layer == null)
        {
            return null;
        }

        return new PocketDirectoryStackMessage
        {
            StackId = dimension.DimensionId,
            DisplayName = DisplayName(dimension.DimensionId),
            CanCreateLayer = false,
            Layers = new List<PocketDirectoryLayerMessage> { layer },
        };
    }

    private PocketDirectoryLayerMessage BuildDirectoryLayer(IServerPlayer player, PocketLayerStack stack, PocketLayerRef layer, Dimension dimension, string currentDimensionId)
    {
        if (!CanViewPocket(player, stack, dimension))
        {
            return null;
        }

        var orphaned = _dimensionLib.IsDimensionOrphaned(dimension.DimensionId);
        return new PocketDirectoryLayerMessage
        {
            Index = layer?.Index ?? 0,
            DimensionId = dimension.DimensionId,
            DisplayName = LayerDisplayName(stack, layer, dimension.DimensionId),
            Prepared = _dimensionLib.IsDimensionPrepared(dimension.DimensionId),
            Orphaned = orphaned,
            CanTeleport = !orphaned && CanTeleportFromDirectory(player, stack, dimension),
            IsCurrent = string.Equals(dimension.DimensionId, currentDimensionId, StringComparison.Ordinal),
            CanEdit = stack != null && CanEditLayer(player, stack),
        };
    }

    private string CurrentPocketDimensionId(IServerPlayer player)
    {
        var pos = player?.Entity?.Pos;
        if (pos == null)
        {
            return null;
        }

        var lookup = _dimensionLib.GetDimensionAt(pos.AsBlockPos);
        return lookup.Success && IsOwnedPocket(lookup.Value) ? lookup.Value.DimensionId : null;
    }

    private static bool IsStackOwner(IServerPlayer player, PocketLayerStack stack)
    {
        return !string.IsNullOrWhiteSpace(stack?.OwnerPlayerUid) && string.Equals(stack.OwnerPlayerUid, PlayerKey(player), StringComparison.Ordinal);
    }

    private PocketLayerStack EnsureLayerStack(Dimension dimension, IServerPlayer owner = null)
    {
        var existing = FindStackForDimension(dimension.DimensionId);
        if (existing != null)
        {
            return existing;
        }

        var stack = new PocketLayerStack
        {
            StackId = dimension.DimensionId,
            DisplayName = ShortName(dimension.DimensionId),
            OwnerPlayerUid = PlayerKey(owner),
            OwnerPlayerName = owner?.PlayerName,
            SizeChunks = dimension.ChunkSizeX,
            SpawnY = dimension.SpawnY,
            Layers = new List<PocketLayerRef>
            {
                new PocketLayerRef { Index = 0, DimensionId = dimension.DimensionId, DisplayName = ShortName(dimension.DimensionId) },
            },
        }.Normalize();
        _layerStacksById[stack.StackId] = stack;
        SaveLinkState();
        return stack;
    }

    private PocketLayerStack FindStackForDimension(string dimensionId)
    {
        return _layerStacksById.Values.FirstOrDefault(stack => stack.Layers.Any(layer => string.Equals(layer.DimensionId, dimensionId, StringComparison.Ordinal)));
    }

    private static PocketLayerRef FindLayer(PocketLayerStack stack, string dimensionId)
    {
        return stack?.Layers.FirstOrDefault(layer => string.Equals(layer.DimensionId, dimensionId, StringComparison.Ordinal));
    }

    private static PocketLayerRef FindLayer(PocketLayerStack stack, int index)
    {
        return stack?.Layers.FirstOrDefault(layer => layer.Index == index);
    }

    private DimensionLibResult<PocketLayerRef> CreateLayer(PocketLayerStack stack, int targetIndex, IServerPlayer player, string displayName = null)
    {
        var existing = FindLayer(stack, targetIndex);
        if (existing != null)
        {
            return DimensionLibResult<PocketLayerRef>.Ok(existing);
        }

        var templateLayer = stack.Layers.OrderBy(layer => Math.Abs(layer.Index - targetIndex)).First();
        var templateLookup = _dimensionLib.GetDimension(templateLayer.DimensionId);
        if (!templateLookup.Success)
        {
            return DimensionLibResult<PocketLayerRef>.Fail(templateLookup.Message, templateLookup.ErrorCode);
        }

        displayName = NormalizeDisplayName(displayName);
        var dimensionId = LayerDimensionId(stack.StackId, targetIndex);
        var lookup = _dimensionLib.GetDimension(dimensionId);
        Dimension dimension;
        if (lookup.Success)
        {
            dimension = lookup.Value;
        }
        else
        {
            var template = templateLookup.Value;
            var spec = new DimensionSpec
            {
                DimensionId = dimensionId,
                OwnerModId = ModId,
                DimensionPlaneId = _dimensionLib.PrimaryDimensionPlaneId,
                ChunkSizeX = template.ChunkSizeX,
                ChunkSizeZ = template.ChunkSizeZ,
                SpawnY = template.SpawnY,
                VisualSettings = template.VisualSettings?.Clone() ?? CreatePocketVisualSettings(),
                AccessPolicy = DimensionAccessPolicy.OwnerOnly,
                Mutability = DimensionMutability.Mutable,
                IsTransient = false,
            };
            var registered = _dimensionLib.RegisterDimension(spec);
            if (!registered.Success)
            {
                return DimensionLibResult<PocketLayerRef>.Fail(registered.Message, registered.ErrorCode);
            }

            dimension = registered.Value;
        }

        var prepared = PreparePocket(dimension, player, "Created a connected pocket space.", recordStandaloneStack: false);
        if (!prepared.Success)
        {
            return DimensionLibResult<PocketLayerRef>.Fail(prepared.Message, prepared.ErrorCode);
        }

        var layer = new PocketLayerRef { Index = targetIndex, DimensionId = dimension.DimensionId, DisplayName = displayName };
        stack.Layers.Add(layer);
        stack.Normalize();
        _layerStacksById[stack.StackId] = stack;
        _layersNeedingInitialSend.Add(dimension.DimensionId);

        foreach (var neighborIndex in new[] { targetIndex - 1, targetIndex + 1 })
        {
            if (FindLayer(stack, neighborIndex) != null)
            {
                var linked = EnsureAdjacentMapping(stack, Math.Min(targetIndex, neighborIndex), Math.Max(targetIndex, neighborIndex));
                if (!linked.Success)
                {
                    return DimensionLibResult<PocketLayerRef>.Fail(linked.Message, linked.ErrorCode);
                }
            }
        }

        SaveLinkState();
        return DimensionLibResult<PocketLayerRef>.Ok(FindLayer(stack, targetIndex));
    }

    private DimensionLibResult EnsureAdjacentMapping(PocketLayerStack stack, int lowerIndex, int upperIndex)
    {
        var lower = FindLayer(stack, lowerIndex);
        var upper = FindLayer(stack, upperIndex);
        if (lower == null || upper == null)
        {
            return DimensionLibResult.Fail("Adjacent connected pocket spaces are required before registering an elevator mapping.", "missing-pocket-layer");
        }

        var mappingId = LayerMappingId(stack.StackId, lowerIndex, upperIndex);
        var registered = _dimensionLib.RegisterMapping(new DimensionMappingSpec
        {
            MappingId = mappingId,
            OwnerModId = ModId,
            SourceDimensionId = lower.DimensionId,
            TargetDimensionId = upper.DimensionId,
            Bidirectional = true,
            Transform = DimensionMappingTransform.Identity(),
            IsTransient = false,
        });
        if (!registered.Success)
        {
            return DimensionLibResult.Fail(registered.Message, registered.ErrorCode);
        }

        lower.UpMappingId = mappingId;
        upper.DownMappingId = mappingId;
        return DimensionLibResult.Ok("Linked connected pocket spaces.");
    }

    private DimensionLibResult EnsureElevatorLanding(BlockPos elevatorPos, bool allowAutoPlaceForNewLayer)
    {
        if (!HasTwoBlockHeadroom(elevatorPos))
        {
            return DimensionLibResult.Fail("The way is blocked where the target Pocket Elevator would be.", "pocketelevator-target-blocked");
        }

        if (IsBlockCode(elevatorPos, PocketElevatorBlockCode))
        {
            return DimensionLibResult.Ok();
        }

        var landingMode = _config.ResolveElevatorLandingMode();
        if (landingMode == PocketElevatorLandingMode.RequireElevatorBlock && !allowAutoPlaceForNewLayer)
        {
            var placeable = ValidateElevatorBlockPlacement(elevatorPos);
            if (!placeable.Success)
            {
                return placeable;
            }

            return DimensionLibResult.Fail("The target connected space needs a Pocket Elevator at the mapped landing.", "missing-target-pocketelevator");
        }

        if (landingMode != PocketElevatorLandingMode.AutoPlaceElevatorIfMissing && !allowAutoPlaceForNewLayer)
        {
            return DimensionLibResult.Ok();
        }

        var canPlace = ValidateElevatorBlockPlacement(elevatorPos);
        if (!canPlace.Success)
        {
            return canPlace;
        }

        return PlacePocketElevator(elevatorPos);
    }

    private DimensionLibResult ValidateElevatorBlockPlacement(BlockPos elevatorPos)
    {
        var targetBlock = _api.World.BlockAccessor.GetBlock(elevatorPos);
        if (!IsBlockCode(elevatorPos, PocketFloorBlockCode) && targetBlock?.Replaceable < 6000)
        {
            return DimensionLibResult.Fail("The way is blocked where the target Pocket Elevator would be.", "pocketelevator-target-blocked");
        }

        var elevatorBlock = GetPocketElevatorBlock();
        if (elevatorBlock == null || elevatorBlock.BlockId == 0)
        {
            return DimensionLibResult.Fail("Pocket Elevator block is unavailable.", "missing-pocketelevator-block");
        }

        return DimensionLibResult.Ok();
    }

    private Block GetPocketElevatorBlock()
    {
        return _api.World.GetBlock(new AssetLocation(PocketElevatorBlockCode));
    }

    private DimensionLibResult EnsurePlacedElevatorAtLoadedLanding(BlockPos elevatorPos)
    {
        return IsBlockCode(elevatorPos, PocketElevatorBlockCode)
            ? DimensionLibResult.Ok()
            : PlacePocketElevator(elevatorPos, verifyPlacement: true);
    }

    private DimensionLibResult PlacePocketElevator(BlockPos elevatorPos, bool verifyPlacement = false)
    {
        var elevatorBlock = GetPocketElevatorBlock();
        if (elevatorBlock == null || elevatorBlock.BlockId == 0)
        {
            return DimensionLibResult.Fail("Pocket Elevator block is unavailable.", "missing-pocketelevator-block");
        }

        _api.World.BlockAccessor.SetBlock(elevatorBlock.BlockId, elevatorPos);
        if (verifyPlacement && !IsBlockCode(elevatorPos, PocketElevatorBlockCode))
        {
            return DimensionLibResult.Fail("Moved to the target layer, but the Pocket Elevator could not be placed at the landing.", "pocketelevator-place-failed");
        }

        return DimensionLibResult.Ok();
    }

    private bool HasTwoBlockHeadroom(BlockPos elevatorPos)
    {
        return IsClearForPlayer(elevatorPos.UpCopy()) && IsClearForPlayer(elevatorPos.UpCopy(2));
    }

    private bool IsClearForPlayer(BlockPos pos)
    {
        var block = _api.World.BlockAccessor.GetBlock(pos);
        return block == null || block.BlockId == 0 || block.Replaceable >= 6000;
    }

    private bool TryFindStandingElevator(IServerPlayer player, out BlockPos elevatorPos, out double playerOffsetY)
    {
        elevatorPos = null;
        playerOffsetY = 1;
        var pos = player?.Entity?.Pos;
        if (pos == null)
        {
            return false;
        }

        var feet = pos.AsBlockPos;
        var underFeet = feet.DownCopy();
        if (IsBlockCode(underFeet, PocketElevatorBlockCode))
        {
            elevatorPos = underFeet;
            playerOffsetY = pos.Y - underFeet.Y;
            return true;
        }

        if (IsBlockCode(feet, PocketElevatorBlockCode))
        {
            elevatorPos = feet;
            playerOffsetY = pos.Y - feet.Y;
            return true;
        }

        return false;
    }

    private static DimensionLocation CreateLocation(IServerPlayer player, string dimensionId)
    {
        return new DimensionLocation
        {
            DimensionId = dimensionId,
            DimensionPlaneId = player.Entity.Pos.Dimension,
            X = player.Entity.Pos.X,
            Y = player.Entity.Pos.Y,
            Z = player.Entity.Pos.Z,
            Yaw = player.Entity.Pos.Yaw,
            Pitch = player.Entity.Pos.Pitch,
            Roll = player.Entity.Pos.Roll,
        };
    }

    private static DimensionLocation CreateBlockLocation(BlockPos pos, string dimensionId)
    {
        return new DimensionLocation
        {
            DimensionId = dimensionId,
            DimensionPlaneId = pos.dimension,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
        };
    }

    private static int NormalizeDirection(int direction)
    {
        return direction < 0 ? -1 : 1;
    }

    private static string LayerDimensionId(string stackId, int index)
    {
        return index == 0 ? stackId : $"{stackId}-layer-{LayerIdPart(index)}";
    }

    private static string LayerMappingId(string stackId, int lowerIndex, int upperIndex)
    {
        return $"{stackId}-map-{LayerIdPart(lowerIndex)}-{LayerIdPart(upperIndex)}";
    }

    private static string LayerIdPart(int index)
    {
        return index < 0 ? "m" + Math.Abs(index) : index.ToString();
    }

    private static string FormatLayer(int index)
    {
        return index > 0 ? "+" + index : index.ToString();
    }

    private void RegisterCommands()
    {
        _api.ChatCommands.GetOrCreate("pocket")
            .WithDescription("Pocket Dimensions commands")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("create")
                .WithDescription("Create and prepare a persistent pocket dimension")
                .WithArgs(new StringArgParser("name [sizeChunks] [spawnY]", true))
                .RequiresPrivilege(_config.CreatePrivilege)
                .HandleWith(HandleCreate)
                .EndSubCommand()
            .BeginSubCommand("enter")
                .WithDescription("Enter a pocket dimension")
                .WithArgs(new StringArgParser("name", true))
                .RequiresPrivilege(_config.EnterPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleEnter)
                .EndSubCommand()
            .BeginSubCommand("exit")
                .WithDescription("Return from the current pocket dimension")
                .RequiresPrivilege(_config.ExitPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleExit)
                .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("List Pocket Dimensions dimensions")
                .RequiresPrivilege(_config.EnterPrivilege)
                .HandleWith(_ => TextCommandResult.Success(BuildPocketList()))
                .EndSubCommand()
            .BeginSubCommand("layers")
                .WithDescription("List Pocket Dimensions layer stacks")
                .RequiresPrivilege(_config.EnterPrivilege)
                .HandleWith(_ => TextCommandResult.Success(BuildLayerStackList()))
                .EndSubCommand()
            .BeginSubCommand("inspect")
                .WithDescription("Inspect the DimensionLib dimension at your current position, or inspect a named pocket")
                .WithArgs(new StringArgParser("name", false))
                .RequiresPrivilege(_config.EnterPrivilege)
                .HandleWith(HandleInspect)
                .EndSubCommand()
            .BeginSubCommand("links")
                .WithDescription("List Pocket Dimensions Waystone links")
                .RequiresPrivilege(_config.ConfigPrivilege)
                .HandleWith(HandleLinks)
                .EndSubCommand()
            .BeginSubCommand("config")
                .WithDescription("Open the Pocket Dimensions config panel")
                .RequiresPrivilege(_config.ConfigPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleConfig)
                .EndSubCommand()
            .BeginSubCommand("bind")
                .WithDescription("Bind the selected placed Waystone to a pocket dimension")
                .WithArgs(new StringArgParser("name", true))
                .RequiresPrivilege(_config.BindPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleBindWaystone)
                .EndSubCommand()
            .BeginSubCommand("unbind")
                .WithDescription("Clear the selected placed Waystone binding")
                .RequiresPrivilege(_config.UnbindPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleUnbindWaystone)
                .EndSubCommand()
            .BeginSubCommand("release")
                .WithDescription("Mark a pocket dimension orphaned")
                .WithArgs(new StringArgParser("name confirm", true))
                .RequiresPrivilege(_config.ReleasePrivilege)
                .HandleWith(HandleRelease)
                .EndSubCommand();
    }

    private TextCommandResult HandleCreate(TextCommandCallingArgs args)
    {
        var cmdArgs = new CmdArgs((string)args[0] ?? string.Empty);
        var name = cmdArgs.PopWord(string.Empty);
        var sizeText = cmdArgs.PopWord(null);
        var spawnYText = cmdArgs.PopWord(null);

        if (string.IsNullOrWhiteSpace(name))
        {
            return TextCommandResult.Error("Usage: /pocket create <name> [sizeChunks] [spawnY]");
        }

        var sizeChunks = int.TryParse(sizeText, out var parsedSize) ? parsedSize : 0;
        var spawnY = int.TryParse(spawnYText, out var parsedSpawnY) ? parsedSpawnY : 0;
        return ToCommandResult(CreateOrPreparePocket(args.Caller.Player as IServerPlayer, name, name, sizeChunks, spawnY));
    }

    private DimensionLibResult CreateOrPreparePocket(IServerPlayer player, string displayName, string slug, int sizeChunks, int spawnY)
    {
        displayName = displayName?.Trim();
        slug = string.IsNullOrWhiteSpace(slug) ? displayName : slug.Trim();
        slug = PocketSlug.Normalize(slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return DimensionLibResult.Fail("Pocket slug is required.", "missing-pocket-slug");
        }

        var dimensionId = ToDimensionId(slug);
        var existing = _dimensionLib.GetDimension(dimensionId);
        if (existing.Success)
        {
            if (!IsOwnedPocket(existing.Value))
            {
                return DimensionLibResult.Fail($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.", "not-pocket-dimension");
            }

            var existingStack = FindStackForDimension(existing.Value.DimensionId);
            if (!CanRefreshExistingPocket(player, existingStack))
            {
                return DimensionLibResult.Fail("A pocket with that slug already exists.", "pocket-exists");
            }

            var refreshSpec = ToSpec(existing.Value);
            ApplyPocketVisualDefaults(refreshSpec);
            var refreshed = _dimensionLib.RegisterDimension(refreshSpec);
            if (!refreshed.Success)
            {
                return DimensionLibResult.Fail(refreshed.Message, refreshed.ErrorCode);
            }

            var preparedExisting = PreparePocket(refreshed.Value, player, "Pocket already exists; refreshed and prepared existing dimension.");
            if (preparedExisting.Success)
            {
                ApplyStackDisplayName(refreshed.Value, displayName);
            }

            return preparedExisting;
        }

        sizeChunks = sizeChunks > 0 ? ClampInt(sizeChunks, 1, _config.MaxSizeChunks) : _config.DefaultSizeChunks;
        spawnY = spawnY > 0 ? ClampInt(spawnY, 1, _api.WorldManager.MapSizeY - 2) : ResolveDefaultSpawnY();
        var spec = new DimensionSpec
        {
            DimensionId = dimensionId,
            OwnerModId = ModId,
            DimensionPlaneId = _dimensionLib.PrimaryDimensionPlaneId,
            ChunkSizeX = sizeChunks,
            ChunkSizeZ = sizeChunks,
            SpawnY = spawnY,
            VisualSettings = CreatePocketVisualSettings(),
            AccessPolicy = DimensionAccessPolicy.OwnerOnly,
            Mutability = DimensionMutability.Mutable,
            IsTransient = false,
        };

        var registered = _dimensionLib.RegisterDimension(spec);
        if (!registered.Success)
        {
            return DimensionLibResult.Fail(registered.Message, registered.ErrorCode);
        }

        var prepared = PreparePocket(registered.Value, player, $"Created pocket '{DisplayName(dimensionId)}'.");
        if (prepared.Success)
        {
            ApplyStackDisplayName(registered.Value, displayName);
        }

        return prepared;
    }

    private void ApplyStackDisplayName(Dimension dimension, string displayName)
    {
        displayName = NormalizeDisplayName(displayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var stack = EnsureLayerStack(dimension);
        stack.DisplayName = displayName;
        var baseLayer = FindLayer(stack, 0);
        if (baseLayer != null)
        {
            baseLayer.DisplayName = stack.DisplayName;
        }

        SaveLinkState();
    }

    private void ApplyLayerDisplayName(PocketLayerStack stack, PocketLayerRef layer, string displayName)
    {
        layer.DisplayName = NormalizeDisplayName(displayName);
        if (layer.Index == 0 && !string.IsNullOrWhiteSpace(layer.DisplayName))
        {
            stack.DisplayName = layer.DisplayName;
        }

        stack.Normalize();
        SaveLinkState();
    }

    private TextCommandResult HandleEnter(TextCommandCallingArgs args)
    {
        var dimensionId = ToDimensionId((string)args[0] ?? string.Empty);
        var lookup = _dimensionLib.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return ToCommandResult(lookup);
        }

        if (!IsOwnedPocket(lookup.Value))
        {
            return TextCommandResult.Error($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.");
        }

        var player = (IServerPlayer)args.Caller.Player;
        var ensured = EnsurePocketInfrastructure(lookup.Value, player);
        if (!ensured.Success)
        {
            return ToCommandResult(ensured);
        }

        var returnLocation = _dimensionLib.CaptureLocation(player);
        if (!returnLocation.Success)
        {
            return ToCommandResult(returnLocation);
        }

        return ToCommandResult(EnterPocket(player, lookup.Value, endpointId: null, unanchoredReturn: returnLocation.Value));
    }

    private TextCommandResult HandleExit(TextCommandCallingArgs args)
    {
        return ToCommandResult(ReturnFromPocket((IServerPlayer)args.Caller.Player));
    }

    private TextCommandResult HandleInspect(TextCommandCallingArgs args)
    {
        var name = ((string)args[0] ?? string.Empty).Trim();
        DimensionLibResult<Dimension> lookup;
        if (!string.IsNullOrWhiteSpace(name))
        {
            lookup = _dimensionLib.GetDimension(ToDimensionId(name));
        }
        else if (args.Caller.Player is IServerPlayer player)
        {
            lookup = _dimensionLib.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        }
        else
        {
            return TextCommandResult.Error("Usage from console: /pocket inspect <name>");
        }

        if (!lookup.Success)
        {
            return ToCommandResult(lookup);
        }

        var dimension = lookup.Value;
        return TextCommandResult.Success(BuildPocketInspection(dimension));
    }

    private TextCommandResult HandleLinks(TextCommandCallingArgs args)
    {
        if (!CanEditConfig(args.Caller.Player as IServerPlayer))
        {
            return TextCommandResult.Error($"Missing privilege '{_config.ConfigPrivilege}'.", "missing-pocket-config-privilege");
        }

        return TextCommandResult.Success(BuildWaystoneLinkReport());
    }

    private TextCommandResult HandleConfig(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (!CanEditConfig(player))
        {
            return TextCommandResult.Error($"Missing privilege '{_config.ConfigPrivilege}'.", "missing-pocket-config-privilege");
        }

        _configController?.SendOpen(player);
        return TextCommandResult.Success("Opening Pocket Dimensions config panel.");
    }

    private TextCommandResult HandleBindWaystone(TextCommandCallingArgs args)
    {
        var name = ((string)args[0] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return TextCommandResult.Error("Usage: /pocket bind <name>");
        }

        var player = (IServerPlayer)args.Caller.Player;
        var waystone = GetSelectedExternalWaystone(player, requireBuildAccess: true);
        if (!waystone.Success)
        {
            return TextCommandResult.Error(waystone.Message, waystone.ErrorCode);
        }

        var dimensionId = ToDimensionId(name);
        var lookup = _dimensionLib.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return ToCommandResult(lookup);
        }

        if (!IsOwnedPocket(lookup.Value))
        {
            return TextCommandResult.Error($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.");
        }

        if (_dimensionLib.IsDimensionOrphaned(dimensionId))
        {
            return TextCommandResult.Error($"Dimension '{DisplayName(dimensionId)}' is orphaned and cannot be bound.", "dimension-orphaned");
        }

        var ensured = EnsurePocketInfrastructure(lookup.Value, player);
        if (!ensured.Success)
        {
            return ToCommandResult(ensured);
        }

        waystone.Value.BindTo(dimensionId, player);
        var linked = RegisterWaystoneLink(waystone.Value, player.CurrentBlockSelection.Position, lookup.Value, player);
        return linked.Success
            ? TextCommandResult.Success($"Bound selected Pocket Waystone to '{DisplayName(dimensionId)}'. Right-click it to enter that pocket.")
            : ToCommandResult(linked);
    }

    private TextCommandResult HandleUnbindWaystone(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var waystone = GetSelectedExternalWaystone(player, requireBuildAccess: true);
        if (!waystone.Success)
        {
            return TextCommandResult.Error(waystone.Message, waystone.ErrorCode);
        }

        RemoveWaystoneLink(waystone.Value.EndpointId);
        waystone.Value.ClearBinding();
        return TextCommandResult.Success("Cleared selected Pocket Waystone binding.");
    }

    private TextCommandResult HandleRelease(TextCommandCallingArgs args)
    {
        var cmdArgs = new CmdArgs((string)args[0] ?? string.Empty);
        var name = cmdArgs.PopWord(string.Empty);
        var confirm = cmdArgs.PopWord(string.Empty);
        var dimensionId = ToDimensionId(name);

        if (string.IsNullOrWhiteSpace(name) || !string.Equals(confirm, "confirm", StringComparison.OrdinalIgnoreCase))
        {
            return TextCommandResult.Success($"Run /pocket release {name} confirm to mark this pocket orphaned.");
        }

        var lookup = _dimensionLib.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return ToCommandResult(lookup);
        }

        if (!IsOwnedPocket(lookup.Value))
        {
            return TextCommandResult.Error($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.");
        }

        var released = _dimensionLib.ReleaseDimension(dimensionId, DimensionReleaseMode.MarkOrphaned);
        if (released.Success)
        {
            RemoveLinksForPocket(dimensionId);
        }

        return ToCommandResult(released);
    }

    private DimensionLibResult PreparePocket(Dimension dimension, IServerPlayer player, string successPrefix, bool recordStandaloneStack = true)
    {
        var prepared = _dimensionLib.PrepareDimension(dimension.DimensionId, new PocketPlatformSource(_api, dimension), player);
        if (prepared.Success && recordStandaloneStack)
        {
            EnsureLayerStack(dimension, player);
        }

        return prepared.Success
            ? DimensionLibResult.Ok($"{successPrefix} Bind a Pocket Waystone with /pocket bind {ShortName(dimension.DimensionId)} to create an entry point.")
            : prepared;
    }

    private DimensionLibResult EnsurePocketInfrastructure(Dimension dimension, IServerPlayer player)
    {
        if (_dimensionLib.IsDimensionPrepared(dimension.DimensionId))
        {
            if (_layersNeedingInitialSend.Remove(dimension.DimensionId))
            {
                var sent = _dimensionLib.PrepareDimension(dimension.DimensionId, sendToPlayer: player);
                if (!sent.Success)
                {
                    return sent;
                }
            }

            var centralElevatorPos = GetCentralElevatorWorldPos(dimension);
            if (!IsBlockCode(centralElevatorPos, PocketElevatorBlockCode))
            {
                var elevator = EnsureElevatorLanding(centralElevatorPos, allowAutoPlaceForNewLayer: true);
                if (!elevator.Success)
                {
                    return elevator;
                }
            }

            if (HasManagedInfrastructure(dimension))
            {
                return DimensionLibResult.Ok("Pocket infrastructure is ready.");
            }
        }

        return PreparePocket(dimension, player, "Prepared pocket infrastructure.");
    }

    private DimensionLibResult EnterPocket(IServerPlayer player, Dimension dimension, string endpointId, DimensionLocation unanchoredReturn = null)
    {
        var entered = _dimensionLib.TeleportToDimension(player, dimension.DimensionId, new DimensionTeleportOptions { RecordReturn = false });
        if (!entered.Success)
        {
            return entered;
        }

        if (!string.IsNullOrWhiteSpace(endpointId))
        {
            SetActiveIngress(player, dimension.DimensionId, endpointId);
            ClearUnanchoredReturn(player, dimension.DimensionId);
            return DimensionLibResult.Ok($"Entered pocket '{DisplayName(dimension.DimensionId)}'.");
        }

        if (unanchoredReturn != null)
        {
            ClearActiveIngress(player, dimension.DimensionId);
            SetUnanchoredReturn(player, dimension.DimensionId, unanchoredReturn);
            return DimensionLibResult.Ok($"Entered pocket '{DisplayName(dimension.DimensionId)}'. Return pedestal will return you to your command entry point.");
        }

        return DimensionLibResult.Ok($"Entered pocket '{DisplayName(dimension.DimensionId)}'. Return pedestal has no active Waystone ingress for this command entry.");
    }

    private bool HasManagedInfrastructure(Dimension dimension)
    {
        var pedestalPos = GetReturnPedestalWorldPos(dimension);
        if (!IsBlockCode(pedestalPos, PocketReturnPedestalBlockCode))
        {
            return false;
        }

        var spawnFloorPos = new BlockPos((int)Math.Floor(dimension.SpawnX), dimension.SpawnY - 1, (int)Math.Floor(dimension.SpawnZ), dimension.DimensionPlaneId);
        return (IsBlockCode(spawnFloorPos, PocketFloorBlockCode) || IsBlockCode(spawnFloorPos, PocketElevatorBlockCode)) && IsBlockCode(GetCentralElevatorWorldPos(dimension), PocketElevatorBlockCode);
    }

    private bool IsBlockCode(BlockPos pos, string code)
    {
        return string.Equals(_api.World.BlockAccessor.GetBlock(pos)?.Code?.ToString(), code, StringComparison.Ordinal);
    }

    private void LoadLinkState()
    {
        _linksByEndpointId.Clear();
        _activeIngressByPlayer.Clear();
        _unanchoredReturnsByPlayer.Clear();
        _layerStacksById.Clear();

        var state = _linkStore.Load();
        foreach (var link in state.Links)
        {
            _linksByEndpointId[link.EndpointId] = link;
        }

        foreach (var playerEntry in state.ActiveIngressByPlayer)
        {
            _activeIngressByPlayer[playerEntry.Key] = new Dictionary<string, string>(playerEntry.Value, StringComparer.Ordinal);
        }

        foreach (var playerEntry in state.UnanchoredReturnsByPlayer)
        {
            _unanchoredReturnsByPlayer[playerEntry.Key] = new Dictionary<string, DimensionLocation>(playerEntry.Value, StringComparer.Ordinal);
        }

        foreach (var stack in state.LayerStacks)
        {
            _layerStacksById[stack.StackId] = stack;
        }
    }

    private void SaveLinkState()
    {
        _linkStore?.Save(new PocketLinkState
        {
            Links = _linksByEndpointId.Values.ToList(),
            ActiveIngressByPlayer = _activeIngressByPlayer,
            UnanchoredReturnsByPlayer = _unanchoredReturnsByPlayer,
            LayerStacks = _layerStacksById.Values.ToList(),
        });
    }

    private DimensionLibResult<PocketWaystoneLink> RegisterWaystoneLink(PocketWaystoneBlockEntity blockEntity, BlockPos position, Dimension pocket, IServerPlayer player)
    {
        if (blockEntity == null || position == null || pocket == null)
        {
            return DimensionLibResult<PocketWaystoneLink>.Fail("A placed bound Waystone is required.", "missing-waystone-link");
        }

        if (!string.Equals(blockEntity.BoundDimensionId, pocket.DimensionId, StringComparison.Ordinal))
        {
            return DimensionLibResult<PocketWaystoneLink>.Fail("Selected Waystone is not bound to that pocket.", "waystone-binding-mismatch");
        }

        var endpointId = blockEntity.EnsureEndpointId();
        var sourceDimension = _dimensionLib.GetDimensionAt(position);
        if (sourceDimension.Success && IsOwnedPocket(sourceDimension.Value))
        {
            return DimensionLibResult<PocketWaystoneLink>.Fail("Pocket Waystones inside pockets cannot be used as entry endpoints.", "inside-pocket-waystone");
        }

        var link = new PocketWaystoneLink
        {
            EndpointId = endpointId,
            PocketDimensionId = pocket.DimensionId,
            SourceDimensionId = sourceDimension.Success ? sourceDimension.Value.DimensionId : null,
            DimensionPlaneId = position.dimension,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            BoundByPlayerUid = blockEntity.BoundByPlayerUid ?? player?.PlayerUID,
            BoundByPlayerName = blockEntity.BoundByPlayerName ?? player?.PlayerName,
        }.Normalize();

        _linksByEndpointId[link.EndpointId] = link;
        SaveLinkState();
        return DimensionLibResult<PocketWaystoneLink>.Ok(link);
    }

    private void RemoveWaystoneLink(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return;
        }

        if (!_linksByEndpointId.TryGetValue(endpointId, out var link))
        {
            return;
        }

        PreserveActiveIngressReturns(endpointId, link);
        _linksByEndpointId.Remove(endpointId);
        RemoveActiveIngressReferences(endpointId);
        SaveLinkState();
    }

    private void PreserveActiveIngressReturns(string endpointId, PocketWaystoneLink link)
    {
        if (link == null)
        {
            return;
        }

        var returnLocation = new DimensionLocation
        {
            DimensionId = link.SourceDimensionId,
            DimensionPlaneId = link.DimensionPlaneId,
            X = link.X + 0.5,
            Y = link.Y + 1,
            Z = link.Z + 0.5,
        };

        foreach (var playerKey in _activeIngressByPlayer.Keys.ToArray())
        {
            var activeIngress = _activeIngressByPlayer[playerKey];
            foreach (var dimensionId in activeIngress.Where(entry => string.Equals(entry.Value, endpointId, StringComparison.Ordinal)).Select(entry => entry.Key).ToArray())
            {
                if (!_unanchoredReturnsByPlayer.TryGetValue(playerKey, out var returnsByPocket))
                {
                    returnsByPocket = new Dictionary<string, DimensionLocation>(StringComparer.Ordinal);
                    _unanchoredReturnsByPlayer[playerKey] = returnsByPocket;
                }

                returnsByPocket[dimensionId] = CloneLocation(returnLocation);
            }
        }
    }

    private void RemoveLinksForPocket(string dimensionId)
    {
        var removedEndpoints = _linksByEndpointId.Values
            .Where(link => string.Equals(link.PocketDimensionId, dimensionId, StringComparison.Ordinal))
            .Select(link => link.EndpointId)
            .ToArray();

        foreach (var endpointId in removedEndpoints)
        {
            _linksByEndpointId.Remove(endpointId);
            RemoveActiveIngressReferences(endpointId);
        }

        var removedReturns = RemoveUnanchoredReturnReferences(dimensionId);
        if (removedEndpoints.Length > 0 || removedReturns)
        {
            SaveLinkState();
        }
    }

    private void SetActiveIngress(IServerPlayer player, string dimensionId, string endpointId)
    {
        var playerKey = PlayerKey(player);
        if (!_activeIngressByPlayer.TryGetValue(playerKey, out var activeIngress))
        {
            activeIngress = new Dictionary<string, string>(StringComparer.Ordinal);
            _activeIngressByPlayer[playerKey] = activeIngress;
        }

        activeIngress[dimensionId] = endpointId;
        SaveLinkState();
    }

    private bool TryGetActiveIngressEndpoint(IServerPlayer player, string dimensionId, out string endpointId)
    {
        endpointId = null;
        return _activeIngressByPlayer.TryGetValue(PlayerKey(player), out var activeIngress) && activeIngress.TryGetValue(dimensionId, out endpointId);
    }

    private void ClearActiveIngress(IServerPlayer player, string dimensionId)
    {
        var playerKey = PlayerKey(player);
        if (!_activeIngressByPlayer.TryGetValue(playerKey, out var activeIngress))
        {
            return;
        }

        activeIngress.Remove(dimensionId);
        if (activeIngress.Count == 0)
        {
            _activeIngressByPlayer.Remove(playerKey);
        }

        SaveLinkState();
    }

    private void SetUnanchoredReturn(IServerPlayer player, string dimensionId, DimensionLocation location)
    {
        if (location == null)
        {
            return;
        }

        var playerKey = PlayerKey(player);
        if (!_unanchoredReturnsByPlayer.TryGetValue(playerKey, out var returnsByPocket))
        {
            returnsByPocket = new Dictionary<string, DimensionLocation>(StringComparer.Ordinal);
            _unanchoredReturnsByPlayer[playerKey] = returnsByPocket;
        }

        returnsByPocket[dimensionId] = CloneLocation(location);
        SaveLinkState();
    }

    private bool TryGetUnanchoredReturn(IServerPlayer player, string dimensionId, out DimensionLocation location)
    {
        location = null;
        if (!_unanchoredReturnsByPlayer.TryGetValue(PlayerKey(player), out var returnsByPocket) || !returnsByPocket.TryGetValue(dimensionId, out var storedLocation))
        {
            return false;
        }

        location = CloneLocation(storedLocation);
        return true;
    }

    private void ClearUnanchoredReturn(IServerPlayer player, string dimensionId)
    {
        var playerKey = PlayerKey(player);
        if (!_unanchoredReturnsByPlayer.TryGetValue(playerKey, out var returnsByPocket))
        {
            return;
        }

        returnsByPocket.Remove(dimensionId);
        if (returnsByPocket.Count == 0)
        {
            _unanchoredReturnsByPlayer.Remove(playerKey);
        }

        SaveLinkState();
    }

    private void RemoveActiveIngressReferences(string endpointId)
    {
        foreach (var playerKey in _activeIngressByPlayer.Keys.ToArray())
        {
            var activeIngress = _activeIngressByPlayer[playerKey];
            foreach (var dimensionId in activeIngress.Where(entry => string.Equals(entry.Value, endpointId, StringComparison.Ordinal)).Select(entry => entry.Key).ToArray())
            {
                activeIngress.Remove(dimensionId);
            }

            if (activeIngress.Count == 0)
            {
                _activeIngressByPlayer.Remove(playerKey);
            }
        }
    }

    private bool RemoveUnanchoredReturnReferences(string dimensionId)
    {
        var removed = false;
        foreach (var playerKey in _unanchoredReturnsByPlayer.Keys.ToArray())
        {
            var returnsByPocket = _unanchoredReturnsByPlayer[playerKey];
            removed |= returnsByPocket.Remove(dimensionId);
            if (returnsByPocket.Count == 0)
            {
                _unanchoredReturnsByPlayer.Remove(playerKey);
            }
        }

        return removed;
    }

    private static DimensionLocation CloneLocation(DimensionLocation location)
    {
        if (location == null)
        {
            return null;
        }

        return new DimensionLocation
        {
            DimensionId = location.DimensionId,
            DimensionPlaneId = location.DimensionPlaneId,
            X = location.X,
            Y = location.Y,
            Z = location.Z,
            Yaw = location.Yaw,
            Pitch = location.Pitch,
            Roll = location.Roll,
        };
    }

    private bool TryGetSingleLinkedEndpoint(string dimensionId, out string endpointId, out string message)
    {
        var links = _linksByEndpointId.Values
            .Where(link => string.Equals(link.PocketDimensionId, dimensionId, StringComparison.Ordinal))
            .ToArray();

        endpointId = null;
        if (links.Length == 0)
        {
            message = $"Pocket '{DisplayName(dimensionId)}' has no linked Waystone. Enter through a bound Waystone or bind one with /pocket bind {ShortName(dimensionId)}.";
            return false;
        }

        if (links.Length > 1)
        {
            message = $"Pocket '{DisplayName(dimensionId)}' has multiple linked Waystones. Enter through the Waystone you want to return to.";
            return false;
        }

        endpointId = links[0].EndpointId;
        message = string.Empty;
        return true;
    }

    private DimensionLibResult<DimensionLocation> ResolveReturnLocation(Dimension dimension, string endpointId, IServerPlayer player)
    {
        if (!_linksByEndpointId.TryGetValue(endpointId ?? string.Empty, out var link))
        {
            return DimensionLibResult<DimensionLocation>.Fail("The linked return Waystone no longer exists.", "missing-pocket-waystone-link");
        }

        if (!string.Equals(link.PocketDimensionId, dimension.DimensionId, StringComparison.Ordinal))
        {
            return DimensionLibResult<DimensionLocation>.Fail("The linked return Waystone points at a different pocket.", "waystone-link-mismatch");
        }

        var waystonePos = new BlockPos(link.X, link.Y, link.Z, link.DimensionPlaneId);
        if (!IsBlockCode(waystonePos, PocketWaystoneBlockCode))
        {
            return DimensionLibResult<DimensionLocation>.Fail("The linked return Waystone block is missing.", "missing-pocket-waystone-block");
        }

        var blockEntity = _api.World.BlockAccessor.GetBlockEntity(waystonePos) as PocketWaystoneBlockEntity;
        if (blockEntity == null || !string.Equals(blockEntity.EndpointId, link.EndpointId, StringComparison.Ordinal) || !string.Equals(blockEntity.BoundDimensionId, dimension.DimensionId, StringComparison.Ordinal))
        {
            return DimensionLibResult<DimensionLocation>.Fail("The linked return Waystone is no longer bound to this pocket.", "waystone-binding-mismatch");
        }

        return DimensionLibResult<DimensionLocation>.Ok(new DimensionLocation
        {
            DimensionId = link.SourceDimensionId,
            DimensionPlaneId = link.DimensionPlaneId,
            X = link.X + 0.5,
            Y = link.Y + 1,
            Z = link.Z + 0.5,
            Yaw = player?.Entity?.Pos?.Yaw ?? 0,
            Pitch = player?.Entity?.Pos?.Pitch ?? 0,
            Roll = player?.Entity?.Pos?.Roll ?? 0,
        });
    }

    private static string PlayerKey(IServerPlayer player)
    {
        return player?.PlayerUID ?? string.Empty;
    }

    private string BuildPocketList()
    {
        var pockets = _dimensionLib.Dimensions.Where(IsOwnedPocket).OrderBy(dimension => dimension.DimensionId, StringComparer.Ordinal).ToArray();
        return pockets.Length == 0
            ? "No Pocket Dimensions dimensions are registered."
            : string.Join("\n", pockets.Select(dimension => $"{DisplayName(dimension.DimensionId)}: chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}, prepared={_dimensionLib.IsDimensionPrepared(dimension.DimensionId)}, orphaned={_dimensionLib.IsDimensionOrphaned(dimension.DimensionId)}"));
    }

    private string BuildLayerStackList()
    {
        var stacks = _layerStacksById.Values.OrderBy(stack => stack.StackId, StringComparer.Ordinal).ToArray();
        if (stacks.Length == 0)
        {
            return "No Pocket Dimensions layer stacks are registered yet. Existing pockets become layer stacks when entered or inspected.";
        }

        return string.Join("\n", stacks.Select(stack =>
        {
            var layers = string.Join(", ", stack.Layers.OrderBy(layer => layer.Index).Select(layer => $"{FormatLayer(layer.Index)}={LayerDisplayName(stack, layer, layer.DimensionId)}"));
            return $"{stack.DisplayName}: {layers}";
        }));
    }

    private string BuildWaystoneLinkReport()
    {
        var links = _linksByEndpointId.Values
            .OrderBy(link => link.PocketDimensionId, StringComparer.Ordinal)
            .ThenBy(link => link.EndpointId, StringComparer.Ordinal)
            .ToArray();
        if (links.Length == 0)
        {
            return "No Pocket Dimensions Waystone links are registered.";
        }

        var report = new StringBuilder();
        foreach (var link in links)
        {
            var pocket = _dimensionLib.GetDimension(link.PocketDimensionId);
            var pos = new BlockPos(link.X, link.Y, link.Z, link.DimensionPlaneId);
            var blockOk = IsBlockCode(pos, PocketWaystoneBlockCode);
            var blockEntity = _api.World.BlockAccessor.GetBlockEntity(pos) as PocketWaystoneBlockEntity;
            var bindingOk = blockEntity != null
                && string.Equals(blockEntity.EndpointId, link.EndpointId, StringComparison.Ordinal)
                && string.Equals(blockEntity.BoundDimensionId, link.PocketDimensionId, StringComparison.Ordinal);

            var status = new List<string>
            {
                pocket.Success ? "pocket=ok" : "pocket=missing",
                pocket.Success && _dimensionLib.IsDimensionOrphaned(link.PocketDimensionId) ? "orphaned=yes" : "orphaned=no",
                blockOk ? "block=ok" : "block=missing",
                bindingOk ? "binding=ok" : "binding=stale"
            };

            report.Append(DisplayName(link.PocketDimensionId))
                .Append(" <- ")
                .Append(link.SourceDimensionId == null ? "overworld" : DisplayName(link.SourceDimensionId))
                .Append(" @ ")
                .Append(link.X).Append(',').Append(link.Y).Append(',').Append(link.Z)
                .Append(" dim=").Append(link.DimensionPlaneId)
                .Append(" endpoint=").Append(link.EndpointId)
                .Append(" (").Append(string.Join(", ", status)).Append(')');

            if (!string.IsNullOrWhiteSpace(link.BoundByPlayerName))
            {
                report.Append(" boundBy=").Append(link.BoundByPlayerName);
            }

            report.AppendLine();
        }

        return report.ToString().TrimEnd();
    }

    private string BuildPocketInspection(Dimension dimension)
    {
        var stack = EnsureLayerStack(dimension);
        var layer = FindLayer(stack, dimension.DimensionId);
        var pedestalPos = GetReturnPedestalWorldPos(dimension);
        var pedestalBlock = _api.World.BlockAccessor.GetBlock(pedestalPos);
        var pedestalCode = pedestalBlock?.Code?.ToString() ?? "missing";
        var elevatorPos = GetCentralElevatorWorldPos(dimension);
        var elevatorBlock = _api.World.BlockAccessor.GetBlock(elevatorPos);
        var elevatorCode = elevatorBlock?.Code?.ToString() ?? "missing";
        return $"{DisplayName(dimension.DimensionId)}: owner={dimension.OwnerModId}, layer={FormatLayer(layer?.Index ?? 0)}, stack={stack.DisplayName}, chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}, spawn=({dimension.SpawnX:0.#},{dimension.SpawnY},{dimension.SpawnZ:0.#}), prepared={_dimensionLib.IsDimensionPrepared(dimension.DimensionId)}, orphaned={_dimensionLib.IsDimensionOrphaned(dimension.DimensionId)}, elevator=({elevatorPos.X},{elevatorPos.Y},{elevatorPos.Z},dim={elevatorPos.dimension}) {elevatorCode}, returnPedestal=({pedestalPos.X},{pedestalPos.Y},{pedestalPos.Z},dim={pedestalPos.dimension}) {pedestalCode}";
    }

    private static bool IsOwnedPocket(Dimension dimension)
    {
        return dimension != null && string.Equals(dimension.OwnerModId, ModId, StringComparison.Ordinal);
    }

    private bool CanAccessPocket(IServerPlayer player, Dimension dimension, out string reason)
    {
        if (!IsOwnedPocket(dimension))
        {
            reason = "Dimension is not owned by Pocket Dimensions.";
            return false;
        }

        var stack = FindStackForDimension(dimension.DimensionId);
        if (CanEnterPocket(player, stack, dimension))
        {
            reason = string.Empty;
            return true;
        }

        reason = CapabilityDeniedReason(_config.DirectoryTeleportCapabilityMode, new[] { _config.EnterPrivilege, _config.UseWaystonePrivilege }, "enter this pocket");
        return false;
    }

    private bool CanUsePocketBlock(IServerPlayer player, Dimension dimension, out string reason)
    {
        return CanUsePocketBlock(player, FindStackForDimension(dimension?.DimensionId), dimension, out reason);
    }

    private bool CanMutatePocketBlock(IServerPlayer player, Dimension dimension, out string reason)
    {
        return CanMutatePocketBlock(player, FindStackForDimension(dimension?.DimensionId), dimension, out reason);
    }

    private bool CanUsePocketBlock(IServerPlayer player, PocketLayerStack stack, Dimension dimension, out string reason)
    {
        if (!IsOwnedPocket(dimension))
        {
            reason = "Dimension is not owned by Pocket Dimensions.";
            return false;
        }

        if (HasStackCapability(player, stack, _config.UsePocketBlocksCapabilityMode, _config.UsePocketBlocksPrivilege))
        {
            reason = string.Empty;
            return true;
        }

        reason = CapabilityDeniedReason(_config.UsePocketBlocksCapabilityMode, _config.UsePocketBlocksPrivilege, "use blocks inside this pocket");
        return false;
    }

    private bool CanMutatePocketBlock(IServerPlayer player, PocketLayerStack stack, Dimension dimension, out string reason)
    {
        if (!IsOwnedPocket(dimension))
        {
            reason = "Dimension is not owned by Pocket Dimensions.";
            return false;
        }

        if (HasStackCapability(player, stack, _config.MutatePocketBlocksCapabilityMode, _config.MutatePocketBlocksPrivilege))
        {
            reason = string.Empty;
            return true;
        }

        reason = CapabilityDeniedReason(_config.MutatePocketBlocksCapabilityMode, _config.MutatePocketBlocksPrivilege, "modify blocks inside this pocket");
        return false;
    }

    private bool CanCreatePocket(IServerPlayer player)
    {
        return HasGlobalCapability(player, _config.CreatePocketCapabilityMode, _config.CreatePrivilege);
    }

    private bool CanRefreshExistingPocket(IServerPlayer player, PocketLayerStack stack)
    {
        return HasPrivilege(player, _config.CreatePrivilege) ||
            IsStackOwner(player, stack) ||
            (stack != null && CanEditLayer(player, stack));
    }

    private bool CanCreateLayer(IServerPlayer player, PocketLayerStack stack)
    {
        return HasStackCapability(player, stack, _config.CreateLayerCapabilityMode, _config.CreatePrivilege);
    }

    private bool CanEditLayer(IServerPlayer player, PocketLayerStack stack)
    {
        return HasStackCapability(player, stack, _config.EditLayerCapabilityMode, _config.CreatePrivilege);
    }

    private bool CanViewPocket(IServerPlayer player, PocketLayerStack stack, Dimension dimension)
    {
        return IsOwnedPocket(dimension) && HasStackCapability(player, stack, _config.DirectoryVisibilityCapabilityMode, _config.EnterPrivilege, _config.UseWaystonePrivilege);
    }

    private bool CanTeleportFromDirectory(IServerPlayer player, PocketLayerStack stack, Dimension dimension)
    {
        return IsOwnedPocket(dimension) && HasStackCapability(player, stack, _config.DirectoryTeleportCapabilityMode, _config.EnterPrivilege, _config.UseWaystonePrivilege);
    }

    private bool CanEnterPocket(IServerPlayer player, PocketLayerStack stack, Dimension dimension)
    {
        return IsOwnedPocket(dimension) &&
            (HasStackCapability(player, stack, _config.DirectoryTeleportCapabilityMode, _config.EnterPrivilege, _config.UseWaystonePrivilege) ||
            HasStackCapability(player, stack, _config.UseWaystoneCapabilityMode, _config.UseWaystonePrivilege) ||
            HasStackCapability(player, stack, _config.UseElevatorCapabilityMode, _config.UseElevatorPrivilege));
    }

    private bool CanUseWaystone(IServerPlayer player, Dimension dimension)
    {
        return IsOwnedPocket(dimension) && HasStackCapability(player, FindStackForDimension(dimension.DimensionId), _config.UseWaystoneCapabilityMode, _config.UseWaystonePrivilege);
    }

    private bool CanUseElevator(IServerPlayer player, PocketLayerStack stack)
    {
        return HasStackCapability(player, stack, _config.UseElevatorCapabilityMode, _config.UseElevatorPrivilege);
    }

    private bool HasGlobalCapability(IServerPlayer player, string modeName, params string[] overridePrivileges)
    {
        if (HasAnyPrivilege(player, overridePrivileges))
        {
            return true;
        }

        return _config.ResolveCapabilityMode(modeName) == PocketCapabilityMode.Public && player != null;
    }

    private bool CanEditConfig(IServerPlayer player)
    {
        return HasPrivilege(player, _config.ConfigPrivilege);
    }

    private bool HasStackCapability(IServerPlayer player, PocketLayerStack stack, string modeName, params string[] overridePrivileges)
    {
        if (HasAnyPrivilege(player, overridePrivileges))
        {
            return true;
        }

        return _config.ResolveCapabilityMode(modeName) switch
        {
            PocketCapabilityMode.Public => player != null,
            PocketCapabilityMode.OwnerOrPrivilege => IsStackOwner(player, stack),
            PocketCapabilityMode.OwnerMemberOrPrivilege => IsStackOwner(player, stack) || IsStackMember(player, stack),
            _ => false,
        };
    }

    private static bool IsStackMember(IServerPlayer player, PocketLayerStack stack)
    {
        var playerKey = PlayerKey(player);
        return !string.IsNullOrWhiteSpace(playerKey) && stack?.MemberPlayerUids?.Any(uid => string.Equals(uid, playerKey, StringComparison.Ordinal)) == true;
    }

    private static bool HasAnyPrivilege(IServerPlayer player, params string[] privileges)
    {
        return privileges != null && privileges.Any(privilege => HasPrivilege(player, privilege));
    }

    private string CapabilityDeniedReason(string modeName, string privilege, string action)
    {
        return CapabilityDeniedReason(modeName, new[] { privilege }, action);
    }

    private string CapabilityDeniedReason(string modeName, string[] privileges, string action)
    {
        var mode = _config.ResolveCapabilityMode(modeName);
        if (mode == PocketCapabilityMode.Disabled)
        {
            return $"Server config disables the ability to {action}.";
        }

        var privilegeText = privileges == null || privileges.Length == 0
            ? "the configured privilege"
            : string.Join(" or ", privileges.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => $"'{value}'"));
        return $"Missing {privilegeText} to {action}.";
    }

    private static bool HasPrivilege(IServerPlayer player, string privilege)
    {
        return !string.IsNullOrWhiteSpace(privilege) && player?.HasPrivilege(privilege) == true;
    }

    private bool IsProtectedPocketBlock(BlockSelection blockSelection)
    {
        if (blockSelection?.Position == null)
        {
            return false;
        }

        var block = _api.World.BlockAccessor.GetBlock(blockSelection.Position);
        var code = block?.Code?.ToString();
        return string.Equals(code, PocketFloorBlockCode, StringComparison.Ordinal) || string.Equals(code, PocketReturnPedestalBlockCode, StringComparison.Ordinal);
    }

    private DimensionLibResult<PocketWaystoneBlockEntity> GetSelectedExternalWaystone(IServerPlayer player, bool requireBuildAccess)
    {
        var selection = player?.CurrentBlockSelection;
        if (selection?.Position == null)
        {
            return DimensionLibResult<PocketWaystoneBlockEntity>.Fail("Look at a placed Pocket Waystone first.", "missing-waystone-selection");
        }

        var block = _api.World.BlockAccessor.GetBlock(selection.Position);
        if (!string.Equals(block?.Code?.ToString(), PocketWaystoneBlockCode, StringComparison.Ordinal))
        {
            return DimensionLibResult<PocketWaystoneBlockEntity>.Fail("Selected block is not a Pocket Waystone.", "not-pocket-waystone");
        }

        var containingDimension = _dimensionLib.GetDimensionAt(selection.Position);
        if (containingDimension.Success && IsOwnedPocket(containingDimension.Value))
        {
            return DimensionLibResult<PocketWaystoneBlockEntity>.Fail("Pocket Waystones inside pockets are not bindable yet. Place a Waystone outside the pocket and bind that one.", "inside-pocket-waystone");
        }

        if (requireBuildAccess && !_api.World.Claims.TryAccess(player, selection.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            return DimensionLibResult<PocketWaystoneBlockEntity>.Fail("You do not have permission to modify that Waystone.", "claim-access-denied");
        }

        var blockEntity = _api.World.BlockAccessor.GetBlockEntity(selection.Position) as PocketWaystoneBlockEntity;
        return blockEntity != null
            ? DimensionLibResult<PocketWaystoneBlockEntity>.Ok(blockEntity)
            : DimensionLibResult<PocketWaystoneBlockEntity>.Fail("Selected Pocket Waystone has no block entity.", "missing-waystone-block-entity");
    }

    private static BlockPos GetReturnPedestalWorldPos(Dimension dimension)
    {
        return new BlockPos(
            dimension.MinBlockX + GetReturnPedestalLocalX(dimension),
            dimension.SpawnY,
            dimension.MinBlockZ + GetReturnPedestalLocalZ(dimension),
            dimension.DimensionPlaneId);
    }

    private static BlockPos GetCentralElevatorWorldPos(Dimension dimension)
    {
        return new BlockPos(
            dimension.MinBlockX + GetCentralElevatorLocalX(dimension),
            dimension.SpawnY - 1,
            dimension.MinBlockZ + GetCentralElevatorLocalZ(dimension),
            dimension.DimensionPlaneId);
    }

    private static int GetCentralElevatorLocalX(Dimension dimension)
    {
        return dimension.ChunkSizeX * GlobalConstants.ChunkSize / 2;
    }

    private static int GetCentralElevatorLocalZ(Dimension dimension)
    {
        return dimension.ChunkSizeZ * GlobalConstants.ChunkSize / 2;
    }

    private static int GetReturnPedestalLocalX(Dimension dimension)
    {
        var sizeX = dimension.ChunkSizeX * GlobalConstants.ChunkSize;
        return Math.Min(sizeX / 2 + 2, sizeX - 1);
    }

    private static int GetReturnPedestalLocalZ(Dimension dimension)
    {
        return dimension.ChunkSizeZ * GlobalConstants.ChunkSize / 2;
    }

    private static string ToDimensionId(string name)
    {
        name = (name ?? string.Empty).Trim().ToLowerInvariant();
        return name.Contains(':') ? name : $"{ModId}:{name}";
    }

    private static string ShortName(string dimensionId)
    {
        return dimensionId != null && dimensionId.StartsWith(ModId + ":", StringComparison.Ordinal)
            ? dimensionId.Substring(ModId.Length + 1)
            : dimensionId;
    }

    private static string DisplayPocketMessage(string message)
    {
        return (message ?? string.Empty).Replace(ModId + ":", string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizeDisplayName(string displayName)
    {
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return displayName.Length <= MaxDisplayNameLength
            ? displayName
            : displayName.Substring(0, MaxDisplayNameLength);
    }

    private static DimensionSpec ToSpec(Dimension dimension)
    {
        return new DimensionSpec
        {
            DimensionId = dimension.DimensionId,
            OwnerModId = dimension.OwnerModId,
            DimensionPlaneId = dimension.DimensionPlaneId,
            ChunkX = dimension.ChunkX,
            ChunkZ = dimension.ChunkZ,
            ChunkSizeX = dimension.ChunkSizeX,
            ChunkSizeZ = dimension.ChunkSizeZ,
            SpawnY = dimension.SpawnY,
            GeneratorId = dimension.GeneratorId,
            VisualSettings = dimension.VisualSettings?.Clone(),
            Seed = dimension.Seed,
            AccessPolicy = dimension.AccessPolicy,
            Mutability = dimension.Mutability,
            IsTransient = dimension.IsTransient,
        };
    }

    private static void ApplyPocketVisualDefaults(DimensionSpec spec)
    {
        spec.VisualSettings = CreatePocketVisualSettings();
    }

    private static DimensionVisualSettings CreatePocketVisualSettings()
    {
        return new DimensionVisualSettings
        {
            Sky = new DimensionSkyVisualSettings
            {
                RenderCover = true,
                Color = new DimensionColor4(0.012f, 0.013f, 0.015f),
            },
            Fog = new DimensionFogVisualSettings
            {
                Color = new DimensionWeightedColor(new DimensionColor3(0.018f, 0.02f, 0.024f), 0.45f),
                Density = new DimensionWeightedFloat(0f, 1.0f),
                FlatDensity = new DimensionWeightedFloat(0f, 1.0f),
                Brightness = new DimensionWeightedFloat(0.65f, 0.35f),
            },
            Ambient = new DimensionAmbientVisualSettings
            {
                Color = new DimensionWeightedColor(new DimensionColor3(0.58f, 0.60f, 0.64f), 0.70f),
            },
            Clouds = new DimensionCloudVisualSettings
            {
                Density = new DimensionWeightedFloat(0f, 0.8f),
                Brightness = new DimensionWeightedFloat(0f, 0.8f),
            },
            Scene = new DimensionSceneVisualSettings
            {
                Brightness = new DimensionWeightedFloat(1.05f, 0.45f),
                MinimumLight = PocketMinimumSceneLight,
                LightLift = new DimensionColor3(0.60f, 0.62f, 0.66f),
            },
        };
    }

    private static TextCommandResult ToCommandResult(DimensionLibResult result)
    {
        var message = DisplayPocketMessage(result.Message);
        return result.Success ? TextCommandResult.Success(message) : TextCommandResult.Error(message, result.ErrorCode);
    }

    private static TextCommandResult ToCommandResult<T>(DimensionLibResult<T> result)
    {
        var message = DisplayPocketMessage(result.Message);
        return result.Success ? TextCommandResult.Success(message) : TextCommandResult.Error(message, result.ErrorCode);
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private int ResolveDefaultSpawnY()
    {
        return _config.DefaultSpawnY > 0
            ? ClampInt(_config.DefaultSpawnY, 1, _api.WorldManager.MapSizeY - 2)
            : _api.WorldManager.MapSizeY / 2;
    }

    private sealed class PocketPlatformSource : IBlockVolumeSource
    {
        private readonly ICoreServerAPI _api;
        private readonly Dimension _dimension;
        private readonly int _platformBlockId;
        private readonly int _returnPedestalBlockId;
        private readonly int _elevatorBlockId;

        public PocketPlatformSource(ICoreServerAPI api, Dimension dimension)
        {
            _api = api;
            _dimension = dimension;
            Bounds = new BlockVolumeBounds(
                dimension.ChunkSizeX * GlobalConstants.ChunkSize,
                api.WorldManager.MapSizeY,
                dimension.ChunkSizeZ * GlobalConstants.ChunkSize);
            _platformBlockId = ResolveBlockId(PocketFloorBlockCode, "pocketfloor", "game:rock-granite", "rock-granite", "game:cobblestone-granite", "cobblestone-granite");
            _returnPedestalBlockId = ResolveBlockId(PocketReturnPedestalBlockCode);
            _elevatorBlockId = ResolveBlockId(PocketElevatorBlockCode);
        }

        public string SourceId => "pocketdimensions:floor-and-return-pedestal";

        public BlockVolumeBounds Bounds { get; }

        public void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token)
        {
            if (_platformBlockId == 0)
            {
                return;
            }

            var chunkSize = GlobalConstants.ChunkSize;
            var baseX = localChunkX * chunkSize;
            var baseZ = localChunkZ * chunkSize;
            var localPos = new BlockPos(0);

            for (var lx = 0; lx < chunkSize; lx++)
            {
                for (var lz = 0; lz < chunkSize; lz++)
                {
                    token.ThrowIfCancellationRequested();
                    var x = baseX + lx;
                    var z = baseZ + lz;
                    writer.SetBlock(_platformBlockId, localPos.Set(x, _dimension.SpawnY - 1, z));
                }
            }

            PlaceReturnPedestalIfInColumn(writer, localChunkX, localChunkZ, localPos);
            PlaceElevatorIfInColumn(writer, localChunkX, localChunkZ, localPos);
        }

        private void PlaceElevatorIfInColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, BlockPos localPos)
        {
            if (_elevatorBlockId == 0)
            {
                return;
            }

            var chunkSize = GlobalConstants.ChunkSize;
            var elevatorX = GetCentralElevatorLocalX(_dimension);
            var elevatorZ = GetCentralElevatorLocalZ(_dimension);
            if (localChunkX != elevatorX / chunkSize || localChunkZ != elevatorZ / chunkSize)
            {
                return;
            }

            writer.SetBlock(_elevatorBlockId, localPos.Set(elevatorX, _dimension.SpawnY - 1, elevatorZ));
        }

        private void PlaceReturnPedestalIfInColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, BlockPos localPos)
        {
            if (_returnPedestalBlockId == 0)
            {
                return;
            }

            var chunkSize = GlobalConstants.ChunkSize;
            var pedestalX = GetReturnPedestalLocalX(_dimension);
            var pedestalZ = GetReturnPedestalLocalZ(_dimension);
            if (localChunkX != pedestalX / chunkSize || localChunkZ != pedestalZ / chunkSize)
            {
                return;
            }

            writer.SetBlock(_returnPedestalBlockId, localPos.Set(pedestalX, _dimension.SpawnY, pedestalZ));
        }

        private int ResolveBlockId(params string[] codes)
        {
            foreach (var code in codes)
            {
                var block = _api.World.GetBlock(new AssetLocation(code));
                if (block != null && block.BlockId != 0)
                {
                    return block.BlockId;
                }
            }

            _api.Logger.Warning("[PocketDimensions] Could not resolve a platform block; created pocket will be empty.");
            return 0;
        }
    }

}
