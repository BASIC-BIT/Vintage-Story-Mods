# Flywheel Power initial feedback release

## Objective

Deliver Flywheel Power as a complete 0.5.0 initial-feedback implementation without publishing a GitHub or ModDB release.

## Reconstructed history

- The historical PRD and implementation were created in `D:\bench\vs\Vintage-Story-Mods-wt\flywheel-prd` between June 25 and June 27, 2026.
- The branch itself never received a Flywheel commit. The project, assets, source, solution wiring, DLL, and package remained uncommitted local work.
- No matching GitHub issue or pull request exists.
- No Claude or Codex session could be matched to the Flywheel worktree. The implementation's human/agent authorship is therefore unconfirmed; `modinfo.json` credits BASIC as the mod author.
- The pre-implementation PRD proposed a single-block prototype. The local implementation later expanded into full-size 3x3x1 and compact variants, friction-coupled and keyed behavior, material variants, procedural rotating meshes, and an unfinished two-network slip transmission.

## Initial feedback scope

- Expose wood/iron-hub, stone/iron-hub, and iron/iron-hub full-size constructions plus the compact iron flywheel for player testing.
- Keep the full-size flywheel's 3x3x1 placement, collision, selection, pivot, axle, and network orientation semantics.
- Set the full-size visible wheel to a 1.6-block diameter, leaving 0.7 block of edge clearance inside the unchanged 3x3 footprint.
- Use a 0.1875-block disc depth, a 0.27-block hub depth, and 0.03-block coupling plates so the wheel reads as substantial without returning to the original oversized silhouette.
- Add a distinct iron bearing collar between the axle and hub, with 0.002 block of radial running clearance around the authored axle.
- Keep the slip transmission source and assets, but remove its active blocktype and runtime registrations.
- Keep keyed flywheel blocktypes and preview assets inactive because their current rigid path does not return inertial torque.
- Use one iron-hub rule and distinct renderer grouping for each released construction; leave the broader material/hub Cartesian product dormant.
- Keep survival recipes deferred and describe the candidate as creative-only.

### Revised full-size geometry checkpoint

All dimensions below are authored/runtime model dimensions in blocks. The rotation center remains exactly `(8, 8, 8)` in
shape coordinates, and the 3x3x1 placement footprint is unchanged.

| Dimension | Original baseline | Previous candidate | Current candidate |
| --- | ---: | ---: | ---: |
| Disc outer radius | 1.0 | 0.5 | 0.8 |
| Disc diameter | 2.0 | 1.0 | 1.6 |
| Edge clearance within 3-block footprint | 0.5 per side | 1.0 per side | 0.7 per side |
| Disc depth | 0.125 | 0.0625 | 0.1875 |
| Hub diameter | 0.5 | 0.4 | 0.56 |
| Hub depth | 0.18 | 0.09 | 0.27 |
| Bearing outside diameter | not distinct | not distinct | 0.38 |
| Axle-to-bearing radial clearance | 0.015 | 0.015 | 0.002 |
| Coupling plate depth | 0.02 each | 0.01 each | 0.03 each |
| Maximum bearing assembly depth | 0.22 | 0.11 | 0.30 |

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

- Build Flywheel Power and its focused test project on .NET 10 against Vintage Story 1.22.6.
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
  core, and 0.0625-block disc depth; the earlier schematic is not approval of that now-superseded model.
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

Owner-directed size and bearing revision, July 27, 2026:

- The authored and procedural full-size models now use a 0.8-block radius, 0.1875-block disc depth, 0.27-block hub depth,
  and 0.03-block coupling plates. The unchanged 3x3x1 footprint retains 0.7 block of clearance around the visible wheel.
- The hub now steps through a distinct iron bearing collar. Its inner radius is 0.142 block around the authored 0.14-block
  axle radius, leaving 0.002 block of radial running clearance; the bearing projects slightly beyond the hub faces.
