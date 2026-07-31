# Cable Car (`ropeway`)

An aerial ropeway for Vintage Story. Build gantry towers by hand, string a haul rope between them,
and ride a cabin across the valley.

## How it works

1. Craft a **Pylon Head** and place it where the sheave should sit. It turns to face you, and the rest
   of the tower is measured from it: the rear gantry goes three blocks away from you. Look at the head
   to read the compass direction off the block info panel.
2. Right-click the pylon head. Translucent ghost cells light up every one of the 21 blocks still
   missing or wrong, and they clear themselves as you fill them: two log or plank posts under each
   gantry corner, **Ropeway Braces** across the top, front and rear.
3. Sneak + right-click the pylon head for the tower guide - the build order in words next to a 3D
   view of the pieces.
4. Right-click a completed tower with an empty hand to pick a link target from a list. The haul rope
   cost is charged from your inventory across as many stacks as it takes. The same click calls the
   cabin home if there is one at the other end.
5. Place a **Ropeway Cabin** at an end tower, climb in, and it departs a few seconds later.

A tower carries at most two spans, so a line is always a simple path. A tower with exactly one span is
an end of the line, and the cabin stops there.

## Limits

- Maximum span: 48 blocks.
- Maximum total line length: 512 blocks.
- One cabin per line, two seats.
- Haul rope: one per four blocks of span, rounded up. Removing a span refunds the same rounded down.

See the in-game handbook under **Ropeway** for the full rules.
