using System;
using System.IO;
using System.Text.Json;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

public sealed class DimensionRegionStore
{
    private const string FileName = "regions.json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ICoreServerAPI _api;
    private readonly string _manifestPath;

    public DimensionRegionStore(ICoreServerAPI api)
    {
        _api = api;
        _manifestPath = Path.Combine(api.GetOrCreateDataPath("ModData"), "dimensionlib", FileName);
    }

    public DimensionRegionManifest Load()
    {
        try
        {
            if (!File.Exists(_manifestPath))
            {
                return new DimensionRegionManifest();
            }

            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<DimensionRegionManifest>(json, JsonOptions) ?? new DimensionRegionManifest();
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[DimensionLib] Failed to load dimension manifest '{0}': {1}", _manifestPath, ex.Message);
            return new DimensionRegionManifest();
        }
    }

    public void Save(DimensionRegionManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
        var tempPath = _manifestPath + ".tmp";
        var json = JsonSerializer.Serialize(manifest ?? new DimensionRegionManifest(), JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _manifestPath, overwrite: true);
    }
}
