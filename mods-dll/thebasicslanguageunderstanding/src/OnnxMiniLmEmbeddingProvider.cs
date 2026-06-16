#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using thebasics.ModSystems.ProximityChat.Semantics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasicslanguageunderstanding;

internal sealed class OnnxMiniLmEmbeddingProvider : ITheBasicsSemanticEmbeddingProvider, IDisposable
{
    private const int MaxTokens = 128;
    private readonly ICoreServerAPI _api;
    private readonly ConcurrentDictionary<string, float[]> _cache = new ConcurrentDictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _cacheOrder = new ConcurrentQueue<string>();
    private readonly InferenceSession? _session;
    private readonly WordPieceTokenizer? _tokenizer;
    private readonly int _maxCacheEntries;

    public OnnxMiniLmEmbeddingProvider(ICoreServerAPI api, string modId, LanguageUnderstandingConfig config)
    {
        _api = api;
        config ??= new LanguageUnderstandingConfig();
        config.InitializeDefaultsIfNeeded();
        _maxCacheEntries = config.MaxCacheEntries;
        ProviderId = $"onnx-{config.ProviderProfile}";

        try
        {
            NativeOnnxRuntimeResolver.Configure(api);
            var modelPath = FindModelAssetPath(api, modId, config.ModelPath);
            var vocabPath = FindModelAssetPath(api, modId, config.VocabPath);
            if (modelPath == null || vocabPath == null)
            {
                ProviderId = $"onnx-{config.ProviderProfile} (missing model assets)";
                api.Logger.Warning($"[thebasics-language-understanding] Missing model or vocab assets ({config.ModelPath}, {config.VocabPath}); semantic embeddings are disabled.");
                return;
            }

            _tokenizer = WordPieceTokenizer.Load(vocabPath);
            _session = new InferenceSession(modelPath);
            Dimensions = ResolveDimensions(_session);
            ProviderId = $"onnx-{config.ProviderProfile}:{Path.GetFileName(modelPath)}";
        }
        catch (Exception ex)
        {
            ProviderId = $"onnx-{config.ProviderProfile} (initialization failed)";
            api.Logger.Warning($"[thebasics-language-understanding] Failed to initialize ONNX embedding provider: {ex.Message}");
        }
    }

    public string ProviderId { get; private set; }

    public int Dimensions { get; private set; }

    public bool IsReady => _session != null && _tokenizer != null && Dimensions > 0;

    public async ValueTask<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var normalizedText = NormalizeCacheKey(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        if (_cache.TryGetValue(normalizedText, out var cached))
        {
            return cached;
        }

        if (!IsReady)
        {
            return null;
        }

        var vector = await Task.Run(() => EmbedCore(normalizedText, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (vector == null)
        {
            return null;
        }

        AddCachedEmbedding(normalizedText, vector);
        return vector;
    }

    public bool TryGetCachedEmbedding(string text, out float[] embedding)
    {
        if (_cache.TryGetValue(NormalizeCacheKey(text), out var cached))
        {
            embedding = cached;
            return true;
        }

        embedding = Array.Empty<float>();
        return false;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    private float[]? EmbedCore(string text, CancellationToken cancellationToken)
    {
        if (_session == null || _tokenizer == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var tokenized = _tokenizer.Tokenize(text, MaxTokens);
        using var results = _session.Run(CreateInputs(_session, tokenized));
        var output = results.FirstOrDefault();
        if (output == null)
        {
            return null;
        }

        var tensor = output.AsTensor<float>();
        var vector = ExtractSentenceVector(tensor, tokenized.AttentionMask);
        return NormalizeVector(vector);
    }

    private void AddCachedEmbedding(string normalizedText, float[] vector)
    {
        if (_cache.TryAdd(normalizedText, vector))
        {
            _cacheOrder.Enqueue(normalizedText);
            TrimCache();
        }
    }

    private void TrimCache()
    {
        while (_cache.Count > _maxCacheEntries && _cacheOrder.TryDequeue(out var oldest))
        {
            _cache.TryRemove(oldest, out _);
        }
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
            var tokenOffset = tokenIndex * embeddingSize;
            for (var dimension = 0; dimension < embeddingSize; dimension++)
            {
                pooled[dimension] += values[tokenOffset + dimension];
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

    private static int ResolveDimensions(InferenceSession session)
    {
        var output = session.OutputMetadata.Values.FirstOrDefault();
        var dimensions = output?.Dimensions;
        if (dimensions == null || dimensions.Length == 0)
        {
            return 384;
        }

        var last = dimensions[^1];
        return last > 0 ? last : 384;
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

    private static string NormalizeCacheKey(string text)
    {
        return string.Join(" ", (text ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? FindModelAssetPath(ICoreServerAPI api, string modId, string configuredPath)
    {
        return FindModelPathInFileSystem(modId, configuredPath) ?? TryMaterializeModelAsset(api, modId, configuredPath);
    }

    private static string? TryMaterializeModelAsset(ICoreServerAPI api, string modId, string configuredPath)
    {
        var assetPath = NormalizeAssetModelPath(configuredPath);
        var asset = api.Assets.TryGet(new AssetLocation(modId, assetPath));
        if (asset?.Data == null || asset.Data.Length == 0)
        {
            return null;
        }

        var cacheDir = api.GetOrCreateDataPath(Path.Combine("Cache", modId, "models"));
        var targetPath = Path.Combine(cacheDir, Path.GetFileName(configuredPath));
        if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != asset.Data.Length)
        {
            File.WriteAllBytes(targetPath, asset.Data);
        }

        return targetPath;
    }

    private static string NormalizeAssetModelPath(string configuredPath)
    {
        var normalized = (configuredPath ?? string.Empty).Replace('\\', '/').Trim('/').ToLowerInvariant();
        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? normalized : $"models/{normalized}";
    }

    private static string? FindModelPathInFileSystem(string modId, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (var root in GetSearchRoots())
        {
            var candidates = new[]
            {
                Path.Combine(root, configuredPath),
                Path.Combine(root, "assets", modId, "models", configuredPath),
                Path.Combine(root, "models", configuredPath)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            yield return assemblyDir;
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }
    }
}
