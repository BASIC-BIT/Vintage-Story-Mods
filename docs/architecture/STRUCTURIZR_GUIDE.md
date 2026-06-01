# Structurizr Diagram Guide

Use Structurizr when the user needs a clear explanation of how a system fits together, not just a file-by-file code tour. Keep the diagrams audience-focused and source-aligned.

## Recommended Visualizer

Use Structurizr Lite through Docker as the default local visualizer:

```powershell
docker run --rm -it -p 8080:8080 -v "${PWD}\docs\architecture:/usr/local/structurizr" structurizr/lite
```

Then open `http://localhost:8080` and select `dimensionlib-api-surface.dsl`.

Why this is the default:

- It gives a browser UI with view switching, pan, zoom, and layout controls.
- It renders landscape, container, component, and dynamic views from the same DSL model.
- It needs no Structurizr Cloud account and does not require a local Java/CLI install.
- It is easier to discuss live with a user than static Mermaid or PlantUML exports.

Use the Structurizr DSL playground only for quick experiments with non-sensitive diagrams. Use CLI validation for repeatable checks.

## Validation

Validate before treating a diagram as done:

```powershell
docker run --rm -v "${PWD}\docs\architecture:/usr/local/structurizr" structurizr/cli validate -workspace /usr/local/structurizr/dimensionlib-api-surface.dsl
```

If a local CLI exists, this equivalent command is fine:

```powershell
structurizr validate -workspace docs/architecture/dimensionlib-api-surface.dsl
```

## Diagramming Workflow

Start with the user question. Name the diagram after the thing it answers.

Useful view types:

- System landscape: who uses the system, what external systems exist, and why the system exists.
- Container view: deployable or runtime pieces and their dependencies.
- Component view: major code seams inside one container, especially APIs, services, adapters, and persistence.
- Dynamic view: one important flow, such as create, enter, save, sync, or release.

Prefer several small diagrams over one complete graph. A good diagram should answer one question in under a minute.

## Modeling Rules

- Use names from the code and product docs so users can search the repo from the diagram labels.
- Keep experimental/debug surfaces visible but styled differently from release commitments.
- Model consumers separately from libraries. For DimensionLib, `Pocket Dimensions` should stay a consumer, not part of core.
- Put external runtime APIs, storage, and asset systems outside the product boundary.
- Add dynamic views for flows that cross boundaries, such as command -> API -> service -> chunk writer -> client sync.
- Avoid modeling every class. Group low-level helpers unless the user needs that seam explained.

## Style Conventions

- `External`: Vintage Story APIs, storage, asset systems, and future consumer examples.
- `API`: public consumer-facing contracts.
- `Core`: DimensionLib server/runtime internals.
- `Visual`: client render/ambient/fog systems.
- `Consumer`: Pocket Dimensions or other mods that consume DimensionLib.
- `Experimental`: debug, QA, generators, visual tuning, or unsettled API surfaces.

## Maintenance Checklist

- Update the DSL when public API names, command names, or core boundaries change.
- Update this directory's `README.md` when adding a new workspace or view.
- Run Structurizr validation after editing DSL.
- Run `git diff --check` before handing off.
- In the final user summary, name the views that answer the user's question and give the Docker Lite command.
