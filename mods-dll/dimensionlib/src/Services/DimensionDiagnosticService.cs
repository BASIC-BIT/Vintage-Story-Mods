using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using DimensionLib.Generation;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class DimensionDiagnosticService
{
    private static readonly Cuboidf FeetProbeBox = new Cuboidf(-0.35f, -0.10f, -0.35f, 0.35f, 0.05f, 0.35f);

    private readonly ICoreServerAPI _api;
    private readonly DimensionGeneratorRegistry _generators;
    private readonly PreparedDimensionTracker _preparedDimensions;

    public DimensionDiagnosticService(ICoreServerAPI api, DimensionGeneratorRegistry generators, PreparedDimensionTracker preparedDimensions)
    {
        _api = api;
        _generators = generators;
        _preparedDimensions = preparedDimensions;
    }

    public DimensionLibResult<string> Validate(Dimension dimension, bool isOrphaned, IServerPlayer player = null)
    {
        var verticalChunks = (_api.WorldManager.MapSizeY + GlobalConstants.ChunkSize - 1) / GlobalConstants.ChunkSize;
        var dimensionChunkOffset = dimension.DimensionPlaneId * GlobalConstants.DimensionSizeInChunks;
        var mapChunkSizeX = _api.WorldManager.MapSizeX / GlobalConstants.ChunkSize;
        var mapChunkSizeZ = _api.WorldManager.MapSizeZ / GlobalConstants.ChunkSize;
        var backingWithinWorldBounds = dimension.ChunkX >= 0 && dimension.ChunkZ >= 0 &&
            dimension.ChunkX + dimension.ChunkSizeX <= mapChunkSizeX &&
            dimension.ChunkZ + dimension.ChunkSizeZ <= mapChunkSizeZ;
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
            $"chunkYRange={dimensionChunkOffset}..{dimensionChunkOffset + verticalChunks - 1} verticalChunks={verticalChunks}",
            $"mapChunks={mapChunkSizeX}x{mapChunkSizeZ}",
            $"backingWithinWorldBounds={backingWithinWorldBounds}",
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
            AppendChunkColumnDiagnostics(lines, dimension, player, verticalChunks, dimensionChunkOffset);
            AppendSpawnDiagnostics(lines, dimension, player, verticalChunks, dimensionChunkOffset);
            AppendCallerDiagnostics(lines, dimension, player);
        }

        return DimensionLibResult<string>.Ok(string.Join("\n", lines));
    }

    private void AppendChunkColumnDiagnostics(List<string> lines, Dimension dimension, IServerPlayer player, int verticalChunks, int dimensionChunkOffset)
    {
        var totalColumns = dimension.ChunkSizeX * dimension.ChunkSizeZ;
        var preparedColumns = 0;
        var loadedColumns = 0;
        var receivedColumns = 0;
        var missingLoadedColumns = new List<string>();
        var missingReceivedColumns = new List<string>();

        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                if (_preparedDimensions.IsChunkPrepared(dimension.DimensionId, localChunkX, localChunkZ))
                {
                    preparedColumns++;
                }

                var chunkX = dimension.ChunkX + localChunkX;
                var chunkZ = dimension.ChunkZ + localChunkZ;
                var loadedCount = CountLoadedChunks(chunkX, chunkZ, dimensionChunkOffset, verticalChunks);
                if (loadedCount == verticalChunks)
                {
                    loadedColumns++;
                }
                else
                {
                    missingLoadedColumns.Add($"{localChunkX},{localChunkZ}:{loadedCount}/{verticalChunks}");
                }

                if (player == null)
                {
                    continue;
                }

                var receivedCount = CountReceivedChunks(player, chunkX, chunkZ, dimensionChunkOffset, verticalChunks);
                if (receivedCount == verticalChunks)
                {
                    receivedColumns++;
                }
                else
                {
                    missingReceivedColumns.Add($"{localChunkX},{localChunkZ}:{receivedCount}/{verticalChunks}");
                }
            }
        }

        lines.Add($"chunkColumnsPrepared={preparedColumns}/{totalColumns}");
        lines.Add($"chunkColumnsLoaded={loadedColumns}/{totalColumns}");
        lines.Add($"chunkColumnsMissingLoaded={FormatLimitedList(missingLoadedColumns)}");
        if (player != null)
        {
            lines.Add($"chunkColumnsReceivedByCaller={receivedColumns}/{totalColumns}");
            lines.Add($"chunkColumnsMissingForCaller={FormatLimitedList(missingReceivedColumns)}");
        }
    }

    private void AppendSpawnDiagnostics(List<string> lines, Dimension dimension, IServerPlayer player, int verticalChunks, int dimensionChunkOffset)
    {
        var spawnX = (int)Math.Floor(dimension.SpawnX);
        var spawnZ = (int)Math.Floor(dimension.SpawnZ);
        var localChunkX = LocalChunkForBlock(spawnX, dimension.MinBlockX);
        var localChunkZ = LocalChunkForBlock(spawnZ, dimension.MinBlockZ);
        var chunkX = dimension.ChunkX + localChunkX;
        var chunkZ = dimension.ChunkZ + localChunkZ;
        var floorChunkY = Math.Max(0, (dimension.SpawnY - 1) / GlobalConstants.ChunkSize);
        var floorInternalChunkY = dimensionChunkOffset + floorChunkY;
        var floorChunk = _api.WorldManager.GetChunk(chunkX, floorInternalChunkY, chunkZ);

        lines.Add($"spawnLocalChunk={localChunkX},{localChunkZ} worldChunk={chunkX},{chunkZ} floorChunkY={floorChunkY} internalChunkY={floorInternalChunkY}");
        lines.Add($"spawnFloorChunkLoaded={floorChunk != null} empty={floorChunk?.Empty.ToString() ?? "unknown"}");
        if (player != null)
        {
            lines.Add($"spawnFloorChunkReceivedByCaller={_api.WorldManager.HasChunk(chunkX, floorInternalChunkY, chunkZ, player)}");
        }

        AppendBlockProbe(lines, "spawnFloor", new BlockPos(spawnX, dimension.SpawnY - 1, spawnZ, dimension.DimensionPlaneId));
        AppendBlockProbe(lines, "spawnFeet", new BlockPos(spawnX, dimension.SpawnY, spawnZ, dimension.DimensionPlaneId));
        AppendBlockProbe(lines, "spawnHead", new BlockPos(spawnX, dimension.SpawnY + 1, spawnZ, dimension.DimensionPlaneId));

        var spawnInternal = InternalPosition(dimension.SpawnX, dimension.SpawnY, dimension.SpawnZ, dimension.DimensionPlaneId);
        var spawnLocalY = new Vec3d(dimension.SpawnX, dimension.SpawnY, dimension.SpawnZ);
        lines.Add($"spawnFeetProbeCollidesInternalY={_api.World.CollisionTester.IsColliding(_api.World.BlockAccessor, FeetProbeBox, spawnInternal, alsoCheckTouch: false)}");
        lines.Add($"spawnFeetProbeCollidesLocalY={_api.World.CollisionTester.IsColliding(_api.World.BlockAccessor, FeetProbeBox, spawnLocalY, alsoCheckTouch: false)}");
        lines.Add($"spawnLoadedVerticalChunks={CountLoadedChunks(chunkX, chunkZ, dimensionChunkOffset, verticalChunks)}/{verticalChunks}");
    }

    private void AppendCallerDiagnostics(List<string> lines, Dimension dimension, IServerPlayer player)
    {
        if (player?.Entity?.Pos == null)
        {
            return;
        }

        var pos = player.Entity.Pos;
        lines.Add($"caller={player.PlayerName} dim={pos.Dimension} pos=({pos.X:0.###},{pos.Y:0.###},{pos.Z:0.###}) internalY={pos.InternalY:0.###}");
        lines.Add($"callerInsideDimension={dimension.ContainsBlock(pos.AsBlockPos)}");
        AppendBlockProbe(lines, "callerFloor", new BlockPos((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y) - 1, (int)Math.Floor(pos.Z), pos.Dimension));
        AppendBlockProbe(lines, "callerFeet", new BlockPos((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y), (int)Math.Floor(pos.Z), pos.Dimension));
        lines.Add($"callerFeetProbeCollides={_api.World.CollisionTester.IsColliding(_api.World.BlockAccessor, FeetProbeBox, pos.XYZ, alsoCheckTouch: false)}");
        lines.Add($"callerEntityBoxCollides={_api.World.CollisionTester.IsColliding(_api.World.BlockAccessor, player.Entity.CollisionBox, pos.XYZ, alsoCheckTouch: false)}");
    }

    private void AppendBlockProbe(List<string> lines, string label, BlockPos pos)
    {
        if (pos.Y < 0 || pos.Y >= _api.WorldManager.MapSizeY)
        {
            lines.Add($"{label}=pos({pos.X},{pos.Y},{pos.Z},dim={pos.dimension}) outside-map-y");
            return;
        }

        var defaultBlock = _api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Default);
        var mostSolidBlock = _api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.MostSolid);
        var collisionBoxCount = mostSolidBlock.GetCollisionBoxes(_api.World.BlockAccessor, pos)?.Length ?? 0;
        lines.Add($"{label}=pos({pos.X},{pos.Y},{pos.Z},dim={pos.dimension},internalY={pos.InternalY}) default={FormatBlock(defaultBlock)} mostSolid={FormatBlock(mostSolidBlock)} collisionBoxes={collisionBoxCount}");
    }

    private int CountLoadedChunks(int chunkX, int chunkZ, int dimensionChunkOffset, int verticalChunks)
    {
        var count = 0;
        for (var chunkY = 0; chunkY < verticalChunks; chunkY++)
        {
            if (_api.WorldManager.GetChunk(chunkX, dimensionChunkOffset + chunkY, chunkZ) != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountReceivedChunks(IServerPlayer player, int chunkX, int chunkZ, int dimensionChunkOffset, int verticalChunks)
    {
        var count = 0;
        for (var chunkY = 0; chunkY < verticalChunks; chunkY++)
        {
            if (_api.WorldManager.HasChunk(chunkX, dimensionChunkOffset + chunkY, chunkZ, player))
            {
                count++;
            }
        }

        return count;
    }

    private static int LocalChunkForBlock(int blockCoordinate, int minBlockCoordinate)
    {
        return Math.Max(0, (blockCoordinate - minBlockCoordinate) / GlobalConstants.ChunkSize);
    }

    private static Vec3d InternalPosition(double x, double y, double z, int dimensionPlaneId)
    {
        return new Vec3d(x, y + dimensionPlaneId * BlockPos.DimensionBoundary, z);
    }

    private static string FormatBlock(Block block)
    {
        return block == null ? "null" : $"{block.BlockId}:{block.Code}";
    }

    private static string FormatLimitedList(IReadOnlyCollection<string> items)
    {
        if (items.Count == 0)
        {
            return "none";
        }

        var shown = items.Take(12).ToArray();
        var suffix = items.Count > shown.Length ? $", +{items.Count - shown.Length} more" : string.Empty;
        return string.Join(", ", shown) + suffix;
    }
}