- All six CI build and whitespace-format targets passed. The BASICs tests passed 488/488, DimensionLib tests passed 58/58,
  Flywheel Power tests passed 19/19, and the repository Lizard gate passed.
- The resulting exact 16-entry package is 44,084 bytes and has SHA-256
  `5A30AE49F5A6C872BBBC6C77B24DBED35DACD80C49CA2F8ECC4E7C20D84DDF53`.
- This new package has not replaced the installed candidate because the owner is currently using the running client. Install,
  restart, and fresh front/three-quarter/side evidence remain required.

Curated materials and grounded-stand candidate, July 27, 2026:

- The active full-size set is now wood, iron, meteoric iron, and steel with the simple iron-hub rule. The structurally
  implausible full-size monolithic stone wheel is removed from discovery. Compact wood, stone, iron, meteoric-iron, and steel
  constructions remain available.
- All nine released constructions have deterministic renderer groups. The compact flywheels now use the authored wooden axle
  instead of the preserved disabled Slip Transmission shaft.
- The horizontal stands now reach the actual ground plane within the existing reserved footprint and visibly include two
  timber bearing posts, iron bearing caps, sleepers, cross ties, hold-down hardware, bracing, and grease cups. Full-size
  horizontal placement requires three solid support blocks beneath the lowest multiblock row; vertical full-size placement
  requires its 3x3 footing, and compact placement requires solid ground immediately beneath the block.
- The disabled Slip Transmission implementation remains preserved. Its shaft asset moved beside the rest of its canonical
  source under `disabled-content/` and no longer ships in the active package.
- All six CI build and whitespace-format targets passed. The BASICs tests passed 488/488, DimensionLib tests passed 58/58,
  Flywheel Power tests passed 21/21, and the repository Lizard gate passed.
- The exact 15-entry package is 46,290 bytes and has SHA-256
  `6000A15236F233C9573C73DE84F678D46146A1D9FD8E2EEA7E797FDFE401B6F9`. Both QA profiles and a downloaded copy of the
  disposable-server package match that hash. The server loaded `FlywheelPower.FlywheelPowerModSystem` and reached
  `WorldReady`; both clients launched without a Flywheel startup exception.
- In-game appearance, variant texture identity, placement/foundation behavior, powered rotation, multiblock interaction, and
  save/reload remain observation-level manual QA gates. Startup and package evidence do not count as visual approval.

Matching-hub, comparison-info, and stand-render correction, July 27, 2026:

- The nine-item release set remains bounded. Full-size wood and iron retain iron hubs; full-size meteoric iron and steel now
  use matching meteoric-iron and steel hubs. Compact iron, meteoric-iron, and steel constructions use matching visible hub
  material without adding another variant axis. Wood and compact stone continue to use iron hubs.
- Both held-item/handbook information and placed-block information now report physically derived rotating mass in kilograms
  and normalized effective inertia. The calculation covers the rotating wheel, hub, bearing collar, coupling plates, and
  wooden axle. Full iron remains the gameplay tuning reference at effective inertia 8.
- The authored stand shapes existed in inventory previews but were suppressed after placement by the mechanical behavior's
  custom tessellation path. Each placed flywheel now registers its horizontal or vertical stand as a separate static
  mechanical renderable; only the axle and wheel receive live rotation.
- The exact 15-entry package is 50,460 bytes and has SHA-256
  `6CFF597A5E76E5448377FAAD81A97B3A5C33524E2696E6473E2024CC9EC6719B`. Both QA profiles and a downloaded copy of the
  disposable-server package match that hash. The restarted server loaded `FlywheelPower.FlywheelPowerModSystem`, reached
  `WorldReady`, and both clients relaunched without a new Flywheel startup exception.
- All six CI build and whitespace-format targets passed. The BASICs tests passed 488/488, DimensionLib tests passed 58/58,
  Flywheel Power tests passed 26/26, and the repository Lizard gate passed.
