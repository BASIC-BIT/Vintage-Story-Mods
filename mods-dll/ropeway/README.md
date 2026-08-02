# Cable Car (`ropeway`)

An aerial ropeway for Vintage Story. Build pylon towers by hand, string a haul rope between them,
and ride a cabin across the valley.

## How it works

1. Craft a **Pylon Footing** and place it on the ground where the tower should stand. It turns to face
   you; look at it to read which way the cabin will pass through off the block info panel, and turn it
   down the line before you build anything.
2. Right-click the footing. Translucent ghost cells light up every one of the 13 blocks still missing
   or wrong, and they clear themselves as you fill them: two four-block log or plank posts either side,
   and a five-wide crossarm of **Ropeway Braces** with the **Pylon Head** in the middle.
3. Sneak + right-click the footing for the tower guide - the build order in words next to a 3D view of
   the pieces.
4. Right-click a completed tower's footing with an empty hand to pick a link target from a list. The
   haul rope cost is charged from your inventory across as many stacks as it takes. The same click
   calls the cabin home if there is one at the other end.
5. Place a **Ropeway Cabin** at an end tower, climb in, and it departs a few seconds later.

Every click a tower takes lands on the footing. The pylon head four blocks up is the sheave the rope
runs over and the cabin's mast threads through, and nothing else.

A tower carries at most two spans, so a line is always a simple path. A tower with exactly one span is
an end of the line, and the cabin stops there.

## Limits

- Maximum span: 48 blocks, measured sheave to sheave (four blocks above each footing).
- Maximum total line length: 320 blocks.
- One cabin per line, two seats.
- Haul rope: one per four blocks of span, rounded up. Removing a span refunds the same rounded down.

See the in-game handbook under **Ropeway** for the full rules.
