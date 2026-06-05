using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DimensionLib.Api;
using DimensionLib.Core;
using DimensionLib.Effects;
using DimensionLib.Generation;
using DimensionLib.Protection;
using DimensionLib.Transfer;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

public sealed class DimensionLibServerService : IDisposable
{
    private const int InitialGeneratedChunkRadius = 1;
    private const int FallbackLazyGeneratedChunkRadius = 2;
    private const int InitialGeneratedChunkBudget = 1;
    private const int LazyGeneratedChunkBudgetPerTick = 1;
    private const int TemporalStabilityTickMs = 250;
    private const int PreparedChunkSendTickMs = 250;
    private const int PreparedChunkSendColumnsPerTick = 2;

    private readonly ICoreServerAPI _api;
    private readonly DimensionManifestService _manifestService;
    private readonly DimensionChunkService _chunkService;
    private readonly ChunkColumnMaterializer _materializer;
    private readonly GeneratedDimensionWindowPreparer _generatedWindowPreparer;
    private readonly GeneratedDimensionStreamer _generatedStreamer;
    private readonly PolicyProviderRegistry _policyProviders;
    private readonly DimensionAccessService _accessService;
    private readonly BlockInteractionProtectionAdapter _blockProtectionAdapter;
    private readonly ReturnPositionStore _returnPositions;
    private readonly DimensionTransferService _transferService;
    private readonly TemporalStabilityGuard _temporalStabilityGuard;
    private readonly PreparedDimensionTracker _preparedDimensions = new PreparedDimensionTracker();
    private readonly DimensionGeneratorRegistry _generators = new DimensionGeneratorRegistry();
    private readonly DimensionMappingRegistry _mappings = new DimensionMappingRegistry();
    private readonly DimensionDiagnosticService _diagnostics;
    private readonly DimensionRegistry _dimensions = new DimensionRegistry(DimensionLibModSystem.FirstPrototypeDimension);
    private readonly Dictionary<string, PreparedChunkSendQueue> _preparedChunkSendQueues = new Dictionary<string, PreparedChunkSendQueue>(StringComparer.Ordinal);
    private long _temporalStabilityListenerId;
    private long _lazyGenerationListenerId;
    private long _preparedChunkSendListenerId;
    private bool _started;

    public DimensionLibServerService(ICoreServerAPI api, IServerNetworkChannel serverChannel)
    {
        _api = api;
        _manifestService = new DimensionManifestService(api);
        _chunkService = new DimensionChunkService(api);
        _materializer = new ChunkColumnMaterializer(api);
        _generatedWindowPreparer = new GeneratedDimensionWindowPreparer(api, _chunkService, _materializer, _preparedDimensions);
        _generatedStreamer = new GeneratedDimensionStreamer(api, _dimensions, _generators, _generatedWindowPreparer, FallbackLazyGeneratedChunkRadius, InitialGeneratedChunkRadius, LazyGeneratedChunkBudgetPerTick);
        _policyProviders = new PolicyProviderRegistry();
        _accessService = new DimensionAccessService(_policyProviders, IsDimensionOrphaned);
        _blockProtectionAdapter = new BlockInteractionProtectionAdapter(_accessService, ResolveDimensionForSelection);
        _returnPositions = new ReturnPositionStore(api.Logger);
        _transferService = new DimensionTransferService(serverChannel);
        _temporalStabilityGuard = new TemporalStabilityGuard(api, IsInsideDimensionLibDimension);
        _diagnostics = new DimensionDiagnosticService(api, _generators, _preparedDimensions);
        LoadPersistedDimensions();
    }

    public IReadOnlyCollection<Dimension> Dimensions => _dimensions.Dimensions;

    public IReadOnlyCollection<DimensionMapping> Mappings => _mappings.Mappings;

    public IReadOnlyCollection<string> GeneratorIds => _generators.GeneratorIds.Concat(new[] { DimensionGeneratorIds.StandardOverworldWindow }).ToArray();

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
        _preparedChunkSendListenerId = _api.Event.RegisterGameTickListener(OnPreparedChunkSendTick, PreparedChunkSendTickMs);
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

