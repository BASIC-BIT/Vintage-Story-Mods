# Flywheel Power

Flywheel Power 0.5.0 is the complete initial-feedback implementation of rotational-energy storage for Vintage Story's vanilla mechanical networks.

## Initial release

The release provides:

- Full-size 3x3x1 friction-coupled flywheels with an eight-spoke timber web and a wood, iron, meteoric-iron, or steel outer tyre.
- Compact wood, stone, iron, meteoric-iron, and steel friction-coupled flywheels on a wooden shaft.
- Independently selected iron, meteoric-iron, or steel hubs, provided the hub is at least as strong as the wheel. Iron and meteoric iron share one strength tier.
- Independent flywheel speed that charges from a faster network and contributes capped torque back to a slower network.
- Material-density and geometry-based inertia, bearing and windage losses, a coupling ramp, and a safe-speed warning.
- Persistent flywheel speed and schematic-safe relative links between the full-size principal block and its multiblock parts.
- Mechanical rendering on all three axes with a distinct renderer group for each released wheel-and-hub construction.
- Two-bearing timber stands with connected cross-bracing, wooden bearing housings, restrained hold-down hardware, and grease cups. Placement requires a solid supporting footprint beneath the stand.
- A staged survival construction loop: lubricated hub-and-bearing sets, timber webs, prepared rims or curved metal tyres, finished wheel assemblies, and separately placed stands.
- Held-item and placed-block information for rotating mass and effective inertia, plus live state, flywheel and network speed, stored-energy percentage, coupling torque, losses, slipping, and overspeed risk.

The full-size wheel keeps a 3x3x1 placement, collision, and selection footprint. Its visible wheel is 1.6 blocks in diameter and 0.1875 block deep, with a 0.12-block radial outer tyre, timber felloe, eight broad spokes, a stepped material-specific hub, and a close-fitting bearing collar around the axle. The compact wheel is 0.92 block in diameter.

## Current content surface

Twenty-three finished wheel assemblies are intentionally discoverable in creative mode and the handbook. Wood and compact stone accept any released hub. Iron and meteoric-iron wheels accept iron, meteoric-iron, or steel hubs. Steel wheels require steel hubs. Finished assemblies cannot be placed directly: place the matching grounded stand, then install the assembly by interacting with it.

The monolithic full-size stone wheel is intentionally omitted because a slab at that scale would be structurally implausible. Stone remains available only for the compact construction.

Survival recipes use animal fat as bearing lubricant, a wooden axle, metal plates for the selected hub, planks for the web and stand, and either prepared wood/stone or curved metal-plate rims. Full-size metal tyres consume eight plates; compact metal blanks consume four.

## Deliberately deferred systems

The unfinished Slip Transmission remains under `src/` and `disabled-content/` for redesign. Its block and behavior are not registered, and its blocktype and localization are not packaged, so it has no creative, handbook, search, recipe, command, placement, or interaction surface.

Keyed flywheel blocktypes and their preview shapes are also retained only under `disabled-content/`. Their current rigid-coupling path follows network speed but does not return inertial torque, so exposing them would imply storage behavior they do not yet provide.

Additional wheel materials, contextual smithing or casting, sound, wear, heat, failures, and richer commissioned models are follow-up work. The initial release deliberately avoids invalid weaker-hub combinations and broader material Cartesian products.

This mod has not had a public release, so obsolete prototype block aliases and their unused shapes were removed rather than carried as fictional migration compatibility.

## Compatibility and limitations

- Targets Vintage Story 1.22.2 and requires the standard Survival mod.
- Universal mod: install the same package on the server and each connecting client.
- Initial-feedback release with both creative discovery and staged survival recipes.
- Balance values are provisional and require testing against real windmill and helve-hammer rigs. The current safe-speed targets assume a flywheel on the fast side of a large gear; adding sails primarily adds torque and acceleration, not a comparable increase in steady-state speed.
- In-game visual, powered-rotation, multiblock, discovery-surface, and save/reload QA are release gates.

Back up a world before testing an experimental mod. Remove placed Flywheel Power blocks before uninstalling it.
