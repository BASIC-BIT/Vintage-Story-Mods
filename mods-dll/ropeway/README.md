# Cable Car (`ropeway`)

An aerial ropeway for Vintage Story. Build pylon towers by hand, string a haul rope between them,
and ride a cabin across the valley. The rope is a loop - one strand at sheave height carrying the cabin
and one coming back over it, a bullwheel's diameter above, turned round by the wheel at each terminal.

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
5. Build two of the line's towers as **stations**. A station is the same sixteen cells in the same
   footprint - place a **Drive Station Footing** or a **Tension Station Footing** instead of the plain one
   and the overlay asks for machinery down one leg and along half the crossarm rather than posts and
   braces. The drive station takes an axle from your mill at the foot of its leg and turns the rope; the
   tension station hangs a counterweight in its leg and keeps it taut. A line needs one of each: no drive
   station and the cabin will not move, no finished tension station and it will not take a cabin at all.
6. Place a **Ropeway Cabin** at an end tower, climb in, and it departs a few seconds later. Left alone it
   runs to the end of the line; press the **ropeway stop key** (**R** by default, rebindable in Settings >
   Controls) to get off at a tower in between. Each press steps the selection one tower further along and
   wraps past the end, which is also how you leave an interior tower in the other direction.

Every click a tower takes lands on the footing. The pylon head four blocks up is the sheave the rope
runs over and the cabin's hanger threads through, and nothing else; on a station that cell is the
**Bullwheel** instead, geared to the machinery along the crossarm and turning whenever the line runs.

The path CURVES through a corner tower rather than turning a hard angle there, and the drawn rope and the
station rail are sampled off the same curve - so at a corner the three read as one mechanism. What decides
whether the cabin clears the posts is which way the tower faces: a corner wants its crossarm square to the
halfway line between its two spans, and a tower has only four cardinals to offer. Get it right and even a
right angle is clean; get it wrong and the cabin clips a post, and the line says so in chat when you string
the span that makes the corner. Warning only, never a refusal - see the handbook's "Corners" and
`docs/KNOWN-ISSUES.md`.

A tower carries at most two spans, so a line is always a simple path. A tower with exactly one span is
an end of the line, and the cabin stops there.

## Limits

- Maximum span: 48 blocks, measured sheave to sheave (four blocks above each footing).
- Maximum total line length: 320 blocks.
- One cabin per line, two seats.
- Haul rope: one per four blocks of span, rounded up. Removing a span refunds the same rounded down.

See the in-game handbook under **Ropeway** for the full rules.
