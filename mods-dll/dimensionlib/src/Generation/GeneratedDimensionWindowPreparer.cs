using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DimensionLib.Api;
using DimensionLib.Services;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Generation;

internal sealed class GeneratedDimensionWindowPreparer
{
    private readonly ICoreServerAPI _api;
    private readonly DimensionChunkService _chunkService;
    private readonly ChunkColumnMaterializer _materializer;
    private readonly PreparedDimensionTracker _preparedDimensions;

    public GeneratedDimensionWindowPreparer(
        ICoreServerAPI api,
        DimensionChunkService chunkService,
        ChunkColumnMaterializer materializer,
        PreparedDimensionTracker preparedDimensions)
    {
        _api = api;
        _chunkService = chunkService;
        _materializer = materializer;
        _preparedDimensions = preparedDimensions;
    }

    public DimensionLibResult PrepareWindow(Dimension dimension, IBlockVolumeSource source, int centerLocalChunkX, int centerLocalChunkZ, int radiusChunks, IServerPlayer sendToPlayer, CancellationToken token, int maxColumns)
    {
        var sourceValidation = BlockVolumeSourceValidator.ValidateBounds(dimension, source);
        if (!sourceValidation.Success)
        {
            return sourceValidation;
        }

        var candidates = BuildLazyGenerationCandidates(dimension, centerLocalChunkX, centerLocalChunkZ, radiusChunks);
        var newlyPreparedChunks = new List<Vec2i>();
        var prepared = 0;
        try
        {
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                if (prepared >= maxColumns)
                {
                    break;
                }

                if (_preparedDimensions.IsChunkPrepared(dimension.DimensionId, candidate.X, candidate.Y))
                {
                    continue;
                }

                PrepareChunk(dimension, source, candidate.X, candidate.Y, sendToPlayer, token);
                newlyPreparedChunks.Add(candidate);
                prepared++;
            }
        }
        catch (OperationCanceledException)
        {
            return DimensionLibResult.Fail($"Preparing dimension '{dimension.DimensionId}' was canceled.", "prepare-canceled");
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[DimensionLib] Failed to lazily prepare dimension '{0}': {1}", dimension.DimensionId, ex.Message);
            return DimensionLibResult.Fail($"Failed to lazily prepare dimension '{dimension.DimensionId}'.", "prepare-failed");
        }

        if (prepared > 0)
        {
            _chunkService.RelightWindow(dimension, newlyPreparedChunks);
            _api.Logger.Notification(
                "[DimensionLib] Prepared {0} generated chunk column(s) for '{1}' around local chunk {2},{3}.",
                prepared,
                dimension.DimensionId,
                centerLocalChunkX,
                centerLocalChunkZ);

            if (sendToPlayer != null)
            {
                _chunkService.ForceSendLocalChunkColumns(dimension, sendToPlayer, newlyPreparedChunks);
            }
        }

        _preparedDimensions.MarkDimensionPrepared(dimension.DimensionId);
        return DimensionLibResult.Ok($"Prepared {prepared} lazy chunk column(s) for dimension '{dimension.DimensionId}'.");
    }

    public Vec2i ResolveCenterLocalChunk(Dimension dimension, IServerPlayer player)
    {
        if (player?.Entity?.Pos != null && dimension.ContainsBlock(player.Entity.Pos.AsBlockPos))
        {
            return ResolveLocalChunk(dimension, player.Entity.Pos.X, player.Entity.Pos.Z);
        }

        return ResolveLocalChunk(dimension, dimension.SpawnX, dimension.SpawnZ);
    }

    public Vec2i ResolveLocalChunk(Dimension dimension, double x, double z)
    {
        return new Vec2i(
            ClampInt((int)Math.Floor((x - dimension.MinBlockX) / GlobalConstants.ChunkSize), 0, dimension.ChunkSizeX - 1),
            ClampInt((int)Math.Floor((z - dimension.MinBlockZ) / GlobalConstants.ChunkSize), 0, dimension.ChunkSizeZ - 1));
    }

    public int GetAllowedChunkRadius(IServerPlayer player, int fallbackRadius, int minimumRadius)
    {
        var viewDistanceBlocks = player?.WorldData?.LastApprovedViewDistance ?? 0;
        if (viewDistanceBlocks <= 0)
        {
            viewDistanceBlocks = player?.WorldData?.DesiredViewDistance ?? 0;
        }

        if (viewDistanceBlocks <= 0)
        {
            return fallbackRadius;
        }

        var requestedRadius = (int)Math.Ceiling(viewDistanceBlocks / (double)GlobalConstants.ChunkSize);
        var maxRadius = _api.Server?.Config?.MaxChunkRadius ?? requestedRadius;
        if (maxRadius <= 0)
        {
            maxRadius = requestedRadius;
        }

        return Math.Max(minimumRadius, Math.Min(maxRadius, requestedRadius));
    }

    private void PrepareChunk(Dimension dimension, IBlockVolumeSource source, int localChunkX, int localChunkZ, IServerPlayer sendToPlayer, CancellationToken token)
    {
        _chunkService.CreateChunkColumn(dimension, localChunkX, localChunkZ);
        _materializer.MaterializeChunk(dimension, source, localChunkX, localChunkZ, token);
        _preparedDimensions.MarkChunkPrepared(dimension.DimensionId, localChunkX, localChunkZ);
        if (sendToPlayer != null)
        {
            _chunkService.ForceSendLocalChunkColumn(dimension, sendToPlayer, localChunkX, localChunkZ);
        }
    }

    private static Vec2i[] BuildLazyGenerationCandidates(Dimension dimension, int centerLocalChunkX, int centerLocalChunkZ, int radiusChunks)
    {
        var candidates = new List<Vec2i>();
        var minX = Math.Max(0, centerLocalChunkX - radiusChunks);
        var maxX = Math.Min(dimension.ChunkSizeX - 1, centerLocalChunkX + radiusChunks);
        var minZ = Math.Max(0, centerLocalChunkZ - radiusChunks);
        var maxZ = Math.Min(dimension.ChunkSizeZ - 1, centerLocalChunkZ + radiusChunks);
        for (var localChunkX = minX; localChunkX <= maxX; localChunkX++)
        {
            for (var localChunkZ = minZ; localChunkZ <= maxZ; localChunkZ++)
            {
                candidates.Add(new Vec2i(localChunkX, localChunkZ));
            }
        }

        return candidates
            .OrderBy(candidate => (candidate.X - centerLocalChunkX) * (candidate.X - centerLocalChunkX) + (candidate.Y - centerLocalChunkZ) * (candidate.Y - centerLocalChunkZ))
            .ThenBy(candidate => candidate.X)
            .ThenBy(candidate => candidate.Y)
            .ToArray();
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
