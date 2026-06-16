#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SemanticAtlasExperiment;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ExperimentOptions.Parse(args);
            Directory.CreateDirectory(options.OutputDirectory);

            var langPath = Path.Combine(options.VintageStoryPath, "assets", "game", "lang", "en.json");
            if (!File.Exists(langPath))
            {
                Console.Error.WriteLine($"Missing Vintage Story language file: {langPath}");
                return 2;
            }

            var languageEntries = LanguageFile.Load(langPath);
            var builder = new CandidateCorpusBuilder(options, languageEntries);
            var corpus = builder.Build();

            ManualSeedDocument? manualSeeds = null;
            if (File.Exists(options.ManualSeedsPath))
            {
                manualSeeds = ManualSeedDocument.Load(options.ManualSeedsPath);
                ReportWriter.WriteManualSeedReview(options, manualSeeds);
            }

            corpus.TrimTo(options.MaxCandidates);
            if (options.IncludeManualSeeds && manualSeeds != null)
            {
                foreach (var candidate in manualSeeds.ToCandidates())
                {
                    corpus.Add(candidate);
                }
            }

            ReportWriter.WriteCandidateArtifacts(options, corpus, languageEntries, builder.Stats, manualSeeds);

            IReadOnlyList<SemanticCluster> clusters = Array.Empty<SemanticCluster>();
            ClusterRunSummary? clusterSummary = null;
            if (!options.NoEmbeddings)
            {
                using var embedder = new OnnxEmbeddingService(options.ModelPath, options.VocabPath);
                var candidatesToEmbed = options.ClusterMode.Equals("staged", StringComparison.OrdinalIgnoreCase)
                    ? CandidateSelector.SelectForStagedEmbedding(corpus.Items, options.MaxEmbeddings).ToArray()
                    : CandidateSelector.SelectForEmbedding(corpus.Items, options.MaxEmbeddings).ToArray();
                Console.WriteLine($"Embedding {candidatesToEmbed.Length:N0} candidates with {Path.GetFileName(options.ModelPath)}...");

                if (options.ClusterMode.Equals("greedy", StringComparison.OrdinalIgnoreCase))
                {
                    var clusterer = new GreedySemanticClusterer(options.MaxClusters, options.ClusterThreshold);
                    clusters = await clusterer.ClusterAsync(candidatesToEmbed, embedder, options).ConfigureAwait(false);
                    clusterSummary = ClusterRunSummary.ForGreedy(candidatesToEmbed.Length, clusters.Count);
                }
                else
                {
                    var staged = new StagedSemanticAtlasBuilder(options);
                    var result = await staged.BuildAsync(candidatesToEmbed, embedder).ConfigureAwait(false);
                    clusters = result.Clusters;
                    clusterSummary = result.Summary;
                }

                ReportWriter.WriteClusterArtifacts(options, clusters, clusterSummary);
                ReportWriter.WriteRuntimeAtlasArtifacts(options, clusters, clusterSummary);
            }

            ReportWriter.WriteSpotCheck(options, corpus, builder.Stats, manualSeeds, clusters, clusterSummary);
            Console.WriteLine($"Wrote semantic atlas experiment output to {options.OutputDirectory}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class ExperimentOptions
{
    private const string DefaultVintageStoryPath = @"D:\Games\Vintagestory";
    private static readonly string ToolRoot = AppContext.BaseDirectory.Contains(Path.Combine("tools", "SemanticAtlasExperiment"), StringComparison.OrdinalIgnoreCase)
        ? FindProjectRootFromBaseDirectory(AppContext.BaseDirectory)
        : Directory.GetCurrentDirectory();

    public string VintageStoryPath { get; private set; } = Environment.GetEnvironmentVariable("VINTAGE_STORY") ?? DefaultVintageStoryPath;

    public string OutputDirectory { get; private set; } = Path.Combine("tools", "SemanticAtlasExperiment", "output", "vintagestory-core");

    public string ModelPath { get; private set; } = Path.Combine("mods-dll", "thebasicslanguageunderstanding", "assets", "thebasicslanguageunderstanding", "models", "model.onnx");

    public string VocabPath { get; private set; } = Path.Combine("mods-dll", "thebasicslanguageunderstanding", "assets", "thebasicslanguageunderstanding", "models", "vocab.txt");

    public string ManualSeedsPath { get; private set; } = Path.Combine("tools", "SemanticAtlasExperiment", "manual-seeds.review.json");

    public string RuntimeAtlasId { get; private set; } = "vintagestory-core-generated";

    public string RuntimeAtlasDisplayName { get; private set; } = "Vintage Story Core Generated";

    public string RuntimeAtlasVersion { get; private set; } = "0.1.0-experiment";

    public string RuntimeAtlasCurationMode { get; private set; } = "core";

    public int MaxCandidates { get; private set; } = 150_000;

    public int MaxEmbeddings { get; private set; } = 25_000;

    public int MaxClusters { get; private set; } = 768;

    public int RuntimeAtlasBucketLimit { get; private set; } = 768;

    public int RuntimeAtlasExamplesPerBucket { get; private set; } = 8;

    public int RuntimeAtlasTargetBuckets { get; private set; } = 512;

    public int SpotCheckCount { get; private set; } = 160;

    public float ClusterThreshold { get; private set; } = 0.66f;

    public string ClusterMode { get; private set; } = "staged";

    public bool IncludeManualSeeds { get; private set; }

    public bool NoEmbeddings { get; private set; }

    public static ExperimentOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = "true";
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            values[key] = value;
        }

        var options = new ExperimentOptions
        {
            VintageStoryPath = GetPath(values, "vintage-story", Environment.GetEnvironmentVariable("VINTAGE_STORY") ?? DefaultVintageStoryPath),
            OutputDirectory = GetPath(values, "output", Path.Combine("tools", "SemanticAtlasExperiment", "output", "vintagestory-core")),
            ModelPath = GetPath(values, "model", Path.Combine("mods-dll", "thebasicslanguageunderstanding", "assets", "thebasicslanguageunderstanding", "models", "model.onnx")),
            VocabPath = GetPath(values, "vocab", Path.Combine("mods-dll", "thebasicslanguageunderstanding", "assets", "thebasicslanguageunderstanding", "models", "vocab.txt")),
            ManualSeedsPath = GetPath(values, "manual-seeds", Path.Combine("tools", "SemanticAtlasExperiment", "manual-seeds.review.json")),
            RuntimeAtlasId = GetString(values, "atlas-id", "vintagestory-core-generated"),
            RuntimeAtlasDisplayName = GetString(values, "atlas-display-name", "Vintage Story Core Generated"),
            RuntimeAtlasVersion = GetString(values, "atlas-version", "0.1.0-experiment"),
            RuntimeAtlasCurationMode = GetString(values, "runtime-atlas-curation", "core"),
            MaxCandidates = GetInt(values, "max-candidates", 150_000),
            MaxEmbeddings = GetInt(values, "max-embeddings", 25_000),
            MaxClusters = GetInt(values, "max-clusters", 768),
            RuntimeAtlasBucketLimit = GetInt(values, "runtime-atlas-buckets", GetInt(values, "max-clusters", 768)),
            RuntimeAtlasExamplesPerBucket = GetInt(values, "runtime-examples-per-bucket", 8),
            RuntimeAtlasTargetBuckets = GetInt(values, "runtime-atlas-target-buckets", 512),
            SpotCheckCount = GetInt(values, "spot-check-count", 160),
            ClusterThreshold = GetFloat(values, "cluster-threshold", 0.66f),
            ClusterMode = GetPath(values, "cluster-mode", "staged"),
            IncludeManualSeeds = GetBool(values, "include-manual-seeds", false),
            NoEmbeddings = GetBool(values, "no-embeddings", false)
        };

        options = options.WithNormalizedPaths();
        options.MaxEmbeddings = Math.Min(options.MaxEmbeddings, options.MaxCandidates);
        return options;
    }

    private ExperimentOptions WithNormalizedPaths()
    {
        return new ExperimentOptions
        {
            VintageStoryPath = NormalizePath(VintageStoryPath),
            OutputDirectory = NormalizePath(OutputDirectory),
            ModelPath = NormalizePath(ModelPath),
            VocabPath = NormalizePath(VocabPath),
            ManualSeedsPath = NormalizePath(ManualSeedsPath),
            RuntimeAtlasId = NormalizeIdentifier(RuntimeAtlasId, "vintagestory-core-generated"),
            RuntimeAtlasDisplayName = string.IsNullOrWhiteSpace(RuntimeAtlasDisplayName) ? "Vintage Story Core Generated" : RuntimeAtlasDisplayName.Trim(),
            RuntimeAtlasVersion = string.IsNullOrWhiteSpace(RuntimeAtlasVersion) ? "0.1.0-experiment" : RuntimeAtlasVersion.Trim(),
            RuntimeAtlasCurationMode = RuntimeAtlasCurationMode.Equals("raw", StringComparison.OrdinalIgnoreCase) ? "raw" : "core",
            MaxCandidates = Math.Max(1_000, MaxCandidates),
            MaxEmbeddings = Math.Max(0, MaxEmbeddings),
            MaxClusters = Math.Max(16, MaxClusters),
            RuntimeAtlasBucketLimit = Math.Max(16, RuntimeAtlasBucketLimit),
            RuntimeAtlasExamplesPerBucket = Math.Clamp(RuntimeAtlasExamplesPerBucket, 1, 24),
            RuntimeAtlasTargetBuckets = Math.Max(16, RuntimeAtlasTargetBuckets),
            SpotCheckCount = Math.Max(20, SpotCheckCount),
            ClusterThreshold = Math.Clamp(ClusterThreshold, 0.45f, 0.95f),
            ClusterMode = ClusterMode.Equals("greedy", StringComparison.OrdinalIgnoreCase) ? "greedy" : "staged",
            IncludeManualSeeds = IncludeManualSeeds,
            NoEmbeddings = NoEmbeddings
        };
    }

    private static string GetPath(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static string GetString(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static float GetFloat(IReadOnlyDictionary<string, string> values, string key, float fallback)
    {
        return values.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        return values.TryGetValue(key, out var value) ? value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase) : fallback;
    }

    private static string NormalizePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(ToolRoot, path));
    }

    private static string NormalizeIdentifier(string value, string fallback)
    {
        var normalized = RuntimeAtlasExporter.NormalizeIdentifier(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string FindProjectRootFromBaseDirectory(string baseDirectory)
    {
        var directory = new DirectoryInfo(baseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

internal sealed class CandidateCorpusBuilder
{
    private static readonly Regex MarkupRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(@"\{[^}]*\}|\[[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex CollapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex WordishRegex = new(@"[\p{L}\p{N}][\p{L}\p{N}' -]*", RegexOptions.Compiled);
    private static readonly Regex AssetCodeRegex = new("[\"']code[\"']\\s*:\\s*[\"']([^\"']+)[\"']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StateArrayRegex = new("[\"']states[\"']\\s*:\\s*\\[(?<values>.*?)\\]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex QuotedValueRegex = new("[\"']([^\"']+)[\"']", RegexOptions.Compiled);

    private readonly ExperimentOptions _options;
    private readonly LanguageFile _language;
    private readonly CandidateCorpus _corpus = new();
    private readonly Dictionary<string, List<TermRef>> _categories = new(StringComparer.OrdinalIgnoreCase);

    public CandidateCorpusBuilder(ExperimentOptions options, LanguageFile language)
    {
        _options = options;
        _language = language;
    }

    public CorpusBuildStats Stats { get; } = new();

    public CandidateCorpus Build()
    {
        AddLanguageEntries();
        AddAssetCodes();
        GenerateMechanicPhrases();
        GenerateGenericPermutations();
        Stats.CategoryCounts = _categories.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.OrdinalIgnoreCase);
        return _corpus;
    }

    private void AddLanguageEntries()
    {
        foreach (var (key, rawValue) in _language.Entries)
        {
            if (!ShouldIncludeLanguageKey(key, rawValue))
            {
                continue;
            }

            var cleaned = CleanText(rawValue);
            if (!IsUsefulCandidate(cleaned))
            {
                continue;
            }

            var tags = ClassifyKey(key).ToArray();
            _corpus.Add(new CandidateTerm(cleaned, "game-lang", key, tags));
            Stats.LanguageCandidates++;
            AddCategorizedTerm(key, cleaned);
        }
    }

    private static bool ShouldIncludeLanguageKey(string key, string rawValue)
    {
        var lowerKey = key.ToLowerInvariant();
        if (lowerKey.StartsWith("item-", StringComparison.Ordinal) ||
            lowerKey.StartsWith("block-", StringComparison.Ordinal) ||
            lowerKey.StartsWith("itemdesc-", StringComparison.Ordinal) ||
            lowerKey.StartsWith("blockhelp-", StringComparison.Ordinal) ||
            lowerKey.StartsWith("handbook-", StringComparison.Ordinal) ||
            lowerKey.StartsWith("lore", StringComparison.Ordinal) ||
            lowerKey.StartsWith("dialogue", StringComparison.Ordinal) ||
            lowerKey.StartsWith("meal", StringComparison.Ordinal) ||
            lowerKey.StartsWith("recipe", StringComparison.Ordinal) ||
            lowerKey.StartsWith("clutter", StringComparison.Ordinal) ||
            lowerKey.StartsWith("groundstored", StringComparison.Ordinal) ||
            lowerKey.StartsWith("smelt", StringComparison.Ordinal) ||
            lowerKey.StartsWith("pulver", StringComparison.Ordinal) ||
            lowerKey.StartsWith("squeeze", StringComparison.Ordinal) ||
            lowerKey.StartsWith("carbur", StringComparison.Ordinal))
        {
            return true;
        }

        var lowerValue = rawValue.ToLowerInvariant();
        return lowerKey.Contains("temporal", StringComparison.Ordinal) ||
               lowerKey.Contains("translocator", StringComparison.Ordinal) ||
               lowerKey.Contains("drifter", StringComparison.Ordinal) ||
               lowerKey.Contains("locust", StringComparison.Ordinal) ||
               lowerKey.Contains("resonance", StringComparison.Ordinal) ||
               lowerValue.Contains("temporal", StringComparison.Ordinal) ||
               lowerValue.Contains("translocator", StringComparison.Ordinal) ||
               lowerValue.Contains("drifter", StringComparison.Ordinal) ||
               lowerValue.Contains("locust", StringComparison.Ordinal) ||
               lowerValue.Contains("resonance", StringComparison.Ordinal) ||
               lowerValue.Contains("rust world", StringComparison.Ordinal) ||
               lowerValue.Contains("jonas falx", StringComparison.Ordinal);
    }

    private void AddCategorizedTerm(string key, string cleaned)
    {
        AddIfMatch(key, cleaned, @"^item-ingot-(?<id>[\w-]+)$", "metals");
        AddIfMatch(key, cleaned, @"^item-nugget-(?<id>[\w-]+)$", "metals");
        AddIfMatch(key, cleaned, @"^item-metalbit-(?<id>[\w-]+)$", "metals");
        AddIfMatch(key, cleaned, @"^item-ore-(?<id>[\w-]+)$", "ores");
        AddIfMatch(key, cleaned, @"^block-ore-.+-(?<id>[\w-]+)-.*$", "ores");
        AddIfMatch(key, cleaned, @"^item-stone-(?<id>[\w-]+)$", "rocks");
        AddIfMatch(key, cleaned, @"^block-rock-(?<id>[\w-]+)$", "rocks");
        AddIfMatch(key, cleaned, @"^block-crop-(?<id>[\w-]+)-.*$", "crops");
        AddIfMatch(key, cleaned, @"^item-seeds-(?<id>[\w-]+)$", "crops");
        AddIfMatch(key, cleaned, @"^item-grain-(?<id>[\w-]+)$", "foods");
        AddIfMatch(key, cleaned, @"^item-vegetable-(?<id>[\w-]+)$", "foods");
        AddIfMatch(key, cleaned, @"^item-fruit-(?<id>[\w-]+)$", "foods");
        AddIfMatch(key, cleaned, @"^item-creature-(?<id>[\w-]+)$", "creatures");
        if (Regex.IsMatch(key, @"^item-creature-(?<id>[\w-]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            AddCreatureCategory(key, cleaned);
        }
        AddIfMatch(key, cleaned, @"^item-(?:pickaxe|axe|shovel|hammer|hoe|knife|saw|spear|bow|chisel)-(?<id>[\w-]+)$", "tools");
        AddIfMatch(key, cleaned, @"^item-(?:pickaxehead|axehead|shovelhead|hammerhead|hoehead|knifeblade|spearhead|sawblade|chiselhead)-(?<id>[\w-]+)$", "tool-parts");

        if (key.Contains("temporal", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("temporal", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("translocator", StringComparison.OrdinalIgnoreCase) || ContainsRiftTerm(cleaned))
        {
            AddCategory("temporal-lore", key, cleaned);
        }

        if (cleaned.Contains("drifter", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("locust", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("bell", StringComparison.OrdinalIgnoreCase))
        {
            AddCategory("creatures-lore", key, cleaned);
        }

        if (key.StartsWith("lore", StringComparison.OrdinalIgnoreCase) || key.Contains("dialogue", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("Jonas", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("Resonance", StringComparison.OrdinalIgnoreCase))
        {
            AddCategory("lore", key, cleaned);
        }
    }

    private void AddIfMatch(string key, string cleaned, string pattern, string category)
    {
        if (Regex.IsMatch(key, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            AddCategory(category, key, cleaned);
        }
    }

    private void AddCreatureCategory(string key, string cleaned)
    {
        if (IsTemplateSafeTerm("phrase:creatures", cleaned) == false)
        {
            return;
        }

        var lower = (key + " " + cleaned).ToLowerInvariant();
        if (ContainsAny(lower, "trader", "merchant", "villager", "surgeon", "herbalist", "inventor", "baker", "musician", "tailor", "clockmaker", "commoner", "malefactor", "blackguard"))
        {
            AddCategory("npc-creatures", key, cleaned);
            return;
        }

        if (ContainsAny(lower, "tamed", "rooster", "hen", "chick", "cockerel", "pullet", "goat", "sheep", "calf", "lamb", "piglet"))
        {
            AddCategory("domestic-creatures", key, cleaned);
            return;
        }

        if (ContainsAny(lower, "drifter", "locust", "wolf", "bear", "hyena", "bell", "nightmare", "corrupt", "tainted", "boar"))
        {
            AddCategory("hostile-creatures", key, cleaned);
            return;
        }

        if (ContainsAny(lower, "fish", "trout", "salmon", "bass", "tuna", "catfish", "pike", "carp", "perch", "hake", "snapper", "tilapia"))
        {
            AddCategory("fish-creatures", key, cleaned);
            return;
        }

        AddCategory("huntable-creatures", key, cleaned);
    }

    private void AddCategory(string category, string key, string text)
    {
        text = NormalizeCategoryTerm(category, text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_categories.TryGetValue(category, out var terms))
        {
            terms = new List<TermRef>();
            _categories[category] = terms;
        }

        if (terms.Any(term => term.Text.Equals(text, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        terms.Add(new TermRef(text, key));
    }

    private static string NormalizeCategoryTerm(string category, string text)
    {
        var normalized = CleanText(text);
        if (category.Equals("metals", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(normalized, @"\s+(ingots?|nuggets?|bits?|metal bits?)$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }
        else if (category.Equals("rocks", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(normalized, @"\s+(stone|rock)$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }
        else if (category.Equals("crops", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(normalized, @"\s+(seeds?|grain)$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            normalized = Regex.Replace(normalized, @"^(growing|mature)\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }
        else if (category.Equals("ores", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(normalized, @"\s+chunks?$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }

        return normalized;
    }

    private void AddAssetCodes()
    {
        foreach (var source in GetAssetSourceRoots())
        {
            if (!Directory.Exists(source.Path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(source.Path, "*.json", SearchOption.AllDirectories))
            {
                Stats.AssetFilesScanned++;
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                foreach (Match match in AssetCodeRegex.Matches(text))
                {
                    var code = match.Groups[1].Value;
                    var normalized = NormalizeAssetCode(code);
                    if (IsUsefulAssetCode(normalized))
                    {
                        _corpus.Add(new CandidateTerm(normalized, $"asset-code:{source.Name}", ShortenPath(file), new[] { "asset-code", source.Tag }));
                        Stats.AssetCodeCandidates++;
                    }
                }

                foreach (Match match in StateArrayRegex.Matches(text))
                {
                    foreach (Match valueMatch in QuotedValueRegex.Matches(match.Groups["values"].Value))
                    {
                        var normalized = NormalizeAssetCode(valueMatch.Groups[1].Value);
                        if (IsUsefulCandidate(normalized))
                        {
                            _corpus.Add(new CandidateTerm(normalized, $"asset-state:{source.Name}", ShortenPath(file), new[] { "asset-state", source.Tag }));
                            Stats.AssetStateCandidates++;
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<AssetSourceRoot> GetAssetSourceRoots()
    {
        var survival = Path.Combine(_options.VintageStoryPath, "assets", "survival");
        yield return new AssetSourceRoot("itemtypes", Path.Combine(survival, "itemtypes"), "item");
        yield return new AssetSourceRoot("blocktypes", Path.Combine(survival, "blocktypes"), "block");
        yield return new AssetSourceRoot("entities", Path.Combine(survival, "entities"), "entity");
        yield return new AssetSourceRoot("worldproperties", Path.Combine(survival, "worldproperties"), "worldproperty");
        yield return new AssetSourceRoot("recipes", Path.Combine(survival, "recipes"), "recipe");
        yield return new AssetSourceRoot("dialogue", Path.Combine(survival, "config", "dialogue"), "dialogue");
        yield return new AssetSourceRoot("lore", Path.Combine(survival, "config", "lore"), "lore");
        yield return new AssetSourceRoot("tradelists", Path.Combine(survival, "config", "tradelists"), "trade");
        yield return new AssetSourceRoot("worldgen", Path.Combine(survival, "worldgen"), "worldgen");
    }

    private void GenerateMechanicPhrases()
    {
        AddTemplatePhrases("phrase:metalworking", new[] { "generated-action", "metalworking" }, Terms("metals"), new[]
        {
            "forge a {0} ingot",
            "cast molten {0} into a mold",
            "smith a {0} pickaxe head",
            "work {0} on the anvil",
            "repair tools with {0}"
        });

        AddTemplatePhrases("phrase:mining", new[] { "generated-action", "mining" }, Terms("ores"), new[]
        {
            "prospect for {0}",
            "mine {0}",
            "crush {0} in the pulverizer",
            "smelt {0} in the bloomery",
            "mark the {0} reading on the map"
        });

        AddTemplatePhrases("phrase:stoneworking", new[] { "generated-action", "stoneworking" }, Terms("rocks"), new[]
        {
            "mine {0} with a pickaxe",
            "quarry {0} stone",
            "knap a {0} knife blade",
            "build a wall from {0}",
            "carry {0} back to camp"
        });

        AddTemplatePhrases("phrase:farming", new[] { "generated-action", "farming" }, Terms("crops"), new[]
        {
            "plant {0} seeds",
            "harvest mature {0}",
            "water the {0} field",
            "store {0} grain in a vessel",
            "save {0} seeds for spring"
        });

        AddTemplatePhrases("phrase:cooking", new[] { "generated-action", "food" }, Terms("foods"), new[]
        {
            "cook {0} in a meal",
            "eat {0} before winter",
            "preserve {0} in the cellar",
            "trade {0} with the merchant",
            "bring {0} to the campfire"
        });

        AddTemplatePhrases("phrase:hostile-creatures", new[] { "generated-action", "mob", "hostile" }, Terms("hostile-creatures").Concat(Terms("creatures-lore")), new[]
        {
            "fight the {0}",
            "avoid the {0} at night",
            "the {0} is near the ruins",
            "warn the camp about the {0}",
            "listen for the {0} underground"
        });

        AddTemplatePhrases("phrase:huntable-creatures", new[] { "generated-action", "mob", "huntable" }, Terms("huntable-creatures"), new[]
        {
            "track the {0}",
            "hunt the {0} carefully",
            "butcher the {0}",
            "bring the {0} meat to camp",
            "watch the {0} near the forest"
        });

        AddTemplatePhrases("phrase:fish-creatures", new[] { "generated-action", "mob", "fish" }, Terms("fish-creatures"), new[]
        {
            "catch the {0}",
            "clean the {0}",
            "cook the {0}",
            "smoke the {0} for winter",
            "trade the {0} at camp"
        });

        AddTemplatePhrases("phrase:domestic-creatures", new[] { "generated-action", "mob", "domestic" }, Terms("domestic-creatures"), new[]
        {
            "feed the {0}",
            "lead the {0} back to the pen",
            "breed the {0}",
            "protect the {0} from wolves",
            "keep the {0} near the barn"
        });

        AddTemplatePhrases("phrase:npc-creatures", new[] { "generated-action", "npc", "trade" }, Terms("npc-creatures"), new[]
        {
            "talk to the {0}",
            "trade with the {0}",
            "ask the {0} for help",
            "find the {0} near the market",
            "bring supplies to the {0}"
        });

        AddTemplatePhrases("phrase:lore", new[] { "generated-action", "lore" }, Terms("temporal-lore").Concat(Terms("lore")), new[]
        {
            "search for {0}",
            "read about {0}",
            "warn the others about {0}"
        });
    }

    private void GenerateGenericPermutations()
    {
        foreach (var candidate in _corpus.Items.ToArray())
        {
            if (_corpus.Count >= _options.MaxCandidates)
            {
                break;
            }

            if (!IsGenericPhraseSafeCandidate(candidate))
            {
                continue;
            }

            foreach (var template in GetGenericTemplates(candidate))
            {
                AddPhrase(string.Format(CultureInfo.InvariantCulture, template, candidate.Text), "phrase:generic", new[] { "generated-generic" }, candidate.SourceKey);
                if (_corpus.Count >= _options.MaxCandidates)
                {
                    break;
                }
            }
        }
    }

    private static IEnumerable<string> GetGenericTemplates(CandidateTerm candidate)
    {
        var family = StagedCandidateClassifier.ResolveFamily(candidate);
        if (candidate.Tags.Contains("manual-seed-review"))
        {
            yield return "remember {0}";
            yield return "ask about {0}";
            yield break;
        }

        switch (family)
        {
            case "temporal-lore":
            case "lore":
                yield return "ask about {0}";
                break;
            case "social-roleplay":
            case "danger-help":
            case "npc-social":
                yield return "ask for {0}";
                yield return "tell the camp about {0}";
                break;
            case "hostile-creatures":
                yield return "warn the camp about {0}";
                break;
            case "community-mods":
                yield return "ask about {0}";
                yield return "plan around {0}";
                break;
        }
    }

    private void AddTemplatePhrases(string source, string[] tags, IEnumerable<TermRef> terms, string[] templates)
    {
        foreach (var term in terms.Where(term => IsTemplateSafeTerm(source, term.Text)).DistinctBy(term => NormalizeKey(term.Text)).Take(2_000))
        {
            var templateTerm = NormalizeTemplateArgument(term.Text);
            if (!IsUsefulCandidate(templateTerm))
            {
                continue;
            }

            foreach (var template in templates)
            {
                AddPhrase(string.Format(CultureInfo.InvariantCulture, template, templateTerm), source, tags, term.SourceKey);
                if (_corpus.Count >= _options.MaxCandidates)
                {
                    return;
                }
            }
        }
    }

    private static string NormalizeTemplateArgument(string text)
    {
        var normalized = CleanText(text);
        return Regex.Replace(normalized, @"^(?:a|an|the)\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
    }

    private static bool IsTemplateSafeTerm(string source, string text)
    {
        var normalized = CleanText(text);
        if (!IsUsefulCandidate(normalized))
        {
            return false;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 6)
        {
            return false;
        }

        if (Regex.IsMatch(normalized, @"\d", RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (source.Equals("phrase:lore", StringComparison.OrdinalIgnoreCase))
        {
            if (words.Length > 4 || Regex.IsMatch(normalized, @"[.!?:;()]", RegexOptions.CultureInvariant))
            {
                return false;
            }

            var lower = normalized.ToLowerInvariant();
            var loreKeywords = new[]
            {
                "temporal",
                "translocator",
                "drifter",
                "locust",
                "rust",
                "resonance",
                "jonas",
                "falx",
                "gear",
                "ruin",
                "archive",
                "bell",
                "rot"
            };
            if (!loreKeywords.Any(keyword => lower.Contains(keyword, StringComparison.Ordinal)) && !ContainsRiftTerm(lower))
            {
                return false;
            }

            if (lower is "on" or "off" or "times" or "repair" or "activate")
            {
                return false;
            }

            return !lower.StartsWith("you ", StringComparison.Ordinal) &&
                   !lower.StartsWith("player ", StringComparison.Ordinal) &&
                   !lower.StartsWith("when ", StringComparison.Ordinal) &&
                   !lower.Contains(" has ", StringComparison.Ordinal) &&
                   !lower.Contains(" will ", StringComparison.Ordinal);
        }

        if (source.Equals("phrase:creatures", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("phrase:hostile-creatures", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("phrase:huntable-creatures", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("phrase:fish-creatures", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("phrase:domestic-creatures", StringComparison.OrdinalIgnoreCase) ||
            source.Equals("phrase:npc-creatures", StringComparison.OrdinalIgnoreCase))
        {
            var lower = normalized.ToLowerInvariant();
            return !lower.StartsWith("dead ", StringComparison.Ordinal) &&
                   !lower.StartsWith("player ", StringComparison.Ordinal) &&
                   !lower.Contains(" succumbed ", StringComparison.Ordinal) &&
                   !lower.Contains(" killed ", StringComparison.Ordinal) &&
                   !lower.Contains(" got ", StringComparison.Ordinal) &&
                   !lower.Contains(" lost ", StringComparison.Ordinal) &&
                   !lower.Contains("hacked", StringComparison.Ordinal) &&
                   !lower.Contains("all over", StringComparison.Ordinal) &&
                   !lower.Contains("armor stand", StringComparison.Ordinal) &&
                   !lower.Contains("(hacked)", StringComparison.Ordinal);
        }

        return true;
    }

    private static bool IsGenericPhraseSafeCandidate(CandidateTerm candidate)
    {
        if (candidate.Source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase) ||
            candidate.Source.StartsWith("asset-code:", StringComparison.OrdinalIgnoreCase) ||
            candidate.Source.StartsWith("asset-state:", StringComparison.OrdinalIgnoreCase) ||
            !StagedCandidateClassifier.IsEvidenceCandidate(candidate) ||
            candidate.Tags.Contains("dialogue") ||
            candidate.Tags.Contains("block-help") ||
            candidate.Tags.Contains("item-description"))
        {
            return false;
        }

        var text = CleanText(candidate.Text);
        if (text.Length > 64 || Regex.IsMatch(text, @"\d|[.!?:;()]", RegexOptions.CultureInvariant))
        {
            return false;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 5)
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        var lowerSourceKey = candidate.SourceKey.ToLowerInvariant();
        var family = StagedCandidateClassifier.ResolveFamily(candidate);
        if (family is not ("temporal-lore" or "lore" or "social-roleplay" or "danger-help" or "npc-social" or "hostile-creatures" or "community-mods"))
        {
            return false;
        }

        if (lower.StartsWith("player ", StringComparison.Ordinal) ||
            lower.StartsWith("can be ", StringComparison.Ordinal) ||
            lower.StartsWith("will ", StringComparison.Ordinal) ||
            lower.Contains(" got ", StringComparison.Ordinal) ||
            lower.Contains(" killed ", StringComparison.Ordinal) ||
            lower.Contains(" lost ", StringComparison.Ordinal) ||
            lower.Contains("succumbed", StringComparison.Ordinal) ||
            lower.Contains("all over", StringComparison.Ordinal))
        {
            return false;
        }

        if (lower.Contains(" into", StringComparison.Ordinal) ||
            lower.Contains("temperature", StringComparison.Ordinal) ||
            lower.StartsWith("will create", StringComparison.Ordinal) ||
            lower.Contains("matching recipe", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("smelt", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("squeeze", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("pulver", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("carbur", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("groundstored", StringComparison.Ordinal) ||
            lowerSourceKey.StartsWith("mealcreation", StringComparison.Ordinal))
        {
            return false;
        }

        if (lower is "uses" or "material" or "fertility" or "header" or "headero" or "writing" or "allowed" or "disallowed" or "visible" or "invisible" or "infinite" or "nope" or "on" or "off")
        {
            return false;
        }

        if (candidate.Source.StartsWith("asset-code:entities", StringComparison.OrdinalIgnoreCase) && ContainsAny(lower, "hair", "head", "face", "eyes", "skin", "boots", "foot", "lowerbody", "upperbody", "clothes"))
        {
            return false;
        }

        if (candidate.Source.StartsWith("asset-code:tradelists", StringComparison.OrdinalIgnoreCase) && ContainsAny(lower, "clothes", "wearables", "foot", "lowerbody", "upperbody", "neck", "arm", "head", "shoulder"))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsRiftTerm(string value)
    {
        return Regex.IsMatch(value, @"\brifts?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private IEnumerable<TermRef> Terms(string category)
    {
        return _categories.TryGetValue(category, out var terms) ? terms : Array.Empty<TermRef>();
    }

    private void AddPhrase(string phrase, string source, string[] tags, string sourceKey)
    {
        var cleaned = CleanText(phrase);
        if (!IsUsefulCandidate(cleaned))
        {
            return;
        }

        _corpus.Add(new CandidateTerm(cleaned, source, sourceKey, tags));
        Stats.GeneratedPhraseCandidates++;
    }

    public static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = WebUtility.HtmlDecode(text);
        cleaned = cleaned.Replace("___NEWPAGE___", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("\\n", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = MarkupRegex.Replace(cleaned, " ");
        cleaned = PlaceholderRegex.Replace(cleaned, " ");
        cleaned = cleaned.Replace("*", " ", StringComparison.OrdinalIgnoreCase).Replace("_", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = CollapseWhitespaceRegex.Replace(cleaned, " ").Trim(' ', '.', ',', ';', ':', '-', '\u2013', '\u2014');
        return cleaned;
    }

    private static bool IsUsefulCandidate(string text)
    {
        if (text.Length < 2 || text.Length > 180)
        {
            return false;
        }

        var lower = text.Trim().ToLowerInvariant();
        if (lower.EndsWith(" of", StringComparison.Ordinal) ||
            lower.EndsWith(" with", StringComparison.Ordinal) ||
            lower.EndsWith(" for", StringComparison.Ordinal) ||
            lower.EndsWith(" from", StringComparison.Ordinal))
        {
            return false;
        }

        if (!WordishRegex.IsMatch(text))
        {
            return false;
        }

        if (text.Count(char.IsLetterOrDigit) < 2)
        {
            return false;
        }

        return !text.Equals("null", StringComparison.OrdinalIgnoreCase) && !text.Equals("true", StringComparison.OrdinalIgnoreCase) && !text.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsefulAssetCode(string text)
    {
        if (!IsUsefulCandidate(text))
        {
            return false;
        }

        return !text.Equals("type", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("state", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("species", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("age", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("size", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("color", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("variant", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("side", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("orientation", StringComparison.OrdinalIgnoreCase) &&
               !text.Equals("habitat", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAssetCode(string code)
    {
        var text = code.Replace('/', ' ').Replace('-', ' ').Replace('_', ' ').Replace("*", " ", StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, @"\{[^}]+\}", " ");
        text = CollapseWhitespaceRegex.Replace(text, " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private static IEnumerable<string> ClassifyKey(string key)
    {
        var lower = key.ToLowerInvariant();
        if (lower.StartsWith("item-", StringComparison.Ordinal)) yield return "item";
        if (lower.StartsWith("block-", StringComparison.Ordinal)) yield return "block";
        if (lower.StartsWith("itemdesc-", StringComparison.Ordinal)) yield return "item-description";
        if (lower.StartsWith("blockhelp-", StringComparison.Ordinal)) yield return "block-help";
        if (lower.StartsWith("dialogue", StringComparison.Ordinal)) yield return "dialogue";
        if (lower.StartsWith("lore", StringComparison.Ordinal) || lower.Contains("resonance", StringComparison.Ordinal)) yield return "lore";
        if (lower.Contains("temporal", StringComparison.Ordinal) || lower.Contains("translocator", StringComparison.Ordinal) || ContainsRiftTerm(lower)) yield return "temporal";
        if (lower.Contains("drifter", StringComparison.Ordinal) || lower.Contains("locust", StringComparison.Ordinal)) yield return "mob";
        if (lower.Contains("recipe", StringComparison.Ordinal)) yield return "recipe";
    }

    private string ShortenPath(string path)
    {
        return Path.GetRelativePath(_options.VintageStoryPath, path).Replace('\\', '/');
    }

    private static string NormalizeKey(string text)
    {
        return CandidateCorpus.Normalize(text);
    }
}

internal sealed class CandidateCorpus
{
    private readonly Dictionary<string, CandidateTerm> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CandidateTerm> Items => _items.Values.ToArray();

    public int Count => _items.Count;

    public void Add(CandidateTerm candidate)
    {
        var text = CandidateCorpusBuilder.CleanText(candidate.Text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var key = Normalize(text);
        if (_items.TryGetValue(key, out var existing))
        {
            if (candidate.Tags.Contains("manual-seed-review"))
            {
                _items[key] = candidate with
                {
                    Text = text,
                    Tags = existing.Tags.Concat(candidate.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                };
            }

            return;
        }

        _items[key] = candidate with { Text = text };
    }

    public void TrimTo(int maxCandidates)
    {
        if (_items.Count <= maxCandidates)
        {
            return;
        }

        var trimmed = _items.Values.Take(maxCandidates).ToArray();
        _items.Clear();
        foreach (var item in trimmed)
        {
            _items[Normalize(item.Text)] = item;
        }
    }

    public static string Normalize(string text)
    {
        return string.Join(" ", (text ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

internal sealed record CandidateTerm(string Text, string Source, string SourceKey, string[] Tags);

internal static class CandidateSelector
{
    public static IEnumerable<CandidateTerm> SelectForStagedEmbedding(IReadOnlyList<CandidateTerm> candidates, int maxEmbeddings)
    {
        if (maxEmbeddings <= 0)
        {
            return Array.Empty<CandidateTerm>();
        }

        var selected = new List<CandidateTerm>(Math.Min(candidates.Count, maxEmbeddings));
        var emittedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddCandidates(candidates
            .Where(candidate => candidate.Tags.Contains("manual-seed-review"))
            .OrderBy(GetStagedPriority)
            .ThenBy(candidate => candidate.SourceKey)
            .ThenBy(candidate => candidate.Text), maxEmbeddings);

        var seedCandidates = SelectRoundRobinByFamily(candidates.Where(candidate => StagedCandidateClassifier.IsSeedCandidate(candidate) && !candidate.Tags.Contains("manual-seed-review"))).ToArray();
        var evidenceCandidates = SelectRoundRobinByFamily(candidates.Where(candidate => !StagedCandidateClassifier.IsSeedCandidate(candidate) && StagedCandidateClassifier.IsEvidenceCandidate(candidate) && !candidate.Tags.Contains("manual-seed-review"))).ToArray();
        var remaining = maxEmbeddings - selected.Count;
        if (remaining <= 0)
        {
            return selected;
        }

        if (seedCandidates.Length + evidenceCandidates.Length <= remaining)
        {
            AddCandidates(seedCandidates, remaining);
            AddCandidates(evidenceCandidates, maxEmbeddings - selected.Count);
            return selected;
        }

        var evidenceBudget = evidenceCandidates.Length == 0 ? 0 : Math.Min(evidenceCandidates.Length, Math.Max(remaining / 4, Math.Min(remaining, 250)));
        AddCandidates(seedCandidates, remaining - evidenceBudget);
        AddCandidates(evidenceCandidates, evidenceBudget);
        AddCandidates(seedCandidates, maxEmbeddings - selected.Count);
        AddCandidates(evidenceCandidates, maxEmbeddings - selected.Count);
        return selected;

        void AddCandidates(IEnumerable<CandidateTerm> source, int maxToAdd)
        {
            if (maxToAdd <= 0)
            {
                return;
            }

            var added = 0;
            foreach (var candidate in source)
            {
                if (selected.Count >= maxEmbeddings || added >= maxToAdd)
                {
                    return;
                }

                if (emittedKeys.Add(GetCandidateKey(candidate)))
                {
                    selected.Add(candidate);
                    added++;
                }
            }
        }
    }

    public static IEnumerable<CandidateTerm> SelectForEmbedding(IReadOnlyList<CandidateTerm> candidates, int maxEmbeddings)
    {
        if (maxEmbeddings <= 0)
        {
            yield break;
        }

        if (candidates.Count <= maxEmbeddings)
        {
            foreach (var candidate in candidates)
            {
                yield return candidate;
            }

            yield break;
        }

        var groups = candidates
            .GroupBy(candidate => candidate.Source)
            .OrderBy(group => GetSourcePriority(group.Key))
            .ThenByDescending(group => group.Count())
            .Select(group => new Queue<CandidateTerm>(group))
            .ToArray();

        var emitted = 0;
        while (emitted < maxEmbeddings && groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups)
            {
                if (emitted >= maxEmbeddings)
                {
                    yield break;
                }

                if (group.Count == 0)
                {
                    continue;
                }

                emitted++;
                yield return group.Dequeue();
            }
        }
    }

    private static int GetSourcePriority(string source)
    {
        if (source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase)) return 0;
        if (source.Equals("game-lang", StringComparison.OrdinalIgnoreCase)) return 1;
        if (source.StartsWith("asset-state:", StringComparison.OrdinalIgnoreCase)) return 2;
        if (source.StartsWith("asset-code:", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static int GetStagedPriority(CandidateTerm candidate)
    {
        if (candidate.Tags.Contains("manual-seed-review") && !candidate.Tags.Contains("manual-phrase")) return 0;
        if (!candidate.Source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase) && !candidate.Tags.Contains("generated-generic") && !candidate.Tags.Contains("generated-action")) return 1;
        if (candidate.Tags.Contains("manual-phrase")) return 2;
        if (candidate.Tags.Contains("generated-action")) return 3;
        if (candidate.Tags.Contains("generated-generic")) return 4;
        return 5;
    }

    private static IEnumerable<CandidateTerm> SelectRoundRobinByFamily(IEnumerable<CandidateTerm> candidates)
    {
        var groups = candidates
            .GroupBy(StagedCandidateClassifier.ResolveFamily, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => StagedCandidateClassifier.GetFamilyPriority(group.Key))
            .ThenByDescending(group => group.Count())
            .Select(group => new Queue<CandidateTerm>(group
                .OrderBy(GetStagedPriority)
                .ThenBy(candidate => GetSourcePriority(candidate.Source))
                .ThenBy(candidate => candidate.Source)
                .ThenBy(candidate => candidate.Text)))
            .ToArray();

        while (groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups)
            {
                if (group.Count > 0)
                {
                    yield return group.Dequeue();
                }
            }
        }
    }

    private static string GetCandidateKey(CandidateTerm candidate)
    {
        return $"{CandidateCorpus.Normalize(candidate.Text)}\u001f{candidate.Source}\u001f{candidate.SourceKey}";
    }
}

internal static class StagedCandidateClassifier
{
    public static bool IsSeedCandidate(CandidateTerm candidate)
    {
        if (candidate.Tags.Contains("generated-generic") || candidate.Tags.Contains("generated-action") || candidate.Tags.Contains("manual-phrase"))
        {
            return false;
        }

        if (candidate.Source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (candidate.Tags.Contains("item-description") || candidate.Tags.Contains("block-help") || candidate.Tags.Contains("dialogue"))
        {
            return false;
        }

        var lowerKey = candidate.SourceKey.ToLowerInvariant();
        if (lowerKey.StartsWith("deathmsg", StringComparison.Ordinal) || lowerKey.StartsWith("smelt", StringComparison.Ordinal) || lowerKey.StartsWith("mealcreation", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public static bool IsEvidenceCandidate(CandidateTerm candidate)
    {
        if (candidate.Tags.Contains("item-description") || candidate.Tags.Contains("block-help"))
        {
            return false;
        }

        var lowerKey = candidate.SourceKey.ToLowerInvariant();
        return !lowerKey.StartsWith("itemdesc-", StringComparison.Ordinal) &&
               !lowerKey.StartsWith("blockhelp-", StringComparison.Ordinal) &&
               !lowerKey.StartsWith("deathmsg", StringComparison.Ordinal);
    }

    public static string ResolveFamily(CandidateTerm candidate)
    {
        var tags = candidate.Tags;
        var key = candidate.SourceKey.ToLowerInvariant();
        var text = candidate.Text.ToLowerInvariant();
        var combined = key + " " + text;

        if (tags.Contains("social-roleplay")) return "social-roleplay";
        if (tags.Contains("danger-help-logistics")) return "danger-help";
        if (tags.Contains("everyday-conversation")) return "social-roleplay";
        if (tags.Contains("everyday-survival-actions")) return "danger-help";
        if (tags.Contains("popular-mods-community")) return "community-mods";
        if (tags.Contains("base-temporal-lore")) return "temporal-lore";
        if (tags.Contains("base-creatures-lore")) return "hostile-creatures";
        if (tags.Contains("base-crafting-survival")) return "crafting-survival";
        if (tags.Contains("fish")) return "fish";
        if (tags.Contains("hostile")) return "hostile-creatures";
        if (tags.Contains("domestic")) return "domestic-creatures";
        if (tags.Contains("huntable")) return "huntable-creatures";
        if (tags.Contains("npc")) return "npc-social";
        if (tags.Contains("metalworking")) return "metals";
        if (tags.Contains("mining")) return "ores-mining";
        if (tags.Contains("stoneworking")) return "stone-building";
        if (tags.Contains("farming")) return "farming";
        if (tags.Contains("food")) return "food";
        if (tags.Contains("temporal")) return "temporal-lore";
        if (tags.Contains("lore")) return "lore";

        if (ContainsAny(combined, "clothes", "shirt", "pants", "gloves", "boots", "bracelet", "necklace", "amulet")) return "clothing";
        if (ContainsAny(combined, "armor", "helmet", "pelt", "hide", "fur-lined")) return "clothing";
        if (ContainsAny(combined, "shield")) return "equipment";
        if (ContainsAny(combined, "fish", "trout", "salmon", "bass", "sturgeon", "clownfish", "angelfish", "triggerfish")) return "fish";
        if (ContainsAny(combined, "drifter", "locust", "wolf", "bear", "hyena", "nightmare", "corrupt")) return "hostile-creatures";
        if (ContainsAny(combined, "trader", "villager", "merchant")) return "npc-social";
        if (ContainsAny(combined, "temporal", "translocator", "rust world", "resonance", "jonas") || ContainsRiftTerm(combined)) return "temporal-lore";
        if (ContainsAny(combined, "falx")) return "equipment";
        if (ContainsAny(combined, "ingot", "bronze", "copper", "iron", "steel", "metal", "meteoric")) return "metals";
        if (ContainsAny(combined, "prospect", "cassiterite", "malachite", "quartz", "bauxite") || ContainsOreTerm(combined)) return "ores-mining";
        if (ContainsAny(combined, "crop", "seed", "grain", "flax", "rye", "turnip", "carrot", "soybean")) return "farming";
        if (ContainsAny(combined, "meal", "porridge", "bread", "fruit", "vegetable", "meat", "cooked", "smoked", "cured")) return "food";
        if (ContainsAny(combined, "clay", "ceramic", "roof", "shingle", "brick", "ashlar", "log", "plank", "stone", "rock")) return "building-materials";
        if (tags.Contains("mob")) return "creatures";
        if (tags.Contains("block")) return "blocks";
        if (tags.Contains("item")) return "items";
        if (tags.Contains("trade")) return "trade";
        return "misc";
    }

    public static int GetFamilyPriority(string family)
    {
        return family switch
        {
            "temporal-lore" => 0,
            "hostile-creatures" => 1,
            "fish" => 2,
            "metals" => 3,
            "ores-mining" => 4,
            "farming" => 5,
            "food" => 6,
            "building-materials" => 7,
            "equipment" => 8,
            "social-roleplay" => 8,
            "danger-help" => 9,
            "community-mods" => 10,
            _ => 20
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsRiftTerm(string value)
    {
        return Regex.IsMatch(value, @"\brifts?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsOreTerm(string value)
    {
        return Regex.IsMatch(value, @"\bores?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

internal sealed record TermRef(string Text, string SourceKey);

internal sealed record AssetSourceRoot(string Name, string Path, string Tag);

internal sealed class CorpusBuildStats
{
    public int LanguageCandidates { get; set; }
    public int AssetFilesScanned { get; set; }
    public int AssetCodeCandidates { get; set; }
    public int AssetStateCandidates { get; set; }
    public int GeneratedPhraseCandidates { get; set; }
    public IReadOnlyDictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
}

internal sealed class LanguageFile
{
    private LanguageFile(Dictionary<string, string> entries)
    {
        Entries = entries;
    }

    public Dictionary<string, string> Entries { get; }

    public static LanguageFile Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), options) ?? new Dictionary<string, string>();
        return new LanguageFile(entries);
    }
}

internal sealed class ManualSeedDocument
{
    public List<ManualSeedPack> SeedPacks { get; set; } = new();

    public static ManualSeedDocument Load(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };
        return JsonSerializer.Deserialize<ManualSeedDocument>(File.ReadAllText(path), options) ?? new ManualSeedDocument();
    }

    public IEnumerable<CandidateTerm> ToCandidates()
    {
        foreach (var pack in SeedPacks)
        {
            var tags = pack.Tags.Concat(new[] { "manual-seed-review", pack.Id }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var term in pack.Terms)
            {
                yield return new CandidateTerm(term, "manual-seed-review", pack.Id, tags);
            }

            foreach (var phrase in pack.Phrases)
            {
                yield return new CandidateTerm(phrase, "manual-seed-review", pack.Id, tags.Concat(new[] { "manual-phrase" }).ToArray());
            }
        }
    }
}

internal sealed class ManualSeedPack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Sources { get; set; } = new();
    public List<string> Terms { get; set; } = new();
    public List<string> Phrases { get; set; } = new();
}

internal sealed class OnnxEmbeddingService : IDisposable
{
    private const int MaxTokens = 128;
    private readonly ConcurrentDictionary<string, float[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;

    public OnnxEmbeddingService(string modelPath, string vocabPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Missing ONNX model.", modelPath);
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException("Missing WordPiece vocabulary.", vocabPath);
        }

        NativeOnnxRuntimeResolver.Configure();
        _tokenizer = WordPieceTokenizer.Load(vocabPath);
        _session = new InferenceSession(modelPath);
    }

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var normalized = CandidateCorpus.Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Task.FromResult<float[]?>(null);
        }

        if (_cache.TryGetValue(normalized, out var cached))
        {
            return Task.FromResult<float[]?>(cached);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tokenized = _tokenizer.Tokenize(normalized, MaxTokens);
            using var results = _session.Run(CreateInputs(_session, tokenized));
            var output = results.FirstOrDefault();
            if (output == null)
            {
                return null;
            }

            var vector = NormalizeVector(ExtractSentenceVector(output.AsTensor<float>(), tokenized.AttentionMask));
            if (vector != null)
            {
                _cache[normalized] = vector;
            }

            return vector;
        }, cancellationToken);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private static List<NamedOnnxValue> CreateInputs(InferenceSession session, TokenizedText tokenized)
    {
        var shape = new[] { 1, tokenized.Length };
        var inputs = new List<NamedOnnxValue>();
        foreach (var inputName in session.InputMetadata.Keys)
        {
            if (inputName.Equals("input_ids", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<long>(tokenized.InputIds, shape)));
            }
            else if (inputName.Equals("attention_mask", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<long>(tokenized.AttentionMask, shape)));
            }
            else if (inputName.Equals("token_type_ids", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<long>(tokenized.TokenTypeIds, shape)));
            }
        }

        return inputs;
    }

    private static float[]? ExtractSentenceVector(Tensor<float> tensor, long[] attentionMask)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var values = tensor.ToArray();
        if (dimensions.Length == 2)
        {
            var vector = new float[dimensions[1]];
            Array.Copy(values, vector, vector.Length);
            return vector;
        }

        if (dimensions.Length != 3)
        {
            return null;
        }

        var sequenceLength = dimensions[1];
        var embeddingSize = dimensions[2];
        var pooled = new float[embeddingSize];
        var tokenCount = 0;
        for (var tokenIndex = 0; tokenIndex < sequenceLength && tokenIndex < attentionMask.Length; tokenIndex++)
        {
            if (attentionMask[tokenIndex] == 0)
            {
                continue;
            }

            tokenCount++;
            var offset = tokenIndex * embeddingSize;
            for (var dimension = 0; dimension < embeddingSize; dimension++)
            {
                pooled[dimension] += values[offset + dimension];
            }
        }

        if (tokenCount == 0)
        {
            return null;
        }

        for (var dimension = 0; dimension < pooled.Length; dimension++)
        {
            pooled[dimension] /= tokenCount;
        }

        return pooled;
    }

    private static float[]? NormalizeVector(float[]? vector)
    {
        if (vector == null || vector.Length == 0)
        {
            return null;
        }

        var magnitudeSquared = 0f;
        foreach (var value in vector)
        {
            magnitudeSquared += value * value;
        }

        if (magnitudeSquared <= 0f || float.IsNaN(magnitudeSquared) || float.IsInfinity(magnitudeSquared))
        {
            return null;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        var normalized = new float[vector.Length];
        for (var index = 0; index < vector.Length; index++)
        {
            normalized[index] = vector[index] / magnitude;
        }

        return normalized;
    }
}

internal sealed class StagedSemanticAtlasBuilder
{
    private readonly ExperimentOptions _options;
    private readonly float _seedThreshold;
    private readonly float _evidenceThreshold;
    private readonly float _mergeThreshold;

    public StagedSemanticAtlasBuilder(ExperimentOptions options)
    {
        _options = options;
        _seedThreshold = options.ClusterThreshold;
        _evidenceThreshold = Math.Clamp(options.ClusterThreshold - 0.08f, 0.55f, 0.88f);
        _mergeThreshold = Math.Clamp(options.ClusterThreshold + 0.12f, 0.82f, 0.94f);
    }

    public async Task<StagedClusterResult> BuildAsync(IReadOnlyList<CandidateTerm> candidates, OnnxEmbeddingService embedder)
    {
        var buckets = new List<MutableAtlasBucket>();
        var embedded = new List<EmbeddedCandidate>();
        using var cts = new CancellationTokenSource();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var vector = await embedder.EmbedAsync(candidate.Text, cts.Token).ConfigureAwait(false);
            if (vector != null)
            {
                embedded.Add(new EmbeddedCandidate(candidate, vector, StagedCandidateClassifier.ResolveFamily(candidate), StagedCandidateClassifier.IsSeedCandidate(candidate)));
            }

            if ((index + 1) % 1_000 == 0)
            {
                Console.WriteLine($"Embedded {index + 1:N0}/{candidates.Count:N0}; staged seeds={embedded.Count(item => item.IsSeed):N0}");
            }
        }

        var seedOutliers = 0;
        foreach (var item in SelectRoundRobinByFamily(embedded.Where(item => item.IsSeed), GetSeedPriority))
        {
            var best = FindBestBucket(item, buckets.Where(bucket => bucket.Family == item.Family));
            if (best.Bucket != null && best.Similarity >= _seedThreshold)
            {
                best.Bucket.AddSeed(item.Vector, item.Candidate, best.Similarity);
                continue;
            }

            if (buckets.Count < _options.MaxClusters)
            {
                buckets.Add(new MutableAtlasBucket(buckets.Count, item.Family, item.Vector, item.Candidate));
            }
            else
            {
                seedOutliers++;
            }
        }

        var mergedBuckets = MergeCompatibleBuckets(buckets);
        var evidenceOutliers = 0;
        foreach (var item in SelectRoundRobinByFamily(embedded.Where(item => !item.IsSeed), GetEvidencePriority))
        {
            var familyCandidates = buckets.Where(bucket => bucket.Family == item.Family || CanAttachAcrossFamilies(item.Family, bucket.Family));
            var best = FindBestBucket(item, familyCandidates);
            if (best.Bucket == null || best.Similarity < _evidenceThreshold)
            {
                evidenceOutliers++;
                continue;
            }

            if (!best.Bucket.CanAcceptEvidence(item.Candidate))
            {
                evidenceOutliers++;
                continue;
            }

            best.Bucket.AddEvidence(item.Vector, item.Candidate, best.Similarity);
        }

        var clusters = buckets
            .OrderByDescending(bucket => bucket.Score)
            .ThenByDescending(bucket => bucket.SeedCount)
            .ThenBy(bucket => bucket.Family)
            .Select((bucket, index) => bucket.ToSemanticCluster(index))
            .ToArray();

        var summary = new ClusterRunSummary(
            "staged",
            embedded.Count,
            embedded.Count(item => item.IsSeed),
            embedded.Count(item => !item.IsSeed),
            clusters.Length,
            seedOutliers + evidenceOutliers,
            seedOutliers,
            evidenceOutliers,
            mergedBuckets,
            clusters.GroupBy(cluster => cluster.Family).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

        return new StagedClusterResult(clusters, summary);
    }

    private int MergeCompatibleBuckets(List<MutableAtlasBucket> buckets)
    {
        var mergeCount = 0;
        var merged = true;
        while (merged)
        {
            merged = false;
            var bestLeft = -1;
            var bestRight = -1;
            var bestSimilarity = _mergeThreshold;

            for (var left = 0; left < buckets.Count; left++)
            {
                for (var right = left + 1; right < buckets.Count; right++)
                {
                    if (!CanMergeFamilies(buckets[left].Family, buckets[right].Family))
                    {
                        continue;
                    }

                    var similarity = VectorMath.Dot(buckets[left].Centroid, buckets[right].Centroid);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestLeft = left;
                        bestRight = right;
                    }
                }
            }

            if (bestLeft >= 0 && bestRight >= 0)
            {
                buckets[bestLeft].MergeFrom(buckets[bestRight], bestSimilarity);
                buckets.RemoveAt(bestRight);
                mergeCount++;
                merged = true;
            }
        }

        return mergeCount;
    }

    private static BucketMatch FindBestBucket(EmbeddedCandidate item, IEnumerable<MutableAtlasBucket> buckets)
    {
        MutableAtlasBucket? bestBucket = null;
        var bestSimilarity = -1f;
        foreach (var bucket in buckets)
        {
            var similarity = VectorMath.Dot(item.Vector, bucket.Centroid);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestBucket = bucket;
            }
        }

        return new BucketMatch(bestBucket, bestSimilarity);
    }

    private static IEnumerable<EmbeddedCandidate> SelectRoundRobinByFamily(IEnumerable<EmbeddedCandidate> candidates, Func<EmbeddedCandidate, int> priority)
    {
        var groups = candidates
            .GroupBy(candidate => candidate.Family, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => StagedCandidateClassifier.GetFamilyPriority(group.Key))
            .ThenByDescending(group => group.Count())
            .Select(group => new Queue<EmbeddedCandidate>(group
                .OrderBy(priority)
                .ThenBy(candidate => candidate.Candidate.Source)
                .ThenBy(candidate => candidate.Candidate.Text)))
            .ToArray();

        while (groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups)
            {
                if (group.Count > 0)
                {
                    yield return group.Dequeue();
                }
            }
        }
    }

    private static int GetSeedPriority(EmbeddedCandidate candidate)
    {
        if (candidate.Candidate.Tags.Contains("manual-seed-review")) return 0;
        if (candidate.Candidate.Source.Equals("game-lang", StringComparison.OrdinalIgnoreCase)) return 1;
        if (candidate.Candidate.Source.StartsWith("asset-state:", StringComparison.OrdinalIgnoreCase)) return 2;
        if (candidate.Candidate.Source.StartsWith("asset-code:", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static int GetEvidencePriority(EmbeddedCandidate candidate)
    {
        if (candidate.Candidate.Tags.Contains("manual-phrase")) return 0;
        if (candidate.Candidate.Tags.Contains("generated-action")) return 1;
        if (candidate.Candidate.Source.Equals("game-lang", StringComparison.OrdinalIgnoreCase)) return 2;
        if (candidate.Candidate.Tags.Contains("generated-generic")) return 3;
        return 4;
    }

    private static bool CanAttachAcrossFamilies(string candidateFamily, string bucketFamily)
    {
        return candidateFamily == bucketFamily || GetMergeGroup(candidateFamily) == GetMergeGroup(bucketFamily);
    }

    private static bool CanMergeFamilies(string left, string right)
    {
        return left == right || GetMergeGroup(left) == GetMergeGroup(right);
    }

    private static string GetMergeGroup(string family)
    {
        return family switch
        {
            "metals" or "ores-mining" => "metal-and-ore",
            "stone-building" or "building-materials" or "blocks" => "building",
            "food" or "farming" or "fish" => "food-and-farming",
            "hostile-creatures" or "huntable-creatures" or "domestic-creatures" or "fish" or "creatures" => "creatures",
            "temporal-lore" or "lore" => "lore",
            "npc-social" or "social-roleplay" or "danger-help" => "social",
            _ => family
        };
    }

}

internal sealed class MutableAtlasBucket
{
    private const int MaxSamples = 20;
    private const int MaxSamplesPerSource = 5;
    private readonly Dictionary<string, int> _tagCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _sourceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CandidateTerm> _samples = new();
    private float _weightedCount;
    private float _totalSimilarity;

    public MutableAtlasBucket(int id, string family, float[] centroid, CandidateTerm first)
    {
        Id = id;
        Family = family;
        Centroid = centroid.ToArray();
        Representative = first;
        Count = 1;
        SeedCount = 1;
        _weightedCount = GetCandidateWeight(first, true);
        AddCounts(first);
        AddSample(first);
    }

    public int Id { get; }

    public string Family { get; private set; }

    public float[] Centroid { get; private set; }

    public int Count { get; private set; }

    public int SeedCount { get; private set; }

    public int EvidenceCount { get; private set; }

    public CandidateTerm Representative { get; private set; }

    public float Score => SeedCount * 4f + EvidenceCount + Math.Max(0, AverageSimilarity) * 25f;

    public void AddSeed(float[] vector, CandidateTerm candidate, float similarity)
    {
        Add(vector, candidate, similarity, true);
    }

    public void AddEvidence(float[] vector, CandidateTerm candidate, float similarity)
    {
        Add(vector, candidate, similarity, false);
    }

    public bool CanAcceptEvidence(CandidateTerm candidate)
    {
        if (!candidate.Tags.Contains("generated-generic") && !candidate.Tags.Contains("generated-action"))
        {
            return true;
        }

        var generatedCount = GetTagCount("generated-generic") + GetTagCount("generated-action");
        return generatedCount < Math.Max(4, SeedCount);
    }

    public void MergeFrom(MutableAtlasBucket other, float similarity)
    {
        var totalWeight = _weightedCount + other._weightedCount;
        for (var index = 0; index < Centroid.Length; index++)
        {
            Centroid[index] = (Centroid[index] * _weightedCount + other.Centroid[index] * other._weightedCount) / totalWeight;
        }

        VectorMath.NormalizeInPlace(Centroid);
        _weightedCount = totalWeight;
        Count += other.Count;
        SeedCount += other.SeedCount;
        EvidenceCount += other.EvidenceCount;
        _totalSimilarity += other._totalSimilarity + similarity;
        foreach (var (tag, count) in other._tagCounts)
        {
            _tagCounts[tag] = _tagCounts.TryGetValue(tag, out var existing) ? existing + count : count;
        }

        foreach (var (source, count) in other._sourceCounts)
        {
            _sourceCounts[source] = _sourceCounts.TryGetValue(source, out var existing) ? existing + count : count;
        }

        foreach (var sample in other._samples)
        {
            AddSample(sample);
        }

        Representative = ChooseRepresentative(Representative, other.Representative);
    }

    public SemanticCluster ToSemanticCluster(int id)
    {
        var topTags = _tagCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(12).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var topSources = _sourceCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(8).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var generatedCount = GetTagCount("generated-generic") + GetTagCount("generated-action");
        var templateFraction = Count == 0 ? 0f : generatedCount / (float)Count;
        var tagPurity = Count == 0 || topTags.Count == 0 ? 0f : topTags.Values.Max() / (float)Count;
        var warnings = BuildWarnings(templateFraction, tagPurity).ToArray();
        return new SemanticCluster(
            id,
            Family,
            Count,
            SeedCount,
            EvidenceCount,
            AverageSimilarity,
            Representative.Text,
            topTags,
            topSources,
            templateFraction,
            tagPurity,
            warnings,
            _samples.Select(sample => new ClusterSample(sample.Text, sample.Source, sample.SourceKey, sample.Tags)).ToArray());
    }

    private float AverageSimilarity => Count <= 1 ? 1f : _totalSimilarity / (Count - 1);

    private void Add(float[] vector, CandidateTerm candidate, float similarity, bool seed)
    {
        var weight = GetCandidateWeight(candidate, seed);
        var totalWeight = _weightedCount + weight;
        for (var index = 0; index < Centroid.Length; index++)
        {
            Centroid[index] = (Centroid[index] * _weightedCount + vector[index] * weight) / totalWeight;
        }

        VectorMath.NormalizeInPlace(Centroid);
        _weightedCount = totalWeight;
        Count++;
        if (seed)
        {
            SeedCount++;
        }
        else
        {
            EvidenceCount++;
        }

        _totalSimilarity += similarity;
        AddCounts(candidate);
        AddSample(candidate);
        Representative = ChooseRepresentative(Representative, candidate);
    }

    private static float GetCandidateWeight(CandidateTerm candidate, bool seed)
    {
        if (candidate.Tags.Contains("manual-seed-review") && !candidate.Tags.Contains("manual-phrase")) return 3f;
        if (candidate.Tags.Contains("manual-phrase")) return 0.75f;
        if (seed) return 1.5f;
        if (candidate.Tags.Contains("generated-action")) return 0.35f;
        if (candidate.Tags.Contains("generated-generic")) return 0.15f;
        return 0.5f;
    }

    private static CandidateTerm ChooseRepresentative(CandidateTerm current, CandidateTerm candidate)
    {
        return GetRepresentativePriority(candidate) < GetRepresentativePriority(current) ? candidate : current;
    }

    private static int GetRepresentativePriority(CandidateTerm candidate)
    {
        if (candidate.Tags.Contains("manual-seed-review") && !candidate.Tags.Contains("manual-phrase")) return 0;
        if (!candidate.Source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase) && !candidate.Tags.Contains("generated-generic") && !candidate.Tags.Contains("generated-action")) return 1;
        if (candidate.Tags.Contains("manual-phrase")) return 2;
        if (candidate.Tags.Contains("generated-action")) return 3;
        return 4;
    }

    private void AddCounts(CandidateTerm candidate)
    {
        foreach (var tag in candidate.Tags)
        {
            _tagCounts[tag] = _tagCounts.TryGetValue(tag, out var count) ? count + 1 : 1;
        }

        _sourceCounts[candidate.Source] = _sourceCounts.TryGetValue(candidate.Source, out var sourceCount) ? sourceCount + 1 : 1;
    }

    private void AddSample(CandidateTerm candidate)
    {
        if (_samples.Any(sample => sample.Text.Equals(candidate.Text, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (_samples.Count >= MaxSamples)
        {
            return;
        }

        var currentSourceSamples = _samples.Count(sample => sample.Source.Equals(candidate.Source, StringComparison.OrdinalIgnoreCase));
        if (currentSourceSamples >= MaxSamplesPerSource && !candidate.Tags.Contains("manual-seed-review"))
        {
            return;
        }

        _samples.Add(candidate);
    }

    private int GetTagCount(string tag)
    {
        return _tagCounts.TryGetValue(tag, out var count) ? count : 0;
    }

    private IEnumerable<string> BuildWarnings(float templateFraction, float tagPurity)
    {
        if (SeedCount == 1 && EvidenceCount > 20) yield return "thin-seed";
        if (templateFraction > 0.65f) yield return "template-heavy";
        if (tagPurity < 0.35f) yield return "mixed-tags";
        if (AverageSimilarity < 0.62f) yield return "low-similarity";
        if (_sourceCounts.Count == 1 && Count > 20) yield return "single-source";
    }
}

internal static class VectorMath
{
    public static float Dot(float[] left, float[] right)
    {
        var sum = 0f;
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }

    public static void NormalizeInPlace(float[] vector)
    {
        var magnitudeSquared = 0f;
        foreach (var value in vector)
        {
            magnitudeSquared += value * value;
        }

        if (magnitudeSquared <= 0f)
        {
            return;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= magnitude;
        }
    }
}

internal sealed record EmbeddedCandidate(CandidateTerm Candidate, float[] Vector, string Family, bool IsSeed);

internal sealed record BucketMatch(MutableAtlasBucket? Bucket, float Similarity);

internal sealed class GreedySemanticClusterer
{
    private readonly int _maxClusters;
    private readonly float _threshold;

    public GreedySemanticClusterer(int maxClusters, float threshold)
    {
        _maxClusters = maxClusters;
        _threshold = threshold;
    }

    public async Task<IReadOnlyList<SemanticCluster>> ClusterAsync(IReadOnlyList<CandidateTerm> candidates, OnnxEmbeddingService embedder, ExperimentOptions options)
    {
        var clusters = new List<MutableCluster>();
        using var cts = new CancellationTokenSource();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var vector = await embedder.EmbedAsync(candidate.Text, cts.Token).ConfigureAwait(false);
            if (vector == null)
            {
                continue;
            }

            var bestCluster = -1;
            var bestSimilarity = -1f;
            for (var clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                var similarity = Dot(vector, clusters[clusterIndex].Centroid);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestCluster = clusterIndex;
                }
            }

            if ((bestCluster < 0 || bestSimilarity < _threshold) && clusters.Count < _maxClusters)
            {
                clusters.Add(new MutableCluster(clusters.Count, vector, candidate));
            }
            else if (bestCluster >= 0)
            {
                clusters[bestCluster].Add(vector, candidate, bestSimilarity);
            }

            if ((index + 1) % 1_000 == 0)
            {
                Console.WriteLine($"Embedded {index + 1:N0}/{candidates.Count:N0}; clusters={clusters.Count:N0}");
            }
        }

        return clusters
            .OrderByDescending(cluster => cluster.Count)
            .ThenBy(cluster => cluster.Id)
            .Select(cluster => cluster.ToSemanticCluster())
            .ToArray();
    }

    private static float Dot(float[] left, float[] right)
    {
        var sum = 0f;
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }
}

internal sealed class MutableCluster
{
    private readonly Dictionary<string, int> _tagCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CandidateTerm> _samples = new();
    private float _totalSimilarity;

    public MutableCluster(int id, float[] centroid, CandidateTerm first)
    {
        Id = id;
        Centroid = centroid.ToArray();
        Representative = first;
        AddTags(first.Tags);
        _samples.Add(first);
        Count = 1;
    }

    public int Id { get; }

    public float[] Centroid { get; private set; }

    public int Count { get; private set; }

    public CandidateTerm Representative { get; private set; }

    public void Add(float[] vector, CandidateTerm candidate, float similarity)
    {
        Count++;
        _totalSimilarity += similarity;
        var previousWeight = Count - 1;
        for (var index = 0; index < Centroid.Length; index++)
        {
            Centroid[index] = (Centroid[index] * previousWeight + vector[index]) / Count;
        }

        NormalizeInPlace(Centroid);
        if (similarity > AverageSimilarity)
        {
            Representative = candidate;
        }

        if (_samples.Count < 16)
        {
            _samples.Add(candidate);
        }

        AddTags(candidate.Tags);
    }

    public SemanticCluster ToSemanticCluster()
    {
        var topTags = _tagCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(12).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var topTagCount = topTags.Count == 0 ? 0 : topTags.Values.Max();
        return new SemanticCluster(
            Id,
            "greedy",
            Count,
            Count,
            0,
            AverageSimilarity,
            Representative.Text,
            topTags,
            new Dictionary<string, int> { [Representative.Source] = Count },
            Count == 0 ? 0f : _samples.Count(sample => sample.Tags.Contains("generated-generic") || sample.Tags.Contains("generated-action")) / (float)_samples.Count,
            Count == 0 ? 0f : topTagCount / (float)Count,
            Array.Empty<string>(),
            _samples.Select(sample => new ClusterSample(sample.Text, sample.Source, sample.SourceKey, sample.Tags)).ToArray());
    }

    private float AverageSimilarity => Count <= 1 ? 1f : _totalSimilarity / (Count - 1);

    private void AddTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            _tagCounts[tag] = _tagCounts.TryGetValue(tag, out var count) ? count + 1 : 1;
        }
    }

    private static void NormalizeInPlace(float[] vector)
    {
        var magnitudeSquared = 0f;
        foreach (var value in vector)
        {
            magnitudeSquared += value * value;
        }

        if (magnitudeSquared <= 0f)
        {
            return;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= magnitude;
        }
    }
}

internal sealed record SemanticCluster(
    int Id,
    string Family,
    int Count,
    int SeedCount,
    int EvidenceCount,
    float AverageSimilarity,
    string Representative,
    IReadOnlyDictionary<string, int> TopTags,
    IReadOnlyDictionary<string, int> TopSources,
    float TemplateFraction,
    float TagPurity,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ClusterSample> Samples);

internal sealed record ClusterSample(string Text, string Source, string SourceKey, string[] Tags);

internal sealed record ClusterRunSummary(
    string Mode,
    int EmbeddedCandidates,
    int SeedCandidates,
    int EvidenceCandidates,
    int ClusterCount,
    int Outliers,
    int SeedOutliers,
    int EvidenceOutliers,
    int MergedBuckets,
    IReadOnlyDictionary<string, int> FamilyCounts)
{
    public static ClusterRunSummary ForGreedy(int embeddedCandidates, int clusterCount)
    {
        return new ClusterRunSummary("greedy", embeddedCandidates, embeddedCandidates, 0, clusterCount, 0, 0, 0, 0, new Dictionary<string, int> { ["greedy"] = clusterCount });
    }
}

internal sealed record StagedClusterResult(IReadOnlyList<SemanticCluster> Clusters, ClusterRunSummary Summary);

internal sealed record RuntimeAtlasDocument(string AtlasId, string DisplayName, string Version, IReadOnlyList<RuntimeAtlasBucket> Buckets);

internal sealed record RuntimeAtlasBucket(string Id, string Label, string Alias, string Family, IReadOnlyList<string> Tags, IReadOnlyList<string> Examples);

internal sealed record RuntimeAtlasBucketReview(
    string Id,
    string Label,
    string Family,
    int SourceClusterId,
    int Count,
    int SeedCount,
    int EvidenceCount,
    float AverageSimilarity,
    float TemplateFraction,
    float TagPurity,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, int> TopSources,
    IReadOnlyList<string> Examples,
    string CurationTier,
    float CurationScore,
    IReadOnlyList<string> CurationReasons);

internal sealed record RuntimeAtlasValidationReport(
    string AtlasId,
    int BucketCount,
    int SourceClusterCount,
    int EmptyLabelCount,
    int EmptyExampleCount,
    int DuplicateIdCount,
    IReadOnlyDictionary<string, int> FamilyCounts,
    IReadOnlyDictionary<string, int> WarningCounts,
    IReadOnlyDictionary<string, int> SourceCounts,
    IReadOnlyList<string> DuplicateIds,
    int ReviewIssueCount,
    IReadOnlyList<RuntimeAtlasBucketReview> ReviewBuckets);

internal sealed record RuntimeAtlasCurationReport(
    string Mode,
    int SourceClusterCount,
    int CoreCandidateCount,
    int ExportedCoreBucketCount,
    int NeedsReviewCount,
    int ExcludedCount,
    IReadOnlyDictionary<string, int> TierCounts,
    IReadOnlyDictionary<string, int> ReasonCounts,
    IReadOnlyDictionary<string, int> CoreFamilyCounts,
    IReadOnlyList<RuntimeAtlasBucketReview> CoreCandidates,
    IReadOnlyList<RuntimeAtlasBucketReview> NeedsReview,
    IReadOnlyList<RuntimeAtlasBucketReview> Excluded);

internal sealed record RuntimeAtlasExport(RuntimeAtlasDocument Document, RuntimeAtlasValidationReport Validation, RuntimeAtlasCurationReport Curation, IReadOnlyList<RuntimeAtlasBucketReview> ReviewBuckets);

internal sealed record CuratedRuntimeAtlasEntry(SemanticCluster Cluster, RuntimeAtlasBucket Bucket, RuntimeAtlasBucketReview Review);

internal sealed record RuntimeAtlasCurationDecision(string Tier, float Score, IReadOnlyList<string> Reasons);

internal static class RuntimeAtlasExporter
{
    private static readonly Regex CollapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static RuntimeAtlasExport Build(ExperimentOptions options, IReadOnlyList<SemanticCluster> clusters)
    {
        var entries = CoalesceEquivalentEntries(BuildCuratedEntries(options, clusters).ToArray(), options.RuntimeAtlasExamplesPerBucket).ToArray();
        var selectedEntries = SelectRuntimeAtlasEntries(options, entries).ToArray();
        var buckets = selectedEntries.Select(entry => entry.Bucket).ToArray();
        var reviewBuckets = selectedEntries.Select(entry => entry.Review).ToArray();
        var curation = BuildCurationReport(options, clusters.Count, entries, selectedEntries);

        var document = new RuntimeAtlasDocument(options.RuntimeAtlasId, options.RuntimeAtlasDisplayName, options.RuntimeAtlasVersion, buckets);
        var validation = BuildValidation(document, clusters.Count, reviewBuckets);
        return new RuntimeAtlasExport(document, validation, curation, reviewBuckets);
    }

    private static IEnumerable<CuratedRuntimeAtlasEntry> BuildCuratedEntries(ExperimentOptions options, IReadOnlyList<SemanticCluster> clusters)
    {
        foreach (var (cluster, index) in clusters.Select((cluster, index) => (cluster, index)))
        {
            var label = ChooseLabel(cluster, index);
            var baseId = NormalizeIdentifier($"{cluster.Family}-{label}");
            if (string.IsNullOrWhiteSpace(baseId))
            {
                baseId = $"bucket-{index + 1:0000}";
            }

            var id = baseId;
            var examples = ChooseExamples(cluster, label, options.RuntimeAtlasExamplesPerBucket);
            var tags = ChooseTags(cluster).ToArray();
            var curation = CurateCluster(cluster, label, examples);
            var bucket = new RuntimeAtlasBucket(id, label, id, cluster.Family, tags, examples);
            yield return new CuratedRuntimeAtlasEntry(cluster, bucket, new RuntimeAtlasBucketReview(
                id,
                label,
                cluster.Family,
                cluster.Id,
                cluster.Count,
                cluster.SeedCount,
                cluster.EvidenceCount,
                cluster.AverageSimilarity,
                cluster.TemplateFraction,
                cluster.TagPurity,
                cluster.Warnings,
                cluster.TopSources,
                examples,
                curation.Tier,
                curation.Score,
                curation.Reasons));
        }
    }

    private static IEnumerable<CuratedRuntimeAtlasEntry> CoalesceEquivalentEntries(IReadOnlyList<CuratedRuntimeAtlasEntry> entries, int maxExamples)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in entries.GroupBy(entry => $"{entry.Review.Family}\u001f{NormalizeIdentifier(entry.Review.Label)}", StringComparer.OrdinalIgnoreCase))
        {
            var merged = group.Count() == 1 ? group.First() : MergeEntries(group.ToArray(), maxExamples);
            var baseId = NormalizeIdentifier($"{merged.Review.Family}-{merged.Review.Label}");
            var id = MakeUniqueIdentifier(string.IsNullOrWhiteSpace(baseId) ? merged.Bucket.Id : baseId, usedIds);
            var bucket = merged.Bucket with { Id = id, Alias = id };
            var review = merged.Review with { Id = id };
            yield return merged with { Bucket = bucket, Review = review };
        }
    }

    private static CuratedRuntimeAtlasEntry MergeEntries(IReadOnlyList<CuratedRuntimeAtlasEntry> entries, int maxExamples)
    {
        var primary = entries.OrderByDescending(entry => entry.Review.CurationScore).First();
        var examples = entries
            .SelectMany(entry => entry.Review.Examples)
            .Where(example => !string.IsNullOrWhiteSpace(example))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxExamples)
            .ToArray();
        var tags = entries
            .SelectMany(entry => entry.Bucket.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
        var topSources = entries
            .SelectMany(entry => entry.Review.TopSources)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(pair => pair.Value))
            .ThenBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value), StringComparer.OrdinalIgnoreCase);
        var warnings = entries
            .SelectMany(entry => entry.Review.Warnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var curationReasons = entries
            .SelectMany(entry => entry.Review.CurationReasons)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalCount = entries.Sum(entry => entry.Review.Count);
        var averageSimilarity = totalCount == 0 ? primary.Review.AverageSimilarity : entries.Sum(entry => entry.Review.AverageSimilarity * entry.Review.Count) / totalCount;
        var templateFraction = totalCount == 0 ? primary.Review.TemplateFraction : entries.Sum(entry => entry.Review.TemplateFraction * entry.Review.Count) / totalCount;
        var tagPurity = totalCount == 0 ? primary.Review.TagPurity : entries.Sum(entry => entry.Review.TagPurity * entry.Review.Count) / totalCount;
        var tier = MergeTier(entries.Select(entry => entry.Review.CurationTier));
        var score = entries.Max(entry => entry.Review.CurationScore) + MathF.Min(12f, MathF.Log2(entries.Count + 1) * 3f);
        var bucket = primary.Bucket with { Tags = tags, Examples = examples };
        var review = primary.Review with
        {
            Count = totalCount,
            SeedCount = entries.Sum(entry => entry.Review.SeedCount),
            EvidenceCount = entries.Sum(entry => entry.Review.EvidenceCount),
            AverageSimilarity = averageSimilarity,
            TemplateFraction = templateFraction,
            TagPurity = tagPurity,
            Warnings = warnings,
            TopSources = topSources,
            Examples = examples,
            CurationTier = tier,
            CurationScore = score,
            CurationReasons = curationReasons
        };

        return primary with { Bucket = bucket, Review = review };
    }

    private static string MergeTier(IEnumerable<string> tiers)
    {
        var tierArray = tiers.ToArray();
        if (tierArray.Any(tier => tier.Equals("excluded", StringComparison.OrdinalIgnoreCase))) return "excluded";
        if (tierArray.Any(tier => tier.Equals("needs-review", StringComparison.OrdinalIgnoreCase))) return "needs-review";
        return "core-candidate";
    }

    private static IEnumerable<CuratedRuntimeAtlasEntry> SelectRuntimeAtlasEntries(ExperimentOptions options, IReadOnlyList<CuratedRuntimeAtlasEntry> entries)
    {
        var limit = Math.Min(options.RuntimeAtlasBucketLimit, options.RuntimeAtlasCurationMode.Equals("raw", StringComparison.OrdinalIgnoreCase) ? options.RuntimeAtlasBucketLimit : options.RuntimeAtlasTargetBuckets);
        if (options.RuntimeAtlasCurationMode.Equals("raw", StringComparison.OrdinalIgnoreCase))
        {
            return entries.Take(limit);
        }

        return SelectCoreEntries(entries, limit);
    }

    private static IEnumerable<CuratedRuntimeAtlasEntry> SelectCoreEntries(IEnumerable<CuratedRuntimeAtlasEntry> entries, int limit)
    {
        var selected = new List<CuratedRuntimeAtlasEntry>(limit);
        var familyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries
                     .Where(entry => entry.Review.CurationTier.Equals("core-candidate", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(entry => entry.Review.CurationScore)
                     .ThenBy(entry => GetFamilyPriority(entry.Review.Family))
                     .ThenBy(entry => entry.Review.Label, StringComparer.OrdinalIgnoreCase))
        {
            if (selected.Count >= limit)
            {
                break;
            }

            var family = entry.Review.Family;
            var count = familyCounts.TryGetValue(family, out var current) ? current : 0;
            if (count >= GetCoreFamilyCap(family))
            {
                continue;
            }

            familyCounts[family] = count + 1;
            selected.Add(entry);
        }

        return selected
            .OrderBy(entry => GetFamilyPriority(entry.Review.Family))
            .ThenByDescending(entry => entry.Review.CurationScore)
            .ThenBy(entry => entry.Review.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RuntimeAtlasCurationReport BuildCurationReport(ExperimentOptions options, int sourceClusterCount, IReadOnlyList<CuratedRuntimeAtlasEntry> entries, IReadOnlyList<CuratedRuntimeAtlasEntry> selectedEntries)
    {
        var reviews = entries.Select(entry => entry.Review).ToArray();
        var selectedReviews = selectedEntries.Select(entry => entry.Review).ToArray();
        var needsReview = reviews.Where(review => review.CurationTier.Equals("needs-review", StringComparison.OrdinalIgnoreCase)).ToArray();
        var excluded = reviews.Where(review => review.CurationTier.Equals("excluded", StringComparison.OrdinalIgnoreCase)).ToArray();
        return new RuntimeAtlasCurationReport(
            options.RuntimeAtlasCurationMode,
            sourceClusterCount,
            reviews.Count(review => review.CurationTier.Equals("core-candidate", StringComparison.OrdinalIgnoreCase)),
            selectedReviews.Length,
            needsReview.Length,
            excluded.Length,
            reviews.GroupBy(review => review.CurationTier, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            reviews.SelectMany(review => review.CurationReasons).GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            selectedReviews.GroupBy(review => review.Family, StringComparer.OrdinalIgnoreCase).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            selectedReviews,
            needsReview,
            excluded);
    }

    private static RuntimeAtlasCurationDecision CurateCluster(SemanticCluster cluster, string label, IReadOnlyList<string> examples)
    {
        var reasons = new List<string>();
        var combined = BuildCombinedReviewText(cluster, label, examples);

        AddHardExclusionReasons(combined, label, cluster.Family, reasons);
        if (reasons.Count > 0)
        {
            return new RuntimeAtlasCurationDecision("excluded", ScoreCluster(cluster, reasons), reasons);
        }

        if (cluster.Family.Equals("misc", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("misc-family-needs-review");
        }

        if (cluster.Warnings.Count > 0)
        {
            reasons.AddRange(cluster.Warnings.Select(warning => $"cluster-warning:{warning}"));
        }

        if (IsLowValueSingleSourceCluster(cluster, label))
        {
            reasons.Add("low-value-single-source-variant-cluster");
        }

        var score = ScoreCluster(cluster, reasons);
        var tier = reasons.Count == 0 ? "core-candidate" : "needs-review";
        return new RuntimeAtlasCurationDecision(tier, score, reasons);
    }

    private static void AddHardExclusionReasons(string combined, string label, string family, ICollection<string> reasons)
    {
        if (ContainsAny(combined, "creativeblock", "creative block", "debug", "placeholder", "accessory randomizer", "randomizer"))
        {
            reasons.Add("creative-debug-placeholder-asset");
        }

        var lowerLabel = label.ToLowerInvariant();
        if (lowerLabel is "drawing" or "how to repair" or "how to craft" or "how to grind" or "how to make glass" or "carburizes into" or "smelts into" or "pulverizes into" or "squeezes into" or "grinds into" or "uses" or "material" or "fertility" or "allowed" or "disallowed" or "visible" or "invisible" or "infinite" or "on" or "off" or "from" or "of" or "health to" or "satiety to")
        {
            reasons.Add("generic-ui-or-recipe-relation-label");
        }

        if (Regex.IsMatch(lowerLabel, @"^(?:health|satiety):?\s+to$", RegexOptions.CultureInvariant))
        {
            reasons.Add("generic-ui-or-recipe-relation-label");
        }

        if (IsLongSentenceFragment(lowerLabel) || ContainsAny(lowerLabel, "can be used", "see the ", "to display ", "in addition to", "minimum and maximum", "optional ingredients", "baking instructions", "cooking recipes", "pie recipes", "recovered a piece", "it's been added", "use a quern to"))
        {
            reasons.Add("ui-or-lore-sentence-fragment");
        }

        if (Regex.IsMatch(lowerLabel, @"^\d+\s+times$", RegexOptions.CultureInvariant) || ContainsAny(lowerLabel, "increase strength/frequency", "capped at", "approx. every", "temporal storm length", "respawn uses", "cost to repair", "configure how"))
        {
            reasons.Add("world-config-slider-label");
        }

        if (lowerLabel.StartsWith("you ", StringComparison.Ordinal) || lowerLabel.StartsWith("you are ", StringComparison.Ordinal) || lowerLabel.StartsWith("you will ", StringComparison.Ordinal) || lowerLabel.StartsWith("out of power", StringComparison.Ordinal) || lowerLabel.StartsWith("the temporal gear allows", StringComparison.Ordinal))
        {
            reasons.Add("ui-sentence-label");
        }

        if (family.Equals("hostile-creatures", StringComparison.OrdinalIgnoreCase) &&
            (ContainsAny(lowerLabel, "beard", "figurehead", "withwindup", "spawns locust", "metal spikes", "painting", "wolframite", "tungsten ore", "nugget", "face wolfskull") || lowerLabel.StartsWith("face ", StringComparison.Ordinal)))
        {
            reasons.Add("hostile-family-substring-noise");
        }

        if (family.Equals("npc-social", StringComparison.OrdinalIgnoreCase) &&
            (ContainsAny(lowerLabel, "bodyskin", "iris", "tatoo", "tattoo", "warmhat", "blade falx", "clothes ", "normal1", "normal2", "normal3", "normal4", "normal5") || lowerLabel.StartsWith("head ", StringComparison.Ordinal) || lowerLabel.StartsWith("face ", StringComparison.Ordinal) || lowerLabel.StartsWith("hair ", StringComparison.Ordinal) || lowerLabel.StartsWith("lips ", StringComparison.Ordinal) || lowerLabel.StartsWith("eyebrows ", StringComparison.Ordinal) || lowerLabel.StartsWith("foot ", StringComparison.Ordinal) || lowerLabel.StartsWith("hand ", StringComparison.Ordinal) || lowerLabel.StartsWith("skin ", StringComparison.Ordinal) || lowerLabel.StartsWith("mustache ", StringComparison.Ordinal) || lowerLabel.StartsWith("lowerbody ", StringComparison.Ordinal) || lowerLabel.StartsWith("shoulder ", StringComparison.Ordinal) || lowerLabel.StartsWith("upperbody ", StringComparison.Ordinal) || lowerLabel.StartsWith("sclera ", StringComparison.Ordinal) || lowerLabel.StartsWith("faceextra ", StringComparison.Ordinal) || lowerLabel.StartsWith("waist ", StringComparison.Ordinal) || lowerLabel.StartsWith("arm ", StringComparison.Ordinal) || lowerLabel.StartsWith("neck ", StringComparison.Ordinal)))
        {
            reasons.Add("npc-cosmetic-or-tradelist-noise");
        }

        if (family.Equals("npc-social", StringComparison.OrdinalIgnoreCase) &&
            (ContainsAny(lowerLabel, "arrow ", "axe ", "hammer ", "hoe ", "pickaxe ", "saw ", "door ", "burnedplanks", "clayplanter", "planks ", "charred", "scorched") || lowerLabel.EndsWith(" merchandise", StringComparison.Ordinal)))
        {
            reasons.Add("npc-trader-inventory-noise");
        }

        if (Regex.IsMatch(lowerLabel, @"^clothes\s+(?:nadiya|commoner|malefactor|blackguard|hunter|guard)\s+(?:face|foot|lowerbody|upperbody|neck|arm|head|shoulder)\b", RegexOptions.CultureInvariant))
        {
            reasons.Add("asset-code-clothing-slot-noise");
        }
    }

    private static bool IsLongSentenceFragment(string lowerLabel)
    {
        if (lowerLabel.Length < 60)
        {
            return false;
        }

        return Regex.Matches(lowerLabel, @"\b[\p{L}\p{N}']+\b", RegexOptions.CultureInvariant).Count >= 9;
    }

    private static bool IsLowValueSingleSourceCluster(SemanticCluster cluster, string label)
    {
        if (!cluster.Warnings.Contains("single-source"))
        {
            return false;
        }

        var lower = label.ToLowerInvariant();
        return cluster.Family is "blocks" or "items" or "clothing" or "building-materials" &&
               (ContainsAny(lower, "creative", "randomizer", "coral", "roof", "brick", "shingle", "ashlar") || cluster.Count > 60);
    }

    private static float ScoreCluster(SemanticCluster cluster, IReadOnlyCollection<string> reasons)
    {
        var score = GetFamilyBaseScore(cluster.Family);
        score += MathF.Min(30f, MathF.Log2(Math.Max(1, cluster.Count)) * 5f);
        score += MathF.Min(20f, cluster.SeedCount / 8f);
        score += MathF.Min(12f, cluster.EvidenceCount / 4f);
        score += cluster.AverageSimilarity * 16f;
        score += MathF.Min(8f, cluster.TopSources.Count * 2f);

        if (cluster.TopTags.ContainsKey("manual-seed-review")) score += 30f;
        if (cluster.TopTags.ContainsKey("generated-action")) score += 5f;
        if (cluster.TopTags.ContainsKey("temporal") || cluster.TopTags.ContainsKey("lore")) score += 10f;
        if (cluster.TopTags.ContainsKey("mob")) score += 8f;
        if (cluster.TopTags.ContainsKey("block-help") || cluster.TopTags.ContainsKey("item-description")) score -= 18f;

        score -= reasons.Count(reason => reason.StartsWith("cluster-warning:", StringComparison.OrdinalIgnoreCase)) * 8f;
        if (reasons.Contains("low-value-single-source-variant-cluster")) score -= 18f;
        if (reasons.Contains("misc-family-needs-review")) score -= 35f;
        if (reasons.Any(reason => reason.Contains("noise", StringComparison.OrdinalIgnoreCase) || reason.Contains("placeholder", StringComparison.OrdinalIgnoreCase))) score -= 100f;

        return score;
    }

    private static float GetFamilyBaseScore(string family)
    {
        return family switch
        {
            "temporal-lore" => 90f,
            "hostile-creatures" => 82f,
            "danger-help" => 78f,
            "social-roleplay" => 76f,
            "crafting-survival" => 74f,
            "lore" => 72f,
            "farming" => 68f,
            "food" => 66f,
            "metals" => 65f,
            "ores-mining" => 64f,
            "npc-social" => 60f,
            "building-materials" => 54f,
            "equipment" => 52f,
            "fish" => 50f,
            "community-mods" => 48f,
            "items" => 42f,
            "blocks" => 38f,
            "clothing" => 34f,
            "misc" => 10f,
            _ => 24f
        };
    }

    private static int GetFamilyPriority(string family)
    {
        return family switch
        {
            "temporal-lore" => 0,
            "hostile-creatures" => 1,
            "danger-help" => 2,
            "social-roleplay" => 3,
            "crafting-survival" => 4,
            "lore" => 5,
            "farming" => 6,
            "food" => 7,
            "metals" => 8,
            "ores-mining" => 9,
            "npc-social" => 10,
            "building-materials" => 11,
            "equipment" => 12,
            "fish" => 13,
            "community-mods" => 14,
            "items" => 15,
            "blocks" => 16,
            "clothing" => 17,
            "misc" => 18,
            _ => 30
        };
    }

    private static int GetCoreFamilyCap(string family)
    {
        return family switch
        {
            "temporal-lore" => 64,
            "hostile-creatures" => 45,
            "danger-help" => 30,
            "social-roleplay" => 30,
            "crafting-survival" => 30,
            "lore" => 45,
            "farming" => 45,
            "food" => 55,
            "metals" => 55,
            "ores-mining" => 45,
            "npc-social" => 35,
            "building-materials" => 45,
            "equipment" => 30,
            "fish" => 25,
            "community-mods" => 20,
            "items" => 35,
            "blocks" => 30,
            "clothing" => 25,
            "misc" => 0,
            _ => 10
        };
    }

    private static string BuildCombinedReviewText(SemanticCluster cluster, string label, IReadOnlyList<string> examples)
    {
        return string.Join(" ", new[] { label, cluster.Representative, cluster.Family }
            .Concat(cluster.TopTags.Keys)
            .Concat(cluster.TopSources.Keys)
            .Concat(examples)
            .Concat(cluster.Samples.Select(sample => sample.Text))
            .Concat(cluster.Samples.Select(sample => sample.SourceKey)))
            .ToLowerInvariant();
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }

    private static RuntimeAtlasValidationReport BuildValidation(RuntimeAtlasDocument document, int sourceClusterCount, IReadOnlyList<RuntimeAtlasBucketReview> reviewBuckets)
    {
        var duplicateIds = document.Buckets
            .GroupBy(bucket => bucket.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var warningCounts = reviewBuckets
            .SelectMany(bucket => bucket.Warnings)
            .GroupBy(warning => warning, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var sourceCounts = reviewBuckets
            .SelectMany(bucket => bucket.TopSources)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(pair => pair.Value))
            .ThenBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value), StringComparer.OrdinalIgnoreCase);

        var reviewIssueBuckets = reviewBuckets
            .Where(bucket => bucket.Warnings.Count > 0 || bucket.Examples.Count == 0 || string.IsNullOrWhiteSpace(bucket.Label))
            .ToArray();

        return new RuntimeAtlasValidationReport(
            document.AtlasId,
            document.Buckets.Count,
            sourceClusterCount,
            document.Buckets.Count(bucket => string.IsNullOrWhiteSpace(bucket.Label)),
            document.Buckets.Count(bucket => bucket.Examples.Count == 0),
            duplicateIds.Length,
            document.Buckets.GroupBy(bucket => bucket.Family, StringComparer.OrdinalIgnoreCase).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            warningCounts,
            sourceCounts,
            duplicateIds,
            reviewIssueBuckets.Length,
            reviewIssueBuckets.Take(250).ToArray());
    }

    private static string MakeUniqueIdentifier(string baseId, HashSet<string> usedIds)
    {
        var id = baseId;
        var suffix = 2;
        while (!usedIds.Add(id))
        {
            id = $"{baseId}-{suffix++}";
        }

        return id;
    }

    private static string ChooseLabel(SemanticCluster cluster, int index)
    {
        var sample = cluster.Samples
            .Select((sample, sampleIndex) => new { Sample = sample, Index = sampleIndex, Text = NormalizeLabelText(sample.Text) })
            .Where(item => IsGoodLabel(item.Text))
            .OrderBy(item => GetLabelPriority(item.Sample))
            .ThenBy(item => item.Index)
            .Select(item => item.Text)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(sample))
        {
            return CanonicalizeBucketLabel(cluster.Family, sample);
        }

        var representative = NormalizeLabelText(cluster.Representative);
        return string.IsNullOrWhiteSpace(representative) ? $"Bucket {index + 1:0000}" : CanonicalizeBucketLabel(cluster.Family, representative);
    }

    private static string CanonicalizeBucketLabel(string family, string label)
    {
        var cleaned = NormalizeLabelText(label);
        var lower = cleaned.ToLowerInvariant();
        if (family.Equals("temporal-lore", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsAny(lower, "static translocator", "broken static translocator", "repaired static translocator", "ruined static translocator")) return "static translocator";
            if (Regex.IsMatch(lower, @"\btemporal\s+gears?\b", RegexOptions.CultureInvariant)) return "temporal gear";
            if (Regex.IsMatch(lower, @"\btemporal\s+storms?\b", RegexOptions.CultureInvariant)) return "temporal storm";
            if (ContainsAny(lower, "resonance archive")) return "resonance archive";
        }

        if (family.Equals("hostile-creatures", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(lower, @"\bdrifters?\b", RegexOptions.CultureInvariant)) return "drifter";
            if (Regex.IsMatch(lower, @"\blocust\s+nests?\b", RegexOptions.CultureInvariant)) return "locust nest";
            if (Regex.IsMatch(lower, @"\blocusts?\b", RegexOptions.CultureInvariant)) return "locust";
            if (Regex.IsMatch(lower, @"\bbowtorn\b", RegexOptions.CultureInvariant)) return "bowtorn";
            if (Regex.IsMatch(lower, @"\bhyenas?\b", RegexOptions.CultureInvariant)) return "hyena";
            if (Regex.IsMatch(lower, @"\bpanda\s+bears?\b", RegexOptions.CultureInvariant)) return "panda bear";
            if (Regex.IsMatch(lower, @"\bbears?\b", RegexOptions.CultureInvariant)) return "bear";
            if (Regex.IsMatch(lower, @"\bwolves?\b", RegexOptions.CultureInvariant)) return "wolf";
        }

        if (family.Equals("equipment", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(lower, @"\bfalx\b", RegexOptions.CultureInvariant))
        {
            return "falx";
        }

        cleaned = Regex.Replace(cleaned, @"^Dead\s+(?:female|male)?\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        cleaned = Regex.Replace(cleaned, @"\s+\((?:female|male)\)$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        return cleaned;
    }

    private static IReadOnlyList<string> ChooseExamples(SemanticCluster cluster, string label, int maxExamples)
    {
        var examples = new List<string>();
        AddExample(label);
        AddExample(cluster.Representative);

        foreach (var sample in cluster.Samples
                     .OrderBy(GetExamplePriority)
                     .ThenBy(sample => sample.Source)
                     .ThenBy(sample => sample.Text))
        {
            AddExample(sample.Text);
            if (examples.Count >= maxExamples)
            {
                break;
            }
        }

        return examples;

        void AddExample(string value)
        {
            if (examples.Count >= maxExamples)
            {
                return;
            }

            var cleaned = CandidateCorpusBuilder.CleanText(value);
            if (string.IsNullOrWhiteSpace(cleaned) || IsNoisyExample(cleaned) || cleaned.Length > 180 || examples.Any(existing => existing.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            examples.Add(cleaned);
        }
    }

    private static bool IsNoisyExample(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains("hacked", StringComparison.Ordinal) ||
               lower.Contains("creativeblock", StringComparison.Ordinal) ||
               lower.Contains("creative block", StringComparison.Ordinal) ||
               lower.Contains("randomizer", StringComparison.Ordinal) ||
               lower.Contains("storm length", StringComparison.Ordinal) ||
               lower.Contains("respawn uses", StringComparison.Ordinal) ||
               lower.Contains("cost to repair", StringComparison.Ordinal) ||
               lower.Contains("configure how", StringComparison.Ordinal) ||
               lower.Contains("spawns locust", StringComparison.Ordinal) ||
               lower.Contains("import/world generation", StringComparison.Ordinal) ||
               Regex.IsMatch(lower, @"^\d+\s+times$", RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> ChooseTags(SemanticCluster cluster)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in new[] { cluster.Family }.Concat(cluster.TopTags.Keys).Select(NormalizeIdentifier).Where(tag => tag.Length > 0).Take(13))
        {
            if (emitted.Add(tag))
            {
                yield return tag;
            }
        }
    }

    private static string NormalizeLabelText(string value)
    {
        var cleaned = CandidateCorpusBuilder.CleanText(value);
        cleaned = CollapseWhitespaceRegex.Replace(cleaned, " ").Trim();
        if (cleaned.Length <= 80)
        {
            return cleaned;
        }

        var truncated = cleaned[..80].Trim();
        var lastSpace = truncated.LastIndexOf(' ');
        return lastSpace > 32 ? truncated[..lastSpace].Trim() : truncated;
    }

    private static bool IsGoodLabel(string text)
    {
        if (text.Length is < 2 or > 80)
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        if (lower.StartsWith("you ", StringComparison.Ordinal) || lower.StartsWith("player ", StringComparison.Ordinal) || lower.StartsWith("when ", StringComparison.Ordinal))
        {
            return false;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 9;
    }

    private static int GetLabelPriority(ClusterSample sample)
    {
        if (sample.Tags.Contains("manual-seed-review") && !sample.Tags.Contains("manual-phrase")) return 0;
        if (!sample.Source.StartsWith("phrase:", StringComparison.OrdinalIgnoreCase) && !sample.Tags.Contains("generated-generic") && !sample.Tags.Contains("generated-action")) return 1;
        if (sample.Tags.Contains("manual-phrase")) return 2;
        if (sample.Tags.Contains("generated-action")) return 3;
        return 4;
    }

    private static int GetExamplePriority(ClusterSample sample)
    {
        if (sample.Tags.Contains("manual-phrase")) return 0;
        if (sample.Tags.Contains("manual-seed-review")) return 1;
        if (sample.Tags.Contains("generated-action")) return 2;
        if (sample.Source.Equals("game-lang", StringComparison.OrdinalIgnoreCase)) return 3;
        if (sample.Tags.Contains("generated-generic")) return 4;
        return 5;
    }
}

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions RuntimeAtlasJsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void WriteCandidateArtifacts(ExperimentOptions options, CandidateCorpus corpus, LanguageFile language, CorpusBuildStats stats, ManualSeedDocument? manualSeeds)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var summary = new
        {
            options.VintageStoryPath,
            options.MaxCandidates,
            options.MaxEmbeddings,
            options.MaxClusters,
            options.RuntimeAtlasBucketLimit,
            options.RuntimeAtlasExamplesPerBucket,
            options.RuntimeAtlasTargetBuckets,
            options.RuntimeAtlasCurationMode,
            options.ClusterThreshold,
            options.ClusterMode,
            options.IncludeManualSeeds,
            LanguageEntryCount = language.Entries.Count,
            CandidateCount = corpus.Count,
            stats.LanguageCandidates,
            stats.AssetFilesScanned,
            stats.AssetCodeCandidates,
            stats.AssetStateCandidates,
            stats.GeneratedPhraseCandidates,
            ManualSeedPackCount = manualSeeds?.SeedPacks.Count ?? 0,
            stats.CategoryCounts
        };

        File.WriteAllText(Path.Combine(options.OutputDirectory, "candidate-summary.json"), JsonSerializer.Serialize(summary, JsonOptions));
        using var writer = new StreamWriter(Path.Combine(options.OutputDirectory, "candidates.sample.jsonl"), false, Encoding.UTF8);
        foreach (var candidate in corpus.Items.Take(Math.Max(options.SpotCheckCount, 500)))
        {
            writer.WriteLine(JsonSerializer.Serialize(candidate));
        }
    }

    public static void WriteClusterArtifacts(ExperimentOptions options, IReadOnlyList<SemanticCluster> clusters, ClusterRunSummary? summary)
    {
        File.WriteAllText(Path.Combine(options.OutputDirectory, "clusters.json"), JsonSerializer.Serialize(clusters, JsonOptions));
        if (summary != null)
        {
            File.WriteAllText(Path.Combine(options.OutputDirectory, "cluster-summary.json"), JsonSerializer.Serialize(summary, JsonOptions));
        }
    }

    public static void WriteRuntimeAtlasArtifacts(ExperimentOptions options, IReadOnlyList<SemanticCluster> clusters, ClusterRunSummary? summary)
    {
        if (clusters.Count == 0)
        {
            return;
        }

        var export = RuntimeAtlasExporter.Build(options, clusters);
        File.WriteAllText(Path.Combine(options.OutputDirectory, $"{options.RuntimeAtlasId}.atlas.json"), JsonSerializer.Serialize(export.Document, RuntimeAtlasJsonOptions));
        File.WriteAllText(Path.Combine(options.OutputDirectory, $"{options.RuntimeAtlasId}.validation.json"), JsonSerializer.Serialize(export.Validation, RuntimeAtlasJsonOptions));
        File.WriteAllText(Path.Combine(options.OutputDirectory, $"{options.RuntimeAtlasId}.curation.json"), JsonSerializer.Serialize(export.Curation, RuntimeAtlasJsonOptions));
        File.WriteAllText(Path.Combine(options.OutputDirectory, $"{options.RuntimeAtlasId}.curation.md"), BuildCurationReport(export));
        File.WriteAllText(Path.Combine(options.OutputDirectory, $"{options.RuntimeAtlasId}.report.md"), BuildRuntimeAtlasReport(options, export, summary));
    }

    public static void WriteManualSeedReview(ExperimentOptions options, ManualSeedDocument document)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var builder = new StringBuilder();
        builder.AppendLine("# Manual Seed Review");
        builder.AppendLine();
        builder.AppendLine("These packs are candidates for human review. They are not included in the generated corpus unless `--include-manual-seeds true` is passed.");
        builder.AppendLine();
        foreach (var pack in document.SeedPacks)
        {
            builder.AppendLine($"## {pack.Name}");
            builder.AppendLine();
            builder.AppendLine($"- Id: `{pack.Id}`");
            builder.AppendLine($"- Status: `{pack.ReviewStatus}`");
            builder.AppendLine($"- Tags: {string.Join(", ", pack.Tags.Select(tag => $"`{tag}`"))}");
            if (pack.Sources.Count > 0)
            {
                builder.AppendLine($"- Sources: {string.Join(", ", pack.Sources)}");
            }

            builder.AppendLine();
            builder.AppendLine("Terms:");
            foreach (var term in pack.Terms)
            {
                builder.AppendLine($"- {term}");
            }

            builder.AppendLine();
            builder.AppendLine("Phrases:");
            foreach (var phrase in pack.Phrases)
            {
                builder.AppendLine($"- {phrase}");
            }

            builder.AppendLine();
        }

        File.WriteAllText(Path.Combine(options.OutputDirectory, "manual-seeds.review.md"), builder.ToString());
    }

    public static void WriteSpotCheck(ExperimentOptions options, CandidateCorpus corpus, CorpusBuildStats stats, ManualSeedDocument? manualSeeds, IReadOnlyList<SemanticCluster> clusters, ClusterRunSummary? clusterSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Semantic Atlas Spot Check");
        builder.AppendLine();
        builder.AppendLine($"Generated at: `{DateTimeOffset.Now:O}`");
        builder.AppendLine($"Vintage Story path: `{options.VintageStoryPath}`");
        builder.AppendLine($"Candidate count: `{corpus.Count:N0}`");
        builder.AppendLine($"Language candidates: `{stats.LanguageCandidates:N0}`");
        builder.AppendLine($"Asset files scanned: `{stats.AssetFilesScanned:N0}`");
        builder.AppendLine($"Asset code candidates: `{stats.AssetCodeCandidates:N0}`");
        builder.AppendLine($"Asset state candidates: `{stats.AssetStateCandidates:N0}`");
        builder.AppendLine($"Generated phrase candidates: `{stats.GeneratedPhraseCandidates:N0}`");
        builder.AppendLine($"Manual seed packs available: `{manualSeeds?.SeedPacks.Count ?? 0:N0}`");
        builder.AppendLine($"Manual seeds included: `{options.IncludeManualSeeds}`");
        builder.AppendLine($"Cluster mode: `{options.ClusterMode}`");
        if (clusterSummary != null)
        {
            builder.AppendLine($"Embedded candidates: `{clusterSummary.EmbeddedCandidates:N0}`");
            builder.AppendLine($"Seed candidates: `{clusterSummary.SeedCandidates:N0}`");
            builder.AppendLine($"Evidence candidates: `{clusterSummary.EvidenceCandidates:N0}`");
            builder.AppendLine($"Outliers: `{clusterSummary.Outliers:N0}`");
            builder.AppendLine($"Seed outliers: `{clusterSummary.SeedOutliers:N0}`");
            builder.AppendLine($"Evidence outliers: `{clusterSummary.EvidenceOutliers:N0}`");
            builder.AppendLine($"Merged buckets: `{clusterSummary.MergedBuckets:N0}`");
        }
        builder.AppendLine();

        if (clusterSummary?.FamilyCounts.Count > 0)
        {
            builder.AppendLine("## Cluster Family Counts");
            builder.AppendLine();
            foreach (var (family, count) in clusterSummary.FamilyCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(30))
            {
                builder.AppendLine($"- `{family}`: `{count:N0}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Category Counts");
        builder.AppendLine();
        foreach (var (category, count) in stats.CategoryCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key))
        {
            builder.AppendLine($"- `{category}`: `{count:N0}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Source Counts");
        builder.AppendLine();
        foreach (var group in corpus.Items.GroupBy(candidate => candidate.Source).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Take(25))
        {
            builder.AppendLine($"- `{group.Key}`: `{group.Count():N0}`");
        }

        WriteSampleGroup(builder, "Generated Action Samples", corpus.Items.Where(candidate => candidate.Tags.Contains("generated-action")).Take(30));
        WriteSampleGroup(builder, "Temporal And Lore Samples", corpus.Items.Where(candidate => candidate.Tags.Contains("temporal") || candidate.Tags.Contains("lore")).Take(30));
        WriteSampleGroup(builder, "Mob Samples", corpus.Items.Where(candidate => candidate.Tags.Contains("mob")).Take(30));
        WriteSampleGroup(builder, "Item And Block Samples", corpus.Items.Where(candidate => candidate.Tags.Contains("item") || candidate.Tags.Contains("block")).Take(30));
        WriteSampleGroup(builder, "Asset Code Samples", corpus.Items.Where(candidate => candidate.Tags.Contains("asset-code") || candidate.Tags.Contains("asset-state")).Take(30));

        builder.AppendLine();
        builder.AppendLine("## First Candidate Samples");
        builder.AppendLine();
        foreach (var candidate in corpus.Items.Take(options.SpotCheckCount))
        {
            builder.AppendLine($"- `{candidate.Text}` | `{candidate.Source}` | `{candidate.SourceKey}` | {string.Join(", ", candidate.Tags.Select(tag => $"`{tag}`"))}");
        }

        if (clusters.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Top Clusters");
            builder.AppendLine();
            foreach (var cluster in clusters.Take(40))
            {
                builder.AppendLine($"### Cluster {cluster.Id} ({cluster.Family}, {cluster.Count:N0} items, seeds {cluster.SeedCount:N0}, evidence {cluster.EvidenceCount:N0}, avg sim {cluster.AverageSimilarity:F3})");
                builder.AppendLine();
                builder.AppendLine($"Representative: `{cluster.Representative}`");
                builder.AppendLine($"Tags: {string.Join(", ", cluster.TopTags.Select(pair => $"`{pair.Key}`={pair.Value}"))}");
                builder.AppendLine($"Sources: {string.Join(", ", cluster.TopSources.Select(pair => $"`{pair.Key}`={pair.Value}"))}");
                builder.AppendLine($"Quality: template fraction `{cluster.TemplateFraction:F2}`, tag purity `{cluster.TagPurity:F2}`");
                if (cluster.Warnings.Count > 0)
                {
                    builder.AppendLine($"Warnings: {string.Join(", ", cluster.Warnings.Select(warning => $"`{warning}`"))}");
                }
                builder.AppendLine();
                foreach (var sample in cluster.Samples.Take(10))
                {
                    builder.AppendLine($"- `{sample.Text}` | `{sample.Source}`");
                }
                builder.AppendLine();
            }
        }

        File.WriteAllText(Path.Combine(options.OutputDirectory, "spot-check.md"), builder.ToString());
    }

    private static void WriteSampleGroup(StringBuilder builder, string title, IEnumerable<CandidateTerm> candidates)
    {
        var samples = candidates.ToArray();
        if (samples.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var candidate in samples)
        {
            builder.AppendLine($"- `{candidate.Text}` | `{candidate.Source}` | `{candidate.SourceKey}` | {string.Join(", ", candidate.Tags.Select(tag => $"`{tag}`"))}");
        }
    }

    private static string BuildRuntimeAtlasReport(ExperimentOptions options, RuntimeAtlasExport export, ClusterRunSummary? summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Atlas Export Report");
        builder.AppendLine();
        builder.AppendLine($"Generated at: `{DateTimeOffset.Now:O}`");
        builder.AppendLine($"Atlas id: `{export.Document.AtlasId}`");
        builder.AppendLine($"Display name: `{export.Document.DisplayName}`");
        builder.AppendLine($"Version: `{export.Document.Version}`");
        builder.AppendLine($"Bucket count: `{export.Document.Buckets.Count:N0}` of `{export.Validation.SourceClusterCount:N0}` source clusters");
        builder.AppendLine($"Curation mode: `{export.Curation.Mode}`");
        builder.AppendLine($"Core candidates before caps: `{export.Curation.CoreCandidateCount:N0}`");
        builder.AppendLine($"Needs-review buckets: `{export.Curation.NeedsReviewCount:N0}`");
        builder.AppendLine($"Excluded buckets: `{export.Curation.ExcludedCount:N0}`");
        builder.AppendLine($"Runtime bucket limit: `{options.RuntimeAtlasBucketLimit:N0}`");
        builder.AppendLine($"Runtime target buckets: `{options.RuntimeAtlasTargetBuckets:N0}`");
        builder.AppendLine($"Examples per bucket: `{options.RuntimeAtlasExamplesPerBucket:N0}`");
        if (summary != null)
        {
            builder.AppendLine($"Cluster mode: `{summary.Mode}`");
            builder.AppendLine($"Embedded candidates: `{summary.EmbeddedCandidates:N0}`");
            builder.AppendLine($"Outliers: `{summary.Outliers:N0}`");
            builder.AppendLine($"Merged buckets: `{summary.MergedBuckets:N0}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Output Files");
        builder.AppendLine();
        builder.AppendLine($"- `{options.RuntimeAtlasId}.atlas.json`: runtime-compatible atlas candidate");
        builder.AppendLine($"- `{options.RuntimeAtlasId}.validation.json`: machine-readable validation and review flags");
        builder.AppendLine($"- `{options.RuntimeAtlasId}.curation.json`: machine-readable curation tiers");
        builder.AppendLine($"- `{options.RuntimeAtlasId}.curation.md`: human-readable curation tiers");
        builder.AppendLine($"- `{options.RuntimeAtlasId}.report.md`: human review summary");
        builder.AppendLine();
        builder.AppendLine("## Validation Summary");
        builder.AppendLine();
        builder.AppendLine($"- Empty labels: `{export.Validation.EmptyLabelCount:N0}`");
        builder.AppendLine($"- Empty example buckets: `{export.Validation.EmptyExampleCount:N0}`");
        builder.AppendLine($"- Duplicate ids: `{export.Validation.DuplicateIdCount:N0}`");
        builder.AppendLine($"- Buckets needing review: `{export.Validation.ReviewIssueCount:N0}`");
        builder.AppendLine($"- Review buckets listed: `{export.Validation.ReviewBuckets.Count:N0}`");
        builder.AppendLine();

        if (export.Validation.FamilyCounts.Count > 0)
        {
            builder.AppendLine("## Family Counts");
            builder.AppendLine();
            foreach (var (family, count) in export.Validation.FamilyCounts.Take(40))
            {
                builder.AppendLine($"- `{family}`: `{count:N0}`");
            }

            builder.AppendLine();
        }

        if (export.Validation.WarningCounts.Count > 0)
        {
            builder.AppendLine("## Warning Counts");
            builder.AppendLine();
            foreach (var (warning, count) in export.Validation.WarningCounts)
            {
                builder.AppendLine($"- `{warning}`: `{count:N0}`");
            }

            builder.AppendLine();
        }

        if (export.Curation.ReasonCounts.Count > 0)
        {
            builder.AppendLine("## Curation Reason Counts");
            builder.AppendLine();
            foreach (var (reason, count) in export.Curation.ReasonCounts.Take(40))
            {
                builder.AppendLine($"- `{reason}`: `{count:N0}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Review Guidance");
        builder.AppendLine();
        builder.AppendLine("Review labels, aliases, examples, and warnings before copying the generated atlas into the runtime asset path. Generated examples seed embeddings only; they are not exact-match gameplay rules.");
        builder.AppendLine();
        builder.AppendLine("## Top Buckets");
        builder.AppendLine();
        foreach (var bucket in export.ReviewBuckets.Take(80))
        {
            builder.AppendLine($"### {bucket.Label} [{bucket.Id}]");
            builder.AppendLine();
            builder.AppendLine($"- Family: `{bucket.Family}`");
            builder.AppendLine($"- Source cluster: `{bucket.SourceClusterId}`; count `{bucket.Count:N0}`; seeds `{bucket.SeedCount:N0}`; evidence `{bucket.EvidenceCount:N0}`");
            builder.AppendLine($"- Quality: avg similarity `{bucket.AverageSimilarity:F3}`; template fraction `{bucket.TemplateFraction:F2}`; tag purity `{bucket.TagPurity:F2}`");
            if (bucket.Warnings.Count > 0)
            {
                builder.AppendLine($"- Warnings: {string.Join(", ", bucket.Warnings.Select(warning => $"`{warning}`"))}");
            }

            builder.AppendLine($"- Sources: {string.Join(", ", bucket.TopSources.Take(6).Select(pair => $"`{pair.Key}`={pair.Value}"))}");
            builder.AppendLine($"- Examples: {string.Join("; ", bucket.Examples.Select(example => $"`{example}`"))}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildCurationReport(RuntimeAtlasExport export)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Atlas Curation Report");
        builder.AppendLine();
        builder.AppendLine($"Atlas id: `{export.Document.AtlasId}`");
        builder.AppendLine($"Mode: `{export.Curation.Mode}`");
        builder.AppendLine($"Source clusters: `{export.Curation.SourceClusterCount:N0}`");
        builder.AppendLine($"Core candidates before caps: `{export.Curation.CoreCandidateCount:N0}`");
        builder.AppendLine($"Exported core buckets: `{export.Curation.ExportedCoreBucketCount:N0}`");
        builder.AppendLine($"Needs-review buckets: `{export.Curation.NeedsReviewCount:N0}`");
        builder.AppendLine($"Excluded buckets: `{export.Curation.ExcludedCount:N0}`");
        builder.AppendLine();

        WriteCounts(builder, "Tier Counts", export.Curation.TierCounts);
        WriteCounts(builder, "Core Family Counts", export.Curation.CoreFamilyCounts);
        WriteCounts(builder, "Reason Counts", export.Curation.ReasonCounts);

        WriteBucketReviewSection(builder, "Core Candidate Sample", export.Curation.CoreCandidates.Take(120));
        WriteBucketReviewSection(builder, "Needs Review Sample", export.Curation.NeedsReview.Take(120));
        WriteBucketReviewSection(builder, "Excluded Sample", export.Curation.Excluded.Take(120));

        return builder.ToString();
    }

    private static void WriteCounts(StringBuilder builder, string title, IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
        {
            return;
        }

        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var (key, count) in counts)
        {
            builder.AppendLine($"- `{key}`: `{count:N0}`");
        }

        builder.AppendLine();
    }

    private static void WriteBucketReviewSection(StringBuilder builder, string title, IEnumerable<RuntimeAtlasBucketReview> buckets)
    {
        var items = buckets.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var bucket in items)
        {
            builder.AppendLine($"### {bucket.Label} [{bucket.Id}]");
            builder.AppendLine();
            builder.AppendLine($"- Tier: `{bucket.CurationTier}`");
            builder.AppendLine($"- Score: `{bucket.CurationScore:F1}`");
            builder.AppendLine($"- Family: `{bucket.Family}`");
            builder.AppendLine($"- Source cluster: `{bucket.SourceClusterId}`; count `{bucket.Count:N0}`; seeds `{bucket.SeedCount:N0}`; evidence `{bucket.EvidenceCount:N0}`");
            if (bucket.Warnings.Count > 0)
            {
                builder.AppendLine($"- Warnings: {string.Join(", ", bucket.Warnings.Select(warning => $"`{warning}`"))}");
            }

            if (bucket.CurationReasons.Count > 0)
            {
                builder.AppendLine($"- Curation reasons: {string.Join(", ", bucket.CurationReasons.Select(reason => $"`{reason}`"))}");
            }

            builder.AppendLine($"- Examples: {string.Join("; ", bucket.Examples.Take(8).Select(example => $"`{example}`"))}");
            builder.AppendLine();
        }
    }
}

internal sealed class WordPieceTokenizer
{
    private const int DefaultMaxTokens = 128;
    private static readonly Regex BasicTokenRegex = new(@"[\p{L}\p{N}]+|[^\s\p{L}\p{N}]", RegexOptions.Compiled);
    private readonly Dictionary<string, int> _vocab;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _unkId;

    private WordPieceTokenizer(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
        _clsId = GetRequiredTokenId("[CLS]");
        _sepId = GetRequiredTokenId("[SEP]");
        _unkId = GetRequiredTokenId("[UNK]");
    }

    public static WordPieceTokenizer Load(string vocabPath)
    {
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        foreach (var line in File.ReadLines(vocabPath))
        {
            var token = line.Trim();
            if (token.Length == 0 || vocab.ContainsKey(token))
            {
                continue;
            }

            vocab[token] = index++;
        }

        return new WordPieceTokenizer(vocab);
    }

    public TokenizedText Tokenize(string text, int maxTokens = DefaultMaxTokens)
    {
        maxTokens = Math.Max(8, maxTokens);
        var tokenIds = new List<long> { _clsId };
        foreach (var token in BasicTokenize(text))
        {
            foreach (var wordPieceId in WordPieceTokenize(token))
            {
                if (tokenIds.Count >= maxTokens - 1)
                {
                    break;
                }

                tokenIds.Add(wordPieceId);
            }

            if (tokenIds.Count >= maxTokens - 1)
            {
                break;
            }
        }

        tokenIds.Add(_sepId);
        return new TokenizedText(tokenIds.ToArray(), Enumerable.Repeat(1L, tokenIds.Count).ToArray(), new long[tokenIds.Count]);
    }

    private static IEnumerable<string> BasicTokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in BasicTokenRegex.Matches(text.ToLowerInvariant()))
        {
            yield return match.Value;
        }
    }

    private IEnumerable<int> WordPieceTokenize(string token)
    {
        if (token.Length > 100)
        {
            yield return _unkId;
            yield break;
        }

        var pieces = new List<int>();
        var start = 0;
        while (start < token.Length)
        {
            var end = token.Length;
            var matched = false;
            while (start < end)
            {
                var piece = token.Substring(start, end - start);
                if (start > 0)
                {
                    piece = "##" + piece;
                }

                if (_vocab.TryGetValue(piece, out var id))
                {
                    pieces.Add(id);
                    start = end;
                    matched = true;
                    break;
                }

                end--;
            }

            if (!matched)
            {
                yield return _unkId;
                yield break;
            }
        }

        foreach (var piece in pieces)
        {
            yield return piece;
        }
    }

    private int GetRequiredTokenId(string token)
    {
        if (_vocab.TryGetValue(token, out var id))
        {
            return id;
        }

        throw new InvalidDataException($"Vocabulary is missing required token {token}.");
    }
}

internal sealed record TokenizedText(long[] InputIds, long[] AttentionMask, long[] TokenTypeIds)
{
    public int Length => InputIds.Length;
}

internal static class NativeOnnxRuntimeResolver
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(InferenceSession).Assembly, ResolveOnnxRuntime);
        }
        catch (InvalidOperationException)
        {
        }

        _configured = true;
    }

    private static IntPtr ResolveOnnxRuntime(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in GetNativeLibraryCandidates(assembly))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetNativeLibraryCandidates(Assembly assembly)
    {
        var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime.dll" : "libonnxruntime.so";
        var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-x64" : RuntimeInformation.RuntimeIdentifier;
        yield return Path.Combine(assemblyDir, "native", fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "native", fileName);
        yield return Path.Combine(assemblyDir, "runtimes", rid, "native", fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        yield return Path.Combine(assemblyDir, fileName);
        yield return Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
