using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class DimensionManifestService
{
    private readonly ICoreServerAPI _api;
    private readonly DimensionRegionStore _store;
    private readonly Dictionary<string, DimensionRegionManifestEntry> _entriesById = new Dictionary<string, DimensionRegionManifestEntry>(StringComparer.Ordinal);

    public DimensionManifestService(ICoreServerAPI api)
    {
        _api = api;
        _store = new DimensionRegionStore(api);
    }

    public IEnumerable<DimensionRegionManifestEntry> LoadEntries()
    {
        var manifest = _store.Load();
        foreach (var entry in manifest.Dimensions ?? new List<DimensionRegionManifestEntry>())
        {
            if (string.IsNullOrWhiteSpace(entry.DimensionId))
            {
                continue;
            }

            _entriesById[entry.DimensionId] = entry;
            yield return entry;
        }
    }

    public void Remove(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _entriesById.Remove(dimensionId.Trim());
        }
    }

    public void Save(IEnumerable<Dimension> dimensions, Func<string, bool> isDimensionOrphaned, Func<Dimension, List<long>> getPreparedChunkKeys)
    {
        try
        {
            var now = DateTime.UtcNow.ToString("O");
            var manifest = new DimensionRegionManifest();
            foreach (var dimension in dimensions.OrderBy(dimension => dimension.DimensionId, StringComparer.Ordinal))
            {
                _entriesById.TryGetValue(dimension.DimensionId, out var existing);
                var entry = DimensionRegionManifestEntry.FromDimension(dimension, isDimensionOrphaned(dimension.DimensionId), now, existing);
                entry.PreparedChunkKeys = getPreparedChunkKeys(dimension);
                _entriesById[dimension.DimensionId] = entry;
                manifest.Dimensions.Add(entry);
            }

            _store.Save(manifest);
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[DimensionLib] Failed to save dimension manifest: {0}", ex.Message);
        }
    }
}
