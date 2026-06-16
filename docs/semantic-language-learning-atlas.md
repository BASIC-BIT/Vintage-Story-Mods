# Semantic Language Learning Atlas

## Purpose

This document captures the desired direction for The BASICs semantic language-learning system. The current MVP uses server-side embeddings to route heard text into curated concept buckets, then stores per-player progress against those buckets.

The goal is a powerful hybrid system:

- Players gradually learn unknown languages through exposure.
- Learning generalizes across related words and phrases.
- Progress has a meaningful finish line.
- Storage remains scalable across many players, languages, and RP character slots.
- Seed concepts are grounded in Vintage Story's actual game text, assets, lore, and mechanics, not shallow hand-written examples.

## Current MVP

Current runtime behavior:

- The BASICs owns gameplay memory, scoring, rendering, persistence, and commands.
- `thebasicslanguageunderstanding` is a server-only sidecar that provides ONNX embeddings.
- The sidecar exposes only `ITheBasicsSemanticEmbeddingProvider`; clients do not need model/runtime files.
- The BASICs loads the curated generated atlas from `assets/thebasics/config/semantic-atlas/vintagestory-core.generated.json`, with the pilot atlas retained as a fallback.
- Atlas bucket vectors are built from stable bucket IDs, labels, aliases, and example strings.
- Per player/language memory stores bucket coverage by atlas bucket ID.
- Player-facing learning units are concept buckets, not individual terms.
- Learned concept notifications are emitted only for organic threshold crossings.
- Organic learning is rate-limited by configurable per-language observation and per-bucket cooldowns. Comprehension/rendering is not cooldown-gated; players can always understand concepts they already know.
- The default generated atlas includes both Vintage Story-specific concepts and reviewed everyday/RP seed packs for ordinary speech, needs, movement, trust, danger, and survival actions.

Current MVP constraints:

- Embeddings are required for organic learning and rendering-time bucket matching.
- If no provider is available, the server still boots and admin-set bucket progress remains usable, but semantic matching is degraded.
- There is no exact phrase matcher as a gameplay path; exact strings in atlas assets seed vectors only.
- The current default atlas is a curated generated Vintage Story core atlas. It is broader than the original pilot but still intentionally marked experimental.
- The packaged embedding model is intentionally small (`all-MiniLM-L6-v2`, quantized ONNX) for beta release ergonomics. Stronger server-local models can be configured through the sidecar model paths, but should be selected with an evaluation pass instead of by package size alone.

The MVP is enough to test the learning loop and UX, not enough to claim broad fluency for a full natural language.

## Coverage Decision

Do not rely on raw observed semantic coverage as the primary fluency signal.

Observed coverage mostly proves that a player has heard semantically nearby concepts before. It is useful as a diagnostic and short-term progress signal, but it is a weak proof of broad language ability.

Preferred fluency signals:

- Coverage over a curated/generated language atlas.
- Coverage over core Vintage Story concept buckets.
- Exposure diversity across concept families.
- Minimum real usage/exposure count.
- Optional future recognition history for player-facing flavor.

Observed phrase coverage should not be the primary gate for fully learning a language.

## Target Architecture

### Global Language Atlas

Create a server/global atlas per language or shared atlas profile. The atlas stores seed buckets once, not per player.

Each atlas bucket contains:

- Stable bucket ID.
- Vector centroid or vector set derived at runtime/server build time from bucket seed strings.
- Human-readable label/name suitable for admin commands and QA reports.
- Optional short debug alias, unique within an atlas profile, for commands such as `temporal-gear` or `black-bronze-tools`.
- Source tags such as `game-lang`, `blocktypes`, `itemtypes`, `manual-seed`, `lore`, `generated-action`.
- Example strings used to create the vectors.
- Optional parent category/family.

The atlas can be much larger than per-player memory because it is stored once per server/mod version.

### Per-Player Coverage

Players should store coverage/confidence against atlas bucket IDs rather than copying every atlas vector.

Example shape:

```text
language=Tradeband
bucket:metals-smithing = 63%
bucket:temporal-occult = 18%
bucket:farming-crops = 42%
```

This scales better than storing hundreds or thousands of `384d` vectors per player/language.

### Personal Buckets

Personal buckets are not part of the MVP. They remain a possible future layer for emergent server/player vocabulary not covered by the atlas.

Examples:

- Server-specific place names.
- Player group slang.
- Modded content not in the base atlas.
- Repeated RP phrases or proper nouns.

If added later, personal buckets should compact and prune aggressively. They would supplement the atlas; they should not be the entire language model.

### Recognition History

Recognized terms are not part of the MVP. If added later, keep recognition history separate from semantic buckets.

Recognition history can be useful for:

- `/langprogress` flavor output.
- Debugging why a sentence revealed or did not reveal.
- Player-facing progress logs.

Recognition history should not be the primary semantic matching mechanism.

### Runtime Matching Rule

At runtime, semantic matching should route heard spans to atlas buckets through embeddings:

