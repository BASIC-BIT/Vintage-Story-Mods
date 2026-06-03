# Architecture Diagrams

This directory contains Structurizr DSL diagrams for DimensionLib and Pocket Dimensions.

## Files

- `dimensionlib-api-surface.dsl`: C4 workspace for DimensionLib, its public API, server/client internals behind that API, and the Pocket Dimensions consumer mod.
- `STRUCTURIZR_GUIDE.md`: durable workflow for using Structurizr to create user-readable system diagrams.

## Views

- `DimensionLibLandscape`: context-level landscape for mod authors, admins, players, DimensionLib, Pocket Dimensions, future consumers, and the Vintage Story runtime.
- `DimensionLibContainers`: container-level view of DimensionLib plus direct consumers and Vintage Story APIs/storage.
- `DimensionLibPublicApi`: public API surface for consumer mods.
- `DimensionLibServerRuntime`: server-side implementation behind `IDimensionLibApi`.
- `DimensionLibClientVisuals`: client-side explicit visual settings renderer path.
- `PocketDimensionsComponents`: Pocket Dimensions command/config/persistence/policy/source/assets and how it consumes DimensionLib.
- `CreatePocketFlow`: dynamic create/materialize flow.
- `EnterPocketFlow`: dynamic enter/return/visual-sync flow.

## How To View

Use Structurizr Lite as the default local visualizer. It gives a browser UI with view switching, pan, zoom, and layout controls without requiring a cloud account.

PowerShell example with Docker:

```powershell
docker run --rm -it -p 8080:8080 -v "${PWD}\docs\architecture:/usr/local/structurizr" structurizr/lite
```

Then open `http://localhost:8080`.

See `STRUCTURIZR_GUIDE.md` for the reusable diagramming workflow and quality checklist.

CLI validation, if the Structurizr CLI is installed:

```powershell
structurizr validate -workspace docs/architecture/dimensionlib-api-surface.dsl
```

Docker CLI validation, without a local Structurizr install:

```powershell
docker run --rm -v "${PWD}\docs\architecture:/usr/local/structurizr" structurizr/cli validate -workspace /usr/local/structurizr/dimensionlib-api-surface.dsl
```

The model intentionally distinguishes normal public API from debug/experimental surfaces. Dashed/low-opacity elements are not release commitments.
