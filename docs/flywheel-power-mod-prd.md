# Flywheel Power Mod PRD

> Historical design record: this PRD was written before the June 2026 prototype implementation. The prototype later expanded to full-size multiblock and compact variants. See `mods-dll/flywheelpower/README.md` for the intentionally narrower initial-feedback release surface.

## Status

- Working name: Flywheel Power Mod
- Branch: `feature/flywheel-prd`
- Worktree: `D:\bench\vs\Vintage-Story-Mods-wt\flywheel-prd`
- Product stage: historical pre-implementation PRD
- Target game: Vintage Story, current modding baseline aligned with the repository's Vintage Story dependency version

## Context

Vintage Story already has a player-facing mechanical power model built around speed, torque, resistance, axles, gears, brakes, clutches, transmissions, windmills, water wheels, and machines such as querns, pulverizers, and helve hammers. The flywheel mod should extend that existing model rather than introduce a separate energy unit or a second power graph.

The product goal is a practical mechanical power buffer that feels grounded in real flywheel behavior. It should smooth unreliable power, store short bursts of mechanical energy, and create a foundation for later control blocks and failure-mode gameplay.

## Product Thesis

The flywheel should not feel like an invisible battery block. It should feel like a rotating mass coupled to the existing mechanical network.

Core behavior:

- A flywheel stores rotational energy as speed.
- Stored energy grows with the square of speed.
- A slow flywheel can smooth small power fluctuations.
- A fast flywheel can provide useful burst power.
- High speed becomes increasingly lossy before it becomes dangerous.
- Real control mechanics such as clutches, torque limiters, brakes, and tachometers are the long-term gameplay surface.

## Design Principles

- Integrate with vanilla mechanical power.
- Prefer real mechanical concepts when they can be expressed simply.
- Keep V0.5 narrow enough to prove the feel of flywheel storage.
- Avoid fake RF-style energy units unless they are internal-only implementation details.
- Make speed useful, costly, and eventually risky.
- Treat materials as physical parameter sets, not simple battery tiers.
- Keep early failure modes limited, understandable, and recoverable.

## V0.5 Scope

V0.5 is a single-block flywheel that connects to the existing mechanical power network and proves the core storage behavior.

### V0.5 Goals

- Add one placeable flywheel block.
- Connect to vanilla mechanical power through normal axle-compatible connectors.
- Track internal flywheel speed independently from the network speed.
- Transfer torque through a simplified slip-coupled model.
- Store and release energy through speed differences between the flywheel and network.
- Apply speed-scaled energy loss from day one.
- Cap transfer torque so energy cannot be dumped instantly into a stopped or slow network.
- Provide readable player feedback through block info or debug info.
- Damage or destroy only the flywheel on overspeed, if overspeed behavior is included in V0.5.

### V0.5 Non-Goals

- No multiblock flywheels.
- No explosive shrapnel or player/block area damage.
- No separate electrical or mana-like storage layer.
- No full reimplementation of Vintage Story mechanical power networks.
- No complex clutch, torque limiter, sprag clutch, or auto-clutch blocks.
- No deep material progression beyond the minimum needed for the first flywheel.
- No GUI unless block info is insufficient.

## V0.5 User Stories

- As a player with a windmill, I want to buffer wind fluctuations so my machines do not immediately stall during short lulls.
- As a player with intermittent machine loads, I want to store excess rotation and spend it on short bursts of work.
- As a player building mechanical rooms, I want the flywheel to visibly and mechanically fit into axle/gear layouts.
- As a player learning the block, I want block info to explain whether it is charging, discharging, or losing speed.
- As a server operator, I want the block to be practical and not destructive in its first version.

## Core Mechanics

### Real-World Reference Model

A real flywheel stores rotational kinetic energy:

```text
energy = 0.5 * inertia * angularSpeed^2
```

Where:

- `inertia` is the flywheel's resistance to angular acceleration.
- `angularSpeed` is its rotational speed.
- Doubling speed gives four times the stored energy.
- Increasing mass or radius increases inertia, with rim mass being more valuable than hub mass.

In a rigid real-world shaft system, the flywheel and shaft share one angular speed. The flywheel stores energy when net torque accelerates the shaft and returns energy when load torque decelerates it.

Vintage Story's mechanical network does not expose a full distributed inertia simulation. V0.5 should therefore model the flywheel as a slip-coupled device with its own internal speed.

### Game Abstraction

The V0.5 flywheel internally tracks:

- `flywheelSpeed`
- `inertia`
- `maxTransferTorque`
- `baseBearingLoss`
- `viscousBearingLoss`
- `windageLoss`
- `safeSpeed`
- optional `damageSpeed`

The mechanical network provides or implies:

- `networkSpeed`
- network torque and resistance through existing Vintage Story APIs

Transfer is based on speed difference:

```text
deltaSpeed = flywheelSpeed - networkSpeed
requestedTorque = couplingStrength * deltaSpeed
transferTorque = clamp(requestedTorque, -maxTransferTorque, maxTransferTorque)
```

Interpretation:

- Positive transfer torque means the flywheel is faster than the network and discharges into it.
- Negative transfer torque means the network is faster than the flywheel and charges it.
- The transfer cap represents finite clutch/belt/coupling strength.
- Any speed mismatch implies slip, which allows future heat and wear behavior.

### Loss Scaling

V0.5 must include speed-scaled loss. This is part of the core feel, not a later polish item.

Suggested loss model:

```text
lossTorque = baseBearingLoss
           + viscousBearingLoss * abs(flywheelSpeed)
           + windageLoss * flywheelSpeed^2

if abs(flywheelSpeed) > safeSpeed:
    lossTorque *= 1 + 5 * (abs(flywheelSpeed) / safeSpeed - 1)
```

Design meaning:

- Base bearing loss is the low-speed mechanical tax.
- Viscous bearing loss covers grease, lubricant shear, and speed-dependent bearing drag.
- Windage loss represents air drag and should dominate at higher speeds.

Real-world grounding:

- Aerodynamic drag force scales roughly with velocity squared.
- Rim velocity increases with angular speed and radius.
- Power lost to drag is torque times speed, so high-speed air losses grow very quickly.

Gameplay result:

- Low-speed storage is efficient but weak.
- Medium-speed storage is useful and practical.
- High-speed storage is powerful but increasingly wasteful.
- Dangerous overspeed can be introduced without making the first version destructive.

V0.5 treats `safeSpeed` as a rated limit rather than a hard cap. Stored energy remains visible above 100% of rated safe
capacity, the tooltip reports the exact rated-speed percentage, orange-red hub sparks and bearing smoke intensify above the rating, and
bearing/windage losses rise linearly from their already speed-scaled baseline. Catastrophic damage is deliberately deferred
until material-dependent strength, warning, debris, and multiplayer consequences can be designed and tested together.

### Transfer Torque Cap

V0.5 must avoid instant energy dumping. A stopped network connected to a high-speed flywheel should not receive all stored energy in one tick.

The transfer cap represents the practical limit of a coupling. It also sets up future failure-mode mechanics:

- If the cap is low, the flywheel slips and transfers power gradually.
- If future upgrades raise the cap, players can transfer more burst power but risk shock loads.
- If future failure modes are enabled, sudden engagement can damage the coupling or flywheel.

V0.5 should use the cap for feel and safety only, not for complex damage propagation.

## Player Feedback

V0.5 should expose enough information to make the block understandable without a GUI.

Block info should include some subset of:

- Flywheel speed.
- Stored energy percent.
- Charge/discharge/idle state.
- Current loss rate or qualitative loss state.
- Coupling state such as slipping or synced.
- Safe speed warning if near overspeed.

Potential qualitative labels:

- `Idle`
- `Absorbing network power`
- `Supplying network power`
- `Coasting`
- `Slipping`
- `High windage losses`
- `Overspeed risk`

## V0.5 Acceptance Criteria

- A flywheel block can be placed in-world and connected to vanilla mechanical power.
- When connected to a faster powered network, the flywheel speed increases over time.
- When connected to a slower loaded network, the flywheel speed decreases while contributing useful torque.
- Transfer torque is capped and does not instantly equalize large speed differences.
- The flywheel loses speed over time when coasting.
- Losses increase with flywheel speed.
- The block's player-facing info clearly indicates whether it is charging, discharging, or coasting.
- Removing or unloading the block does not corrupt the mechanical network.
- State persists across world save/load.
- The mod can be built and packaged through the repository's normal mod build flow once implementation begins.

## V1 Scope Direction

V1 should make flywheel systems interesting to build, not just useful to place.

Candidate V1 features:

- Multiblock flywheels.
- Material-dependent inertia, safe speed, losses, and balancing requirements.
- Independent hub/fixture materials separate from wheel material.
- Protective housings as a separate material surface for damage containment or maintenance safety.
- Better bearings and lubrication.
- Ghetto tachometer block or handheld tach tool.
- More visible rotational animation and sound feedback.
- Basic flywheel damage model.
- Configurable server tuning for loss, capacity, and failure severity.