1. Tokenize heard text into candidate spans.
2. For long messages, split useful tokens into overlapping fixed-size chunks.
3. Prioritize chunk and span embedding budget with atlas-derived hint tokens. Rendering uses buckets the recipient has progress in; organic learning routes against the full atlas.
4. Embed chunks/spans within bounded per-message budgets.
5. Compare vectors with atlas bucket vectors.
6. Resolve overlaps by stronger semantic match; rendering prefers narrower spans on similarity ties.
7. Apply the recipient's bucket confidence to matched original word positions for rendering.
8. Increase listener bucket confidence for organic exposure.

Do not add an exact phrase matcher for gameplay. Exact atlas strings are examples and seed material for vectors; learned `100%` coverage applies to embedding-matched paraphrases, not only canonical bucket phrases. Lexical hints may order embedding work, but they must not reveal text without a passing embedding match.

## Full Language Learning

Support a threshold where a player fully learns a language.

Current MVP rule:

```text
required = min(atlasBucketCount, max(configuredMinimumBuckets, ceil(atlasBucketCount * learnedBucketPercent)))
if learnedAtlasBucketCount >= required
then promote to fully known language
```

When a concept bucket crosses its learned threshold in the current MVP:

- Promote that bucket to `100%` coverage.
- Record learned time.
- Show a learned concept notification only for organic learning.
- Keep admin-set threshold promotions quiet for QA/setup commands.

When a whole language is promoted:

- Add the language to the player's known languages.
- Clear partial semantic memory for that language through the existing `AddLanguage` path.
- Show a milestone notification for organic promotion only.
- Keep admin-triggered promotion quiet; admin command output is the confirmation.

Future broader rules can add concept family diversity and minimum exposure counts if the current learned-bucket rule proves too easy to grind.

Organic learning should remain deliberately paced. Cooldowns should limit progress gain from repeated exposure without limiting comprehension of already-learned concepts. The current default posture is a short per-language observation cooldown plus a longer per-bucket cooldown so repeated tutoring or phrase spam does not rapidly max out a concept.

Do not require players to hear every synonym or every possible string forever.

## Bucket Compaction

When personal buckets exceed their cap, do not only drop the weakest bucket. First attempt compaction.

Suggested compaction process:

1. Find the most similar bucket pair.
2. If pair similarity is above a safe threshold, merge them.
3. If no pair is similar enough, prune the weakest/stalest bucket.

Suggested merge math:

```text
aWeight = sqrt(a.ExposureCount) * a.Strength
bWeight = sqrt(b.ExposureCount) * b.Strength
newVector = normalize(a.Vector * aWeight + b.Vector * bWeight)
newExposureCount = a.ExposureCount + b.ExposureCount
newStrength = max(a.Strength, b.Strength) + compactBonus
```

Do not sum strengths directly; that creates accidental super-buckets.

Suggested compact bonus:

```text
compactBonus = min(8, similarity * log2(totalExposure + 1))
```

If similarity is too low, do not force-merge unrelated concepts just because the cache is full. Drop or decay weak/stale buckets instead.

## Seed Bucket Philosophy

Seed buckets should be rich and heavy. The cost of seed strings is paid once when building atlas vectors. Prefer representative strings and multiple phrase variants over tiny word lists.

Seed buckets should include:

- Individual nouns and verbs.
- Natural phrases players might say.
- Generated item/block names.
- Game UI strings where relevant.
- Lore vocabulary and proper nouns.
- Manual social/RP concepts not present in item/block assets.
- Everyday conversational and survival language that is not specific to Vintage Story assets, such as greetings, agreement, trust, directions, food, water, medicine, wounds, time, movement, and simple requests.

Manual examples must be treated as placeholders, not as complete domain coverage. For example, an “occult/lovecraftian” bucket must not stop at generic words like `rift nightmare madness abyss`. It should include Vintage Story-specific lore and mechanics such as temporal gears, rusty gears, temporal stability, temporal storms, rifts, rust world, drifters, corrupt and sawblade locusts, locust workshops, Resonance Archives, Jonas Falx, rot, ruined machinery, gears, and other actual game/lore concepts.

## Atlas Generation Pipeline

The strongest version is a generated research pipeline that builds seed buckets from real game assets.

### Inputs

Read from the installed game path, currently available through `VINTAGE_STORY=D:\Games\Vintagestory` on this machine:

- `assets/game/lang/en.json`
- `assets/game/lang/*.json` for optional multilingual cross-checking
- `assets/survival/itemtypes/**/*.json`
- `assets/survival/blocktypes/**/*.json`
- `assets/survival/entities/**/*.json` if present
- `assets/survival/worldgen/**/*.json` for structures, lore locations, dungeon names, and environmental concepts
- First-party and target-server mod assets when enabled
- The BASICs language/config strings where relevant

The repo itself only contains a small `mods-dll/thebasics/assets/game/lang/en.json` shim, so atlas generation must not rely only on checked-in mod assets.

### String Extraction

Extract candidate strings from:

