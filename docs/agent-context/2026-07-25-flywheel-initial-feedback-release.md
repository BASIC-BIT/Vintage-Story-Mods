# Flywheel Power initial feedback release

## Objective

Deliver Flywheel Power as a complete 0.5.0 initial-feedback implementation without publishing it.

## Reconstructed history

- The historical PRD and implementation were created in `D:\bench\vs\Vintage-Story-Mods-wt\flywheel-prd` between June 25 and June 27, 2026.
- The branch itself never received a Flywheel commit. The project, assets, source, solution wiring, DLL, and package remained uncommitted local work.
- No matching GitHub issue or pull request exists.
- No Claude or Codex session could be matched to the Flywheel worktree. The implementation's human/agent authorship is therefore unconfirmed; `modinfo.json` credits BASIC as the mod author.
- The pre-implementation PRD proposed a single-block prototype. The local implementation later expanded into full-size 3x3x1 and compact variants, friction-coupled and keyed behavior, material variants, procedural rotating meshes, and an unfinished two-network slip transmission.

## Initial feedback scope

- Expose wood/iron-hub, stone/iron-hub, and iron/iron-hub full-size constructions plus the compact iron flywheel for player testing.
- Keep the full-size flywheel's 3x3x1 placement, collision, selection, pivot, axle, and network orientation semantics.
- Reduce the full-size visible wheel from a 2-block diameter to a 1-block diameter, leaving 1 block of edge clearance inside the unchanged 3x3 footprint.
- Keep the radial core at a 0.5-block diameter while halving the complete authored depth profile: disc depth from 0.125 to 0.0625 block, hub depth from 0.18 to 0.09 block, and each coupling plate from 0.02 to 0.01 block.
- Keep the slip transmission source and assets, but remove its active blocktype and runtime registrations.
- Keep keyed flywheel blocktypes and preview assets inactive because their current rigid path does not return inertial torque.
- Use one iron-hub rule and distinct renderer grouping for each released construction; leave the broader material/hub Cartesian product dormant.
- Keep survival recipes deferred and describe the candidate as creative-only.

### Revised full-size geometry checkpoint

All dimensions below are authored/runtime model dimensions in blocks. The rotation center remains exactly `(8, 8, 8)` in
shape coordinates, and the 3x3x1 placement footprint is unchanged.

| Dimension | Before owner update | Revised candidate | Change |
| --- | ---: | ---: | ---: |
| Disc outer radius | 1.0 | 0.5 | -0.5 block |
| Disc diameter | 2.0 | 1.0 | -50% |
| Edge clearance within 3-block footprint | 0.5 per side | 1.0 per side | +0.5 block per side |
| Disc depth | 0.125 | 0.0625 | -50% |
| Hub diameter | 0.5 | 0.5 | unchanged |
| Hub depth | 0.18 | 0.09 | -50% |
| Coupling plate depth | 0.02 each | 0.01 each | -50% |
| Hub/plate assembly depth | 0.22 | 0.11 | -50% |

## Intentionally inactive source

`mods-dll/flywheelpower/disabled-content/` retains:

- The unfinished slip transmission blocktype.
- Keyed flywheel blocktypes and preview shapes whose current behavior does not buffer power.

Slip transmission C# and shape sources remain in place. Nothing under `disabled-content/` is packaged.

## Deletion audit ledger

An independent read-only audit reviewed repository history, the full PR diff, current source and assets, references, tests,
documentation, generated files, and the exact package.

- **Active required:** released blocktypes, horizontal/vertical frame shapes, coupled wheel shapes, axles, renderer, mechanics,
  multiblock code, metadata, package wiring, and tests.
- **Intentionally deferred source:** canonical Slip Transmission source/blocktype/localization/stand, non-legacy keyed
  flywheel blocktypes/localization/preview shapes, rigid-coupling branches, and future-material density cases.
- **Migration compatibility required:** none. Flywheel Power has never appeared on `main`, in a repository tag or release, or
  in another merged PR, so no published world can depend on the prototype aliases or absolute part-link format.
