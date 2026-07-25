# Flywheel Power

Flywheel Power is an experimental Vintage Story mod for gathering early feedback on rotational-energy storage in vanilla mechanical networks.

## Initial feedback scope

- Full-size 3x3x1 friction-coupled and keyed flywheels.
- Compact friction-coupled and keyed flywheels.
- Material variants for comparing visual and mechanical feel.
- Block information for speed, stored energy, transfer, losses, and overspeed risk.
- Creative-mode discovery only. Survival recipes and progression are intentionally deferred until the core behavior has been tested.

## Intentionally unavailable

The slip transmission implementation is preserved under `src/` and `disabled-content/`, but its block and behavior are not registered and its blocktype is not packaged. The current transfer model does not behave reliably enough for player use. Keeping it outside active assets prevents creative inventory, handbook, search, placement, and interaction exposure while leaving the work available for a future redesign.

The pre-release legacy flywheel blocktypes are also preserved under `disabled-content/` but are not packaged. No published world requires those migration aliases, and loading them beside the material variants would expose obsolete block codes.

## Compatibility

This candidate targets Vintage Story 1.22.2 and requires the standard Survival mod. It is universal and must be installed on both the server and each connecting client.

Back up a world before testing an experimental mod. Remove placed Flywheel Power blocks before uninstalling it.