            if (_preparedChunkSendListenerId != 0)
            {
                _api.Event.UnregisterGameTickListener(_preparedChunkSendListenerId);
                _preparedChunkSendListenerId = 0;
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

        if (!_dimensions.TryGet(spec.DimensionId, out _) && !DimensionRegionAllocator.TryAssignSparseRegion(spec, _dimensions.Values))
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

    public DimensionLibResult<DimensionMapping> RegisterMapping(DimensionMappingSpec spec)
    {
        var validation = DimensionMappingSpecValidator.Validate(spec);
        if (!validation.Success)
        {
            return DimensionLibResult<DimensionMapping>.Fail(validation.Message, validation.ErrorCode);
        }

        var source = GetDimension(spec.SourceDimensionId);
        if (!source.Success)
        {
            return DimensionLibResult<DimensionMapping>.Fail(source.Message, source.ErrorCode);
        }

        var target = GetDimension(spec.TargetDimensionId);
        if (!target.Success)
        {
            return DimensionLibResult<DimensionMapping>.Fail(target.Message, target.ErrorCode);
        }

        return _mappings.Register(spec);
    }

    public DimensionLibResult<DimensionMapping> GetMapping(string mappingId)
    {
        return _mappings.Get(mappingId);
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
        if (IsStandardOverworldSourceDimension(dimension))
        {
            return PrepareStandardOverworldSourceDimensionWindow(dimension, sendToPlayer, token, InitialGeneratedChunkRadius, InitialGeneratedChunkBudget);
        }

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
            var radius = _generatedWindowPreparer.GetAllowedChunkRadius(player, FallbackLazyGeneratedChunkRadius, InitialGeneratedChunkRadius);
            var prepared = IsStandardOverworldSourceDimension(lookup.Value)
                ? PrepareStandardOverworldSourceDimensionWindow(lookup.Value, player, default, radius, InitialGeneratedChunkBudget)
                : PrepareGeneratedDimensionWindow(lookup.Value.DimensionId, radius, player, default, InitialGeneratedChunkBudget);
            if (!prepared.Success)
            {
                return prepared;
            }

            if (IsStandardOverworldSourceDimension(lookup.Value))
            {
                var center = _generatedWindowPreparer.ResolveCenterLocalChunk(lookup.Value, player);
                if (!_preparedDimensions.IsChunkPrepared(lookup.Value.DimensionId, center.X, center.Y))
                {
                    return DimensionLibResult.Fail($"Standard overworld source chunks for '{lookup.Value.DimensionId}' are still preparing. Retry /dlib enter {lookup.Value.DimensionId} in a few seconds.", "standard-overworld-source-loading");
                }
            }
        }

        return TeleportToDimension(player, lookup.Value.DimensionId);
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
        if (options.ForceSendDimension)
        {
            QueuePreparedChunksNear(dimension, player, options.X ?? dimension.SpawnX, options.Z ?? dimension.SpawnZ);
        }

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
        }

        _transferService.MovePlayer(player, location.DimensionPlaneId, location.X, location.Y, location.Z, location.Yaw, location.Pitch, location.Roll);
        _transferService.SyncClientTransfer(player, location.DimensionPlaneId, location.X, location.Y, location.Z, location.Yaw, location.Pitch, location.Roll, visibleDimension);
        if (visibleDimension != null)
        {
            QueuePreparedChunksNear(visibleDimension, player, location.X, location.Z);
        }