- Visual confirmation of the newly registered stand, matching advanced hubs, tooltip values, and the remaining five cards is
  still required. Package and log evidence do not count as visual approval.

Ordered hubs, model-review tooling, and direction-rebase candidate, July 27, 2026:

- Full-size and compact constructions now expose independently selected iron, meteoric-iron, or steel hubs. The hub must
  meet or exceed the wheel tier; iron and meteoric iron are equivalent for this rule, while steel wheels require steel hubs.
  This produces 23 intentional construction choices rather than the unrestricted 27-choice Cartesian product.
- The compact wheel radius increased from 0.38 to 0.46 block. The full-size support gained mirrored cross-bracing that
  reaches the wooden bearing housings. The face and rim registration marks overlap slightly across the wheel edge so texture
  filtering cannot expose a gap.
- A repository `vintage-story-model-renderer` skill now produces deterministic front, back, left, right, top, bottom, and
  isometric images plus bounds and source-hash metadata from the authored shapes and procedural dimension authority. This is
  automated geometry evidence only, not in-game or human visual approval.
- Flywheel signed speed, last network speed, torque, and render phase now rebase together when vanilla network discovery
  reverses the propagation basis. This preserves stored energy and avoids treating an unchanged rotating flywheel as suddenly
  counter-rotating after a connect or disconnect operation.
- The full solution builds with four pre-existing The BASICs analyzer warnings and no errors. The BASICs tests pass 488/488,
  DimensionLib tests pass 58/58, Flywheel Power tests pass 32/32, all six whitespace checks pass, the Lizard complexity gate
  passes, the agent-tooling index passes, and `git diff --check` reports no errors.
- The exact 15-entry package is 52,818 bytes and has SHA-256
  `43AF076D7CE344B4727604D6EC4A3307B0C0C0CD9847EE1D0CCD2820C25B0DD8`. It contains only the DLL, PDB, metadata, README,
  active blocktypes/localization, two active flywheel models, the common wooden axle, and four stand shapes.
- Both dedicated QA profiles contain that exact package hash. A copy uploaded to the disposable server by SFTP was downloaded
  again and matched the same hash and 52,818-byte size. After restart, the Vintage Story 1.22.2 server loaded
  `FlywheelPower.FlywheelPowerModSystem` and reached `WorldReady` without a Flywheel startup exception. Profile2 then joined
  the server with the exact package and finalized the level without a new Flywheel startup or renderer-registration error.
- The two clients remain available for the five in-game cards below. No observation-level interaction was performed after
  the guarded attempt to enable the installed Agent Control mod was rejected before execution; no focus or input reached
  either client. The owner can enable Agent Control in Profile2 with `Ctrl+Alt+F8`, or explicitly authorize that single
  focus-and-enable action, before automated interaction resumes. Startup/package evidence does not pass the visual cards.

In-game cards remaining:

1. **Proportions, stand, and variant appearance** (P0)
   - Config: Creative world, representative full-size and compact choices covering all wheel and hub materials.
   - Do: Place full-size and compact examples and view them straight on, at roughly 45 degrees, and directly from the side.
   - Expect: The full disc spans 1.6 blocks inside the 3x3 plane with deliberate edge clearance, a close-fitting bearing collar, and a thin 0.1875-block profile. The compact disc spans 0.92 block without intersecting its stand. The two-bearing timber stand visibly reaches the ground, its four braces meet the wooden bearing housings, and it reads as a supported machine rather than a decorative floating frame. Wood, stone, copper, the three bronzes, iron, meteoric iron, and steel textures remain correctly assigned; selected hub material is independently visible; every axle is wood; the red face and rim registration marks meet without a bright or dark gap.
   - Watch for: A wheel that nearly fills the footprint, a bulky central drum, an axle gap, wheel/frame clipping, disconnected braces, floating sleepers, missing stand components, material variants sharing the wrong texture, a split registration mark, or an edge-on profile that reads as a thick cylinder.