- **Test/evidence only:** this temporary QA handoff, the historical pre-implementation PRD, ignored build output, and the exact
  local ZIP checkpoint.
- **Deleted as obsolete/unreferenced:** never-published `flywheellegacy` and `keyedflywheellegacy` blocktypes; unreferenced
  generic full-size and compact frame shapes; unreachable material/hub texture and localization branches; legacy-only
  direction names; active keyed-only shaft text; and the superseded absolute `cx/cy/cz/cd` part-link fields.

The shared `slip-transmission-shaft.json` name is historical, but the asset remains active because the released compact
flywheel uses it for its axle and inventory overlay. Renaming it would add churn without changing player-facing discovery.

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
- The BASICs tests passed 481/481, DimensionLib tests passed 58/58, and Flywheel Power tests passed 18/18.
- The repository Lizard complexity gate passed with Flywheel Power included.
- The earlier `flywheelpower_0_5_0.zip` checkpoint contained 18 entries and had SHA-256
  `34F03EBE7DE3B4321C267304E932B545E90EAC0389C72613181EC29A2186D855`; it is superseded by the revised-geometry candidate.
- Exact archive inspection confirmed that no disabled blocktype, disabled localization, or `disabled-content/` path is packaged.
- A local Vintage Story 1.22.2 disposable server loaded the packaged mod after `game`, `creative`, and `survival`, instantiated
  `FlywheelPower.FlywheelPowerModSystem`, finalized assets without errors, and reached `WorldReady`. The only smoke warnings
  were expected blocked-mod-list network failures in the sandbox.
- A superseded source-geometry comparison inspected the earlier 2-block disc and 0.125-block depth from front,
  three-quarter, and side views. The owner subsequently directed a further 0.5-block radial reduction and a 50% depth
  reduction. Fresh in-game visual evidence is required for the resulting 1-block disc, 1-block edge clearance, 0.5-block
  core, and 0.0625-block disc depth; the earlier schematic is not approval of the revised model.
- An independent exact-commit code review found five issues. Before the next candidate, keyed and unsafe Cartesian-product
  material surfaces were narrowed out, procedural normals and relative schematic links were added, and the inventory preview
  was rebuilt at released scale. Owner feedback then restored a curated wood/stone/iron set with deterministic renderer groups.

Revised geometry candidate staged July 26, 2026:

- All six CI build and whitespace-format targets passed after the final deletion audit. The BASICs tests passed 488/488,
  DimensionLib tests passed 58/58, Flywheel Power tests passed 18/18, and the repository Lizard gate passed.
- The geometry-only package staged to the shared QA environment has SHA-256
  `3D85D75D3B2203A4470C1E836C5B969E188B7B827BFCD2BEFE2245A903D8CF05`.
- The final offline curated-material package contains exactly 16 entries, is 43,709 bytes, and has SHA-256
  `4AD74CE091867052511498F85ABB66A2C49C53350AC5D38052A03597FE84FA9D`. It has not been copied to either
  client profile or the disposable server while the owner is using the computer.
- The package script now enforces that exact 16-entry allowlist and rejects disabled content, unsupported material mappings or
  localization, and missing renderer groups. Exact archive inspection confirms the authored model matches the revised source.
- The older geometry-only package was staged to both dedicated QA profiles and the disposable server. The server reached `WorldReady` with
  `FlywheelPower.FlywheelPowerModSystem` loaded and no Flywheel startup error.
- At that checkpoint, in-game visual, rotation/alignment, multiblock, and save/reload cards remained pending while the owner
  was using the computer. BASIC later authorized a fresh build, install, server restart, and QA attempt.

QA deployment correction, July 27, 2026:

- The first authorized client attempt loaded an older `flywheelpower.zip` instead of the reviewed
  `flywheelpower_0_5_0.zip`. Both files declared version 0.5.0, so Vintage Story reported a duplicate mod ID and selected the
  stale prototype package. That package exposed old content and registered only renderer key `flywheelpower`, causing compact
  placement to crash when current assets requested `flywheelpower-compact-iron`.