### Multiblock Flywheels

V1 should consider moving serious storage to multiblock structures.

Reasons:

- Large flywheels should occupy meaningful physical space.
- Bigger radius matters in real inertia calculations.
- Multiblocks support visible engineering builds.
- They provide natural progression beyond the V0.5 single block.

Possible multiblock concepts:

- Axle hub plus rim segments.
- Spoked wheel with metal banding.
- Heavy enclosed flywheel housing.
- Counter-rotating paired flywheels as a late-game stability upgrade.

### Hubs, Fixtures, and Housings

The center shaft fitting should be treated as a hub or fixture rather than as part of the wheel material. This gives players a useful visual and mechanical distinction:

- Wheel/rim material controls inertia, safe speed, and stored energy potential.
- Hub/fixture material controls torque transfer, shaft shock tolerance, and keyed or friction-coupled failure limits.
- Housing material can later control whether flywheel damage stays contained, whether shrapnel-style failures are suppressed, and how safe maintenance is near high-speed flywheels.

V0.5 can expose separate visual wheel and hub materials for demo clarity. Mechanical differentiation should wait until the balance targets are clearer.

## V1.5 Scope Direction

V1.5 should introduce richer mechanical control and failure-mode gameplay.

Candidate V1.5 features:

- Torque limiter.
- Flywheel brake.
- Fail-closed spring clutch.
- Auto-clutch.
- Sprag or one-way clutch.
- Materialized clutch and gear torque ratings.
- Fixed-ratio and variable-ratio mechanical transmissions.
- Slip heat and clutch wear.
- Overspeed damage progression.
- Bad balance effects.

### Balance Test Targets

The flywheel needs tuning against actual mechanical loads before material stats become final. Useful test rigs:

- One small windmill driving a single helve hammer.
- One small windmill driving a line of helve hammers.
- One large windmill driving the same loads.
- Powered network charging a flywheel, then disconnecting or starving the source to observe discharge duration.

The main question is whether stored energy feels like smoothing and burst support rather than a battery that trivializes windmill sizing.

### Transmission and Torque Ratings

Vintage Story's current clutch and wooden gear visuals imply weak materials that should not survive arbitrary shock loads. A future torque model should consider the total available source torque and the resistance/load currently connected to a network.

Potential design direction:

- Wooden clutches and gears have low torque ratings and fail or slip under shock loads.
- Bronze, iron, meteoric iron, and steel variants raise torque ratings and durability.
- A fixed-ratio transmission trades speed for torque or torque for speed between two mechanical networks.
- A variable-ratio transmission can ramp the ratio over time, effectively revving the output network instead of instantly locking two different speeds together.
- A transmission may need to be implemented as a bridge between two vanilla mechanical networks, which is conceptually awkward but could model real behavior better than trying to force everything into one rigid network.

This should remain a V1.5+ system unless V0.5 balance proves impossible without at least a simple torque limiter.

### Torque Limiter

A torque limiter protects the system by slipping or disconnecting above a threshold.

Gameplay role:

- Prevent destructive shock loads.
- Let players tune safety versus burst transfer.
- Add a mechanical upgrade that is useful before advanced automation.

### Flywheel Brake

A brake dumps stored rotational energy intentionally.

Gameplay role:

- Safely stop a flywheel before maintenance or rewiring.
- Provide controlled energy disposal.
- Add sound, heat, and wear hooks later.

### Fail-Closed Spring Clutch

A cheap emergency clutch that snaps shut when source-side speed gets too low.

Gameplay role:

- Low-tech backup power engagement.
- Manual reset required.
- Cheap but rough on the flywheel/coupling.
- Fits the desired ghetto mechanical power aesthetic.

### Auto-Clutch

A more expensive configurable clutch with low/high thresholds and hysteresis.

Gameplay role:

- Smoothly automate charge/discharge behavior.
- Reduce manual micromanagement.
- Provide late early-game or mid-game mechanical control.

### Sprag Clutch

A one-way clutch that transmits torque in one direction and freewheels in the other.

Gameplay role:

- Charge-only or discharge-only flywheel setups.
- Prevent backdrive.
- Make mechanical layouts more expressive.

## Materials and Physical Implications

Materials should not be simple capacity tiers. Each material should imply tradeoffs.

### Wood

Wood is plausible for a primitive flywheel, but it should be low-speed and low-capacity.

Potential behavior:

- Cheap and available early.
- Low inertia unless built large.
- Low safe speed.
- Higher windage and bearing loss.
- Useful for smoothing tiny loads, not serious storage.

### Stone

Stone is dense but brittle and poor in tension. That makes it awkward for a true flywheel.

Design stance:

- Avoid a simple `crafted from loose stones` flywheel.
- If included, make it an in-world carved millstone or boulder-derived wheel.
- Require balancing or careful crafting.
- Give it high mass but very low safe speed.
- Make it bad at high RPM.

Open concern:

- Vintage Story does not currently let players casually pick up boulders, so stone flywheel acquisition needs a believable crafting or world-interaction path.

### Metal

Metal should be the practical long-term path.

Potential behavior:

- Higher safe speed.
- Better balance.
- Better compatibility with improved bearings.
- Higher effective storage due to safe speed more than raw mass.

Likely tiers:

- Crude banded wooden flywheel.
- Cast or wrought iron flywheel.
- Steel flywheel.
- Enclosed high-speed flywheel, if the mod ever wants late-game storage.

## Failure Model Direction

V0.5 should be conservative. V1.5 can become more interesting.

Failure progression candidates:

- Warning sounds and wobble near safe speed.
- Increased losses above safe speed.
- Crack damage above damage speed.
- Permanent max-speed degradation after damage.
- Destruction if overspeed continues.
- Coupling damage from high slip.
- Bearing damage from sustained high speed.
- Balance damage from crude materials or poor assembly.

V1.5 should not start with area explosions unless explicitly chosen later. The preferred early failure boundary is damage or destruction of the flywheel/coupling only.

## Implementation Notes

The implementation should inspect and integrate with Vintage Story's existing `Vintagestory.GameContent.Mechanics` classes.

Important concepts from current sources:

- `MechanicalNetwork` tracks network speed, torque, resistance, and angle.
- `IMechanicalPowerNode.GetTorque(long tick, float speed, out float resistance)` is the key torque/resistance hook.
- Vanilla brakes add variable resistance.
- Vanilla rotors produce torque toward target speed.
- Vanilla axles and transmissions add small resistance.
- Vanilla networks already include speed-based drag per node.

Potential implementation shape:

- Add a new mod under `mods-dll/` once implementation begins.
- Implement a block entity behavior that participates as a mechanical power node.
- Use `GetTorque` to return flywheel-driven torque or charging resistance based on speed difference.
- Persist internal flywheel speed and damage state in tree attributes.
- Reuse vanilla mechanical rendering patterns where possible.

Key technical question:

- Can a custom block entity behavior cleanly join vanilla mechanical networks and provide custom torque/resistance without patching `MechanicalNetwork`? If yes, V0.5 should avoid Harmony patches. If no, evaluate the smallest patch surface.

## Resolved V0.5 Decisions

- Overspeed is non-destructive in V0.5. It uses uncapped rated-capacity feedback, escalating visual warning, and progressive
  loss scaling above rated speed. Damage and destruction remain later failure-model work.

## Open Questions

- What should the first flywheel material be: wood, iron, or a deliberately generic prototype flywheel?
- Should V0.5 expose exact numeric speed/energy or mostly qualitative information?
- Should stored energy be displayed as a percent of current safe capacity?
- How much should a single-block V0.5 flywheel help with a quern, pulverizer, or helve hammer?
- Should the default coupling behave like a friction clutch, belt, or abstract coupler?
- Should the V1 ghetto tach be a block, a handheld tool, or both?

## Suggested First Implementation Milestone

Before building recipes, art, or many tiers, implement a prototype block with configurable constants and debug block info.

Prototype constants:

```text
inertia
couplingStrength
maxTransferTorque
baseBearingLoss
viscousBearingLoss
windageLoss
safeSpeed
damageSpeed
```

Prototype validation scenarios:

- Charge from a creative rotor or strong windmill.
- Coast disconnected and verify speed decays faster at high speed.
- Discharge into a quern or other consumer.
- Connect high flywheel speed to a stopped/slow network and verify transfer is gradual.
- Save/load and verify stored speed persists.
- Break/remove and verify the mechanical network rebuilds cleanly.

## PRD Completion Criteria

This PRD is ready for implementation planning when these decisions are made:

- V0.5 overspeed behavior is chosen: rated, visible, progressively lossy, and non-destructive.
- First flywheel material/theme is chosen.
- Desired V0.5 player feedback style is chosen.
- Initial balancing targets are chosen for at least one vanilla machine load.
- Technical feasibility of custom vanilla mechanical-power node integration is confirmed.