2. **Rotation and axle alignment** (P0)
   - Config: Creative world with a powered mechanical network.
   - Do: Place full-size flywheels on X, Y, and Z axes, connect each to mechanical power, and observe rotation from front and side. On one rig, connect and disconnect a machine or axle from the opposite side while the flywheel is moving.
   - Expect: Each wheel rotates around its axle without orbiting, wobbling, translating, or separating from the hub. Network rebuilding may rebase the displayed sign, but the wheel must not physically snap into the opposite direction or report a large new slip solely because topology changed.
   - Watch for: Wrong rotation axis, reversed model orientation, off-center pivots, axle/frame misalignment, or sudden counter-rotation after a connection change.

3. **Foundation, multiblock selection, and removal** (P0)
   - Config: Any full-size flywheel.
   - Do: First attempt unsupported full-size and compact placement, then place on the required solid footing. Place one horizontal full stand by targeting its bottom-center ground cell and another by targeting the center principal cell from the side. Target and break the center and several outer footprint cells, including a placement with negative X/Z coordinates if practical.
   - Expect: Unsupported placement gives the specific foundation message. Both bottom-center and center-principal targeting place the same supported horizontal full stand, using three ground blocks under the lowest row. Supported vertical placement uses the full 3x3 footing; compact placement uses the block beneath it. Every multiblock cell delegates to the principal flywheel, breaking any part removes the complete structure once, and no invisible blockers remain.
   - Watch for: Placement in midair, a vague `Not enough space` message for missing foundation, false rejection on solid ground, orphan part blocks, duplicate drops, missing selection, or a structure that fails after save/reload.

4. **Creative and handbook surface** (P1)
   - Config: Creative mode and handbook/search available.
   - Do: Search for `flywheel` and `slip transmission`, and inspect relevant creative tabs.
   - Expect: Sixty-eight active choices are discoverable, 22 full-size and 46 compact. The strength order is wood/stone, copper, bronze, iron/meteoric iron, then steel; every exposed hub is at the wheel's tier or above. Full-size hubs start at iron, while compact hubs also include copper and all three bronze alloys. Hovered items and placed blocks show rotating mass and effective inertia. No full-size stone flywheel, weaker-hub combination, Slip Transmission entry, item, recipe, or placeable block appears.
   - Watch for: Raw localization keys, missing physical comparison lines, a renderer crash, incorrect hub texture, steel wheels with weaker hubs, legacy generic flywheel aliases, or hidden transmission variants.

5. **Save/reload behavior** (P0)
   - Config: A placed and mechanically connected full-size flywheel.
   - Do: Let the flywheel rotate, save and exit, reload the world, then inspect and break an outer footprint cell.
   - Expect: The structure reloads intact, remains connected, retains its principal/part relationship, and responds normally.
   - Watch for: Missing outer-cell delegation, wrong-dimension lookups, mechanical network errors, or log exceptions.

## Follow-ups outside this candidate

- Replace the slip transmission's cross-network torque model before re-registering any content.
- Implement real inertial contribution for keyed flywheels before restoring their blocktypes.
- Consider additional wheel materials only after the ordered-hub renderer grouping and material progression receive feedback.
- Commission richer material-specific wheel/frame models and textures if the initial silhouette and construction choices test well.
- Design survival recipes and material progression after feedback establishes which variants are worth keeping. Include animal
  fat as bearing lubricant and evaluate a filled 3x3x1 casting-mold workflow for the very large metal constructions rather
  than pretending they fit a normal grid recipe.
- Balance inertia, coupling, losses, safe speed, and block info against real windmill/machine rigs.
- Add sound, wear, heat, and failure behavior only after the basic storage loop is understandable.

### Balance harness for follow-up tuning

Use a reproducible creative test rig before changing safe speed:

1. Windmill rotor with one, two, and four sail sets feeding a large wooden gear.
2. Record slow-side steady speed and spin-up time with no flywheel.
3. Put the flywheel on the large gear's 5.5x fast side; record steady speed, time to 25/50/75 percent of safe energy, and
   coast-down time.
