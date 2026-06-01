using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DimensionLib.Api;
using DimensionLib.Core;
using DimensionLib.Effects;
using DimensionLib.Generation;
using DimensionLib.Lab;
using DimensionLib.Lighting;
using DimensionLib.Protection;
using DimensionLib.Transfer;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

public sealed class DimensionLibServerService : IDisposable
{
    public const string DebugDimensionId = "dimensionlib:debug-spike";
    public const string OverworldOppositeDimensionId = "dimensionlib:test-overworld-opposite";
    public const string NetherCavernDimensionId = "dimensionlib:test-nether-cavern";
    private const int InitialGeneratedChunkRadius = 1;
    private const int FallbackLazyGeneratedChunkRadius = 2;
    private const int InitialGeneratedChunkBudget = 1;
    private const int LazyGeneratedChunkBudgetPerTick = 1;
    private const int TemporalStabilityTickMs = 250;

    private readonly ICoreServerAPI _api;
    private readonly DimensionManifestService _manifestService;
    private readonly DimensionChunkService _chunkService;
    private readonly ChunkColumnMaterializer _materializer;
    private readonly DebugDimensionPlatformBuilder _debugPlatformBuilder;
    private readonly ChunkLightFloorApplier _lightFloorApplier;
    private readonly GeneratedDimensionWindowPreparer _generatedWindowPreparer;
    private readonly GeneratedDimensionStreamer _generatedStreamer;
    private readonly PolicyProviderRegistry _policyProviders;
    private readonly DimensionAccessService _accessService;
    private readonly BlockInteractionProtectionAdapter _blockProtectionAdapter;
    private readonly ReturnPositionStore _returnPositions;
    private readonly DimensionTransferService _transferService;
    private readonly VisualTuningBroadcaster _visualTuningBroadcaster;
    private readonly TemporalStabilityGuard _temporalStabilityGuard;
    private readonly PreparedDimensionTracker _preparedDimensions = new PreparedDimensionTracker();
    private readonly DimensionGeneratorRegistry _generators = new DimensionGeneratorRegistry();
    private readonly DimensionDiagnosticService _diagnostics;
    private readonly DimensionRegistry _dimensions = new DimensionRegistry(DimensionLibModSystem.FirstPrototypeDimension);
    private long _temporalStabilityListenerId;
    private long _lazyGenerationListenerId;
    private bool _started;

    public DimensionLibServerService(ICoreServerAPI api, IServerNetworkChannel serverChannel)
    {
        _api = api;
        _manifestService = new DimensionManifestService(api);
        _chunkService = new DimensionChunkService(api);
        _materializer = new ChunkColumnMaterializer(api);
        _debugPlatformBuilder = new DebugDimensionPlatformBuilder(api);
        _lightFloorApplier = new ChunkLightFloorApplier(api);
        _generatedWindowPreparer = new GeneratedDimensionWindowPreparer(api, _chunkService, _materializer, _lightFloorApplier, _preparedDimensions);
        _generatedStreamer = new GeneratedDimensionStreamer(api, _dimensions, _generators, _generatedWindowPreparer, FallbackLazyGeneratedChunkRadius, InitialGeneratedChunkRadius, LazyGeneratedChunkBudgetPerTick);
        _policyProviders = new PolicyProviderRegistry();
        _accessService = new DimensionAccessService(_policyProviders, IsDimensionOrphaned);
        _blockProtectionAdapter = new BlockInteractionProtectionAdapter(_accessService, ResolveDimensionForSelection);
        _returnPositions = new ReturnPositionStore(api.Logger);
        _transferService = new DimensionTransferService(serverChannel);
        _visualTuningBroadcaster = new VisualTuningBroadcaster(api, serverChannel);
        _temporalStabilityGuard = new TemporalStabilityGuard(api, IsInsideDimensionLibDimension);
        _diagnostics = new DimensionDiagnosticService(api, _generators, _preparedDimensions);
        RegisterBuiltInGenerators();
        LoadPersistedDimensions();
        RegisterDimension(DebugDimensionSpec());
    }

    public IReadOnlyCollection<Dimension> Dimensions => _dimensions.Dimensions;

    public IReadOnlyCollection<string> GeneratorIds => _generators.GeneratorIds;

