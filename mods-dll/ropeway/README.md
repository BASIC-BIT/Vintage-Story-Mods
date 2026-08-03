# Cable Car (`ropeway`)

An aerial ropeway for Vintage Story. Build pylon towers by hand, string a haul rope between them,
and ride a cabin across the valley.

## How it works

1. Craft a **Pylon Footing** and place it on the ground where the tower should stand. It turns to face
   you; look at it to read which way the cabin will pass through off the block info panel, and turn it
   down the line before you build anything.
2. Right-click the footing. Translucent ghost cells light up every one of the 15 blocks still missing
   or wrong, and they clear themselves as you fill them: two four-block posts either side - logs,
   debarked logs, planks, raw stone, cobblestone, drystone, polished rock or stone bricks - and a
   seven-wide crossarm of **Ropeway Braces** with the **Pylon Head** in the middle.
3. Sneak + right-click the footing for the tower guide - the build order in words next to a 3D view of
   the pieces.
4. Right-click a completed tower's footing with an empty hand to pick a link target from a list. The
   haul rope cost is charged from your inventory across as many stacks as it takes. The same click
   calls the cabin home if there is one at the other end.
5. Place a **Ropeway Cabin** at an end tower, climb in, and it departs a few seconds later. Left alone it
   runs to the end of the line; press the **ropeway stop key** (**R** by default, rebindable in Settings >
   Controls) to get off at a tower in between. Each press steps the selection one tower further along and
   wraps past the end, which is also how you leave an interior tower in the other direction.

Every click a tower takes lands on the footing. The pylon head four blocks up is the sheave the rope
runs over and the cabin's hanger threads through, and nothing else.

Corners are gentle-only. A tower faces one of four cardinals, so a right-angle bend puts the cabin
through a post - see the handbook's "Corners" and `docs/KNOWN-ISSUES.md`.

A tower carries at most two spans, so a line is always a simple path. A tower with exactly one span is
an end of the line, and the cabin stops there.

## Limits

- Maximum span: 48 blocks, measured sheave to sheave (four blocks above each footing).
- Maximum total line length: 320 blocks.
- One cabin per line, two seats.
- Haul rope: one per four blocks of span, rounded up. Removing a span refunds the same rounded down.

See the in-game handbook under **Ropeway** for the full rules.
