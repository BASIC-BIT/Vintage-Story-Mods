#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public interface ITheBasicsSemanticEmbeddingProvider
{
    string ProviderId { get; }

    int Dimensions { get; }

    bool IsReady { get; }

    ValueTask<float[]?> EmbedAsync(string text, CancellationToken cancellationToken);

    bool TryGetCachedEmbedding(string text, out float[] embedding);
}
