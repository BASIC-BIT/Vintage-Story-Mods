using System;
using System.Collections.Generic;
using DimensionLib.Api;
using DimensionLib.Generation;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class DimensionDiagnosticService
{
    private readonly ICoreServerAPI _api;
    private readonly DimensionGeneratorRegistry _generators;
    private readonly PreparedDimensionTracker _preparedDimensions;

    public DimensionDiagnosticService(ICoreServerAPI api, DimensionGeneratorRegistry generators, PreparedDimensionTracker preparedDimensions)
    {
        _api = api;
        _generators = generators;
        _preparedDimensions = preparedDimensions;
    }

    public DimensionLibResult<string> Validate(Dimension dimension, bool isOrphaned)
    {
        var lines = new List<string>
        {
            $"dimension={dimension.DimensionId}",
            $"owner={dimension.OwnerModId}",
            $"plane={dimension.DimensionPlaneId}",
            $"chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}",
            $"blocks=({dimension.MinBlockX},{dimension.MinBlockZ})..({dimension.MaxBlockX},{dimension.MaxBlockZ})",
            $"spawn=({dimension.SpawnX:0.#},{dimension.SpawnY},{dimension.SpawnZ:0.#})",
            $"generator={dimension.GeneratorId ?? "none"}",
            $"visualSettings={(dimension.VisualSettings == null ? "none" : "explicit")}",
            $"minimumSceneLight={dimension.MinimumSceneLight:0.###}",
            $"seed={dimension.Seed}",
            $"prepared={_preparedDimensions.IsDimensionPrepared(dimension.DimensionId)}",
            $"preparedChunks={_preparedDimensions.GetPreparedChunkCount(dimension)}/{dimension.ChunkSizeX * dimension.ChunkSizeZ}",
            $"orphaned={isOrphaned}",
        };

        if (!string.IsNullOrWhiteSpace(dimension.GeneratorId))
        {
            if (string.Equals(dimension.GeneratorId, DimensionGeneratorIds.StandardOverworldWindow, StringComparison.Ordinal))
            {
                lines.Add("generatorStatus=standard-overworld-source-window");
                lines.Add("source=vanilla-standard-worldgen-peek-materialized");
            }
            else if (!_generators.TryGet(dimension.GeneratorId, out var generator))
            {
                lines.Add($"generatorStatus=missing:{dimension.GeneratorId}");
            }
            else
            {
                var source = generator.CreateSource(dimension);
                var sourceValidation = BlockVolumeSourceValidator.ValidateBounds(dimension, source);
                lines.Add($"generatorStatus={(sourceValidation.Success ? "ok" : sourceValidation.ErrorCode)}");
                lines.Add($"source={source.SourceId} bounds={source.Bounds.SizeX}x{source.Bounds.SizeY}x{source.Bounds.SizeZ}");
            }
        }

        if (_preparedDimensions.IsDimensionPrepared(dimension.DimensionId))
        {
            var spawnX = (int)Math.Round(dimension.SpawnX);
            var spawnZ = (int)Math.Round(dimension.SpawnZ);
            var floorPos = new BlockPos(spawnX, dimension.SpawnY - 1, spawnZ, dimension.DimensionPlaneId);
            var feetPos = new BlockPos(spawnX, dimension.SpawnY, spawnZ, dimension.DimensionPlaneId);
            var headPos = new BlockPos(spawnX, dimension.SpawnY + 1, spawnZ, dimension.DimensionPlaneId);
            lines.Add($"spawnFloorBlockId={_api.World.BlockAccessor.GetBlockId(floorPos)}");
            lines.Add($"spawnFeetBlockId={_api.World.BlockAccessor.GetBlockId(feetPos)}");
            lines.Add($"spawnHeadBlockId={_api.World.BlockAccessor.GetBlockId(headPos)}");
        }

        return DimensionLibResult<string>.Ok(string.Join("\n", lines));
    }
}