4. Add one helve hammer, then two, and record minimum speed during a work cycle plus recovery time.
5. Repeat with representative wood/iron, iron/steel, and steel/steel full-size constructions and one compact construction.
6. While moving, disconnect and reconnect the consumer on each side of the flywheel and verify direction continuity.

The current 3.5 rps full-size safe speed is intentionally close to the approximately 3.3 rps expected from a vanilla
0.6-rps windmill through the 5.5x large-gear ratio. More sails mostly add torque and shorten spin-up; a slow-side rig can show
only a few percent stored energy because the tooltip reports the square of the speed ratio.

### Minimal later visual-asset commission

If feedback supports the curated release set, commission one coherent material pass rather than a new mechanic:

- Four drop-in full-size appearances: timber construction with visible planking/banding, cast or fabricated iron, meteoric
  iron with restrained crystalline differentiation, and worked steel. Add a compact segmented-stone appearance with iron
  restraint, but do not restore a monolithic full-size stone slab.
- Preserve the exact `(8, 8, 8)` model center, rotation axis, 1.6-block full-size diameter, 0.92-block compact diameter,
  0.1875-block full-size disc depth, stepped hub/bearing center, axle attachment semantics, and existing collision/selection
  footprints.
- Deliver Vintage Story shape JSON plus atlas-ready textures and front, three-quarter, and side previews for each construction
  and its compact counterpart.
- Keep the bearing collar and wooden axle visually consistent while giving the meteoric-iron and steel hubs restrained
  matching finishes. Focus the commission on readable material construction, restrained edge detail, and a stronger
  real-flywheel silhouette rather than animation, new gameplay, or additional variants.

### Scoped survival construction direction

The preferred full-size construction is a three-zone rotating assembly rather than the current solid material annulus:

1. A tiered metal hub and bearing assembly around the wooden axle.
2. An eight- or twelve-spoke timber web with a wooden felloe ring.
3. Curved plates forming a narrow material-specific outer tyre.

An illustrative 0.12-block radial iron tyre from radius 0.68 to 0.8 would use about 32 percent of the rotating metal mass of
the current solid iron annulus while retaining about 49 percent of that annulus's polar inertia before adding the wooden
felloe, spokes, hub, bearings, plates, and axle. This is not final balance, but it demonstrates why material concentrated at
the perimeter can remain powerful while making plate-based construction and the displayed mass more credible.

Recommended assembly progression:

1. Forge or craft a hub-and-bearing assembly from the selected hub material, a wooden axle, two bearing components, fastening
   hardware, and animal fat for lubrication.
2. Build a timber wheel web from spokes and curved felloe segments.
3. Curve several metal plates into tyre segments. The selected wheel material applies to this outer tyre, while the web
   remains wood.
4. Combine the hub assembly, timber web, and complete tyre set into a finished flywheel item.
5. Place the grounded timber stand first, reserving the existing multiblock footprint, then install the finished wheel item
   into the stand.

The stand should remain the single multiblock principal and block entity. Its assembly state can change from empty to
wheel-installed while storing wheel and hub material identity. This gives the desired physical installation interaction
without creating an independently placeable rolling wheel entity or multiple competing mechanical-network principals.
Breaking or disassembling the stand should return the installed wheel separately from the stand when safe.

Open decisions for a survival-design follow-up:

- Eight broad spokes versus twelve narrower spokes.
- A 0.08- to 0.12-block tyre radial width, chosen using rendered readability and the balance harness rather than recipe cost
  alone.
- Dedicated bearing items versus reuse of a generic parts item. Dedicated paired bearings communicate the mechanic better;
  generic parts keep the initial item surface smaller.
- Grid recipes versus contextual assembly interactions. Start with item intermediates plus one stand-install interaction;
  add staged in-world construction only if that interaction is fun in testing.
