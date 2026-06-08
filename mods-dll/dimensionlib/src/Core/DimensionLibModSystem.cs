using System.Collections.Generic;
using System.Threading;
using DimensionLib.Api;
using DimensionLib.Commands;
using DimensionLib.Network;
using DimensionLib.Services;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Core;

public sealed class DimensionLibModSystem : ModSystem, IDimensionLibApi
{
    public const string ModId = "dimensionlib";
    public const int FirstPrototypeDimension = 3;

    private readonly HashSet<string> _visibleChunkColumns = new HashSet<string>(System.StringComparer.Ordinal);
    private DimensionLibServerService _serverService;
    private DimensionVisualSystem _visualSystem;
    private ICoreClientAPI _clientApi;

    public int PrimaryDimensionPlaneId => FirstPrototypeDimension;

    public IReadOnlyCollection<Dimension> Dimensions => _serverService?.Dimensions ?? System.Array.Empty<Dimension>();

    public IReadOnlyCollection<DimensionMapping> Mappings => _serverService?.Mappings ?? System.Array.Empty<DimensionMapping>();

    public IReadOnlyCollection<string> GeneratorIds => _serverService?.GeneratorIds ?? System.Array.Empty<string>();

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.ObjectCache["dimensionlib:api"] = this;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        var serverChannel = api.Network.RegisterChannel(ModId)
            .RegisterMessageType<DimensionTransferMessage>();

        _serverService = new DimensionLibServerService(api, serverChannel);
        _serverService.Start();
        new DimensionLibCommandRegistrar(api, _serverService).Register();
        api.Logger.Notification("DimensionLib registered root maintenance commands under /dlib.");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        api.Network.RegisterChannel(ModId)
            .RegisterMessageType<DimensionTransferMessage>()
            .SetMessageHandler<DimensionTransferMessage>(OnDimensionTransferMessage);