    public Dimension DebugDimension => _dimensions.GetRequired(DebugDimensionId);

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _api.Event.GameWorldSave += SaveManifest;
        _api.Event.CanPlaceOrBreakBlock += _blockProtectionAdapter.OnCanPlaceOrBreakBlock;
        _api.Event.CanUseBlock += _blockProtectionAdapter.OnCanUseBlock;
        _api.Event.BreakBlock += _blockProtectionAdapter.OnBreakBlock;
        _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        _temporalStabilityListenerId = _api.Event.RegisterGameTickListener(OnTemporalStabilityTick, TemporalStabilityTickMs);
        _lazyGenerationListenerId = _api.Event.RegisterGameTickListener(OnLazyGenerationTick, 1000);
        _started = true;
    }

    public void Dispose()
    {
        if (_started)
        {
            _api.Event.GameWorldSave -= SaveManifest;
            _api.Event.CanPlaceOrBreakBlock -= _blockProtectionAdapter.OnCanPlaceOrBreakBlock;
            _api.Event.CanUseBlock -= _blockProtectionAdapter.OnCanUseBlock;
            _api.Event.BreakBlock -= _blockProtectionAdapter.OnBreakBlock;
            _api.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            if (_temporalStabilityListenerId != 0)
            {
                _api.Event.UnregisterGameTickListener(_temporalStabilityListenerId);
                _temporalStabilityListenerId = 0;
            }

            if (_lazyGenerationListenerId != 0)
            {
                _api.Event.UnregisterGameTickListener(_lazyGenerationListenerId);
                _lazyGenerationListenerId = 0;
            }

            _started = false;
        }

        SaveManifest();
    }

    public DimensionLibResult<Dimension> RegisterDimension(DimensionSpec spec)
    {
        var validation = DimensionSpecValidator.Validate(spec, DimensionLibModSystem.FirstPrototypeDimension);
        if (!validation.Success)
        {
            return DimensionLibResult<Dimension>.Fail(validation.Message, validation.ErrorCode);
        }

        if (!_dimensions.TryGet(spec.DimensionId, out _) && spec.Placement == DimensionPlacement.AutomaticSparse && !DimensionRegionAllocator.TryAssignSparseRegion(spec, _dimensions.Values))
        {
            return DimensionLibResult<Dimension>.Fail("No free sparse DimensionLib region was found.", "no-free-region");
        }

        var result = _dimensions.Register(spec);
        if (result.Success)
        {
            SaveManifest();
        }

        return result;
    }

    public DimensionLibResult<Dimension> GetDimension(string dimensionId)
    {
        return _dimensions.Get(dimensionId);
    }

    public DimensionLibResult<Dimension> GetDimensionAt(BlockPos pos)
    {
        return _dimensions.GetAt(pos);
    }

    public bool IsDimensionPrepared(string dimensionId)
    {
        return _preparedDimensions.IsDimensionPrepared(dimensionId);
    }

    public bool IsDimensionOrphaned(string dimensionId)
    {
        return _dimensions.IsOrphaned(dimensionId);
    }

    public DimensionLibResult RegisterPolicyProvider(string ownerModId, IDimensionPolicyProvider provider)
    {
        return _policyProviders.Register(ownerModId, provider);
    }

    public DimensionLibResult RegisterGenerator(IDimensionGenerator generator)
    {
        return _generators.Register(generator);
    }

    public DimensionLibResult PrepareDimension(string dimensionId, IBlockVolumeSource source = null, IServerPlayer sendToPlayer = null, CancellationToken token = default)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        if (source != null)
        {
            var sourceValidation = BlockVolumeSourceValidator.ValidateBounds(lookup.Value, source);
            if (!sourceValidation.Success)
            {
                return sourceValidation;
            }
        }

        try
        {
            _chunkService.CreateChunkColumns(lookup.Value);
            if (source != null)
            {
                _materializer.Materialize(lookup.Value, source, token);
            }

            _chunkService.Relight(lookup.Value);
            _preparedDimensions.MarkAllChunksPrepared(lookup.Value);
            _preparedDimensions.MarkDimensionPrepared(lookup.Value.DimensionId);
            if (sendToPlayer != null)
            {
                ForceSend(lookup.Value, sendToPlayer);
            }
        }
        catch (OperationCanceledException)
        {
            return DimensionLibResult.Fail($"Preparing dimension '{lookup.Value.DimensionId}' was canceled.", "prepare-canceled");
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[DimensionLib] Failed to prepare dimension '{0}': {1}", lookup.Value.DimensionId, ex.Message);
            return DimensionLibResult.Fail($"Failed to prepare dimension '{lookup.Value.DimensionId}'.", "prepare-failed");
        }

        return DimensionLibResult.Ok($"Prepared dimension '{lookup.Value.DimensionId}'.");
    }

    public DimensionLibResult PrepareGeneratedDimension(string dimensionId, IServerPlayer sendToPlayer = null, CancellationToken token = default)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        if (string.IsNullOrWhiteSpace(dimension.GeneratorId))
        {
            return DimensionLibResult.Fail($"Dimension '{dimension.DimensionId}' has no generator id.", "missing-dimension-generator");
        }

        if (!_generators.TryGet(dimension.GeneratorId, out var generator))
        {
            return DimensionLibResult.Fail($"Generator '{dimension.GeneratorId}' is not registered.", "unknown-generator");
        }

        return PrepareDimension(dimension.DimensionId, generator.CreateSource(dimension), sendToPlayer, token);
    }

    public DimensionLibResult CreateTestDimension(string testId, string dimensionId = null, int? sizeChunks = null, long? seed = null, IServerPlayer player = null, CancellationToken token = default)
    {
        if (!TryCreateTestDimensionSpec(testId, dimensionId, sizeChunks, seed, out var spec, out var normalizedTestId))
        {
            return DimensionLibResult.Fail("Test dimension must be 'overworld-opposite' or 'nether-cavern'.", "unknown-test-dimension");
        }

        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            if (_dimensions.TryGet(spec.DimensionId, out var existing))
            {
                spec.ChunkX = existing.ChunkX;
                spec.ChunkZ = existing.ChunkZ;
                if (!sizeChunks.HasValue)
                {
                    spec.ChunkSizeX = existing.ChunkSizeX;
                    spec.ChunkSizeZ = existing.ChunkSizeZ;
                }
            }
        }
        else if (_dimensions.TryGet(spec.DimensionId, out var existing) && !DimensionSpecValidator.SameClaim(existing, spec) && IsBuiltInTestDimension(existing, normalizedTestId))
        {
            _dimensions.Remove(existing.DimensionId);
            _manifestService.Remove(existing.DimensionId);
            _preparedDimensions.RemoveDimension(existing.DimensionId);
        }

        var registered = RegisterDimension(spec);
        if (!registered.Success)
        {
            return DimensionLibResult.Fail(registered.Message, registered.ErrorCode);
        }

        var prepared = PrepareGeneratedDimensionWindow(spec.DimensionId, InitialGeneratedChunkRadius, player, token, InitialGeneratedChunkBudget);
        if (!prepared.Success)
        {
            return prepared;
        }

        return DimensionLibResult.Ok($"Created test dimension '{normalizedTestId}' as '{spec.DimensionId}' with lazy chunk generation. Use /dlib enter {spec.DimensionId}.");
    }

    public DimensionLibResult<string> ValidateDimension(string dimensionId)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult<string>.Fail(lookup.Message, lookup.ErrorCode);
        }

        return _diagnostics.Validate(lookup.Value, IsDimensionOrphaned(lookup.Value.DimensionId));
    }

    public DimensionLibResult EnterDimension(IServerPlayer player, string dimensionId)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        if (!string.IsNullOrWhiteSpace(lookup.Value.GeneratorId))
        {
            var prepared = PrepareGeneratedDimensionWindow(lookup.Value.DimensionId, _generatedWindowPreparer.GetAllowedChunkRadius(player, FallbackLazyGeneratedChunkRadius, InitialGeneratedChunkRadius), player, default, InitialGeneratedChunkBudget);
            if (!prepared.Success)
            {
                return prepared;
            }
        }

        return TeleportToDimension(player, lookup.Value.DimensionId);
    }

    public DimensionLibResult PrepareDebugDimension(IServerPlayer player = null)
    {
        var dimension = DebugDimension;
        _chunkService.CreateChunkColumns(dimension);
        _debugPlatformBuilder.Fill(dimension);
        _debugPlatformBuilder.LogSample(dimension);
        _chunkService.Relight(dimension);
        _preparedDimensions.MarkAllChunksPrepared(dimension);
        _preparedDimensions.MarkDimensionPrepared(dimension.DimensionId);
        if (player != null)
        {
            ForceSend(dimension, player);
        }

        return DimensionLibResult.Ok("Prepared DimensionLib debug dimension.");
    }

    public DimensionLibResult ForceSendDimension(string dimensionId, IServerPlayer player)
    {
        if (player == null)
        {
            return DimensionLibResult.Fail("Player is required.", "missing-player");
        }

        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        if (!_accessService.CanEnter(player, lookup.Value, out var reason))
        {
            return DimensionLibResult.Fail(reason, "access-denied");
        }

        if (!IsDimensionPrepared(lookup.Value.DimensionId))
        {
            return DimensionLibResult.Fail($"Dimension '{lookup.Value.DimensionId}' has not been prepared yet.", "dimension-not-prepared");
        }

        ForceSend(lookup.Value, player);
        return DimensionLibResult.Ok($"Sent dimension '{lookup.Value.DimensionId}' to {player.PlayerName}.");
    }

    public DimensionLibResult SendVisualTuning(IServerPlayer player, string raw)
    {
        return _visualTuningBroadcaster.Send(player, raw);
    }

    public DimensionLibResult<DimensionLocation> CaptureLocation(IServerPlayer player)
    {
        if (player?.Entity?.Pos == null)
        {
            return DimensionLibResult<DimensionLocation>.Fail("Online player is required.", "missing-player");
        }

        var pos = player.Entity.Pos;
        var dimensionId = TryGetDimensionAtPosition(pos.AsBlockPos, out var dimension) ? dimension.DimensionId : null;
        return DimensionLibResult<DimensionLocation>.Ok(DimensionLocation.From(pos, dimensionId));
    }

    public DimensionLibResult ApplyAmbientLightFloor(string dimensionId, int level)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        level = ClampInt(level, 0, 31);
        var chunks = _preparedDimensions.GetPreparedLocalChunks(dimension).ToArray();
        if (chunks.Length == 0)
        {
            return DimensionLibResult.Fail($"Dimension '{dimension.DimensionId}' has no prepared chunks.", "dimension-not-prepared");
        }

        var updatedLights = _lightFloorApplier.ApplyBlocklightFloor(dimension, level, chunks, DimensionLightPolicy.For(dimension));
        var sentPlayers = 0;
        foreach (var player in _api.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            if (player?.Entity?.Pos == null || player.Entity.Pos.Dimension != dimension.DimensionPlaneId)
            {
                continue;
            }

            _chunkService.ForceSendLocalChunkColumns(dimension, player, chunks);
            sentPlayers++;
        }

        return DimensionLibResult.Ok($"Applied ambient light floor {level} to {updatedLights} air light cell(s) in {chunks.Length} chunk column(s) for '{dimension.DimensionId}' and resent to {sentPlayers} player(s).");
    }

    public DimensionLibResult TeleportToDimension(IServerPlayer player, string dimensionId, DimensionTeleportOptions options = null)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        if (!_accessService.CanEnter(player, dimension, out var reason))
        {
            return DimensionLibResult.Fail(reason, "access-denied");
        }

        if (!IsDimensionPrepared(dimension.DimensionId))
        {
            return DimensionLibResult.Fail($"Dimension '{dimension.DimensionId}' has not been prepared yet.", "dimension-not-prepared");
        }

        options ??= new DimensionTeleportOptions();
        if (options.ForceSendDimension)
        {
            ForceSend(dimension, player);
        }

        if (options.RecordReturn && _returnPositions.ShouldRecord(player, IsInsideDimensionLibDimension))
        {
            _returnPositions.Record(player);
        }

        _transferService.MovePlayer(
            player,
            dimension.DimensionPlaneId,
            options.X ?? dimension.SpawnX,
            options.Y ?? dimension.SpawnY,
            options.Z ?? dimension.SpawnZ,
            options.Yaw ?? player.Entity.Pos.Yaw,
            options.Pitch ?? player.Entity.Pos.Pitch,
            options.Roll ?? player.Entity.Pos.Roll);
        _transferService.SyncClientTransfer(
            player,
            dimension.DimensionPlaneId,
            options.X ?? dimension.SpawnX,
            options.Y ?? dimension.SpawnY,
            options.Z ?? dimension.SpawnZ,
            options.Yaw ?? player.Entity.Pos.Yaw,
            options.Pitch ?? player.Entity.Pos.Pitch,
            options.Roll ?? player.Entity.Pos.Roll,
            dimension);

        return DimensionLibResult.Ok($"Teleported {player.PlayerName} to dimension '{dimension.DimensionId}'.");
    }

    public DimensionLibResult TeleportToLocation(IServerPlayer player, DimensionLocation location)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (location == null)
        {
            return DimensionLibResult.Fail("Location is required.", "missing-location");
        }

        var visibleDimension = ResolveVisibleDimension(location);
        if (!string.IsNullOrWhiteSpace(location.DimensionId) && visibleDimension == null)
        {
            return DimensionLibResult.Fail($"Location dimension '{location.DimensionId}' is not registered.", "unknown-location-dimension");
        }

        if (visibleDimension != null)
        {
            if (IsDimensionOrphaned(visibleDimension.DimensionId))
            {
                return DimensionLibResult.Fail($"Dimension '{visibleDimension.DimensionId}' is orphaned.", "dimension-orphaned");
            }

            if (location.DimensionPlaneId != visibleDimension.DimensionPlaneId || !visibleDimension.ContainsBlock(location.AsBlockPos()))
            {
                return DimensionLibResult.Fail($"Location does not belong to dimension '{visibleDimension.DimensionId}'.", "location-dimension-mismatch");
            }

            if (!_accessService.CanEnter(player, visibleDimension, out var reason))
            {
                return DimensionLibResult.Fail(reason, "access-denied");
            }

            if (!IsDimensionPrepared(visibleDimension.DimensionId))
            {
                return DimensionLibResult.Fail($"Dimension '{visibleDimension.DimensionId}' has not been prepared yet.", "dimension-not-prepared");
            }

            ForceSend(visibleDimension, player);
        }

        _transferService.MovePlayer(player, location.DimensionPlaneId, location.X, location.Y, location.Z, location.Yaw, location.Pitch, location.Roll);
        _transferService.SyncClientTransfer(player, location.DimensionPlaneId, location.X, location.Y, location.Z, location.Yaw, location.Pitch, location.Roll, visibleDimension);
        return DimensionLibResult.Ok($"Teleported {player.PlayerName} to saved location.");
    }

    public DimensionLibResult ReturnPlayer(IServerPlayer player)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        if (!_returnPositions.TryGet(player, out var returnPos))
        {
            return DimensionLibResult.Fail("No recorded DimensionLib return point for this session.", "missing-return-point");
        }

        _returnPositions.Clear(player);
        _transferService.MovePlayer(player, returnPos.Dimension, returnPos.X, returnPos.Y, returnPos.Z, returnPos.Yaw, returnPos.Pitch, returnPos.Roll);
        _transferService.SyncClientTransfer(player, returnPos.Dimension, returnPos.X, returnPos.Y, returnPos.Z, returnPos.Yaw, returnPos.Pitch, returnPos.Roll, visibleDimension: null);
        return DimensionLibResult.Ok($"Returned {player.PlayerName} to the recorded origin.");
    }

    public DimensionLibResult ReleaseDimension(string dimensionId, DimensionReleaseMode mode = DimensionReleaseMode.ForgetOnly)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        if (mode == DimensionReleaseMode.MarkOrphaned)
        {
            _dimensions.MarkOrphaned(dimension.DimensionId);
            _preparedDimensions.UnmarkDimension(dimension.DimensionId);
            SaveManifest();
            return DimensionLibResult.Ok($"Marked dimension '{dimension.DimensionId}' orphaned.");
        }

        if (mode == DimensionReleaseMode.ClearBlocksAndForget)
        {
            _chunkService.ClearBlocks(dimension);
        }

        _dimensions.Remove(dimension.DimensionId);
        _manifestService.Remove(dimension.DimensionId);
        _preparedDimensions.RemoveDimension(dimension.DimensionId);
        SaveManifest();
        return DimensionLibResult.Ok($"Released dimension '{dimension.DimensionId}' with mode {mode}.");
    }

    private DimensionLibResult PrepareGeneratedDimensionWindow(string dimensionId, int radiusChunks, IServerPlayer sendToPlayer = null, CancellationToken token = default, int maxColumns = int.MaxValue)
    {
        var lookup = GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        if (string.IsNullOrWhiteSpace(dimension.GeneratorId))
        {
            return DimensionLibResult.Fail($"Dimension '{dimension.DimensionId}' has no generator id.", "missing-dimension-generator");
        }

        if (!_generators.TryGet(dimension.GeneratorId, out var generator))
        {
            return DimensionLibResult.Fail($"Generator '{dimension.GeneratorId}' is not registered.", "unknown-generator");
        }

        var source = generator.CreateSource(dimension);
        var sourceValidation = BlockVolumeSourceValidator.ValidateBounds(dimension, source);
        if (!sourceValidation.Success)
        {
            return sourceValidation;
        }

        var center = _generatedWindowPreparer.ResolveCenterLocalChunk(dimension, sendToPlayer);
        return _generatedWindowPreparer.PrepareWindow(dimension, source, center.X, center.Y, radiusChunks, sendToPlayer, token, maxColumns);
    }

    public DimensionLibResult EnterDebugDimension(IServerPlayer player)
    {
        var prepare = PrepareDebugDimension(player);
        return prepare.Success ? TeleportToDimension(player, DebugDimensionId) : prepare;
    }

    private void ForceSend(Dimension dimension, IServerPlayer player)
    {
        if (_preparedDimensions.TryGetPartialPreparedLocalChunks(dimension, out var preparedChunks))
        {
            _chunkService.ForceSendLocalChunkColumns(dimension, player, preparedChunks);
            return;
        }

        _chunkService.ForceSendAllChunkColumns(dimension, player);
    }

    private void OnTemporalStabilityTick(float dt)
    {
        _temporalStabilityGuard.Tick(dt);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        if (player?.Entity?.Pos == null || !TryGetDimensionAtPosition(player.Entity.Pos.AsBlockPos, out var dimension))
        {
            return;
        }

        LoadPreparedChunkColumns(dimension);
        ForceSend(dimension, player);
        _transferService.SyncClientTransfer(player, dimension.DimensionPlaneId, player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z, player.Entity.Pos.Yaw, player.Entity.Pos.Pitch, player.Entity.Pos.Roll, dimension);
    }

    private void OnLazyGenerationTick(float dt)
    {
        _generatedStreamer.Tick(dt);
    }

    private Dimension ResolveDimensionForSelection(BlockSelection blockSel)
    {
        if (blockSel?.Position == null)
        {
            return null;
        }

        var lookup = GetDimensionAt(blockSel.Position);
        return lookup.Success ? lookup.Value : null;
    }

    private Dimension ResolveVisibleDimension(DimensionLocation location)
    {
        if (!string.IsNullOrWhiteSpace(location.DimensionId))
        {
            var lookup = GetDimension(location.DimensionId);
            return lookup.Success ? lookup.Value : null;
        }

        return TryGetDimensionAtPosition(location.AsBlockPos(), out var dimension) ? dimension : null;
    }

    private bool IsInsideDimensionLibDimension(BlockPos pos)
    {
        return TryGetDimensionAtPosition(pos, out _);
    }

    private bool TryGetDimensionAtPosition(BlockPos pos, out Dimension dimension)
    {
        dimension = null;
        if (pos == null)
        {
            return false;
        }

        return _dimensions.TryGetAt(pos, out dimension);
    }

    private void LoadPreparedChunkColumns(Dimension dimension)
    {
        if (_preparedDimensions.TryGetPartialPreparedLocalChunks(dimension, out var preparedChunks))
        {
            _chunkService.LoadLocalChunkColumns(dimension, preparedChunks);
            return;
        }

        _chunkService.LoadAllChunkColumns(dimension);
    }

    private void LoadPersistedDimensions()
    {
        foreach (var entry in _manifestService.LoadEntries())
        {
            var dimension = entry.ToDimension();
            _dimensions.Load(dimension, entry.IsOrphaned || dimension.IsTransient);

            if (entry.PreparedChunkKeys?.Count > 0)
            {
                _preparedDimensions.LoadPreparedChunks(dimension.DimensionId, entry.PreparedChunkKeys);
            }
        }
    }

    private void SaveManifest()
    {
        _manifestService.Save(_dimensions.Values, IsDimensionOrphaned, _preparedDimensions.GetPreparedChunkKeys);
    }

    private static DimensionSpec DebugDimensionSpec()
    {
        return BuiltInTestDimensionFactory.DebugDimensionSpec();
    }

    private static bool IsBuiltInTestDimension(Dimension dimension, string normalizedTestId)
    {
        return BuiltInTestDimensionFactory.IsBuiltInTestDimension(dimension, normalizedTestId);
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private void RegisterBuiltInGenerators()
    {
        RegisterGenerator(new OverworldOppositeDimensionGenerator(_api));
        RegisterGenerator(new NetherCavernDimensionGenerator(_api));
    }

    private static bool TryCreateTestDimensionSpec(string testId, string dimensionId, int? sizeChunks, long? seed, out DimensionSpec spec, out string normalizedTestId)
    {
        return BuiltInTestDimensionFactory.TryCreateTestDimensionSpec(testId, dimensionId, sizeChunks, seed, out spec, out normalizedTestId);
    }
}