- Exact plate and fastener counts. Treat recipes as gameplay abstraction, but keep the tooltip's derived mass honest to the
  modeled timber and metal volumes.

## Retirement condition

Remove this packet after the initial-feedback pull request is merged or closed and all surviving follow-ups have durable issues or product documentation.

## Stand-first construction implementation, July 27, 2026

- The finished flywheel block-items now act as installable wheel assemblies. Direct placement is rejected with a specific
  stand-first message; the player places either a full-size or compact grounded stand and installs a matching assembly by
  interacting with it.
- Full-size empty stands reserve the same 3x3x1 cells and foundation as an installed wheel. Breaking an installed machine
  returns the stand and wheel assembly separately.
- The full-size rotating model is no longer a solid material annulus. It uses eight broad timber spokes, a timber felloe from
  radius 0.56 to 0.68 block, and a material-specific outer tyre from radius 0.68 to 0.8 block. Pivots, axle alignment, overall
  diameter, 0.1875-block depth, and the 3x3x1 interaction footprint remain unchanged.
- The horizontal stand brace rotation was corrected so its front and rear A-frame members terminate at the bearing support
  instead of projecting away into open space.
- Survival construction is intentionally abstracted into three intermediates: a lubricated hub-and-bearing set, timber web,
  and prepared rim or tyre. Full-size metal tyres require eight plates and compact metal blanks require four; animal fat is
  consumed in every bearing set.
- A future smithing or 3x3 casting workflow may replace the grid abstraction if feedback shows the extra interaction is worth
  the content and maintenance cost. It is not required for the initial-feedback loop.

## Twenty-four-view textured model review, July 27, 2026

- The full-size A-frame braces extend 1.5 model units farther into the cross-tie so their lower corners remain visibly joined
  from both isometric directions. Their upper endpoints, bearing alignment, rotation origins, footprint, and collision
  behavior are unchanged.
- The two full-size and two compact decorative hold-down blocks were removed. They read as unexplained metal squares on the
  base and did not contribute to support, interaction, collision, recipes, or mechanical behavior.
- The procedural review renderer now corrects opposite winding for the rear registration mark, which had allowed the red mark
  to appear through the wheel in the first isometric view even though the runtime mesh used the correct front/back winding.
- Every render manifest now produces eight views in three modes: six orthographic profiles and two opposing top-down
  isometrics, each in wireframe, stable material-ID color, and resolved-texture/UV mode. This yields exactly 24 primary images,
  three per-mode sheets, one combined sheet, and metadata containing input hashes, representation, texture resolution, bounds,
  modes, views, and image count.
- Texture lookup now follows Vintage Story's `game:` domain across installed `game`, `survival`, and `creative` content packs.
  Authored face UV rectangles and quarter-turn rotations are sampled directly; omitted cuboid UVs receive deterministic
  size-proportional mapping. Representative empty-stand and inventory/held-assembly manifests ensure those distinct shapes
  are reviewed independently from the installed mechanical model.
- The inventory/held assembly shapes are now generated from `FlywheelModelDimensions.cs`: eight full-size spokes plus
  sixteen-segment felloe, tyre, bearing, hub, and coupling rings, and sixteen-segment compact wheel/bearing/hub rings. This
  replaces the previous solid square hub and coarse overlapping preview bands. The evidence script fails if these authored
  package shapes drift from the dimension-driven generator.
- This renderer is automated geometry and texture evidence. It does not reproduce atlas padding, mipmaps, animation, game
  lighting, the player skeleton or hands, or final held transforms, so those remain bounded in-game QA.

## Coplanar-overlap and bearing-housing review, July 27, 2026

- The renderer now tests every pair of transformed face polygons for same-facing coplanarity and positive-area overlap.
  Shared edges and opposite-facing internal joints are excluded. Findings include element names, face names, overlap area,
  and plane distance in metadata; Flywheel's evidence script fails on any finding.
