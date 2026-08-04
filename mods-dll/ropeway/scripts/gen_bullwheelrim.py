"""Emit shapes/block/bullwheelrim.json - the turning half of the bullwheel.

A spoked wheel standing in the plane of the line, above the crossarm, on the sheave's drive boss. It is
drawn by BullwheelRenderer, NOT by the chunk tesselator, so it may sit outside its own block cell: the
cells above a crossarm are empty in every tower.

Why above the rope rather than around it: the rope line at y=8 is the sheave throat, and the cabin's
hanger blade rides up that throat at every tower. Anything in there is something the cabin hits.
"""
import json, math, pathlib

OUT = pathlib.Path(__file__).resolve().parents[1] / "assets/ropeway/shapes/block/bullwheelrim.json"

CY = 25.7          # wheel centre, in the block's own 1/16 units. The SWEPT circle has to clear the crossarm
                   # cell at 16, not the authored pose: the furthest corner sits 9.6504 out, so the bar is
                   # 25.6504. 25.2 looked clear only because the check below measured the flat-face reach.
R = 9.0            # outer radius. 18 units across = 1.125 blocks, so the wheel clears the crossarm
RIM = 1.4          # radial thickness of the felloe
SPOKES = 8         # 4 bars through the hub
SEG = 8            # octagonal rim

apothem = R * math.cos(math.pi / SEG)
half = R * math.sin(math.pi / SEG)
FACES = {f: {"texture": "#metal"} for f in ("north", "east", "south", "west", "up", "down")}


def el(name, x0, y0, z0, x1, y1, z1, deg):
    return {"name": name, "from": [x0, y0, z0], "to": [x1, y1, z1],
            "rotationOrigin": [8.0, CY, 8.0], "rotationX": deg, "faces": FACES}


elements = [el("hub", 6.3, CY - 2.2, 5.8, 9.7, CY + 2.2, 10.2, 0)]

# 4 bars through the centre = 8 spokes, at 45 degree steps. Each bar is 0.02 thinner than the last so no
# two of them share an x plane where they cross at the hub - the crossing is buried inside the hub anyway,
# but coplanar faces inside a solid are still coplanar faces and this scene is checked for them.
for i in range(SPOKES // 2):
    inset = 0.02 * i
    elements.append(el(f"spoke{i}", 6.75 + inset, CY - (apothem - RIM / 2), 7.2,
                       9.25 - inset, CY + (apothem - RIM / 2), 8.8, 45.0 * i))

# The felloe, as an octagon of chords. Neighbours meet at a mitre they cannot cut, so they overlap a corner;
# alternating segments are 0.02 narrower so no two touching ones share an x plane.
for i in range(SEG):
    inset = 0.02 * (i % 2)
    elements.append(el(f"felloe{i}", 6.5 + inset, CY + apothem - RIM / 2, 8 - half,
                       9.5 - inset, CY + apothem + RIM / 2, 8 + half, 360.0 * i / SEG))

OUT.write_text(json.dumps({
    "textureWidth": 16, "textureHeight": 16,
    "textures": {"metal": "game:block/metal/sheet/iron1"},
    "elements": elements,
}, indent=2) + "\n")

# Rotating a corner about the axle does not change its distance from the axle, so the angle each box is
# authored at says nothing about how low the wheel gets once it turns: what dips into the cell below is the
# furthest CORNER of any element, swept all the way round. The check here used to be
# hypot(a, h) * cos(atan2(h, a)), which reduces to a exactly - the flat-face reach - so its max() compared
# the apothem with itself and printed a number it had not computed.
reach = max(math.hypot(y - CY, z - 8)
            for e in elements
            for y in (e["from"][1], e["to"][1])
            for z in (e["from"][2], e["to"][2]))

print("elements", len(elements), "| wheel spans y",
      round(CY - (apothem + RIM / 2), 3), "..", round(CY + (apothem + RIM / 2), 3),
      "| swept reach", round(reach, 4), "| lowest swept point", round(CY - reach, 4),
      "(the crossarm cell tops out at 16)")
