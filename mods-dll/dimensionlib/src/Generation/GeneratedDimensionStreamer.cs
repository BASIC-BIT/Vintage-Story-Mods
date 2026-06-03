using System.Linq;
using DimensionLib.Api;
using DimensionLib.Services;
using Vintagestory.API.Server;

namespace DimensionLib.Generation;

internal sealed class GeneratedDimensionStreamer
{
    private readonly ICoreServerAPI _api;
    private readonly DimensionRegistry _dimensions;
    private readonly DimensionGeneratorRegistry _generators;
    private readonly GeneratedDimensionWindowPreparer _windowPreparer;
    private readonly int _fallbackRadius;
    private readonly int _minimumRadius;
    private readonly int _budgetPerTick;

    public GeneratedDimensionStreamer(
        ICoreServerAPI api,
        DimensionRegistry dimensions,
        DimensionGeneratorRegistry generators,
        GeneratedDimensionWindowPreparer windowPreparer,
        int fallbackRadius,
        int minimumRadius,
        int budgetPerTick)
    {
        _api = api;
        _dimensions = dimensions;
        _generators = generators;
        _windowPreparer = windowPreparer;
        _fallbackRadius = fallbackRadius;
        _minimumRadius = minimumRadius;
        _budgetPerTick = budgetPerTick;
    }

    public void Tick(float dt)
    {
        foreach (var player in _api.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            var entity = player.Entity;
            if (entity?.Pos == null || !_dimensions.TryGetAt(entity.Pos.AsBlockPos, out var dimension) || string.IsNullOrWhiteSpace(dimension.GeneratorId))
            {
                continue;
            }

            var localChunk = _windowPreparer.ResolveLocalChunk(dimension, entity.Pos.X, entity.Pos.Z);
            var radius = _windowPreparer.GetAllowedChunkRadius(player, _fallbackRadius, _minimumRadius);
            var result = IsStandardOverworldSourceDimension(dimension)
                ? _windowPreparer.PrepareStandardOverworldSourceWindow(dimension, localChunk.X, localChunk.Y, radius, player, default, _budgetPerTick)
                : PrepareGeneratedWindow(dimension, localChunk.X, localChunk.Y, radius, player);
            if (!result.Success)
            {
                _api.Logger.Warning("[DimensionLib] Lazy generation tick failed for '{0}': {1}", dimension.DimensionId, result.Message);
            }
        }
    }

    private DimensionLibResult PrepareGeneratedWindow(DimensionLib.Api.Dimension dimension, int localChunkX, int localChunkZ, int radius, IServerPlayer player)
    {
        if (!_generators.TryGet(dimension.GeneratorId, out var generator))
        {
            return DimensionLibResult.Fail($"Generator '{dimension.GeneratorId}' is not registered.", "unknown-generator");
        }

        return _windowPreparer.PrepareWindow(dimension, generator.CreateSource(dimension), localChunkX, localChunkZ, radius, player, default, _budgetPerTick);
    }

    private static bool IsStandardOverworldSourceDimension(DimensionLib.Api.Dimension dimension)
    {
        return string.Equals(dimension.GeneratorId, DimensionGeneratorIds.StandardOverworldWindow, System.StringComparison.Ordinal);
    }
}
