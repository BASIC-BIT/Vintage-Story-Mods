# Model Assets

This directory contains the local embedding model files packaged by the sidecar:

- `model.onnx`
- `vocab.txt`

The initial model is `sentence-transformers/all-MiniLM-L6-v2`, specifically the quantized `onnx/model_quint8_avx2.onnx` artifact renamed to `model.onnx`, plus its matching WordPiece `vocab.txt`.

The provider expects a BERT/WordPiece-style MiniLM sentence-embedding model with `input_ids`, `attention_mask`, and optionally `token_type_ids` inputs.
