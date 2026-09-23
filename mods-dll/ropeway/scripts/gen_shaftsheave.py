"""Emit the four shapes a SHAFT needs that a ropeway tower has no use for.

  shaftsheave.json   the head sheave's static half: bedplate, headframe, axle, chain case and the WRAP
  shaftrim.json      its turning half, drawn by BullwheelRenderer exactly as bullwheelrim.json is
  shaftweight.json   the counterweight, drawn by ShaftRenderer at the car's mirror
  shaftstrand.json   one block of haul rope, scaled in Y by ShaftRenderer to make either strand

ONE FRAME, and every number below hangs off it. The sheave cell is SpanMath.SheaveHeight above the shaft
head's footing, so the block's own centre IS the line's top anchor. The counterweight's lane runs
2 * BEBullwheel.ShaftWrapOut = 3.0 blocks along the head's passage facing, which in the north frame these
shapes are authored in is -Z; the wheel's hub sits half way along it, so the going strand is tangent to the
rope circle at the shaft axis (z = 8) and the return strand at z = 8 - 48. The hub's HEIGHT is
BullwheelRenderer.RimPivotY = 25.7 units, which is the height layshaft and drivehead already run their bar
at - so the drive is one unbroken shaft from the gearbox to the wheel and nothing had to move for it.

WHY THE WRAP IS AUTHORED HERE and not drawn like the strands: it never moves. The two strands have moving
ends (see ShaftRenderer), the arc between their two tangent points does not, so it is chunk mesh and costs
nothing per frame. BEPylonBase.WrapPath is left alone for the same reason it is left alone on a ropeway
terminal - it builds its arc in the vertical plane containing the tower's DeadSide, which is null at a
vertical terminal by construction, and generalising it would be two parameters bought for one caller.

Run: python scripts/gen_shaftsheave.py
"""
import json
import math
import pathlib

OUT = pathlib.Path(__file__).resolve().parents[1] / "assets/ropeway/shapes/block"

B = 16.0
HUB_Y = 25.7                  # BullwheelRenderer.RimPivotY, in the block's own 1/16 units
RHO = 24.0                    # BEBullwheel.ShaftWrapOut = 1.5 blocks: the rope's radius on this wheel
HUB_Z = 8.0 - RHO             # the lane runs -Z in the north frame
ROPE = 0.96                   # BEPylonBase.CableRadius, in units - the rope is 1.92 across

# The rim's swept corner has to sit exactly one rope half-thickness inside the rope circle, which is the same
# derivation BullwheelRenderer.WrapRadius carries for the bullwheel: bed the rope on the felloe's CORNERS,
# not its flats, or the corners stand proud through the rope's own cross-section on every quarter turn.
REACH = RHO - ROPE
FELLOE = 2.4                  # radial thickness of the rim band
SEG = 8                       # octagonal rim, as bullwheelrim.json
RIM_X0, RIM_X1 = 5.0, 11.0    # the wheel's own thickness band; the headframe stands outside it

WRAP_CHORDS = 9               # midpoints at 90, 67.5 ... -90 degrees, so the two END chords straddle the
                              # tangent points exactly as BEPylonBase.WrapPath's do
WRAP_STEP = 180.0 / (WRAP_CHORDS - 1)

TEX = {
    "girder": "game:block/metal/riveted/iron1",
    "shaft": "game:block/metal/plate/steel",
    "machine": "game:block/metal/tarnished/iron",
    "stone": "game:block/stone/rock/andesite1",
    "rope": "game:block/cloth/reedrope",
}


def solve_radius(target):
    """Outer radius whose furthest swept corner is `target` units from the axle."""
    def reach(r):
        apothem = r * math.cos(math.pi / SEG)
        half = r * math.sin(math.pi / SEG)
        return math.hypot(apothem + FELLOE / 2, half)

    lo, hi = 1.0, 60.0
    for _ in range(80):
        mid = (lo + hi) / 2
        if reach(mid) < target:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


RIM_R = solve_radius(REACH)


def faces(tex):
    return {f: {"texture": "#" + tex} for f in ("north", "east", "south", "west", "up", "down")}


def el(name, a, b, tex, origin=None, rot_x=None):
    e = {"name": name, "from": [round(v, 4) for v in a], "to": [round(v, 4) for v in b]}
    if origin is not None:
        e["rotationOrigin"] = [round(v, 4) for v in origin]
        e["rotationX"] = round(rot_x, 4)
    e["faces"] = faces(tex)
    return e