- The initial audit found same-plane overlap between the full-size crossed brace pairs. The front and rear layers are now
  separated symmetrically by 0.25 model unit (1/64 block), with their rotation origins shifted identically so slope, base
  connection, bearing connection, collision, and multiblock semantics remain unchanged.
- The audit also exposed the compact cross-tie boundary, the shaft/cap intersection, full-size spoke/felloe face overlap,
  and coplanar cuboid segments in the inventory/held preview shapes. These were fixed with source-level insets, deterministic
  segment depth layering, and tiny per-ring phase offsets rather than suppressed warnings.
- The metal grease-cup placeholders were removed from both stands. Each formerly solid bearing cap is now four wooden housing
  pieces surrounding a 4.5-by-4.5-model-unit shaft opening. The wooden axle passes through visible clearance instead of
  intersecting a solid timber block. The diagonal braces now terminate against the lower housing rail; their rotated maximum
  height is below the bore opening, so they no longer intrude into the axle path.

## Expanded material matrix and compact clearance, July 27, 2026

- The supported wheel materials are wood, copper, tin bronze, bismuth bronze, black bronze, iron, meteoric iron, and steel.
  Compact wheels additionally support stone. Full-size hubs remain iron, meteoric iron, or steel; compact hubs support
  copper, all three bronzes, iron, meteoric iron, and steel.
- Hub eligibility follows one generated strength order: wood/stone (0), copper (1), all bronzes (2), iron/meteoric iron (3),
  and steel (4). A hub must meet or exceed the wheel tier. The complete logical matrix contains 22 full-size and 46 compact
  assemblies, with exact renderer, texture, recipe, localization, creative, and handbook mappings for all 68.
- Copper and bronze physical profiles use the densities declared by Vintage Story 1.22.2's metal-plate assets: 8960 kg/m3
  copper, 7600 tin bronze, 7900 bismuth bronze, and 9000 black bronze. The tooltip mass and inertia therefore distinguish
  the alloys rather than treating bronze as one generic material.
- A deterministic generator now owns the player-facing material matrix. Builds, packaging, and visual evidence fail if its
  generated blocktypes, component items, recipes, or localization drift from the source policy.
- The compact horizontal stand's center cross-tie was split into front and rear members, and its bearing housings were moved
  outward by 0.5 model unit. The vertical bearing rails were shortened by 0.25 model unit. An axis-aware cylinder/cuboid test
  proves neither orientation intersects the 0.92-meter compact wheel.
- Representative copper/iron, black-bronze/steel, wood/copper, and mixed-bronze model manifests augment the existing
  material evidence. All 17 manifests render 24 primary images apiece with resolved textures and zero coplanar overlaps.
- The compact stone blank recipe now uses one chisel tool slot rather than accidentally requiring two chisels.

## Ground-timber proportion pass, July 28, 2026

- The horizontal full-size stand sleepers now rise 4 model units from the unchanged ground plane instead of 2; its front
  and rear cross ties rise 3.75 model units instead of 2 and start halfway up the sleepers rather than passing through
  nearly their full height. The horizontal compact stand sleepers likewise rise 4 model
  units instead of 1.5, and its cross ties rise 2.5 model units instead of 1.25.
- The compact cross ties moved outward to `z=0.5..2.25` and `z=13.75..15.5` so the heavier timber construction stays
  outside the 0.92-meter wheel envelope. Axle center, pivots, rotation axis, placement origin, collision/selection behavior,
  and reserved footprints remain unchanged.
- Four affected placed-model manifests, empty and installed full/compact stands, each produce 24 wireframe, material-ID,
  and textured views with resolved textures and zero coplanar overlaps. Automated cylinder/cuboid clearance and authored
  ground-height tests pass; Vintage Story remains authoritative for final in-game appearance.
