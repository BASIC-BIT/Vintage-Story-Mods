using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

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
    private const float PocketMinimumSceneLight = 0.18f;

    private ICoreServerAPI _api;
    private IDimensionLibApi _dimensionLib;
    private PocketLinkStore _linkStore;
    private PocketDimensionsConfig _config = new PocketDimensionsConfig();
    private readonly Dictionary<string, PocketWaystoneLink> _linksByEndpointId = new Dictionary<string, PocketWaystoneLink>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _activeIngressByPlayer = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, DimensionLocation>> _unanchoredReturnsByPlayer = new Dictionary<string, Dictionary<string, DimensionLocation>>(StringComparer.Ordinal);

    public override double ExecuteOrder()
    {
        return 1.1;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("PocketWaystoneBlock", typeof(PocketWaystoneBlock));
        api.RegisterBlockClass("PocketReturnPedestalBlock", typeof(PocketReturnPedestalBlock));
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
        api.Logger.Notification("[PocketDimensions] Registered /pocket commands.");
    }

    public override void Dispose()
    {
        if (_api != null)
        {
            _api.Event.GameWorldSave -= SaveLinkState;
            SaveLinkState();
        }

        base.Dispose();
    }

    public bool CanEnter(IServerPlayer player, Dimension dimension, out string reason)
    {
        reason = string.Empty;
        return CanAccessPocket(player, dimension, out reason);
    }

    public bool CanUseBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, out string reason)
    {
        reason = string.Empty;
        return CanAccessPocket(player, dimension, out reason);
    }

    public bool CanMutateBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, DimensionBlockMutationKind mutationKind, out string reason)
    {
        reason = string.Empty;
        if (mutationKind == DimensionBlockMutationKind.Break && IsProtectedPocketBlock(blockSelection))
        {
            reason = "Pocket Dimension anchors are indestructible.";
            return false;
        }

        return CanAccessPocket(player, dimension, out reason);
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
        var ensured = EnsurePocketInfrastructure(dimension, player);
        if (!ensured.Success)
        {
            return ensured;
        }

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
                return DimensionLibResult.Fail(linkError, "missing-pocket-waystone-link");
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

    public void ForgetWaystoneEndpoint(string endpointId)
    {
        RemoveWaystoneLink(endpointId);
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

        var dimensionId = ToDimensionId(name);
        var existing = _dimensionLib.GetDimension(dimensionId);
        if (existing.Success)
        {
            if (!IsOwnedPocket(existing.Value))
            {
                return TextCommandResult.Error($"Dimension '{DisplayName(dimensionId)}' is not owned by Pocket Dimensions.");
            }

            var refreshSpec = ToSpec(existing.Value);
            ApplyPocketVisualDefaults(refreshSpec);
            var refreshed = _dimensionLib.RegisterDimension(refreshSpec);
            if (!refreshed.Success)
            {
                return ToCommandResult(refreshed);
            }

            return ToCommandResult(PreparePocket(refreshed.Value, args.Caller.Player as IServerPlayer, "Pocket already exists; refreshed and prepared existing dimension."));
        }

        var sizeChunks = int.TryParse(sizeText, out var parsedSize) ? ClampInt(parsedSize, 1, _config.MaxSizeChunks) : _config.DefaultSizeChunks;
        var spawnY = int.TryParse(spawnYText, out var parsedSpawnY) ? ClampInt(parsedSpawnY, 1, _api.WorldManager.MapSizeY - 2) : ResolveDefaultSpawnY();
        var spec = new DimensionSpec
        {
            DimensionId = dimensionId,
            OwnerModId = ModId,
            DimensionPlaneId = _dimensionLib.PrimaryDimensionPlaneId,
            ChunkSizeX = sizeChunks,
            ChunkSizeZ = sizeChunks,
            SpawnY = spawnY,
            VisualSettings = CreatePocketVisualSettings(),
            Kind = DimensionKind.Pocket,
            AccessPolicy = DimensionAccessPolicy.OwnerOnly,
            Mutability = DimensionMutability.Mutable,
            IsTransient = false,
        };

        var registered = _dimensionLib.RegisterDimension(spec);
        if (!registered.Success)
        {
            return ToCommandResult(registered);
        }

        return ToCommandResult(PreparePocket(registered.Value, args.Caller.Player as IServerPlayer, $"Created pocket '{DisplayName(dimensionId)}'."));
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

    private DimensionLibResult PreparePocket(Dimension dimension, IServerPlayer player, string successPrefix)
    {
        var prepared = _dimensionLib.PrepareDimension(dimension.DimensionId, new PocketPlatformSource(_api, dimension), player);
        return prepared.Success
            ? DimensionLibResult.Ok($"{successPrefix} Bind a Pocket Waystone with /pocket bind {ShortName(dimension.DimensionId)} to create an entry point.")
            : prepared;
    }

    private DimensionLibResult EnsurePocketInfrastructure(Dimension dimension, IServerPlayer player)
    {
        return _dimensionLib.IsDimensionPrepared(dimension.DimensionId) && HasManagedInfrastructure(dimension)
            ? DimensionLibResult.Ok("Pocket infrastructure is ready.")
            : PreparePocket(dimension, player, "Prepared pocket infrastructure.");
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
        return IsBlockCode(spawnFloorPos, PocketFloorBlockCode);
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
    }

    private void SaveLinkState()
    {
        _linkStore?.Save(new PocketLinkState
        {
            Links = _linksByEndpointId.Values.ToList(),
            ActiveIngressByPlayer = _activeIngressByPlayer,
            UnanchoredReturnsByPlayer = _unanchoredReturnsByPlayer,
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

    private string BuildPocketInspection(Dimension dimension)
    {
        var pedestalPos = GetReturnPedestalWorldPos(dimension);
        var pedestalBlock = _api.World.BlockAccessor.GetBlock(pedestalPos);
        var pedestalCode = pedestalBlock?.Code?.ToString() ?? "missing";
        return $"{DisplayName(dimension.DimensionId)}: owner={dimension.OwnerModId}, chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}, spawn=({dimension.SpawnX:0.#},{dimension.SpawnY},{dimension.SpawnZ:0.#}), prepared={_dimensionLib.IsDimensionPrepared(dimension.DimensionId)}, orphaned={_dimensionLib.IsDimensionOrphaned(dimension.DimensionId)}, returnPedestal=({pedestalPos.X},{pedestalPos.Y},{pedestalPos.Z},dim={pedestalPos.dimension}) {pedestalCode}";
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
            Kind = dimension.Kind,
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
            RenderSkyCover = true,
            SkyRed = 0.012f,
            SkyGreen = 0.013f,
            SkyBlue = 0.015f,
            SkyAlpha = 1f,
            FogRed = 0.018f,
            FogGreen = 0.02f,
            FogBlue = 0.024f,
            FogColorWeight = 0.45f,
            AmbientRed = 0.58f,
            AmbientGreen = 0.60f,
            AmbientBlue = 0.64f,
            AmbientColorWeight = 0.70f,
            FogDensityWeight = 1.0f,
            FlatFogDensityWeight = 1.0f,
            CloudDensityWeight = 0.8f,
            CloudBrightnessWeight = 0.8f,
            SceneBrightness = 1.05f,
            SceneBrightnessWeight = 0.45f,
            FogBrightness = 0.65f,
            FogBrightnessWeight = 0.35f,
            MinimumSceneLight = PocketMinimumSceneLight,
            LightLiftRed = 0.60f,
            LightLiftGreen = 0.62f,
            LightLiftBlue = 0.66f,
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

        public string BindPrivilege { get; set; } = Privilege.root;

        public string ReleasePrivilege { get; set; } = Privilege.root;

        public int DefaultSizeChunks { get; set; } = 3;

        public int MaxSizeChunks { get; set; } = 16;

        public int DefaultSpawnY { get; set; }

        public void Normalize()
        {
            CreatePrivilege = NormalizePrivilege(CreatePrivilege, Privilege.root);
            EnterPrivilege = NormalizePrivilege(EnterPrivilege, Privilege.root);
            ExitPrivilege = NormalizePrivilege(ExitPrivilege, Privilege.root);
            UseWaystonePrivilege = NormalizePrivilege(UseWaystonePrivilege, EnterPrivilege);
            BindPrivilege = NormalizePrivilege(BindPrivilege, Privilege.root);
            ReleasePrivilege = NormalizePrivilege(ReleasePrivilege, Privilege.root);
            MaxSizeChunks = ClampInt(MaxSizeChunks, 1, 64);
            DefaultSizeChunks = ClampInt(DefaultSizeChunks, 1, MaxSizeChunks);
            DefaultSpawnY = Math.Max(0, DefaultSpawnY);
        }

        private static string NormalizePrivilege(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
