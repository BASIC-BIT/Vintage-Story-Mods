# Semantic Atlas Experiment

Offline experiment tooling for building a large Vintage Story semantic language atlas candidate corpus.

The tool is intentionally outside the runtime mod path. It extracts and expands terms from installed game assets, optionally includes review-only manual seed packs, embeds phrases with the same MiniLM ONNX model used by `thebasicslanguageunderstanding`, and emits review artifacts for clustering experiments.

## Quick Run

```powershell
dotnet run --project tools\SemanticAtlasExperiment\SemanticAtlasExperiment.csproj -- `
  --vintage-story D:\Games\Vintagestory `
  --max-candidates 50000 `
  --max-embeddings 12000 `
  --max-clusters 384 `
  --include-manual-seeds true `
  --cluster-mode staged
```

Outputs are written to `tools/SemanticAtlasExperiment/output/vintagestory-core/`, which is ignored by git via the repo's `**/output/` rule.

The staged run also writes a runtime-shaped atlas candidate:

- `vintagestory-core-generated.atlas.json`
- `vintagestory-core-generated.validation.json`
- `vintagestory-core-generated.curation.json`
- `vintagestory-core-generated.curation.md`
- `vintagestory-core-generated.report.md`

The generated atlas JSON matches the runtime `SemanticLanguageAtlasDocument` shape. By default it contains the curated `core-candidate` tier, not every raw cluster. Treat it as a review artifact until validation and curation look clean. The current promoted runtime copy is `mods-dll/thebasics/assets/thebasics/config/semantic-atlas/vintagestory-core.generated.json`.

## Full-Scale Experiment

```powershell
dotnet run --project tools\SemanticAtlasExperiment\SemanticAtlasExperiment.csproj --configuration Release -- `
  --vintage-story D:\Games\Vintagestory `
  --max-candidates 250000 `
  --max-embeddings 250000 `
  --max-clusters 1536 `
  --cluster-threshold 0.66 `
  --include-manual-seeds true `
  --cluster-mode staged
```

Use `--include-manual-seeds true` only after reviewing `manual-seeds.review.json`.

## Runtime Atlas Export Options

```powershell
dotnet run --project tools\SemanticAtlasExperiment\SemanticAtlasExperiment.csproj --configuration Release -- `
  --vintage-story D:\Games\Vintagestory `
  --max-candidates 250000 `
  --max-embeddings 100000 `
  --max-clusters 1536 `
  --runtime-atlas-buckets 1536 `
  --runtime-atlas-target-buckets 512 `
  --runtime-atlas-curation core `
  --runtime-examples-per-bucket 8 `
  --cluster-threshold 0.66 `
  --atlas-id vintagestory-core-generated `
  --atlas-display-name "Vintage Story Core Generated" `
  --atlas-version "0.1.0-experiment" `
  --include-manual-seeds true `
  --cluster-mode staged
```

Generated examples seed embedding vectors only. They are not an exact phrase matcher and should be reviewed as semantic coverage examples, not canonical phrases players must say.

Use `--runtime-atlas-curation raw` only when you intentionally want the runtime-shaped export to contain the first raw clusters up to `--runtime-atlas-buckets`. The default `core` mode ranks clusters, excludes known creative/debug/UI noise, moves warning-heavy clusters into `needs-review`, applies family caps, and emits a smaller core atlas candidate for review.

## Cluster Modes

`--cluster-mode staged` is the default. It protects reviewed manual seeds, selects capped embedding runs with family round-robin, creates seed buckets before attaching generated phrase evidence, leaves low-similarity candidates as outliers, and writes source/family quality metrics.

`--cluster-mode greedy` keeps the original proof-of-concept behavior. It embeds candidates in source order and greedily assigns each candidate to the nearest cluster above the threshold until `--max-clusters` is reached. This is useful as a baseline, but it is order-sensitive and can let broad early clusters absorb later terms.

## Output Files

- `spot-check.md`: human-readable extraction and clustering summary.
- `candidate-summary.json`: counts and options.
- `candidates.sample.jsonl`: sample generated candidates.
- `clusters.json`: cluster summaries and representative examples.
- `cluster-summary.json`: staged/greedy run counts, seed/evidence/outlier totals, merge totals, and family counts.
- `manual-seeds.review.md`: review rendering of the candidate manual packs.
- `<atlas-id>.atlas.json`: runtime-compatible generated atlas candidate, suitable for promotion after review.
- `<atlas-id>.validation.json`: bucket counts, duplicate/empty checks, warning counts, source counts, and flagged bucket samples.
- `<atlas-id>.curation.json`: full curation tier data for `core-candidate`, `needs-review`, and `excluded` clusters.
- `<atlas-id>.curation.md`: human-readable curation report with core, review, and excluded samples.
- `<atlas-id>.report.md`: human-readable generated atlas review report.

## Notes

- The extractor starts with `assets/game/lang/en.json`, then scans asset files for codes and simple variant values.
- Variant/template expansion is deliberately conservative where game mechanics matter. Generic phrase permutations are intentionally narrow so they do not swamp object-family clusters.
- Manual and web-researched terms are kept in `manual-seeds.review.json` so they can be reviewed before becoming atlas inputs.
- Staged mode treats generated phrases as evidence/examples rather than primary bucket seeds where possible, and caps generated evidence per bucket so examples do not dominate the seed terms.
