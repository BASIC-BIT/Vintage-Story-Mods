# The BASICs Language Understanding

Optional server-side sidecar for semantic language-learning in The BASICs.

This mod owns the embedding runtime only. The BASICs owns gameplay memory, reveal scoring, persistence, and rendering.

Included local model assets:

- `assets/thebasicslanguageunderstanding/models/model.onnx`
- `assets/thebasicslanguageunderstanding/models/vocab.txt`

The initial model is `sentence-transformers/all-MiniLM-L6-v2` using the quantized `onnx/model_quint8_avx2.onnx` artifact and its matching WordPiece `vocab.txt`.

The package script includes ONNX Runtime native files for `win-x64` and `linux-x64` from the restored `Microsoft.ML.OnnxRuntime` NuGet package. Vintage Story requires unmanaged libraries inside the mod `native/` folder, so both Windows and Linux native files are packaged there.

Server configuration lives in `ModConfig/thebasicslanguageunderstanding.json`:

```json
{
  "ProviderProfile": "minilm-lite",
  "ModelPath": "model.onnx",
  "VocabPath": "vocab.txt"
}
```

`ModelPath` and `VocabPath` can point at packaged assets under `assets/thebasicslanguageunderstanding/models/`, files beside the mod assembly, files under a local `models/` folder, or absolute server filesystem paths. This keeps The BASICs gameplay code independent from the embedding model and allows stronger model variants without changing clients.
