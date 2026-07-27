# Flywheel Power

Flywheel Power 0.5.0 is the complete initial-feedback implementation of rotational-energy storage for Vintage Story's vanilla mechanical networks.

## Initial release

The release provides:

- Full-size 3x3x1 friction-coupled flywheels in four curated constructions: wood and iron with iron hubs, meteoric iron with a meteoric-iron hub, and steel with a steel hub.
- Compact wood, stone, iron, meteoric-iron, and steel friction-coupled flywheels on a wooden shaft.
- Independent flywheel speed that charges from a faster network and contributes capped torque back to a slower network.
- Material-density and geometry-based inertia, bearing and windage losses, a coupling ramp, and a safe-speed warning.
- Persistent flywheel speed and schematic-safe relative links between the full-size principal block and its multiblock parts.
- Mechanical rendering on all three axes with a distinct renderer group for each released construction.
- Two-bearing timber stands with iron caps, hold-down hardware, and grease cups. Placement requires a solid supporting footprint beneath the stand.
- Held-item and placed-block information for rotating mass and effective inertia, plus live state, flywheel and network speed, stored-energy percentage, coupling torque, losses, slipping, and overspeed risk.

The full-size wheel keeps a 3x3x1 placement, collision, and selection footprint. Its visible disc is 1.6 blocks in diameter and 0.1875 block deep, with a stepped iron hub and close-fitting bearing collar around the axle.

## Current content surface

Nine choices are intentionally discoverable in creative mode and the handbook:

- Wooden Flywheel (Iron Hub)
- Iron Flywheel (Iron Hub)
- Meteoric Iron Flywheel (Meteoric Iron Hub)
- Steel Flywheel (Steel Hub)
- Compact Wooden Flywheel
- Compact Stone Flywheel
- Compact Iron Flywheel
- Compact Meteoric Iron Flywheel
- Compact Steel Flywheel

The monolithic full-size stone wheel is intentionally omitted because a slab at that scale would be structurally implausible. Stone remains available only for the compact construction.

Survival recipes and material progression are deferred until feedback establishes which constructions and balance targets are worth keeping.

## Deliberately deferred systems

The unfinished Slip Transmission remains under `src/` and `disabled-content/` for redesign. Its block and behavior are not registered, and its blocktype and localization are not packaged, so it has no creative, handbook, search, recipe, command, placement, or interaction surface.

Keyed flywheel blocktypes and their preview shapes are also retained only under `disabled-content/`. Their current rigid-coupling path follows network speed but does not return inertial torque, so exposing them would imply storage behavior they do not yet provide.

Additional wheel materials, freely mixed hubs, recipes, sound, wear, heat, failures, and richer commissioned models are follow-up work. The initial release deliberately avoids the full material-by-hub Cartesian product.

This mod has not had a public release, so obsolete prototype block aliases and their unused shapes were removed rather than carried as fictional migration compatibility.

## Compatibility and limitations

- Targets Vintage Story 1.22.2 and requires the standard Survival mod.
- Universal mod: install the same package on the server and each connecting client.
- Creative-only feedback release with no survival recipes.
- Balance values are provisional and require testing against real windmill and machine rigs.
- In-game visual, powered-rotation, multiblock, discovery-surface, and save/reload QA are release gates.

Back up a world before testing an experimental mod. Remove placed Flywheel Power blocks before uninstalling it.