- Language file values.
- Language file keys when values are templated or missing.
- Item/block/entity codes after normalization.
- Variant groups and allowed variant values.
- Handbook text and lore pages.
- Worldgen structure names such as Resonance Archives and locust workshops.
- Game mechanic descriptions such as temporal stability, temporal storms, rifts, and rust world.

Strip VTML/HTML tags, handbook links, hotkey tags, formatting markers, and placeholder syntax before embedding.

### Template Expansion

Many strings are templated or wildcarded. The pipeline should expand reasonable variants rather than embedding the raw template.

Examples:

- `item-creature-drifter-*` should expand to surface/deep/tainted/corrupt/nightmare/double-headed drifters when variants are discoverable.
- Stone, metal, wood, and crop variants should expand into natural item/block names.
- Action phrases should respect plausible interactions: mine granite, quarry stone, saw planks, forge iron, harvest rye. Avoid nonsensical phrases like sawing granite unless the game actually has that interaction.

Expansion can be exact where variants are explicit, and sampled where full permutation would explode.

### Phrase Generation

Generate representative phrases, not only terms.

Examples:

- `mine granite with a pickaxe`
- `forge a wrought iron ingot`
- `repair a broken static translocator with temporal gears`
- `a corrupt sawblade locust attacks in the rust world`
- `a temporal storm is approaching`
- `plant flax seeds in farmland`
- `trade rusty gears with a merchant`

The phrase generator should use game-aware action constraints where possible.

### Manual Seed Packs

Some concept families need hand-authored additions because they are social/RP or not strongly represented in assets.

Examples:

- Social trust: friend, stranger, kin, family, promise, debt, oath, betrayal, bargain.
- Danger/help: help, flee, danger, ambush, wounded, safe, shelter, rescue.
- Roleplay logistics: meet at home, bring supplies, guard the road, share food.

Manual packs should be layered on top of generated assets, not used as a substitute for game asset ingestion.

### Clustering And Naming

After extraction/generation:

1. Embed all candidate strings.
2. Cluster vectors into candidate buckets.
3. Infer names from top representative terms/phrases.
4. Emit a review report for humans/agents.
5. Allow manual corrections to bucket names, merges, splits, and tags.

Bucket labels should be stable enough for QA and admin testing. The generated label can start from the representative term, but reviewed/runtime atlas assets should expose a unique label or alias that humans can type without copying an opaque ID.

This can be a fun research project and does not need to run in-game. It can be an offline tool that produces versioned atlas assets.

### Output

The offline experiment tool writes deterministic, reviewable artifacts under `tools/SemanticAtlasExperiment/output/vintagestory-core/`:

```text
vintagestory-core-generated.atlas.json
vintagestory-core-generated.validation.json
vintagestory-core-generated.curation.json
vintagestory-core-generated.curation.md
vintagestory-core-generated.report.md
```

The generated `.atlas.json` file matches the runtime `SemanticLanguageAtlasDocument` shape. By default it contains the curated `core-candidate` tier rather than every raw cluster. After review, the current promoted runtime copy lives at `mods-dll/thebasics/assets/thebasics/config/semantic-atlas/vintagestory-core.generated.json`; regenerate and review the output artifacts before replacing that asset.

Current repeatable command shape:

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
  --include-manual-seeds true `
  --cluster-mode staged
```

The curation pass sorts clusters into three offline review tiers:

- `core-candidate`: structurally clean, useful gameplay/RP concepts selected for the generated runtime atlas candidate.
- `needs-review`: valid clusters with mixed tags, template-heavy examples, single-source variant bulk, or unclear family assignment.
- `excluded`: known creative/debug/placeholder/UI relation noise that should not enter the default atlas without a deliberate manual override.

The in-game mod should load compact reviewed atlas assets, not rerun the full generation pipeline on server boot. Generated examples remain vector seed material only; they must not become an exact phrase matcher or canonical phrase requirement.

### Testing Hooks

For atlas QA, add an admin-only command that sets one player's expertise/coverage for a single bucket by language and bucket label/alias. This lets us test rendering and progress behavior for one concept family without grinding exposure messages.

Candidate command shape:

```text
/adminsetlangbucket <player> <language> <bucket-label-or-id> <0-100>
```

The command validates that the bucket exists in the loaded atlas, shows the resolved bucket label/family back to the admin, and updates only that bucket's per-player atlas coverage. Values at or above the learned threshold promote that bucket to `100%`, but admin-set progress should not emit learned concept notifications. It should not mark the whole language known unless normal promotion rules subsequently decide the player qualifies.

## Open Design Questions

- How many atlas buckets should ship in the default core atlas?
- Should personal bucket cap remain small once atlas coverage exists?
- Should The BASICs ship one core atlas, or should the sidecar own atlas profiles alongside model profiles?
- How should partially learned players intentionally speak a partially learned language without weird/gibberish output?

## Near-Term Implementation Order

1. Build an offline asset-ingest pipeline against `VINTAGE_STORY` assets.
2. Tune semantic similarity thresholds, learning rates, and chunk budgets against live QA transcripts.
3. Decide whether personal buckets or recognition history are still needed after atlas coverage exists.
4. Design partial-language speaking behavior.