- An independent renderer audit found that whole-face centroid sorting could paint one polygon in front even when its depth
  crossed another polygon within the same projected area. Material and textured modes now triangulate faces and use a
  deterministic NumPy per-pixel orthographic depth buffer with depth-tested outlines; three crossing-depth fixtures cover
  front and both opposing isometric views. Wireframe intentionally retains through-model edges.
- The audit also found that the registration mark was one flat quad spanning components at three different depths. Runtime,
  procedural-review, inventory, and held geometry now split it across the bearing face, coupling plate, and wheel surface,
  retaining continuous radial endpoints and the rim wrap without a floating sheet through the hub.
- A read-only survey of vanilla and installed-mod construction surfaces selected a bounded mixed workflow. Each supported
  hub metal has an anvil recipe producing four material-specific bearing fittings from one hot ingot. Compact bearing sets
  consume eight fittings, one wooden axle, and rendered fat; full sets consume 32 fittings, one axle, and fat, preserving
  the previous two-ingot and eight-ingot metal costs while removing hammer-in-grid smithing and the second whole axle.
- The heavier full-size stand now consumes eight vanilla support beams and four nails/strips, matching its structural
  timbers. Compact stands retain the simpler plank-and-nail recipe. Curved plate segments on the anvil and multi-stage
  in-world construction remain follow-ups because plate-only smithing is not enforceable through vanilla JSON and partial
  stand states would add serialization and model scope.

## Final 1.22.6 release-candidate checkpoint, August 11, 2026

- The released surface now contains 68 wheel-and-hub combinations: 22 full-size and 46 compact. Player-facing intermediate
  names consistently use "wheel." Compact assembly uses its wheel plus a bearing set; only full-size assembly retains a
  separate timber web.
- A forged bearing fitting is one open saddle, not a complete ring. Its anvil target is a 7-by-6 open U using 27 voxels,
  comfortably within one ingot, and yields four fittings. Compact bearing sets consume four fittings; full-size sets
  consume 16.
- Normal placed blocks no longer show Flywheel-specific telemetry. `ShowDebugBlockInfo` defaults off and restores the full
  diagnostic panel when deliberately enabled. Overspeed sparks and smoke remain visible gameplay feedback.
- Stand placement on a top or bottom face remains horizontal and follows player yaw by default. Holding Sneak while placing
  on that face deliberately selects the supported vertical orientation; side-face placement keeps the selected face axis.
- Flywheel Power targets Vintage Story 1.22.6. All six configured projects build with zero errors; the only build warnings
  are four pre-existing The BASICs analyzer findings. Flywheel Power tests pass 68/68 and DimensionLib tests pass 58/58.
  The all-repository The BASICs test lane is temporarily dependent on PR #219: current `main` lacks the 1.22.6
  `IServerPlayer.IsInInteractionRangeOf` test-fixture implementation and produces 123 unrelated dynamic-proxy failures.
- All six configured whitespace checks, the agent-tooling check, generated-content drift checks, the Lizard gate, and 54/54
  renderer regression tests pass. The exact model sweep covers 74 manifests and 1,776 primary images with zero unresolved
  textures, zero coplanar overlaps, and exactly 24 images per manifest.
- The exact package contains 31 allowlisted entries, is 83,918 bytes, and has SHA-256
  `AC6D9F8FCD9675DD07F7CCABF38AB245BB6606C30E92DFC833A9D02711565ED6`. Archive inspection finds no disabled content,
  compact-web residue, or obsolete player-facing tyre, rim, or blank labels.
- Runtime/model manual QA remains applicable because the final post-deployment delta changes recipes, compact mass, and the
  vertical-placement gesture. Before merge, the exact final package still needs the bounded observations: forge the 7-by-6
  saddle from one ingot and confirm four fittings; confirm compact/full bearing recipes consume 4/16 fittings; confirm
  handbook assembly uses wheel plus bearing for compact and wheel plus web plus bearing for full-size; confirm compact mass
  no longer counts a timber web; and confirm Sneak plus top/bottom placement selects the vertical orientation.