        _visualSystem = new DimensionVisualSystem(api);
        _visualSystem.Start();
    }

    public override void Dispose()
    {
        _serverService?.Dispose();
        _visualSystem?.Dispose();
        _visibleChunkColumns.Clear();
        base.Dispose();
    }

    public DimensionLibResult<Dimension> RegisterDimension(DimensionSpec spec)
    {
        EnsureServerReady();
        return _serverService.RegisterDimension(spec);
    }

    public DimensionLibResult<Dimension> GetDimension(string dimensionId)
    {
        EnsureServerReady();
        return _serverService.GetDimension(dimensionId);
    }

    public DimensionLibResult<Dimension> GetDimensionAt(BlockPos pos)
    {
        EnsureServerReady();
        return _serverService.GetDimensionAt(pos);
    }

    public DimensionLibResult<DimensionMapping> RegisterMapping(DimensionMappingSpec spec)
    {
        EnsureServerReady();
        return _serverService.RegisterMapping(spec);
    }

    public DimensionLibResult<DimensionMapping> GetMapping(string mappingId)
    {
        EnsureServerReady();
        return _serverService.GetMapping(mappingId);
    }

    public DimensionLibResult<DimensionMappedLocation> ResolveMappedLocation(DimensionLocation sourceLocation, string mappingId, DimensionMappingTeleportOptions options = null)
    {
        EnsureServerReady();
        return _serverService.ResolveMappedLocation(sourceLocation, mappingId, options);
    }

    public DimensionLibResult<DimensionLocalPosition> ResolveLocalPosition(DimensionLocation location)
    {
        EnsureServerReady();
        return _serverService.ResolveLocalPosition(location);
    }

    public bool IsDimensionPrepared(string dimensionId)
    {
        EnsureServerReady();
        return _serverService.IsDimensionPrepared(dimensionId);
    }

    public bool IsDimensionOrphaned(string dimensionId)
    {
        EnsureServerReady();
        return _serverService.IsDimensionOrphaned(dimensionId);
    }

    public DimensionLibResult RegisterPolicyProvider(string ownerModId, IDimensionPolicyProvider provider)
    {
        EnsureServerReady();
        return _serverService.RegisterPolicyProvider(ownerModId, provider);
    }

    public DimensionLibResult RegisterGenerator(IDimensionGenerator generator)
    {
        EnsureServerReady();
        return _serverService.RegisterGenerator(generator);
    }

    public DimensionLibResult PrepareDimension(string dimensionId, IBlockVolumeSource source = null, IServerPlayer sendToPlayer = null, CancellationToken token = default)
    {
        EnsureServerReady();
        return _serverService.PrepareDimension(dimensionId, source, sendToPlayer, token);
    }

    public DimensionLibResult PrepareGeneratedDimension(string dimensionId, IServerPlayer sendToPlayer = null, CancellationToken token = default)
    {
        EnsureServerReady();
        return _serverService.PrepareGeneratedDimension(dimensionId, sendToPlayer, token);
    }

    public DimensionLibResult<string> ValidateDimension(string dimensionId)
    {
        EnsureServerReady();
        return _serverService.ValidateDimension(dimensionId);
    }

    public DimensionLibResult<DimensionLocation> CaptureLocation(IServerPlayer player)
    {
        EnsureServerReady();
        return _serverService.CaptureLocation(player);
    }

    public DimensionLibResult TeleportToDimension(IServerPlayer player, string dimensionId, DimensionTeleportOptions options = null)
    {
        EnsureServerReady();
        return _serverService.TeleportToDimension(player, dimensionId, options);
    }

    public DimensionLibResult TeleportToLocation(IServerPlayer player, DimensionLocation location)
    {
        EnsureServerReady();
        return _serverService.TeleportToLocation(player, location);
    }

    public DimensionLibResult TeleportAcrossMapping(IServerPlayer player, string mappingId, DimensionMappingTeleportOptions options = null)
    {
        EnsureServerReady();
        return _serverService.TeleportAcrossMapping(player, mappingId, options);
    }

    public DimensionLibResult ReturnPlayer(IServerPlayer player)
    {
        EnsureServerReady();
        return _serverService.ReturnPlayer(player);
    }

    public DimensionLibResult ReleaseDimension(string dimensionId, DimensionReleaseMode mode = DimensionReleaseMode.ForgetOnly)
    {
        EnsureServerReady();
        return _serverService.ReleaseDimension(dimensionId, mode);
    }

    private void OnDimensionTransferMessage(DimensionTransferMessage message)
    {
        var entity = _clientApi?.World.Player?.Entity;
        if (entity == null)
        {
            return;
        }

        entity.Pos.SetPos(message.X, message.Y, message.Z);
        entity.Pos.Yaw = message.Yaw;
        entity.Pos.Pitch = message.Pitch;
        entity.Pos.Roll = message.Roll;
        entity.Pos.Motion.Set(0, 0, 0);
        entity.PositionBeforeFalling.Set(message.X, message.Y, message.Z);
        entity.ChangeDimension(message.DimensionPlaneId);
        _visualSystem?.SetActiveVisualSettings(message.DimensionPlaneId, message.DimensionId, message.VisualSettings);

        for (var cx = message.ChunkX; cx < message.ChunkX + message.ChunkSizeX; cx++)
        {
            for (var cz = message.ChunkZ; cz < message.ChunkZ + message.ChunkSizeZ; cz++)
            {
                MarkChunkColumnVisible(cx, cz, message.DimensionPlaneId);
            }
        }
    }

    private void MarkChunkColumnVisible(int chunkX, int chunkZ, int dimensionPlaneId)
    {
        var key = $"{dimensionPlaneId}:{chunkX}:{chunkZ}";
        if (_visibleChunkColumns.Add(key))
        {
            _clientApi.World.SetChunkColumnVisible(chunkX, chunkZ, dimensionPlaneId);
        }
    }

    private void EnsureServerReady()
    {
        if (_serverService == null)
        {
            throw new System.InvalidOperationException("DimensionLib server services are not available on this side.");
        }
    }
}
