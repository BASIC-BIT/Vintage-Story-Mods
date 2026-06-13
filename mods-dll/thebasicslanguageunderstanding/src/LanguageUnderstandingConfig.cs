#nullable enable

namespace thebasicslanguageunderstanding;

public sealed class LanguageUnderstandingConfig
{
    public string ProviderProfile { get; set; } = "minilm-lite";

    public string ModelPath { get; set; } = "model.onnx";

    public string VocabPath { get; set; } = "vocab.txt";

    public void InitializeDefaultsIfNeeded()
    {
        ProviderProfile = string.IsNullOrWhiteSpace(ProviderProfile) ? "minilm-lite" : ProviderProfile.Trim();
        ModelPath = string.IsNullOrWhiteSpace(ModelPath) ? "model.onnx" : ModelPath.Trim();
        VocabPath = string.IsNullOrWhiteSpace(VocabPath) ? "vocab.txt" : VocabPath.Trim();
    }
}