def write(name, elements, keys):
    (OUT / name).write_text(json.dumps({
        "textureWidth": 16, "textureHeight": 16,
        "textures": {k: TEX[k] for k in keys},
        "elements": elements,
    }, indent=2) + "\n")
    return len(elements)


# ---------------------------------------------------------------- the head sheave, static half
sheave = [
    # A bedplate with a 4x4 hole down the middle, because the going strand comes up through this cell to the
    # wheel. It is the block's whole floor otherwise: the car's roof stops half a block under it (the parked
    # car's roof top is anchor - 1.0 and this cell's bottom is anchor - 0.5), so nothing ever passes through.
    el("bedplatenorth", [0, 0, 0], [16, 2, 6], "girder"),
    el("bedplatesouth", [0, 0, 10], [16, 2, 16], "girder"),
    el("bedplatewest", [0, 0, 6], [6, 2, 10], "girder"),
    el("bedplateeast", [10, 0, 6], [16, 2, 10], "girder"),
]

# The headframe's beam clears the ROPE and not the rim: the wrap runs at radius RHO with its own
# half-thickness on top of that, so the bar has to start above HUB_Y + RHO + ROPE = 50.66 or the arc goes
# through it. Measured on the render before it was moved - the rim's top at 48.74 was what the first
# number cleared, and the rope is a unit and a half further out than the metal it is bedded on.
TOP = HUB_Y + RHO + ROPE + 5

# How far the beam reaches PAST the hub, along the lane. It used to be 6 units, which carried the hanger
# and nothing else: the wheel's own far swept edge is REACH = 23.04 units out from that hub and the return
# strand is a full RHO = 24 out, so the frame covered the shaft strand, stopped 17 units short of the wheel
# it is drawn to carry, and left the wheel and the counterweight's rope hanging in open air past the end of
# it. That reads exactly as the author's "the rope isn't aligned with the wheel, it's off to the side" -
# renders/qa1/shaft-before. Now it clears the return strand's far face by a unit, so the bar visibly spans
# BOTH strands with the wheel slung under its middle, which is what a headframe over a counterweighted
# hoistway looks like. It stays a cantilever off the one column pair on purpose: the only place a second
# pair could stand is the lane, and the lane is the column the counterweight travels down.
BEAM_REACH = RHO + ROPE + 1
for tag, x0, x1 in (("west", 0.5, 4.5), ("east", 11.5, 15.5)):
    sheave += [
        # Outside the wheel's own thickness band and on the far side of the cell from the lane, so the
        # headframe carries the wheel without standing in the rope, the wheel or the car's way.
        el("column" + tag, [x0, 2, 9.5], [x1, TOP, 15.5], "girder"),
        el("beam" + tag, [x0, TOP - 4, HUB_Z - BEAM_REACH], [x1, TOP, 9.5], "girder"),
        el("hanger" + tag, [x0, HUB_Y + 1, HUB_Z - 3], [x1, TOP - 4, HUB_Z + 3], "girder"),
    ]

sheave += [
    el("hubaxle", [0.5, HUB_Y - 1, HUB_Z - 1], [15.5, HUB_Y + 1, HUB_Z + 1], "shaft"),
    # The lay shaft next door brings the bar in at x 16, z 7..9, y 24.7..26.7 - its own `shaft` element - and
    # this case carries it across to the axle. East of the wheel's thickness band, so nothing turns inside it.
    # Its west face is 0.2 clear of the east hanger's, which stands in the same column: two plates flush in
    # one plane is 10 unit^2 of z-fight, and the renderer's coplanar audit is what said so.
    el("chaincase", [11.7, HUB_Y - 3, HUB_Z - 2], [16, HUB_Y + 3, 9], "machine"),
]

