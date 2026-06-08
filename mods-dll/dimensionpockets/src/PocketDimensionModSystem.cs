using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private const float PocketMinimumSceneLight = 0.18f;
    private const int HudUpdateTickMs = 250;

    private ICoreServerAPI _api;
    private ICoreClientAPI _clientApi;
    private IDimensionLibApi _dimensionLib;
    private PocketLinkStore _linkStore;
    private PocketDimensionsConfig _config = new PocketDimensionsConfig();
    private IServerNetworkChannel _serverChannel;
    private IClientNetworkChannel _clientChannel;
    private PocketCoordinatesHud _coordinatesHud;
    private PocketDirectoryDialog _directoryDialog;
    private long _hudUpdateListenerId;
    private readonly Dictionary<string, PocketWaystoneLink> _linksByEndpointId = new Dictionary<string, PocketWaystoneLink>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _activeIngressByPlayer = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, DimensionLocation>> _unanchoredReturnsByPlayer = new Dictionary<string, Dictionary<string, DimensionLocation>>(StringComparer.Ordinal);
    private readonly Dictionary<string, PocketLayerStack> _layerStacksById = new Dictionary<string, PocketLayerStack>(StringComparer.Ordinal);
    private readonly HashSet<string> _playersWithPocketHud = new HashSet<string>(StringComparer.Ordinal);

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
            .SetMessageHandler<PocketElevatorTravelRequest>(OnElevatorTravelRequest)
            .SetMessageHandler<PocketLayerCreationResponse>(OnLayerCreationResponse)
            .SetMessageHandler<PocketElevatorPlacementResponse>(OnElevatorPlacementResponse)
            .SetMessageHandler<PocketDirectoryRequest>(OnPocketDirectoryRequest)
            .SetMessageHandler<PocketDirectoryActionRequest>(OnPocketDirectoryActionRequest);
        _linkStore = new PocketLinkStore(api);
        LoadLinkState();
        api.ObjectCache[WaystoneServiceCacheKey] = this;
        _config = LoadConfig(api);
        var policyResult = _dimensionLib.RegisterPolicyProvider(ModId, this);
        if (!policyResult.Success)
        {
            api.Logger.Warning("[PocketDimensions] Failed to register DimensionLib policy provider: {0}", policyResult.Message);
        }

        RegisterCommands();
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
            .SetMessageHandler<PocketLayerCreationPrompt>(OnLayerCreationPrompt)
            .SetMessageHandler<PocketElevatorPlacementPrompt>(OnElevatorPlacementPrompt)
            .SetMessageHandler<PocketHudStateMessage>(OnPocketHudState)
            .SetMessageHandler<PocketDirectoryStateMessage>(OnPocketDirectoryState);

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

        if (!HasPrivilege(player, _config.UseWaystonePrivilege))
        {
            return DimensionLibResult.Fail($"Missing privilege '{_config.UseWaystonePrivilege}'.", "missing-waystone-use-privilege");
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

        var linked = RegisterWaystoneLink(blockEntity, blockSelection.Position, lookup.Value, player);
        if (!linked.Success)
        {
            return DimensionLibResult.Fail(linked.Message, linked.ErrorCode);
        }

        var ensured = EnsurePocketInfrastructure(lookup.Value, player);
        return ensured.Success ? EnterPocket(player, lookup.Value, linked.Value.EndpointId) : ensured;
    }

    public DimensionLibResult ReturnFromPocket(IServerPlayer player, BlockSelection blockSelection = null)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        var lookup = _dimensionLib.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        if (!lookup.Success || !IsOwnedPocket(lookup.Value))
        {
            return DimensionLibResult.Fail("You are not inside a Pocket Dimensions pocket.", "not-in-pocket");
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

    public void ForgetWaystoneEndpoint(string endpointId)
    {
        RemoveWaystoneLink(endpointId);
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
        if (!HasPrivilege(player, _config.UseElevatorPrivilege))
        {
            return DimensionLibResult.Fail($"Missing privilege '{_config.UseElevatorPrivilege}'.", "missing-elevator-use-privilege");
        }

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
        if (layer == null)
        {
            return DimensionLibResult.Fail($"Pocket layer metadata for '{DisplayName(dimension.DimensionId)}' is unavailable.", "missing-pocket-layer");
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
            return DimensionLibResult.Fail($"Missing privilege '{_config.MutatePocketBlocksPrivilege}' to place a Pocket Elevator on the target layer.", "missing-elevator-place-privilege");
        }

        var landing = EnsureElevatorLanding(targetElevatorPos, target.Value.CreatedTargetLayer || placeMissingElevator);
        if (!landing.Success)
        {
            if (IsMissingTargetElevator(landing.ErrorCode))
            {
                if (!HasPrivilege(player, _config.MutatePocketBlocksPrivilege))
                {
                    return DimensionLibResult.Fail($"Missing privilege '{_config.MutatePocketBlocksPrivilege}' to place a Pocket Elevator on the target layer.", "missing-elevator-place-privilege");
                }

                PromptElevatorPlacement(player, stack, layer.Index, targetIndex, direction);
                return DimensionLibResult.Ok($"Confirm creating a Pocket Elevator on layer {FormatLayer(targetIndex)} to continue.");
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

        return DimensionLibResult.Ok($"Moved to {DisplayName(target.Value.TargetLayer.DimensionId)} layer {FormatLayer(target.Value.TargetLayer.Index)}.");
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

        if (!HasPrivilege(player, _config.CreatePrivilege))
        {
            return DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Fail($"Missing privilege '{_config.CreatePrivilege}' to create a new pocket layer.", "missing-layer-create-privilege");
        }

        if (!createMissingLayer)
        {
            PromptLayerCreation(player, stack, sourceLayer.Index, targetIndex, direction);
            return DimensionLibResult<(PocketLayerRef TargetLayer, bool CreatedTargetLayer)>.Ok((null, false), $"Confirm creating layer {FormatLayer(targetIndex)} to continue.");
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

        SendPocketDirectory(player, result.Message, result.Success);
    }

    private DimensionLibResult HandlePocketDirectoryAction(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        return request?.Action?.Trim().ToLowerInvariant() switch
        {
            "enter" => EnterPocketFromDirectory(player, request.DimensionId),
            "createpocket" => CreatePocketFromDirectory(player, request),
            _ => DimensionLibResult.Fail("Unknown Pocket Directory action.", "unknown-pocket-directory-action"),
        };
    }

    private DimensionLibResult CreatePocketFromDirectory(IServerPlayer player, PocketDirectoryActionRequest request)
    {
        if (!HasPrivilege(player, _config.CreatePrivilege))
        {
            return DimensionLibResult.Fail($"Missing privilege '{_config.CreatePrivilege}' to create a pocket.", "missing-pocket-create-privilege");
        }

        var result = CreateOrPreparePocket(player, request?.DisplayName, request?.Slug, request?.SizeChunks ?? 0, request?.SpawnY ?? 0);
        return result.Success
            ? DimensionLibResult.Ok(result.Message)
            : DimensionLibResult.Fail(result.Message, result.ErrorCode);
    }

    private DimensionLibResult EnterPocketFromDirectory(IServerPlayer player, string dimensionId)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return DimensionLibResult.Fail("Select a pocket layer first.", "missing-pocket-selection");
        }

        var lookup = _dimensionLib.GetDimension(dimensionId.Trim());
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        if (!CanAccessPocket(player, lookup.Value, out var reason))
        {
            return DimensionLibResult.Fail(reason, "missing-pocket-access-privilege");
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

        var text = $"Create a new pocket layer {FormatLayer(prompt.TargetLayerIndex)} {(prompt.Direction > 0 ? "above" : "below")} {prompt.StackName}?";
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

        var text = $"Create a Pocket Elevator at the matching landing on layer {FormatLayer(prompt.TargetLayerIndex)} in {prompt.StackName}?";
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
            error = DimensionLibResult.Fail("Pocket layer confirmation is no longer valid.", "pocketlayer-confirmation-stale");
            return false;
        }

        var stack = FindStackForDimension(dimensionLookup.Value.DimensionId);
        var layer = stack == null ? null : FindLayer(stack, dimensionLookup.Value.DimensionId);
        if (stack == null || layer == null ||
            !string.Equals(stack.StackId, stackId, StringComparison.Ordinal) ||
            layer.Index != sourceLayerIndex ||
            targetLayerIndex != layer.Index + direction)
        {
            error = DimensionLibResult.Fail("Pocket layer confirmation is stale. Try the elevator again.", "pocketlayer-confirmation-stale");
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
            dimensionId => _clientChannel?.SendPacket(new PocketDirectoryActionRequest { Action = "enter", DimensionId = dimensionId }),
            (displayName, slug, sizeChunks, spawnY) => _clientChannel?.SendPacket(new PocketDirectoryActionRequest
            {
                Action = "createPocket",
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
        if (player == null || !HasPrivilege(player, _config.CreatePrivilege))
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

    private void SendPocketDirectory(IServerPlayer player, string message = null, bool success = true)
    {
        if (player == null)
        {
            return;
        }

        _serverChannel?.SendPacket(BuildPocketDirectoryState(player, message, success), player);
    }

    private PocketDirectoryStateMessage BuildPocketDirectoryState(IServerPlayer player, string message, bool success)
    {
        var state = new PocketDirectoryStateMessage
        {
            Message = message,
            Success = success,
            CanCreatePocket = HasPrivilege(player, _config.CreatePrivilege),
            CanCreateLayer = HasPrivilege(player, _config.CreatePrivilege),
        };

        var currentDimensionId = CurrentPocketDimensionId(player);
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

    private PocketDirectoryStackMessage BuildDirectoryStack(IServerPlayer player, PocketLayerStack stack, string currentDimensionId, HashSet<string> emittedDimensions)
    {
        var entry = new PocketDirectoryStackMessage
        {
            StackId = stack.StackId,
            DisplayName = stack.DisplayName,
            OwnerPlayerUid = stack.OwnerPlayerUid,
            OwnerPlayerName = stack.OwnerPlayerName,
            IsOwner = IsStackOwner(player, stack),
            CanCreateLayer = HasPrivilege(player, _config.CreatePrivilege),
        };

        foreach (var layer in stack.Layers.OrderBy(layer => layer.Index))
        {
            var dimension = _dimensionLib.GetDimension(layer.DimensionId);
            if (!dimension.Success || !IsOwnedPocket(dimension.Value))
            {
                continue;
            }

            emittedDimensions.Add(dimension.Value.DimensionId);
            var layerEntry = BuildDirectoryLayer(player, layer.Index, dimension.Value, currentDimensionId);
            if (layerEntry != null)
            {
                entry.Layers.Add(layerEntry);
            }
        }

        return entry;
    }

    private PocketDirectoryStackMessage BuildDirectoryStandalonePocket(IServerPlayer player, Dimension dimension, string currentDimensionId)
    {
        var layer = BuildDirectoryLayer(player, 0, dimension, currentDimensionId);
        if (layer == null)
        {
            return null;
        }

        return new PocketDirectoryStackMessage
        {
            StackId = dimension.DimensionId,
            DisplayName = DisplayName(dimension.DimensionId),
            CanCreateLayer = HasPrivilege(player, _config.CreatePrivilege),
            Layers = new List<PocketDirectoryLayerMessage> { layer },
        };
    }

    private PocketDirectoryLayerMessage BuildDirectoryLayer(IServerPlayer player, int layerIndex, Dimension dimension, string currentDimensionId)
    {
        if (!CanAccessPocket(player, dimension, out _))
        {
            return null;
        }

        var orphaned = _dimensionLib.IsDimensionOrphaned(dimension.DimensionId);
        return new PocketDirectoryLayerMessage
        {
            Index = layerIndex,
            DimensionId = dimension.DimensionId,
            DisplayName = DisplayName(dimension.DimensionId),
            Prepared = _dimensionLib.IsDimensionPrepared(dimension.DimensionId),
            Orphaned = orphaned,
            CanTeleport = !orphaned,
            IsCurrent = string.Equals(dimension.DimensionId, currentDimensionId, StringComparison.Ordinal),
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
                new PocketLayerRef { Index = 0, DimensionId = dimension.DimensionId },
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

    private DimensionLibResult<PocketLayerRef> CreateLayer(PocketLayerStack stack, int targetIndex, IServerPlayer player)
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

        var prepared = PreparePocket(dimension, player, $"Created pocket layer {FormatLayer(targetIndex)}.", recordStandaloneStack: false);
        if (!prepared.Success)
        {
            return DimensionLibResult<PocketLayerRef>.Fail(prepared.Message, prepared.ErrorCode);
        }

        var layer = new PocketLayerRef { Index = targetIndex, DimensionId = dimension.DimensionId };
        stack.Layers.Add(layer);
        stack.Normalize();
        _layerStacksById[stack.StackId] = stack;

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
            return DimensionLibResult.Fail("Adjacent pocket layers are required before registering an elevator mapping.", "missing-pocket-layer");
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
        return DimensionLibResult.Ok($"Linked pocket layers {FormatLayer(lowerIndex)} and {FormatLayer(upperIndex)}.");
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

            return DimensionLibResult.Fail("Target layer needs a Pocket Elevator at the mapped landing.", "missing-target-pocketelevator");
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
            .BeginSubCommand("bind")
                .WithDescription("Bind the selected placed Waystone to a pocket dimension")
                .WithArgs(new StringArgParser("name", true))
                .RequiresPrivilege(_config.BindPrivilege)
                .RequiresPlayer()
                .HandleWith(HandleBindWaystone)
                .EndSubCommand()
            .BeginSubCommand("unbind")
                .WithDescription("Clear the selected placed Waystone binding")
                .RequiresPrivilege(_config.BindPrivilege)
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
        slug = NormalizePocketSlug(slug);
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
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var stack = EnsureLayerStack(dimension);
        stack.DisplayName = displayName.Trim();
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

        if (!_linksByEndpointId.Remove(endpointId))
        {
            return;
        }

        RemoveActiveIngressReferences(endpointId);
        SaveLinkState();
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
            var layers = string.Join(", ", stack.Layers.OrderBy(layer => layer.Index).Select(layer => $"{FormatLayer(layer.Index)}={DisplayName(layer.DimensionId)}"));
            return $"{stack.DisplayName}: {layers}";
        }));
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

        if (HasPrivilege(player, _config.EnterPrivilege) || HasPrivilege(player, _config.UseWaystonePrivilege))
        {
            reason = string.Empty;
            return true;
        }

        reason = string.Equals(_config.EnterPrivilege, _config.UseWaystonePrivilege, StringComparison.Ordinal)
            ? $"Missing privilege '{_config.EnterPrivilege}'."
            : $"Missing privilege '{_config.EnterPrivilege}' or '{_config.UseWaystonePrivilege}'.";
        return false;
    }

    private bool CanUsePocketBlock(IServerPlayer player, Dimension dimension, out string reason)
    {
        return HasOwnedPocketPrivilege(player, dimension, _config.UsePocketBlocksPrivilege, "use blocks inside this pocket", out reason);
    }

    private bool CanMutatePocketBlock(IServerPlayer player, Dimension dimension, out string reason)
    {
        return HasOwnedPocketPrivilege(player, dimension, _config.MutatePocketBlocksPrivilege, "modify blocks inside this pocket", out reason);
    }

    private static bool HasOwnedPocketPrivilege(IServerPlayer player, Dimension dimension, string privilege, string action, out string reason)
    {
        if (!IsOwnedPocket(dimension))
        {
            reason = "Dimension is not owned by Pocket Dimensions.";
            return false;
        }

        if (HasPrivilege(player, privilege))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"Missing privilege '{privilege}' to {action}.";
        return false;
    }

    private static bool HasPrivilege(IServerPlayer player, string privilege)
    {
        return player?.HasPrivilege(privilege) == true;
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

    private static string NormalizePocketSlug(string value)
    {
        value = (value ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new List<char>(value.Length);
        var lastDash = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == ':')
            {
                chars.Add(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                chars.Add('-');
                lastDash = true;
            }
        }

        return new string(chars.ToArray()).Trim('-');
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

    private static PocketDimensionsConfig LoadConfig(ICoreServerAPI api)
    {
        PocketDimensionsConfig config;
        try
        {
            config = api.LoadModConfig<PocketDimensionsConfig>(ConfigName) ?? new PocketDimensionsConfig();
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[PocketDimensions] Failed to load config; using defaults: {0}", ex.Message);
            config = new PocketDimensionsConfig();
        }

        config.Normalize();
        api.StoreModConfig(config, ConfigName);
        return config;
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

    private sealed class PocketDimensionsConfig
    {
        public string CreatePrivilege { get; set; } = Privilege.root;

        public string EnterPrivilege { get; set; } = Privilege.root;

        public string ExitPrivilege { get; set; } = Privilege.root;

        public string UseWaystonePrivilege { get; set; } = Privilege.root;

        public string UseElevatorPrivilege { get; set; } = Privilege.root;

        public string UsePocketBlocksPrivilege { get; set; } = Privilege.root;

        public string MutatePocketBlocksPrivilege { get; set; } = Privilege.root;

        public string BindPrivilege { get; set; } = Privilege.root;

        public string ReleasePrivilege { get; set; } = Privilege.root;

        public string ElevatorLandingMode { get; set; } = nameof(PocketElevatorLandingMode.RequireElevatorBlock);

        public int DefaultSizeChunks { get; set; } = 3;

        public int MaxSizeChunks { get; set; } = 16;

        public int DefaultSpawnY { get; set; }

        public void Normalize()
        {
            CreatePrivilege = NormalizePrivilege(CreatePrivilege, Privilege.root);
            EnterPrivilege = NormalizePrivilege(EnterPrivilege, Privilege.root);
            ExitPrivilege = NormalizePrivilege(ExitPrivilege, Privilege.root);
            UseWaystonePrivilege = NormalizePrivilege(UseWaystonePrivilege, EnterPrivilege);
            UseElevatorPrivilege = NormalizePrivilege(UseElevatorPrivilege, UseWaystonePrivilege);
            UsePocketBlocksPrivilege = NormalizePrivilege(UsePocketBlocksPrivilege, Privilege.root);
            MutatePocketBlocksPrivilege = NormalizePrivilege(MutatePocketBlocksPrivilege, Privilege.root);
            BindPrivilege = NormalizePrivilege(BindPrivilege, Privilege.root);
            ReleasePrivilege = NormalizePrivilege(ReleasePrivilege, Privilege.root);
            if (!Enum.TryParse<PocketElevatorLandingMode>(ElevatorLandingMode, ignoreCase: true, out var parsed))
            {
                parsed = PocketElevatorLandingMode.RequireElevatorBlock;
            }

            ElevatorLandingMode = parsed.ToString();
            MaxSizeChunks = ClampInt(MaxSizeChunks, 1, 64);
            DefaultSizeChunks = ClampInt(DefaultSizeChunks, 1, MaxSizeChunks);
            DefaultSpawnY = Math.Max(0, DefaultSpawnY);
        }

        private static string NormalizePrivilege(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        public PocketElevatorLandingMode ResolveElevatorLandingMode()
        {
            return Enum.TryParse<PocketElevatorLandingMode>(ElevatorLandingMode, ignoreCase: true, out var parsed)
                ? parsed
                : PocketElevatorLandingMode.RequireElevatorBlock;
        }
    }
}