        return DimensionLibResult.Ok($"Teleported {player.PlayerName} to saved location.");
    }

    public DimensionLibResult TeleportAcrossMapping(IServerPlayer player, string mappingId, DimensionMappingTeleportOptions options = null)
    {
        if (player?.Entity == null)
        {
            return DimensionLibResult.Fail("Online player is required.", "missing-player");
        }

        var mappingLookup = GetMapping(mappingId);
        if (!mappingLookup.Success)
        {
            return DimensionLibResult.Fail(mappingLookup.Message, mappingLookup.ErrorCode);
        }

        var mapping = mappingLookup.Value;
        var sourceLookup = GetDimension(mapping.SourceDimensionId);
        if (!sourceLookup.Success)
        {
            return DimensionLibResult.Fail(sourceLookup.Message, sourceLookup.ErrorCode);
        }

        var targetLookup = GetDimension(mapping.TargetDimensionId);
        if (!targetLookup.Success)
        {
            return DimensionLibResult.Fail(targetLookup.Message, targetLookup.ErrorCode);
        }

        options ??= new DimensionMappingTeleportOptions();
        var playerBlockPos = player.Entity.Pos.AsBlockPos;
        var source = sourceLookup.Value;
        var target = targetLookup.Value;
        var startsInSource = source.ContainsBlock(playerBlockPos);
        var startsInTarget = target.ContainsBlock(playerBlockPos);
        if (!startsInSource && !startsInTarget)
        {
            return DimensionLibResult.Fail($"Player is not inside either endpoint for mapping '{mapping.MappingId}'.", "not-in-mapped-dimension");
        }

        if (startsInTarget && !mapping.Bidirectional)
        {
            return DimensionLibResult.Fail($"Mapping '{mapping.MappingId}' is not bidirectional.", "mapping-not-bidirectional");
        }

        var reverse = startsInTarget && !startsInSource;
        var from = reverse ? target : source;
        var to = reverse ? source : target;
        if (IsDimensionOrphaned(to.DimensionId))
        {
            return DimensionLibResult.Fail($"Dimension '{to.DimensionId}' is orphaned.", "dimension-orphaned");
        }

        if (!IsDimensionPrepared(to.DimensionId))
        {
            return DimensionLibResult.Fail($"Dimension '{to.DimensionId}' has not been prepared yet.", "dimension-not-prepared");
        }

        var mapped = DimensionMappingResolver.MapLocalPosition(mapping.Transform, from, to, player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z, reverse, options);
        var destination = new DimensionLocation
        {
            DimensionId = to.DimensionId,
            DimensionPlaneId = to.DimensionPlaneId,
            X = mapped.X,
            Y = mapped.Y,
            Z = mapped.Z,
            Yaw = player.Entity.Pos.Yaw,
            Pitch = player.Entity.Pos.Pitch,
            Roll = player.Entity.Pos.Roll,
        };

        if (!to.ContainsBlock(destination.AsBlockPos()) || destination.Y < 0 || destination.Y >= _api.WorldManager.MapSizeY)
        {
            return DimensionLibResult.Fail($"Mapping '{mapping.MappingId}' resolves outside dimension '{to.DimensionId}'.", "mapped-location-out-of-bounds");
        }

        if (!_accessService.CanEnter(player, to, out var reason))
        {
            return DimensionLibResult.Fail(reason, "access-denied");
        }

        if (options.RequireCollisionFreeDestination && DestinationWouldCollide(player, destination))
        {
            return DimensionLibResult.Fail($"Mapping '{mapping.MappingId}' target is blocked.", "mapped-location-blocked");
        }

        return TeleportToLocation(player, destination);
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
        if (IsStandardOverworldSourceDimension(dimension))
        {
            return PrepareStandardOverworldSourceDimensionWindow(dimension, sendToPlayer, token, radiusChunks, maxColumns);
        }

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

    private DimensionLibResult PrepareStandardOverworldSourceDimensionWindow(Dimension dimension, IServerPlayer sendToPlayer, CancellationToken token, int radiusChunks, int maxColumns)
    {
        var center = _generatedWindowPreparer.ResolveCenterLocalChunk(dimension, sendToPlayer);
        return _generatedWindowPreparer.PrepareStandardOverworldSourceWindow(dimension, center.X, center.Y, radiusChunks, sendToPlayer, token, maxColumns);
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

        QueuePreparedChunksNear(dimension, player, player.Entity.Pos.X, player.Entity.Pos.Z);
        _transferService.SyncClientTransfer(player, dimension.DimensionPlaneId, player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z, player.Entity.Pos.Yaw, player.Entity.Pos.Pitch, player.Entity.Pos.Roll, dimension);
    }

    private void OnLazyGenerationTick(float dt)
    {
        _generatedStreamer.Tick(dt);
    }

    private void OnPreparedChunkSendTick(float dt)
    {
        if (_preparedChunkSendQueues.Count == 0)
        {
            return;
        }

        var keys = _preparedChunkSendQueues.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!_preparedChunkSendQueues.TryGetValue(key, out var queue))
            {
                continue;
            }

            if (!_dimensions.TryGet(queue.DimensionId, out var dimension))
            {
                _preparedChunkSendQueues.Remove(key);
                continue;
            }

            var player = _api.World.AllOnlinePlayers.OfType<IServerPlayer>().FirstOrDefault(candidate => string.Equals(candidate.PlayerUID, queue.PlayerUid, StringComparison.Ordinal));
            if (player?.Entity?.Pos == null || player.Entity.Pos.Dimension != dimension.DimensionPlaneId)
            {
                _preparedChunkSendQueues.Remove(key);
                continue;
            }

            var centerLocalChunk = _generatedWindowPreparer.ResolveLocalChunk(dimension, player.Entity.Pos.X, player.Entity.Pos.Z);
            var chunksToSend = queue.Chunks
                .Where(chunk => !queue.SentChunkKeys.Contains(PreparedChunkSendQueue.ChunkKey(chunk)))
                .OrderBy(chunk => DistanceSquared(chunk, centerLocalChunk))
                .ThenBy(chunk => chunk.X)
                .ThenBy(chunk => chunk.Y)
                .Take(PreparedChunkSendColumnsPerTick)
                .ToArray();
            if (chunksToSend.Length == 0)
            {
                _preparedChunkSendQueues.Remove(key);
                continue;
            }

            foreach (var chunk in chunksToSend)
            {
                _chunkService.LoadLocalChunkColumn(dimension, chunk.X, chunk.Y);
                _chunkService.ForceSendLocalChunkColumn(dimension, player, chunk.X, chunk.Y);
                queue.SentChunkKeys.Add(PreparedChunkSendQueue.ChunkKey(chunk));
            }

            if (queue.SentChunkKeys.Count >= queue.Chunks.Count)
            {
                _preparedChunkSendQueues.Remove(key);
            }
        }
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

    private bool DestinationWouldCollide(IServerPlayer player, DimensionLocation destination)
    {
        var collisionPos = new Vec3d(
            destination.X,
            destination.Y + destination.DimensionPlaneId * GlobalConstants.DimensionSizeInChunks * GlobalConstants.ChunkSize,
            destination.Z);
        return _api.World.CollisionTester.IsColliding(_api.World.BlockAccessor, player.Entity.CollisionBox.Clone(), collisionPos, alsoCheckTouch: false);
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

    private void QueuePreparedChunksNear(Dimension dimension, IServerPlayer player, double centerX, double centerZ)
    {
        if (player?.Entity == null || dimension == null)
        {
            return;
        }

        var centerLocalChunk = _generatedWindowPreparer.ResolveLocalChunk(dimension, centerX, centerZ);
        var sendRadius = _generatedWindowPreparer.GetAllowedChunkRadius(player, FallbackLazyGeneratedChunkRadius, InitialGeneratedChunkRadius);
        var chunks = _preparedDimensions.GetPreparedLocalChunks(dimension)
            .Where(chunk => Math.Abs(chunk.X - centerLocalChunk.X) <= sendRadius && Math.Abs(chunk.Y - centerLocalChunk.Y) <= sendRadius)
            .OrderBy(chunk => DistanceSquared(chunk, centerLocalChunk))
            .ThenBy(chunk => chunk.X)
            .ThenBy(chunk => chunk.Y)
            .ToList();
        if (chunks.Count == 0)
        {
            return;
        }

        player.CurrentChunkSentRadius = 0;
        _preparedChunkSendQueues[PreparedChunkSendQueueKey(player.PlayerUID, dimension.DimensionId)] = new PreparedChunkSendQueue(player.PlayerUID, dimension.DimensionId, chunks);
    }

    private static string PreparedChunkSendQueueKey(string playerUid, string dimensionId)
    {
        return $"{playerUid}:{dimensionId}";
    }

    private static int DistanceSquared(Vec2i chunk, Vec2i centerLocalChunk)
    {
        var dx = chunk.X - centerLocalChunk.X;
        var dz = chunk.Y - centerLocalChunk.Y;
        return dx * dx + dz * dz;
    }

    private sealed class PreparedChunkSendQueue
    {
        public PreparedChunkSendQueue(string playerUid, string dimensionId, List<Vec2i> chunks)
        {
            PlayerUid = playerUid;
            DimensionId = dimensionId;
            Chunks = chunks;
        }

        public string PlayerUid { get; }

        public string DimensionId { get; }

        public List<Vec2i> Chunks { get; }

        public HashSet<long> SentChunkKeys { get; } = new HashSet<long>();

        public static long ChunkKey(Vec2i chunk)
        {
            return ((long)chunk.X << 32) | (uint)chunk.Y;
        }
    }

    private void LoadPersistedDimensions()
    {
        foreach (var entry in _manifestService.LoadEntries())
        {
            var spec = entry.ToSpec();
            var validation = DimensionSpecValidator.Validate(spec, DimensionLibModSystem.FirstPrototypeDimension);
            if (!validation.Success)
            {
                _api.Logger.Warning("[DimensionLib] Skipping persisted dimension '{0}': {1}", entry.DimensionId ?? "<missing>", validation.Message);
                continue;
            }

            var dimension = spec.ToDimension();
            var collision = _dimensions.Values.FirstOrDefault(other => DimensionSpecValidator.RegionsOverlap(other, dimension));
            if (collision != null)
            {
                _api.Logger.Warning("[DimensionLib] Skipping persisted dimension '{0}' because it overlaps '{1}'.", dimension.DimensionId, collision.DimensionId);
                continue;
            }

            _dimensions.Load(dimension, entry.IsOrphaned || dimension.IsTransient);

            if (entry.PreparedChunkKeys?.Count > 0)
            {
                _preparedDimensions.LoadPreparedChunks(dimension, entry.PreparedChunkKeys);
            }
        }
    }

    private void SaveManifest()
    {
        _manifestService.Save(_dimensions.Values, IsDimensionOrphaned, _preparedDimensions.GetPreparedChunkKeys);
    }

    private static bool IsStandardOverworldSourceDimension(Dimension dimension)
    {
        return string.Equals(dimension?.GeneratorId, DimensionGeneratorIds.StandardOverworldWindow, StringComparison.Ordinal);
    }
}