- The stale packages were moved from both QA profiles into the recoverable local quarantine
  `D:\Games\VSProfiles\FlywheelPower-Quarantine\20260727-055517`. Each profile now contains only the reviewed package name.
- Full-size placement still intentionally reserves the unchanged 3x3x1 multiblock plane. The existing specific placement
  message is now returned when any of its eight part cells is unavailable, instead of vanilla's ambiguous `Not enough space`.
- After that correction, all six CI build and whitespace-format targets passed. The BASICs tests passed 488/488,
  DimensionLib tests passed 58/58, Flywheel Power tests passed 19/19, and the repository Lizard gate passed.
- The corrected package contains exactly 16 entries, is 43,722 bytes, and has SHA-256
  `422D2738E48D1DBF6868945BA3FFE4E3E5972C0F8DC65341204BA18ED3514988`. Both QA profiles and the disposable server match that
  hash. The server loaded `FlywheelPower.FlywheelPowerModSystem`, reached `WorldReady`, and began serving without a Flywheel
  exception.
- The client crash is a failed QA observation, not a pass. Visible QA remains paused after the owner stopped Computer Use;
  do not relaunch or control the client until BASIC explicitly resumes visible control.

In-game cards remaining after the interrupted attempt:

1. **Full-size proportions and clearance** (P0)
   - Config: Creative world, any full-size flywheel material.
   - Do: Place a full-size flywheel and view it straight on, at roughly 45 degrees, and directly from the side.
   - Expect: The disc spans about one block inside the 3x3 plane, leaves roughly one block of clearance at each edge, has a visibly small center core, and reads as a thin sheet/disc.
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
   - Expect: Four active choices are discoverable—three full-size iron-hub constructions (wood, stone, and iron wheels) plus compact iron—and no Slip Transmission entry, item, recipe, or placeable block appears.
   - Watch for: Raw localization keys, legacy generic flywheel aliases, or hidden transmission variants.

5. **Save/reload behavior** (P0)
   - Config: A placed and mechanically connected full-size flywheel.
   - Do: Let the flywheel rotate, save and exit, reload the world, then inspect and break an outer footprint cell.
   - Expect: The structure reloads intact, remains connected, retains its principal/part relationship, and responds normally.
   - Watch for: Missing outer-cell delegation, wrong-dimension lookups, mechanical network errors, or log exceptions.

## Follow-ups outside this candidate

- Replace the slip transmission's cross-network torque model before re-registering any content.
- Decide whether compact variants belong in the long-term product surface or remain test fixtures.
- Implement real inertial contribution for keyed flywheels before restoring their blocktypes.
- Consider additional wheel materials and hub combinations only after the curated renderer-group approach and material progression receive feedback.
- Commission richer material-specific wheel/frame models and textures if the initial silhouette and construction choices test well.
- Design survival recipes and material progression after feedback establishes which variants are worth keeping.
- Balance inertia, coupling, losses, safe speed, and block info against real windmill/machine rigs.
- Add sound, wear, heat, and failure behavior only after the basic storage loop is understandable.

### Minimal later visual-asset commission

If feedback supports the three-construction release set, commission one coherent material pass rather than a new mechanic:

- Three drop-in full-size appearances: timber construction with visible planking/banding, segmented dressed stone with iron
  restraint, and a cast or fabricated iron wheel.
- Preserve the exact `(8, 8, 8)` model center, rotation axis, 1-block wheel diameter, 0.0625-block disc depth, 0.5-block iron
  hub, axle attachment semantics, and existing 3x3x1 collision/selection footprint.
- Deliver Vintage Story shape JSON plus atlas-ready textures and front, three-quarter, and side previews for each construction.
- Keep the iron hub/axle visually consistent across the set; focus the commission on readable material construction, restrained
  edge detail, and a stronger real-flywheel silhouette rather than animation, new gameplay, or additional variants.

## Retirement condition

Remove this packet after the initial-feedback pull request is merged or closed and all surviving follow-ups have durable issues or product documentation.
