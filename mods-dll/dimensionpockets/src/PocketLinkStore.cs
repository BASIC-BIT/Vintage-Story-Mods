using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DimensionLib.Api;
using Vintagestory.API.Server;

namespace PocketDimensions;

internal sealed class PocketLinkStore
{
    private const string FileName = "waystone-links.json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ICoreServerAPI _api;
    private readonly string _path;

    public PocketLinkStore(ICoreServerAPI api)
    {
        _api = api;
        _path = Path.Combine(api.GetOrCreateDataPath("ModData"), "pocketdimensions", FileName);
    }

    public PocketLinkState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PocketLinkState();
            }

            var json = File.ReadAllText(_path);
            return Normalize(JsonSerializer.Deserialize<PocketLinkState>(json, JsonOptions));
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[PocketDimensions] Failed to load Waystone links '{0}': {1}", _path, ex.Message);
            return new PocketLinkState();
        }
    }

    public void Save(PocketLinkState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tempPath = _path + ".tmp";
            var json = JsonSerializer.Serialize(Normalize(state), JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _api.Logger.Warning("[PocketDimensions] Failed to save Waystone links '{0}': {1}", _path, ex.Message);
        }
    }

    private static PocketLinkState Normalize(PocketLinkState state)
    {
        var normalized = new PocketLinkState();
        if (state == null)
        {
            return normalized;
        }

        normalized.Links = (state.Links ?? new List<PocketWaystoneLink>())
            .Where(link => link != null && !string.IsNullOrWhiteSpace(link.EndpointId) && !string.IsNullOrWhiteSpace(link.PocketDimensionId))
            .GroupBy(link => link.EndpointId.Trim(), StringComparer.Ordinal)
            .Select(group => group.Last().Normalize())
            .OrderBy(link => link.EndpointId, StringComparer.Ordinal)
            .ToList();

        normalized.ActiveIngressByPlayer = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (state.ActiveIngressByPlayer != null)
        {
            foreach (var playerEntry in state.ActiveIngressByPlayer)
            {
                if (string.IsNullOrWhiteSpace(playerEntry.Key) || playerEntry.Value == null)
                {
                    continue;
                }

                var pocketEntries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pocketEntry in playerEntry.Value)
                {
                    if (!string.IsNullOrWhiteSpace(pocketEntry.Key) && !string.IsNullOrWhiteSpace(pocketEntry.Value))
                    {
                        pocketEntries[pocketEntry.Key.Trim()] = pocketEntry.Value.Trim();
                    }
                }

                if (pocketEntries.Count > 0)
                {
                    normalized.ActiveIngressByPlayer[playerEntry.Key.Trim()] = pocketEntries;
                }
            }
        }

        normalized.UnanchoredReturnsByPlayer = new Dictionary<string, Dictionary<string, DimensionLocation>>(StringComparer.Ordinal);
        if (state.UnanchoredReturnsByPlayer != null)
        {
            foreach (var playerEntry in state.UnanchoredReturnsByPlayer)
            {
                if (string.IsNullOrWhiteSpace(playerEntry.Key) || playerEntry.Value == null)
                {
                    continue;
                }

                var pocketEntries = new Dictionary<string, DimensionLocation>(StringComparer.Ordinal);
                foreach (var pocketEntry in playerEntry.Value)
                {
                    var location = NormalizeLocation(pocketEntry.Value);
                    if (!string.IsNullOrWhiteSpace(pocketEntry.Key) && location != null)
                    {
                        pocketEntries[pocketEntry.Key.Trim()] = location;
                    }
                }

                if (pocketEntries.Count > 0)
                {
                    normalized.UnanchoredReturnsByPlayer[playerEntry.Key.Trim()] = pocketEntries;
                }
            }
        }

        normalized.LayerStacks = (state.LayerStacks ?? new List<PocketLayerStack>())
            .Where(stack => stack != null && !string.IsNullOrWhiteSpace(stack.StackId))
            .GroupBy(stack => stack.StackId.Trim(), StringComparer.Ordinal)
            .Select(group => group.Last().Normalize())
            .Where(stack => stack.Layers.Count > 0)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToList();

        return normalized;
    }

    private static DimensionLocation NormalizeLocation(DimensionLocation location)
    {
        if (location == null || !IsFinite(location.X) || !IsFinite(location.Y) || !IsFinite(location.Z))
        {
            return null;
        }

        return new DimensionLocation
        {
            DimensionId = string.IsNullOrWhiteSpace(location.DimensionId) ? null : location.DimensionId.Trim(),
            DimensionPlaneId = location.DimensionPlaneId,
            X = location.X,
            Y = location.Y,
            Z = location.Z,
            Yaw = location.Yaw,
            Pitch = location.Pitch,
            Roll = location.Roll,
        };
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed class PocketLinkState
{
    public List<PocketWaystoneLink> Links { get; set; } = new List<PocketWaystoneLink>();

    public Dictionary<string, Dictionary<string, string>> ActiveIngressByPlayer { get; set; } = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

    public Dictionary<string, Dictionary<string, DimensionLocation>> UnanchoredReturnsByPlayer { get; set; } = new Dictionary<string, Dictionary<string, DimensionLocation>>(StringComparer.Ordinal);

    public List<PocketLayerStack> LayerStacks { get; set; } = new List<PocketLayerStack>();
}

internal sealed class PocketWaystoneLink
{
    public string EndpointId { get; set; }

    public string PocketDimensionId { get; set; }

    public string SourceDimensionId { get; set; }

    public int DimensionPlaneId { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    public string BoundByPlayerUid { get; set; }

    public string BoundByPlayerName { get; set; }

    public PocketWaystoneLink Normalize()
    {
        EndpointId = EndpointId?.Trim();
        PocketDimensionId = PocketDimensionId?.Trim();
        SourceDimensionId = string.IsNullOrWhiteSpace(SourceDimensionId) ? null : SourceDimensionId.Trim();
        BoundByPlayerUid = string.IsNullOrWhiteSpace(BoundByPlayerUid) ? null : BoundByPlayerUid.Trim();
        BoundByPlayerName = string.IsNullOrWhiteSpace(BoundByPlayerName) ? null : BoundByPlayerName.Trim();
        return this;
    }
}

internal sealed class PocketLayerStack
{
    public string StackId { get; set; }

    public string DisplayName { get; set; }

    public string OwnerPlayerUid { get; set; }

    public string OwnerPlayerName { get; set; }

    public List<string> MemberPlayerUids { get; set; } = new List<string>();

    public List<string> MemberPlayerNames { get; set; } = new List<string>();

    public int SizeChunks { get; set; }

    public int SpawnY { get; set; }

    public List<PocketLayerRef> Layers { get; set; } = new List<PocketLayerRef>();

    public PocketLayerStack Normalize()
    {
        StackId = StackId?.Trim();
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? StackId : DisplayName.Trim();
        OwnerPlayerUid = string.IsNullOrWhiteSpace(OwnerPlayerUid) ? null : OwnerPlayerUid.Trim();
        OwnerPlayerName = string.IsNullOrWhiteSpace(OwnerPlayerName) ? null : OwnerPlayerName.Trim();
        MemberPlayerUids = NormalizeStringList(MemberPlayerUids);
        MemberPlayerNames = NormalizeStringList(MemberPlayerNames);
        SizeChunks = Math.Max(1, SizeChunks);
        SpawnY = Math.Max(1, SpawnY);
        Layers = (Layers ?? new List<PocketLayerRef>())
            .Where(layer => layer != null && !string.IsNullOrWhiteSpace(layer.DimensionId))
            .GroupBy(layer => layer.Index)
            .Select(group => group.Last().Normalize())
            .OrderBy(layer => layer.Index)
            .ToList();
        return this;
    }

    private static List<string> NormalizeStringList(List<string> values)
    {
        return (values ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }
}

internal sealed class PocketLayerRef
{
    public int Index { get; set; }

    public string DimensionId { get; set; }

    public string DisplayName { get; set; }

    public string UpMappingId { get; set; }

    public string DownMappingId { get; set; }

    public PocketLayerRef Normalize()
    {
        DimensionId = DimensionId?.Trim();
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim();
        UpMappingId = string.IsNullOrWhiteSpace(UpMappingId) ? null : UpMappingId.Trim();
        DownMappingId = string.IsNullOrWhiteSpace(DownMappingId) ? null : DownMappingId.Trim();
        return this;
    }
}
