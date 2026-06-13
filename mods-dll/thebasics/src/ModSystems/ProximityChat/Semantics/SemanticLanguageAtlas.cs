#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public sealed class SemanticLanguageAtlasDocument
{
    public string AtlasId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public List<SemanticLanguageAtlasBucket> Buckets { get; set; } = new List<SemanticLanguageAtlasBucket>();
}

public sealed class SemanticLanguageAtlasBucket
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new List<string>();

    public List<string> Examples { get; set; } = new List<string>();

    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Id : Label;

    public IEnumerable<string> GetIdentifiers()
    {
        yield return Id;
        yield return Alias;
        yield return Label;
    }
}

public sealed class SemanticLanguageAtlasCatalog
{
    private static readonly AssetLocation[] DefaultAtlasAssets =
    {
        new AssetLocation("thebasics", "config/semantic-atlas/vintagestory-core.generated.json"),
        new AssetLocation("thebasics", "config/semantic-atlas/vintagestory-core.pilot.json"),
        new AssetLocation("thebasics", "semantic-atlas/vintagestory-core.generated.json"),
        new AssetLocation("thebasics", "semantic-atlas/vintagestory-core.pilot.json")
    };

    private readonly Dictionary<string, SemanticLanguageAtlasBucket> _byIdentifier;

    private SemanticLanguageAtlasCatalog(string atlasId, string displayName, string version, IReadOnlyList<SemanticLanguageAtlasBucket> buckets)
    {
        AtlasId = atlasId;
        DisplayName = displayName;
        Version = version;
        Buckets = buckets;
        _byIdentifier = new Dictionary<string, SemanticLanguageAtlasBucket>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucket in buckets)
        {
            foreach (var identifier in bucket.GetIdentifiers().Select(NormalizeIdentifier).Where(identifier => identifier.Length > 0))
            {
                _byIdentifier.TryAdd(identifier, bucket);
            }
        }
    }

    public string AtlasId { get; }

    public string DisplayName { get; }

    public string Version { get; }

    public IReadOnlyList<SemanticLanguageAtlasBucket> Buckets { get; }

    public bool HasBuckets => Buckets.Count > 0;

    public static SemanticLanguageAtlasCatalog Empty { get; } = new SemanticLanguageAtlasCatalog(string.Empty, string.Empty, string.Empty, Array.Empty<SemanticLanguageAtlasBucket>());

    public static SemanticLanguageAtlasCatalog LoadDefault(ICoreServerAPI api)
    {
        try
        {
            var catalog = TryLoadDefault(api);
            if (catalog.HasBuckets)
            {
                return catalog;
            }

            api?.Logger?.Warning($"[thebasics] No semantic language atlas asset found at {string.Join(" or ", DefaultAtlasAssets.Select(location => location.ToString()))}.");
            return Empty;
        }
        catch (Exception ex)
        {
            api?.Logger?.Warning($"[thebasics] Failed to load semantic language atlas: {ex.Message}");
            return Empty;
        }
    }

    private static SemanticLanguageAtlasCatalog TryLoadDefault(ICoreServerAPI api)
    {
        foreach (var assetLocation in DefaultAtlasAssets)
        {
            var catalog = TryLoadAsset(api, assetLocation);
            if (catalog.HasBuckets)
            {
                api?.Logger?.Notification($"[thebasics] Loaded semantic language atlas '{catalog.AtlasId}' with {catalog.Buckets.Count} buckets from {assetLocation}.");
                return catalog;
            }
        }

        return Empty;
    }

    private static SemanticLanguageAtlasCatalog TryLoadAsset(ICoreServerAPI api, AssetLocation assetLocation)
    {
        var asset = api?.Assets?.TryGet(assetLocation);
        return asset == null
            ? Empty
            : FromDocument(asset.ToObject<SemanticLanguageAtlasDocument>());
    }

    public static SemanticLanguageAtlasCatalog FromDocument(SemanticLanguageAtlasDocument? document)
    {
        if (document?.Buckets == null)
        {
            return Empty;
        }

        var buckets = document.Buckets
            .Select(NormalizeBucket)
            .Where(bucket => !string.IsNullOrWhiteSpace(bucket.Id))
            .GroupBy(bucket => bucket.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(bucket => bucket.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bucket => bucket.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SemanticLanguageAtlasCatalog(
            document.AtlasId?.Trim() ?? string.Empty,
            document.DisplayName?.Trim() ?? string.Empty,
            document.Version?.Trim() ?? string.Empty,
            buckets);
    }

    public bool TryResolveBucket(string identifier, out SemanticLanguageAtlasBucket bucket)
    {
        return _byIdentifier.TryGetValue(NormalizeIdentifier(identifier), out bucket!);
    }

    public SemanticLanguageAtlasBucket? FindBucket(string bucketId)
    {
        return Buckets.FirstOrDefault(bucket => string.Equals(bucket.Id, bucketId, StringComparison.OrdinalIgnoreCase));
    }

    public static string FormatBucket(SemanticLanguageAtlasBucket bucket)
    {
        if (bucket == null)
        {
            return string.Empty;
        }

        var alias = string.IsNullOrWhiteSpace(bucket.Alias) ? bucket.Id : bucket.Alias;
        return $"{VtmlUtils.EscapeVtml(bucket.DisplayName)} [{VtmlUtils.EscapeVtml(alias)}]";
    }

    public string FormatSuggestions(int count = 8)
    {
        return string.Join(", ", Buckets
            .Take(Math.Max(0, count))
            .Select(bucket => VtmlUtils.EscapeVtml(string.IsNullOrWhiteSpace(bucket.Alias) ? bucket.Id : bucket.Alias)));
    }

    private static SemanticLanguageAtlasBucket NormalizeBucket(SemanticLanguageAtlasBucket bucket)
    {
        var id = NormalizeIdentifier(bucket.Id);
        var alias = NormalizeIdentifier(string.IsNullOrWhiteSpace(bucket.Alias) ? bucket.Id : bucket.Alias);
        var label = string.IsNullOrWhiteSpace(bucket.Label) ? id : bucket.Label.Trim();
        return new SemanticLanguageAtlasBucket
        {
            Id = id,
            Alias = alias,
            Label = label,
            Family = (bucket.Family ?? string.Empty).Trim(),
            Tags = NormalizeList(bucket.Tags),
            Examples = NormalizeList(bucket.Examples)
        };
    }

    public static string NormalizeIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var chars = identifier.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }
}