# THE WRAP. Chord midpoints on the rope circle, vertices on rho / cos(step/2) so the midpoints land on rho
# rather than the corners - the same construction WrapPath uses, for the same reason: the FIRST chord's
# midpoint has to sit exactly on the going strand's centreline and the LAST on the return strand's, or the
# rope leaves the wheel off the strand it is supposed to leave on.
vertex = RHO / math.cos(math.radians(WRAP_STEP) / 2)
chord = vertex * math.sin(math.radians(WRAP_STEP) / 2)
for k in range(WRAP_CHORDS):
    # Alternating chords a fiftieth of a unit thinner. The arc turns in a VERTICAL plane, so every chord's
    # side faces lie in the two planes x = 8 +/- ROPE and a mitred joint leaves them coplanar; phasing the
    # thickness is gen_bullwheelrim.py's own trick and BEPylonBase.JointPhase's.
    inset = 0.02 * (k % 2)
    sheave.append(el(
        f"wrap{k}",
        [8 - ROPE + inset, HUB_Y + RHO - ROPE, HUB_Z - chord],
        [8 + ROPE - inset, HUB_Y + RHO + ROPE, HUB_Z + chord],
        "rope", [8.0, HUB_Y, HUB_Z], 90.0 - k * WRAP_STEP))

count = write("shaftsheave.json", sheave, ("girder", "shaft", "machine", "rope", "stone"))

# ---------------------------------------------------------------- the rim, turning half
apothem = RIM_R * math.cos(math.pi / SEG)
half = RIM_R * math.sin(math.pi / SEG)
origin = [(RIM_X0 + RIM_X1) / 2, HUB_Y, 8.0]

# Authored about z = 8 and NOT about the hub: BullwheelRenderer.RimMatrix rotates about
# (0.5, RimPivotY, 0.5) and then translates by BEBullwheel.WrapOffset, which on a shaft is the lane's own
# half. Author it at the hub as well and the wheel ends up two half-lanes out.
rim = [el("hub", [RIM_X0 + 0.6, HUB_Y - 2.9, 8 - 2.9], [RIM_X1 - 0.6, HUB_Y + 2.9, 8 + 2.9], "machine")]
for i in range(SEG // 2):
    inset = 0.02 * i
    rim.append(el(f"spoke{i}",
                  [RIM_X0 + 0.9 + inset, HUB_Y - (apothem - FELLOE / 2), 8 - 1.1],
                  [RIM_X1 - 0.9 - inset, HUB_Y + (apothem - FELLOE / 2), 8 + 1.1],
                  "machine", origin, 45.0 * i))
for i in range(SEG):
    inset = 0.02 * (i % 2)
    rim.append(el(f"felloe{i}",
                  [RIM_X0 + 0.5 + inset, HUB_Y + apothem - FELLOE / 2, 8 - half],
                  [RIM_X1 - 0.5 - inset, HUB_Y + apothem + FELLOE / 2, 8 + half],
                  "machine", origin, 360.0 * i / SEG))
write("shaftrim.json", rim, ("machine",))

# ---------------------------------------------------------------- the counterweight
# Its rope point is the TOP of this column, 56 units = 3.5 blocks above the shoe - which is the car's own
# hangDrop + CabinHalfHeight. So the two bodies are exact mirrors and the weight's shoe lands on the foot's
# tension guide at the same instant the car's floor lands on the head's plinth.
write("shaftweight.json", [
    el("shoe", [1, 0, 1], [15, 2, 15], "girder"),
    el("mass", [2, 2, 2], [14, 38, 14], "stone"),
    el("crown", [1, 38, 1], [15, 40, 15], "girder"),
    el("rod", [7, 40, 7], [9, 56, 9], "shaft"),
], ("girder", "stone", "shaft"))

# ---------------------------------------------------------------- one block of rope
write("shaftstrand.json", [el("strand", [8 - ROPE, 0, 8 - ROPE], [8 + ROPE, 16, 8 + ROPE], "rope")], ("rope",))

print(f"sheave elements    {count}  (of which {WRAP_CHORDS} are the wrap)")
print(f"rim outer radius   {RIM_R:.3f} units   swept reach {REACH:.3f}   rope radius {RHO:.1f}")
print(f"hub                (8, {HUB_Y}, {HUB_Z})   lane {2 * (8 - HUB_Z) / B:.2f} blocks")
print(f"rim spans y        {HUB_Y - REACH:.2f} .. {HUB_Y + REACH:.2f}")
print(f"parked car roof    -8.00 units (anchor - 1.0)  -> rim clearance {(HUB_Y - REACH + 8) / B:.3f} blocks")
print(f"headframe top      {TOP:.2f} units = {TOP / B:.2f} cells above the sheave's own floor")
