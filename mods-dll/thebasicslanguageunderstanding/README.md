# The BASICs Language Understanding

Optional server-side sidecar for semantic language-learning in The BASICs.

This mod owns the embedding runtime only. The BASICs owns gameplay memory, reveal scoring, persistence, and rendering.

Included local model assets:

- `assets/thebasicslanguageunderstanding/models/model.onnx`
- `assets/thebasicslanguageunderstanding/models/vocab.txt`

The initial model is `sentence-transformers/all-MiniLM-L6-v2` using the quantized `onnx/model_quint8_avx2.onnx` artifact and its matching WordPiece `vocab.txt`.

This small default is an intentional beta-release tradeoff: it keeps the server-only sidecar package and startup/runtime costs modest, while still allowing the gameplay system to validate the semantic language-learning loop. Larger or stronger embedding models may improve broad concept understanding, but should be selected through an evaluation pass against the atlas and representative RP messages rather than by model size alone.

The package script includes ONNX Runtime native files for `win-x64`, `linux-x64`, and `osx-arm64` from the restored `Microsoft.ML.OnnxRuntime` NuGet package. Vintage Story requires unmanaged libraries inside the mod `native/` folder, so supported platform native files are packaged there.

Server configuration lives in `ModConfig/thebasicslanguageunderstanding.json`:

```json
{
  "ProviderProfile": "minilm-lite",
  "ModelPath": "model.onnx",
  "VocabPath": "vocab.txt",
  "MaxCacheEntries": 4096
}
```

`ModelPath` and `VocabPath` can point at packaged assets under `assets/thebasicslanguageunderstanding/models/`, files beside the mod assembly, files under a local `models/` folder, or absolute server filesystem paths. This keeps The BASICs gameplay code independent from the embedding model and allows stronger model variants without changing clients.

The current provider expects a BERT/WordPiece-style ONNX sentence embedding model with compatible inputs. Model swaps outside that shape may need a new provider profile or adapter rather than only changing the file paths.
