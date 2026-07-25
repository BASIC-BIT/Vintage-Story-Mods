# Flywheel Power initial feedback release

## Objective

Prepare the previously uncommitted Flywheel Power prototype as a bounded 0.5.0 feedback candidate without publishing it.

## Reconstructed history

- The historical PRD and implementation were created in `D:\bench\vs\Vintage-Story-Mods-wt\flywheel-prd` between June 25 and June 27, 2026.
- The branch itself never received a Flywheel commit. The project, assets, source, solution wiring, DLL, and package remained uncommitted local work.
- No matching GitHub issue or pull request exists.
- No Claude or Codex session could be matched to the Flywheel worktree. The implementation's human/agent authorship is therefore unconfirmed; `modinfo.json` credits BASIC as the mod author.
- The pre-implementation PRD proposed a single-block prototype. The local implementation later expanded into full-size 3x3x1 and compact variants, friction-coupled and keyed behavior, material variants, procedural rotating meshes, and an unfinished two-network slip transmission.

## Initial feedback scope

- Preserve the existing flywheel mechanics for player testing.
- Keep the full-size flywheel's 3x3x1 placement, collision, selection, pivot, axle, and network orientation semantics.
- Reduce the full-size visible wheel to a 2-block diameter with 0.5-block edge clearance.
- Reduce the radial core to a 0.5-block diameter and disc depth to 0.125 block.
- Keep the slip transmission source and assets, but remove its active blocktype and runtime registrations.
- Keep survival recipes deferred and describe the candidate as creative-only.

## Intentionally inactive source

`mods-dll/flywheelpower/disabled-content/` retains:

- The unfinished slip transmission blocktype.
- Pre-release legacy flywheel blocktypes that duplicate the active block codes.

Slip transmission C# and shape sources remain in place. Nothing under `disabled-content/` is packaged.

## Verification

Automated:

- Build Flywheel Power and its focused test project on .NET 10 against Vintage Story 1.22.2.
- Run all repository test projects.
- Verify formatting and repository complexity checks.
- Package `flywheelpower_0_5_0.zip`.
- Inspect the exact archive entries and verify disabled content is absent.
- Boot a disposable server with the packaged mod and check load logs if the local server supports an isolated data path.

Completed July 25, 2026:

- Repository CI-equivalent builds and whitespace checks passed for all six configured projects.
- The BASICs tests passed 481/481, DimensionLib tests passed 58/58, and Flywheel Power tests passed 7/7.
- The repository Lizard complexity gate passed with Flywheel Power included.
- `flywheelpower_0_5_0.zip` contains 23 entries and has SHA-256
  `4C86F29EECEB301B8140712BE663B15AA35FD58BF01288B31E02BDDE2DCCEFB3`.
- Exact archive inspection confirmed that no disabled blocktype, disabled localization, or `disabled-content/` path is packaged.
- A local Vintage Story 1.22.2 disposable server loaded the packaged mod after `game`, `creative`, and `survival`, instantiated
  `FlywheelPower.FlywheelPowerModSystem`, finalized assets without errors, and reached `WorldReady`. The only smoke warnings
  were expected blocked-mod-list network failures in the sandbox.
- A source-geometry comparison was inspected from front, three-quarter, and side views. An independent taste review found
  the two-block disc, half-block edge clearance, 0.5-block core, and 0.125-block depth consistent with the brief. This is
  bounded schematic evidence, not human in-game visual approval.

Manual approval required before execution:

1. **Full-size proportions and clearance** (P0)
   - Config: Creative world, any full-size flywheel material.
   - Do: Place a full-size flywheel and view it straight on, at roughly 45 degrees, and directly from the side.
   - Expect: The disc spans about two blocks inside the 3x3 plane, leaves roughly half a block of clearance at each edge, has a visibly small center core, and reads as a thin sheet/disc.
   - Watch for: A wheel that still nearly fills the footprint, a bulky central drum, clipping through the frame, or an edge-on profile that still reads as a thick cylinder.

2. **Rotation and axle alignment** (P0)
   - Config: Creative world with a powered mechanical network.
   - Do: Place full-size flywheels on X, Y, and Z axes, connect each to mechanical power, and observe rotation from front and side.
   - Expect: Each wheel rotates around its axle without orbiting, wobbling, translating, or separating from the hub.
   - Watch for: Wrong rotation axis, reversed model orientation, off-center pivots, or axle/frame misalignment.

3. **Multiblock selection and removal** (P0)
   - Config: Any full-size flywheel.
   - Do: Target and break the center and several outer footprint cells, including a placement with negative X/Z coordinates if practical.
   - Expect: Every footprint cell delegates to the principal flywheel, breaking any part removes the complete structure once, and no invisible blockers remain.
   - Watch for: Orphan part blocks, duplicate drops, missing selection, or a structure that fails after save/reload.

4. **Creative and handbook surface** (P1)
   - Config: Creative mode and handbook/search available.
   - Do: Search for `flywheel` and `slip transmission`, and inspect relevant creative tabs.
   - Expect: Active flywheel variants are discoverable; no Slip Transmission entry, item, recipe, or placeable block appears.
   - Watch for: Raw localization keys, legacy generic flywheel aliases, or hidden transmission variants.

5. **Save/reload behavior** (P0)
   - Config: A placed and mechanically connected full-size flywheel.
   - Do: Let the flywheel rotate, save and exit, reload the world, then inspect and break an outer footprint cell.
   - Expect: The structure reloads intact, remains connected, retains its principal/part relationship, and responds normally.
   - Watch for: Missing outer-cell delegation, wrong-dimension lookups, mechanical network errors, or log exceptions.

## Follow-ups outside this candidate

- Replace the slip transmission's cross-network torque model before re-registering any content.
- Decide whether compact variants belong in the long-term product surface or remain test fixtures.
- Design survival recipes and material progression after feedback establishes which variants are worth keeping.
- Balance inertia, coupling, losses, safe speed, and block info against real windmill/machine rigs.
- Add sound, wear, heat, and failure behavior only after the basic storage loop is understandable.

## Retirement condition

Remove this packet after the initial-feedback pull request is merged or closed and all surviving follow-ups have durable issues or product documentation.
