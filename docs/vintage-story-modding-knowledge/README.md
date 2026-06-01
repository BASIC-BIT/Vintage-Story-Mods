# Vintage Story Modding Knowledge Tree

This tree captures reusable Vintage Story modding research that should survive beyond one feature branch. It is intentionally source-oriented: each note should point to the VS API/decompiled file, asset file, shader, or local mod pattern that supports the conclusion.

## Purpose

- Preserve deep integration findings from DimensionLib and The BASICs work.
- Give future agents and modders a hierarchical map before they dive into decompiled VS internals.
- Provide source material for a future portable Vintage Story modding skill/toolbox pack.

## Promotion Path

- Current level: repo documentation under `docs/vintage-story-modding-knowledge/`.
- Target level: portable skill pack once multiple entries are stable and tool-agnostic.
- Reason: the information is reusable across Vintage Story mods, but the taxonomy and examples are still evolving.
- Over-promotion cost: a premature skill would freeze half-tested advice and hide uncertainty.
- Demotion path: keep niche or version-specific findings as plain docs referenced by a smaller skill.
- Verification signal: future modding tasks use these notes to avoid rediscovering render, lighting, asset, packaging, and server/client pitfalls.

## Tree

- `rendering-and-lighting.md`: sky/sun/moon replacement, fog, cave fog, chunk light, and custom post-processing hooks.
- Asset authoring: custom block JSON, cross-domain texture/sound references, creative inventory, drops, and shape reuse.
- Packaging and deployment: zip entry layout, forward-slash paths, client profile sync, Pterodactyl test-server loop.
- Networked mod systems: universal code mod channel setup, client/server transfer packets, and connection timing.
- Dimension and world data: VS dimension planes, internal Y offsets, chunk-column creation/loading, relight, and persistence.
- QA workflows: screenshot capture, log triage, multi-client profile handling, and non-interactive server-console control.

Only the rendering/lighting branch is populated so far.
