using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ropeway.Tests;

/// <summary>
/// The C# and the asset JSON were built in separate lanes, so every code one side hardcodes is a
/// handshake the other side can silently break. These are those handshakes, and nothing else - the
/// game itself is the only thing that can tell you whether a shape looks right.
/// </summary>
public class RopewayAssetContractTests
{
    private static readonly string ModRoot = FindModRoot();
    private static readonly string Assets = Path.Combine(ModRoot, "assets", "ropeway");

    private static string FindModRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "mods-dll", "ropeway");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate mods-dll/ropeway above " + AppContext.BaseDirectory);
    }

    private static JsonElement Load(params string[] relative)
    {
        var path = Path.Combine(new[] { Assets }.Concat(relative).ToArray());
        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        return JsonDocument.Parse(File.ReadAllText(path), options).RootElement.Clone();
    }

    [Theory]
    // RopewayLinkService.HaulRopeCode, BlockPylonBase.CabinItemCode, RopewayLinkService.CabinEntityCode.
    [InlineData("itemtypes/haulrope.json", "haulrope")]
    [InlineData("itemtypes/cabin.json", "cabin")]
    [InlineData("entities/cabin.json", "cabin")]
    [InlineData("blocktypes/pylonbase.json", "pylonbase")]
    [InlineData("blocktypes/pylonhead.json", "pylonhead")]
    [InlineData("blocktypes/bullwheel.json", "bullwheel")]
    [InlineData("blocktypes/brace.json", "brace")]
    [InlineData("blocktypes/tensionweight.json", "tensionweight")]
    [InlineData("blocktypes/drivehousing.json", "drivehousing")]
    // The stations and the seven cells their legs and crossarms want. Every one of these codes is named by a
    // multiblockStructure wildcard, and a typo there is a tower that can never be completed.
    [InlineData("blocktypes/drivestation.json", "drivestation")]
    [InlineData("blocktypes/tensionstation.json", "tensionstation")]
    [InlineData("blocktypes/layshaft.json", "layshaft")]
    [InlineData("blocktypes/drivehead.json", "drivehead")]
    [InlineData("blocktypes/driveshaft.json", "driveshaft")]
    [InlineData("blocktypes/tensionhead.json", "tensionhead")]
    [InlineData("blocktypes/tensionguide.json", "tensionguide")]
    public void CodesTheGameplayCodeHardcodesExist(string file, string expectedCode)
    {
        Assert.Equal(expectedCode, Load(file.Split('/')).GetProperty("code").GetString());
    }

    /// <summary>
    /// The mechanical power hookup, all of which is JSON that C# cannot check for itself. It lives on the
    /// DRIVE HOUSING at the foot of a station's machine leg, and on nothing else - the three footings and
    /// the bullwheel are all asserted clean, because a second consumer anywhere on a tower would let
    /// <c>PoolSpeed</c> see one network twice through two different blocks.
    /// <para>
    /// <c>mechPartShape: null</c> is the one that has to be asserted rather than trusted:
    /// <c>BEBehaviorMPBase.Initialize</c> defaults <c>Shape</c> to <c>Block.Shape</c> and then
    /// UNCONDITIONALLY calls <c>AddDeviceForRender</c>, so dropping this key does not fail, it puts the
    /// whole housing model into the instanced spinning renderer. An explicit JSON null is what makes
    /// <c>AsObject</c> return null - a missing key returns the default instead - so the key must be present
    /// AND null.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheDriveHousingDeclaresTheVanillaMPConsumerBehaviour()
    {
        var housing = Load("blocktypes", "drivehousing.json");
        var behaviors = housing.GetProperty("entityBehaviors").EnumerateArray().ToList();
        var consumer = behaviors.Single(b => b.GetProperty("name").GetString() == "MPConsumer");

        var properties = consumer.GetProperty("properties");
        Assert.True(properties.TryGetProperty("mechPartShape", out var shape), "mechPartShape must be present");
        Assert.Equal(JsonValueKind.Null, shape.ValueKind);

        // The behaviour's own default is 0.1. The station that owns this cell rewrites it every second, so
        // what the JSON pins is the state a housing is in before its first tick - which must be the idle
        // one, or a fresh chunk load taxes the network for a second on every drive of a long line.
        Assert.Equal(RopewayPower.IdleResistance, properties.GetProperty("resistance").GetSingle(), 4);

        Assert.Equal("BlockDriveHousing", housing.GetProperty("class").GetString());
        Assert.Equal("DriveHousing", housing.GetProperty("entityClass").GetString());

        // No side variant: the housing connects on every horizontal face, so orientation decides nothing -
        // and a block with no orientation cannot be placed 90 degrees out, which is the failure the
        // crossarm's oriented blocks still carry.
        Assert.False(housing.TryGetProperty("variantgroups", out _));

        foreach (var footing in new[] { "pylonbase.json", "drivestation.json", "tensionstation.json" })
        {
            Assert.False(Load("blocktypes", footing).TryGetProperty("entityBehaviors", out _));
        }

        // The bullwheel turns; it does not consume. It carried this behaviour for one trial, and leaving it
        // on would put the drive back four blocks up, where reaching it cost sixteen vanilla blocks of
        // scaffold.
        Assert.False(Load("blocktypes", "bullwheel.json").TryGetProperty("entityBehaviors", out _));
    }

    /// <summary>
    /// The turning half of the bullwheel is a SEPARATE shape, drawn by <c>BullwheelRenderer</c> over the
    /// static one, and at a TERMINAL it no longer stands over the tower. It moves one cell out along the
    /// dead side and <see cref="BullwheelRenderer.WrapDrop"/> down, so its groove lands on the haul rope and
    /// <see cref="BEPylonBase.WrapPath"/> can close the rope round it.
    /// <para>
    /// So THE OLD ASSERT HAS LOST ITS PREMISE IN THAT POSE and this test is its replacement rather than its
    /// deletion. It used to say the rim stays above the crossarm cell the cabin passes through; the wrapped
    /// rim does enter that cell's airspace, and nothing is there to hit. The property the old one was ever a
    /// proxy for is asserted directly instead: NOTHING THE CABIN CAN REACH TOUCHES THE WHEEL, swept over
    /// every position travel can put a cabin in and measured against every element of its shape.
    /// </para>
    /// <para>
    /// The un-wrapped pose keeps the old assert, because there the premise is exactly right: a station the
    /// line runs THROUGH draws no wrap, its wheel stays where it was, and a cabin really does pass under it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach()
    {
        var rim = Load("shapes", "block", "bullwheelrim.json").GetProperty("elements").EnumerateArray().ToList();
        Assert.NotEmpty(rim);

        var centre = rim[0].GetProperty("rotationOrigin")[1].GetDouble();

        // The RENDERER spins the rim about this same axis, and it is the one number the two lanes share.
        // Re-author the wheel about a different centre with the renderer left alone and it stops turning and
        // starts orbiting. This pins the number; TheRimTurnsOnItsOwnAxleAtEveryAngleAndEveryFacing pins the
        // chain that consumes it, because either one alone leaves the other free to drift.
        // A tolerance rather than decimal places: the float constant is 1.6062500476837158 against the
        // double's 1.60625, which lands exactly on a 4-dp midpoint and rounds the two opposite ways. Any
        // drift worth catching is a tenth of a unit, 0.00625 blocks, sixty times the bar.
        Assert.Equal(BullwheelRenderer.RimPivotY, centre / 16, 1e-4);

        // Every element is a box turned about the wheel's own axis, and turning a corner about that axis does
        // not change its distance from it - so the angles the boxes are authored at say nothing about how low
        // the wheel gets, and what may dip into the cell is the furthest CORNER of any element swept all the
        // way round. Reading each box at its own authored angle was what hid the 0.45 unit this used to miss:
        // the octagon rests on a flat and brings a corner to the bottom a twentieth of a turn later.
        var reach = 0.0;
        var ball = 0.0;
        foreach (var element in rim)
        {
            // X and Z as well as Y, because the renderer hardcodes the block's own centre line for both and
            // a rim re-authored about a different one would turn crabwise with nothing to say so.
            var origin = element.GetProperty("rotationOrigin").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            Assert.Equal(8, origin[0], 3);
            Assert.Equal(centre, origin[1], 3);
            Assert.Equal(8, origin[2], 3);

            var from = element.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            var to = element.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

            var swept = 0.0;
            foreach (var y in new[] { from[1], to[1] })
            foreach (var z in new[] { from[2], to[2] })
            {
                swept = Math.Max(swept, Math.Sqrt((y - centre) * (y - centre) + (z - 8) * (z - 8)));
            }

            reach = Math.Max(reach, swept);

            // The furthest any of it ever gets from the AXLE, which is a sphere about the axle and therefore
            // says the same thing whichever way the wheel is turned and whichever way it is carried out. Per
            // element, because the hub is the widest along the axle and the felloe the furthest out from it,
            // and neither wins on both.
            var spanX = Math.Max(Math.Abs(from[0] - 8), Math.Abs(to[0] - 8));
            ball = Math.Max(ball, Math.Sqrt(spanX * spanX + swept * swept));
        }

        // THE UNWRAPPED POSE, unchanged: a station the line runs through keeps its wheel over the tower, and
        // there the rim really does have to stay out of the cell the cabin's hanger blade rides up into.
        Assert.True(centre - reach >= 16,
            $"the turning wheel sweeps down to {centre - reach}, inside the crossarm cell the cabin passes through");

        // rho is FORCED, not chosen: it is the swept reach with the cable's own half-thickness bedded on it.
        // Bed the rope on the felloe's flats instead and the corners stand proud through the rope's own
        // cross-section every quarter turn, which is a wheel that saws its own cable.
        Assert.Equal(BullwheelRenderer.WrapRadius, (reach + BEPylonBase.CableRadius * 16) / 16, 1e-4);

        // THE LOOP'S OWN SEPARATION, tied to the wheel right here where the wheel is measured. A rope that
        // enters the groove at the bottom and wraps 180 degrees leaves at the top, one DIAMETER up, so
        // re-authoring the rim moves both strands rather than leaving a literal behind.
        Assert.Equal(BEPylonBase.ReturnLift, 2 * (reach / 16 + BEPylonBase.CableRadius), 1e-4);

        // The frustum sphere, over BOTH poses the line can put the wheel in - a number that fits only the
        // tallest stops meaning anything when the tallest is deleted. Nothing in the game complains when it
        // is too small; the wheel simply vanishes at the edge of the screen on a tower the player is looking
        // at. The HOLD-DOWN is what sets it now: at a station the line runs through, the wheel goes straight
        // up onto the return strand and its axle stands 1.989 blocks off the block centre against the
        // wrapped pose's 1.200.
        var wrapped = Math.Sqrt(BullwheelRenderer.WrapOut * BullwheelRenderer.WrapOut
                                + BullwheelRenderer.WrapRadius * BullwheelRenderer.WrapRadius);
        var held = BullwheelRenderer.RimPivotY - 0.5 + BullwheelRenderer.HoldDownRise;

        Assert.True(held > wrapped, "the hold-down is no longer the pose that reaches furthest");
        Assert.True(BullwheelRenderer.CullRadius >= held + ball / 16,
            $"the held-down wheel reaches {held + ball / 16} blocks from the block centre, outside the "
            + $"{BullwheelRenderer.CullRadius}-block frustum sphere it is culled against");

        // THE HOLD-DOWN'S OWN TANGENCY, and it is why the wheel moves at all at a through station. Where it
        // rests, the axle is 1.106 above the anchor and the return strand 1.326, so 0.220 above the axle and
        // 1.123 blocks of rope run INSIDE the swept circle every revolution. Lifted, the rim's lowest swept
        // point lands exactly on the strand's upper surface.
        Assert.True(BullwheelRenderer.RimPivotY - 0.5 - reach / 16 < BEPylonBase.ReturnLift,
            "the return strand clears the resting wheel, so the hold-down lift is buying nothing");
        Assert.Equal(BEPylonBase.ReturnLift + BEPylonBase.CableRadius, held - reach / 16, 1e-4);

        // ---------------------------------------------------------------- the cabin, swept
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();

        // The whole wrapped assembly is ONE DISC in the plane it turns in: the rim sweeps everything inside
        // its own reach and the drawn ring is bedded on the outside of that, so rho + CableRadius covers both
        // and neither can be regressed without the other noticing. Frame is u along the dead side from the
        // tower centre and w above the rope's centreline, which is where the wheel's groove sits.
        var wheel = BullwheelRenderer.WrapRadius + BEPylonBase.CableRadius;

        (string Name, double Gap) Worst(double stand)
        {
            var worst = ("", double.MaxValue);
            foreach (var (name, element) in elements)
            {
                // Both ways along the line: the cabin is symmetric front to back and its shape's +X faces
                // whichever end of the line it happens to be pointing at, so its reach toward the wheel is
                // the larger of the two ends.
                var along = Math.Max(
                    Math.Abs(element.GetProperty("from")[0].GetDouble()),
                    Math.Abs(element.GetProperty("to")[0].GetDouble())) / 16;
                var low = element.GetProperty("from")[1].GetDouble() / 16 - hangDrop;
                var high = element.GetProperty("to")[1].GetDouble() / 16 - hangDrop;

                // EVERY POSITION THE CABIN CAN REACH. Travel is clamped to the anchors - MinTravel and
                // MaxTravel are Cumulative[0] and Cumulative[n-1] and PositionAt clamps outside them - so on
                // the dead side of a terminal the furthest a cabin ever comes is parked AT the tower. Swept
                // rather than assumed, so a park spot moved past the anchor fails here rather than in game.
                for (var step = 0; step <= 64; step++)
                {
                    var travelled = -8.0 * step / 64;
                    var du = Math.Max(0, Math.Max(travelled - along - stand, stand - travelled - along));
                    var dw = Math.Max(0, Math.Max(low - BullwheelRenderer.WrapRadius,
                        BullwheelRenderer.WrapRadius - high));

                    var gap = Math.Sqrt(du * du + dw * dw) - wheel;
                    if (gap < worst.Item2) worst = (name, gap);
                }
            }

            return worst;
        }

        var closest = Worst(BullwheelRenderer.WrapOut);
        Assert.True(closest.Gap > 0,
            $"{closest.Name} is {-closest.Gap:0.####} blocks inside the wrapped wheel. A clamp closed ON the "
            + "rope and a wheel the rope is tangent to cannot share a point, so the wheel has to stand "
            + "further out along the dead side - it cannot be lowered where it is.");

        // The grip is what comes closest, which is the whole reason the wheel could not simply drop: it is
        // the only part of a cabin that reaches above the rope at all.
        Assert.Equal("jawtop", closest.Name);
        Assert.Equal(0.286, closest.Gap, 3);

        // ...and the same sweep with the wheel left where it stands is NEGATIVE. That is what makes this an
        // assert rather than a restatement of the constant: at zero offset the grip is INSIDE the wheel,
        // 1.44 units into the rim on its own and 3.36 into the rim and the ring together.
        Assert.True(Worst(0).Gap < 0,
            "this passes with the wheel over the tower, so it is not measuring the wrap at all");

        // The clearance that is actually load bearing, and it is in PLAN. The ring's near edge lands 4.43
        // units out along the line and the grip reaches 2.10, so the two never share a column and no
        // vertical margin is holding this up - which is why one cell rather than the 0.641 blocks at which
        // the wrap merely stops cutting the grip.
        var grip = Find(elements, "jawtop").GetProperty("to")[0].GetDouble() / 16;
        Assert.Equal(0.146, BullwheelRenderer.WrapOut - wheel - grip, 3);
    }

    /// <summary>
    /// THE CABIN NEVER TOUCHES THE RETURN STRAND, and the proof is a pure vertical - which is why no position
    /// and no yaw can beat it. The whole cabin, every element of it, is under <c>jawtop</c> at 0.15 blocks
    /// above the rope it is clamped to; the whole return strand is over 1.2663. So the gap is 1.1163 blocks,
    /// it is the same 1.1163 with the cabin parked at a terminal under the wrap's own arc, and there is no
    /// plan geometry in it at all to sweep.
    /// <para>
    /// The bar is the jaw's OWN authored play on the rope it is clamped to - 0.0025 blocks per side. 1.1163
    /// is 446 times it. Asserting the mechanism rather than a swept minimum is deliberate: a sweep would pass
    /// for whatever cabin length and whatever yaw law happened to be shipped, and this fails the moment any
    /// part of the cabin reaches above the strand's underside, which is the only way it could ever touch.
    /// </para>
    /// <para>
    /// Lateral stacking is what this compares against and it is worse on the same number: 2*rho ACROSS the
    /// line leaves 0.94 blocks at a right-angle corner, against a roof rather than a grip.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCabinNeverReachesTheReturnStrandAtAnyPositionOrYaw()
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();

        var highest = ("", double.MinValue);
        foreach (var (name, element) in elements)
        {
            var top = element.GetProperty("to")[1].GetDouble() / 16 - hangDrop;
            if (top > highest.Item2) highest = (name, top);
        }

        // The grip, and it is the only part of a cabin that reaches over the rope at all.
        Assert.Equal("jawtop", highest.Item1);
        Assert.Equal(0.15, highest.Item2, 4);

        var underside = BEPylonBase.ReturnLift - BEPylonBase.CableRadius;
        var gap = underside - highest.Item2;

        // The jaw's own play on the rope it IS closed on, which is the tightest bar there is to beat.
        var jaw = (Find(elements, "jawtop").GetProperty("from")[1].GetDouble()
                   - Find(elements, "jawbottom").GetProperty("to")[1].GetDouble()) / 32 - BEPylonBase.CableRadius;

        Assert.True(gap > jaw,
            $"{highest.Item1} comes within the jaw's own {jaw} blocks of play of the return strand");
        Assert.Equal(0.0025, jaw, 4);
        Assert.Equal(1.1163, gap, 4);
    }

    /// <summary>
    /// ...AND THE SWEPT PROOF, over the geometry that is actually drawn, because the mechanism above is only
    /// as good as its premise - that the strand really is <c>ReturnLift</c> over the rope the jaw is closed
    /// on, everywhere. At a plain END tower it was not: the lift ramped back to zero over the tower's own
    /// <c>TrimForTowers</c> window so the two strands converged onto the sheave, and the cabin parks on that
    /// sheave and departs along the ramp. Measured off the shipped emission, the return strand's centreline
    /// was inside the <c>jawtop</c> plate from the anchor to 0.77 blocks out, deepest at 0.065 - 0.77 blocks
    /// of travel, every trip, both ways, on any line with a plain tower at an end. That is the same defect at
    /// the same scale as the one <c>BULLWHEEL-WRAP-SPEC</c> §3b refused a dropped wheel for (0.90 blocks).
    /// <para>
    /// A ramp that starts anywhere else does not exist to move it to. The strand's own half thickness on
    /// either side of a plate at w +0.0625..+0.15 means the lift is inside cabin metal for 0.2075 of its
    /// 1.3263 blocks, which is 16% of ANY window at ANY span length; travel is clamped to the anchor and the
    /// plate reaches 0.131 blocks past it, so there is no stretch of a span the cabin cannot put its grip on.
    /// What the strand ends on instead is <c>returnshoe</c>, which was already under it -
    /// <see cref="TheTowerCarriesTheReturnStrandOnItsOwnShoe"/>.
    /// </para>
    /// <para>
    /// So this walks every point of every drawn return strand against every position the cabin can hang at,
    /// on five topologies, and the one it exists for is the first. The wrap's own arc is swept against a
    /// parked cabin by <see cref="TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach"/> and is not
    /// re-measured here.
    /// </para>
    /// </summary>
    [Fact]
    public void TheReturnStrandStaysAWheelAboveTheCabinOnEveryTopology()
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();

        // The grip: the only part of a cabin that reaches above the rope, and how far along the line it
        // reaches from wherever the cabin is standing.
        var jawtop = Find(elements, "jawtop");
        var plate = jawtop.GetProperty("to")[1].GetDouble() / 16 - hangDrop;
        var reach = Math.Max(Math.Abs(jawtop.GetProperty("from")[0].GetDouble()),
                             Math.Abs(jawtop.GetProperty("to")[0].GetDouble())) / 16;

        var jaw = (jawtop.GetProperty("from")[1].GetDouble()
                   - Find(elements, "jawbottom").GetProperty("to")[1].GetDouble()) / 32 - BEPylonBase.CableRadius;

        RopewayLine Line(params (int X, int Y, int Z)[] towers) =>
            RopewayLine.FromTowers(towers.Select(t => new BlockPos(t.X, t.Y, t.Z)).ToList())!;

        // me is the tower that draws; the topologies are every shape a half-span can be drawn in.
        var topologies = new (string Name, RopewayLine Line, int Me, bool Pitched)[]
        {
            ("plain END tower", Line((0, 64, 0), (0, 64, 30)), 0, false),
            ("plain END tower, one-block span", Line((0, 64, 0), (1, 64, 0)), 0, false),
            ("plain END tower, 53 degrees of pitch", Line((0, 64, 0), (3, 68, 0)), 0, true),
            ("mid-line straight", Line((0, 64, -24), (0, 64, 0), (0, 64, 24)), 1, false),
            ("90 degree corner", Line((0, 64, -24), (0, 64, 0), (24, 64, 0)), 1, false)
        };

        foreach (var (name, line, me, pitched) in topologies)
        {
            var worst = double.MaxValue;
            for (var peer = 0; peer < line.Towers.Length; peer++)
            {
                if (peer == me) continue;

                var going = BEPylonBase.HalfSpanPath(line, me, peer);
                var returning = BEPylonBase.Lift(going, BEPylonBase.ReturnLift);

                // EVERY position the cabin can hang at against EVERY point of the drawn strand. The cabin
                // hangs on the going strand, so its plate stands `plate` above going[i]; the strand is only
                // over that plate while it is within the grip's own half length along the line.
                for (var i = 0; i < going.Count; i++)
                for (var j = 0; j < returning.Count; j++)
                {
                    var dx = going[i].X - going[j].X;
                    var dy = going[i].Y - going[j].Y;
                    var dz = going[i].Z - going[j].Z;
                    if (dx * dx + dy * dy + dz * dz > reach * reach) continue;

                    worst = Math.Min(worst, returning[j].Y - BEPylonBase.CableRadius - (going[i].Y + plate));
                }
            }

            Assert.True(worst > jaw,
                $"on a {name} the cabin's grip comes within {worst:0.####} blocks of the return strand, "
                + $"inside the {jaw} blocks of play the jaw is authored with on the rope it IS clamped to");

            // Flat, the sweep measures the mechanism's own number and cannot beat it. Pitched, it gives up
            // one sample step of the rope's own rise across the grip - 0.1 blocks at 53 degrees - because the
            // slot is horizontal and the rope through it is not, which is a fact about the jaw that predates
            // the loop and is measured against the going strand in ONETRACK-REVIEW-geometry.
            if (!pitched) Assert.Equal(1.1163, worst, 4);
            else Assert.True(worst > 1.0, $"a pitched span gives up {1.1163 - worst:0.####} blocks");
        }
    }

    /// <summary>
    /// The tightest thing the stacked loop creates, and it is LATERAL rather than vertical: at a terminal the
    /// return strand threads BETWEEN the bullwheel's own bearing caps. Their tops stand 0.24 units INTO the
    /// strand's vertical band, so nothing vertical is holding this up - what clears is 0.74 units per side,
    /// and <c>bullwheel.json</c> cannot widen its caps after this.
    /// <para>
    /// Derived off the shape rather than pinned as a pose: every element whose y band overlaps the strand's
    /// is measured, so a new element authored into that band is caught rather than assumed away.
    /// </para>
    /// </summary>
    [Fact]
    public void TheReturnStrandClearsTheBullwheelsOwnBearings()
    {
        // The strand's band in the head cell's own units: the cell's centre is the anchor, so y = 8 is w = 0.
        var low = 8 + 16 * (BEPylonBase.ReturnLift - BEPylonBase.CableRadius);
        var high = 8 + 16 * (BEPylonBase.ReturnLift + BEPylonBase.CableRadius);
        var half = 16 * BEPylonBase.CableRadius;

        var worst = ("", double.MaxValue);
        var reaching = new List<string>();

        foreach (var shape in new[] { "bullwheel.json", "pylonhead.json" })
        foreach (var element in Load("shapes", "block", shape).GetProperty("elements").EnumerateArray())
        {
            var name = shape[..^5] + "." + element.GetProperty("name").GetString();
            var from = element.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            var to = element.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

            // The shoe is what CARRIES the strand; its top face is the strand's own underside by
            // construction, so it is the one element allowed to touch the band.
            if (to[1] <= low + 1e-6 || from[1] >= high - 1e-6) continue;

            reaching.Add(name);

            // Across the line, which is the head cell's own x: the throat runs along z on both shapes. The
            // better of the two sides, because an element clears by standing off ONE of them - a box that
            // straddles the strand fails on both and reports the shallower burial, which is still negative.
            var clear = Math.Max(from[0] - (8 + half), 8 - half - to[0]);
            if (clear < worst.Item2) worst = (name, clear);
        }

        // Exactly two things reach into the band, both of them the wheel's bearings. Named, so a third
        // arriving fails here rather than quietly changing what "worst" means.
        Assert.Equal(new[] { "bullwheel.bearingcapeast", "bullwheel.bearingcapwest" }, reaching.OrderBy(n => n));

        Assert.True(worst.Item2 > 0,
            $"{worst.Item1} is {-worst.Item2:0.###} units into the return strand, which passes between the "
            + "bullwheel's own bearings and has nowhere else to go");
        Assert.Equal(0.74, worst.Item2, 2);
    }

    /// <summary>
    /// The plain tower carries the return strand on its own shoe, and the shoe's TOP FACE is that strand's
    /// underside - derived from <see cref="BEPylonBase.ReturnLift"/> rather than typed, so re-authoring the
    /// rim moves the saddle with the rope instead of leaving it a fifth of a unit out.
    /// <para>
    /// The mast starts where <c>housing</c> ends, which is what keeps the 0.20-unit soffit clearance over a
    /// parked cabin's grip - the tightest number in the machine - untouched by any of this;
    /// <see cref="TheCabinFitsThroughTheTower"/> still owns it.
    /// </para>
    /// <para>
    /// SHORT along the line, and that is the lesson the deleted rail plate paid for rather than a dimension:
    /// a fixture on the tower's own cardinal is only correct where the path passes through the anchor, and
    /// that is one point. A long shoe would re-earn the bug where a guide roller ended up 1.37 units inside a
    /// plate that was right when parked.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTowerCarriesTheReturnStrandOnItsOwnShoe()
    {
        var head = Load("shapes", "block", "pylonhead.json").GetProperty("elements").EnumerateArray().ToList();

        JsonElement Element(string name) =>
            head.Single(e => e.GetProperty("name").GetString() == name);

        var shoe = Element("returnshoe");
        var mast = Element("returnmast");

        var shoeFrom = shoe.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var shoeTo = shoe.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

        // THE one derived number: the saddle's top face is the strand's own underside.
        Assert.Equal(8 + 16 * (BEPylonBase.ReturnLift - BEPylonBase.CableRadius), shoeTo[1], 4);

        // As wide as the sheave throat below it and no wider - the strand's own slot, both ends of the tower.
        var throat = Element("sheavecheekeast").GetProperty("from")[0].GetDouble()
                     - Element("sheavecheekwest").GetProperty("to")[0].GetDouble();
        Assert.Equal(throat, shoeTo[0] - shoeFrom[0], 4);

        // 8 units along the line, the sheave's own depth. Nothing longer.
        Assert.Equal(8, shoeTo[2] - shoeFrom[2], 4);
        Assert.True(shoeTo[2] - shoeFrom[2] <= 8, "the return shoe is a fixture on the tower's cardinal, so a "
                                                  + "longer one is metal a corner's rope runs off the side of");

        // A flat saddle, not a channel: nothing stands above its top face to catch a strand that has run off
        // the side of it at a corner.
        Assert.All(head, e => Assert.True(
            e.GetProperty("to")[1].GetDouble() <= shoeTo[1] + 1e-6,
            $"{e.GetProperty("name")} stands above the return shoe's bearing face"));

        // The mast picks up where the sheave housing stops, so no clearance below the crossarm moves.
        Assert.Equal(Element("housing").GetProperty("to")[1].GetDouble(),
            mast.GetProperty("from")[1].GetDouble(), 4);
        Assert.Equal(shoeFrom[1], mast.GetProperty("to")[1].GetDouble(), 4);

        // ...and bullwheel.json gets NEITHER, because at a terminal the wheel is the carrier and at a station
        // the line runs through it is lifted onto the strand instead.
        Assert.DoesNotContain(
            Load("shapes", "block", "bullwheel.json").GetProperty("elements").EnumerateArray(),
            e => e.GetProperty("name").GetString()!.StartsWith("return"));
    }

    /// <summary>
    /// The mass is authored in the SHAPE now, not drawn by the block entity at a height that meant a charge,
    /// and it has to hang inside the guide rails rather than through its own pad or out the top. Its cell
    /// used to be a six-element shape reaching three cells up out of a one-cell hole with nothing checking
    /// the headroom; the rails now carry on in <c>ropeway:tensionguide</c>, which the station's multiblock
    /// check requires, so the headroom is checked by construction and the mass stays inside its own cell.
    /// </summary>
    [Fact]
    public void TheHangingMassStaysInsideTheGuideItHangsIn()
    {
        var elements = Load("shapes", "block", "tensionweight.json").GetProperty("elements").EnumerateArray().ToList();
        var mass = elements.Single(e => e.GetProperty("name").GetString() == "mass");

        var from = mass.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var to = mass.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

        Assert.True(from[1] >= 2, $"the mass sinks into its own pad: {from[1]}");
        Assert.True(elements.All(e => e.GetProperty("to")[1].GetDouble() <= 16),
            "the tension weight reaches out of its own cell again; the guide above it is ropeway:tensionguide's job");

        // Half a block wide, so the two rails at 2..4 and 12..14 bracket it exactly.
        Assert.Equal(4, from[0]);
        Assert.Equal(12, to[0]);

        // And it hangs LOW - a tensioner takes up slack, it does not climb. Above the bottom third and it
        // is reading as a gauge again.
        Assert.True(to[1] <= 16, $"the mass hangs at {to[1]}, which reads as a raised weight rather than a tensioner");
    }

    /// <summary>
    /// A weight that is not on the mechanical network is a deliberate decision, not an omission: it is a
    /// rope tensioner, not a machine. The towers are the network's consumers.
    /// </summary>
    [Fact]
    public void TheTensionWeightIsNotAMechanicalPowerNode()
    {
        Assert.False(Load("blocktypes", "tensionweight.json").TryGetProperty("entityBehaviors", out _));
    }

    /// <summary>
    /// A "//" comment key is only ignorable where the game parses an untyped bag. Inside "textures" every
    /// key is deserialised as a CompositeTexture, so a comment there is a hard parse error - and the game
    /// does not fail loudly, it logs and then "will ignore most of the attributes", which silently strips
    /// multiblockStructure and leaves towers that can never be completed. That shipped once; this is the
    /// guard. Same reasoning for any other strongly-typed dictionary a blocktype carries.
    /// </summary>
    [Theory]
    [InlineData("blocktypes/pylonbase.json")]
    [InlineData("blocktypes/pylonhead.json")]
    [InlineData("blocktypes/bullwheel.json")]
    [InlineData("blocktypes/brace.json")]
    [InlineData("blocktypes/tensionweight.json")]
    [InlineData("blocktypes/drivehousing.json")]
    [InlineData("blocktypes/drivestation.json")]
    [InlineData("blocktypes/tensionstation.json")]
    [InlineData("blocktypes/layshaft.json")]
    [InlineData("blocktypes/drivehead.json")]
    [InlineData("blocktypes/driveshaft.json")]
    [InlineData("blocktypes/tensionhead.json")]
    [InlineData("blocktypes/tensionguide.json")]
    [InlineData("entities/cabin.json")]
    [InlineData("itemtypes/haulrope.json")]
    [InlineData("itemtypes/cabin.json")]
    public void NoCommentKeysInsideStronglyTypedDictionaries(string file)
    {
        var root = Load(file.Split('/'));

        foreach (var typed in new[] { "textures", "textesByType", "shapeByType", "sounds" })
        {
            if (!root.TryGetProperty(typed, out var dict) || dict.ValueKind != JsonValueKind.Object) continue;

            var comments = dict.EnumerateObject().Where(p => p.Name.StartsWith("//")).Select(p => p.Name).ToList();
            Assert.True(comments.Count == 0,
                $"{file}: \"{typed}\" contains comment key(s) {string.Join(", ", comments)}. " +
                "The game parses every key there into a typed object, so this throws at load and the whole " +
                "attributes block - multiblockStructure included - is discarded. Put the note at the top level.");
        }
    }

    /// <summary>
    /// Every <c>#key</c> a shape's faces name has to be declared somewhere the game will look, or that face
    /// draws the unknown-texture checker and the only warning is a line on the tesselation thread nobody
    /// reads: <i>"Missing mapping for texture code #machine during shape tesselation of block
    /// ropeway:bullwheel"</i> (TextureSource.cs:53-65).
    /// <para>
    /// This is the palette's only automated gate, because nothing else covers it. The model renderer cannot:
    /// a manifest carries its own scene-global texture map and never reads a blocktype at all. The build
    /// cannot: no C# names a texture key except the two on <c>BEPylonBase</c> below.
    /// </para>
    /// <para>
    /// "Somewhere the game will look" is the blocktype's own map OR the declared shape's, because
    /// <c>BlockTextureAtlasManager.CollectAndBakeTexturesFromShape</c> folds a shape's map into the block's
    /// for any key the block did not already declare. <c>bullwheelrim.json</c> is the exception and is
    /// checked against the BLOCK alone: <c>BEBullwheel</c> tesselates it with
    /// <c>Tesselator.TesselateShape(Block, ...)</c>, and that collector only ever walks a block's DECLARED
    /// shapes - a shape a block entity draws at runtime is never one of them.
    /// </para>
    /// The reverse is deliberately NOT asserted. A declared key no face uses still bakes into the atlas
    /// (Block.cs:2514), which is the only reason the drawn cable's <c>rope</c> and the drawn rail's
    /// <c>metal</c> can live on the three footings at all.
    /// <para>
    /// Names are only half of it, and the missing half was a silent no-op. The palette is written TWICE for
    /// every block - once in <c>blocktypes/*.json</c> and once in the shape's own <c>textures</c> map - and
    /// where both declare a key <c>ResolveTextureCodes</c> only <c>Add</c>s the shape's entry for a key the
    /// block did NOT already declare (:306-328), so the BLOCKTYPE wins and the shape's copy is dead weight.
    /// Re-pointing a shape's key at another sprite therefore changes nothing in game and used to change
    /// nothing here either: the reviewer set <c>shapes/block/brace.json</c>'s <c>girder</c> to basalt, left
    /// the blocktype alone, and got 174 green. <see cref="AssertShadowCopyAgrees"/> is the other half.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryTextureKeyAShapeUsesIsDeclaredWhereTheGameWillLookForIt()
    {
        foreach (var folder in new[] { "blocktypes", "itemtypes" })
        {
            foreach (var path in Directory.GetFiles(Path.Combine(Assets, folder), "*.json"))
            {
                var file = folder + "/" + Path.GetFileName(path);
                var root = Load(folder, Path.GetFileName(path));

                foreach (var shapeBase in ShapeBases(root))
                {
                    // A vanilla shape (haulrope borrows game:item/resource/rope) is not ours to check.
                    if (!shapeBase.StartsWith("ropeway:")) continue;

                    var shape = Load(("shapes/" + shapeBase["ropeway:".Length..] + ".json").Split('/'));
                    AssertDeclares(file + " -> " + shapeBase, FaceKeys(shape), Declared(root), TextureKeys(shape));
                    AssertShadowCopyAgrees(file, shapeBase, root, shape);
                }
            }
        }

        // The rim, against the bullwheel BLOCK only - see the remarks.
        AssertDeclares("bullwheelrim.json -> blocktypes/bullwheel.json",
            FaceKeys(Load("shapes", "block", "bullwheelrim.json")),
            Declared(Load("blocktypes", "bullwheel.json")));
        AssertShadowCopyAgrees("blocktypes/bullwheel.json", "ropeway:block/bullwheelrim",
            Load("blocktypes", "bullwheel.json"), Load("shapes", "block", "bullwheelrim.json"));

        // The cabin entity declares no textures of its own, so its shape's map is the whole mapping -
        // EntityTextureAtlasManager.LoadShapeTextures is what puts it on Client.Textures.
        var cabin = Load("shapes", "entity", "cabin.json");
        AssertDeclares("shapes/entity/cabin.json", FaceKeys(cabin), TextureKeys(cabin));
    }

    /// <summary>
    /// The cable, the wrap ring, both station rails and both outriggers are drawn in C# rather than by any
    /// shape: <c>BEPylonBase.OnTesselation</c> reads <c>textures["rope"]</c> and <c>textures["metal"]</c> off
    /// the FOOTING block (BEPylonBase.cs:602-603). So <c>metal</c> on a footing is not that block's own
    /// decoration - it is every rail on every line - and the footing's own plate and boss sit on
    /// <c>shaft</c> so a cosmetic edit cannot reach the rail by accident.
    /// <para>
    /// The plate/ family requirement is the flat-sample rule and it is a proxy, not the measurement:
    /// <c>BuildBox</c> does <c>Array.Fill(mesh.Uv, 0.5f)</c> (BEPylonBase.cs:989), so a drawn box shows one
    /// sprite's CENTRE texel and nothing else. What that needs is |centre - mean| under about 2 L, and in
    /// this library only plate/ and reedrope pass; riveted/iron1 misses by 6.5 L and would draw a rail
    /// visibly darker than the riveted crossarm it hangs under, with no rivets to show for it. Measured in
    /// docs/agentic/ingest/cablecar/PALETTE-SPEC.md 4d.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRunTimeDrawnRopeAndRailAreOneSpriteEachAcrossEveryFooting()
    {
        var footings = new[] { "pylonbase.json", "drivestation.json", "tensionstation.json" };
        var rails = new SortedSet<string>();

        foreach (var footing in footings)
        {
            var textures = Load("blocktypes", footing).GetProperty("textures");
            foreach (var key in new[] { "rope", "metal" })
            {
                Assert.True(textures.TryGetProperty(key, out _),
                    $"blocktypes/{footing} dropped \"{key}\", which BEPylonBase.OnTesselation reads by name off " +
                    "this block. There is no shape face to notice it is gone - the cable or the rail simply " +
                    "stops being drawn on every line keyed to this footing.");
            }

            var rail = textures.GetProperty("metal").GetProperty("base").GetString()!;
            Assert.StartsWith("game:block/metal/plate/", rail);
            rails.Add(rail);
        }

        Assert.True(rails.Count == 1, $"the three footings draw the station rail in {rails.Count} different " +
                                      $"sprites ({string.Join(", ", rails)}); one line's rail would change " +
                                      "colour at a station.");

        // The rope has to agree across six files, three of which authored it as a shape face rather than a
        // runtime draw: a tie hanging off a tension leg and the cable it takes up slack in are the same rope.
        var ropes = new SortedSet<string>();
        foreach (var file in footings.Concat(new[] { "tensionhead.json", "tensionguide.json", "tensionweight.json" }))
        {
            ropes.Add(Load("blocktypes", file).GetProperty("textures").GetProperty("rope").GetProperty("base").GetString()!);
        }

        foreach (var shape in new[] { "tensionhead.json", "tensionguide.json", "tensionweight.json" })
        {
            ropes.Add(Load("shapes", "block", shape).GetProperty("textures").GetProperty("rope").GetString()!);
        }

        Assert.True(ropes.Count == 1, $"the mod's rope resolves to {ropes.Count} sprites " +
                                      $"({string.Join(", ", ropes)}). Every rope in the mod is 2 units across, " +
                                      "so it reads as one flat colour and a mismatch shows as a tie that is a " +
                                      "different rope from the cable it hangs beside.");
    }

    /// <summary>
    /// One key, one sprite, across the whole mod. The shadow-copy check next door only proves a blocktype and
    /// its own shape agree with EACH OTHER, so moving a key on both copies of one block passes it while the
    /// block changes material in game - a pylon head in steel against six brace cells in riveted iron is the
    /// exact "the ladder collapses" failure the palette sweep rejected, and it was green until this ran.
    /// <para>
    /// Itemtypes are out of scope on purpose rather than by omission: an inventory icon is drawn from the item
    /// atlas, and haulrope deliberately borrows vanilla's own rope ITEM sprite for the coil in your hand while
    /// every rope in the world is the block sprite. Those are two different pictures of one material and
    /// forcing them equal would be wrong.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryPaletteKeyResolvesToExactlyOneSpriteAcrossTheWholeMod()
    {
        var values = new SortedDictionary<string, SortedSet<string>>();
        var sources = new SortedDictionary<string, SortedSet<string>>();

        void Claim(string file, JsonElement root)
        {
            if (!root.TryGetProperty("textures", out var textures)) return;

            foreach (var texture in textures.EnumerateObject())
            {
                if (texture.Name.StartsWith("//")) continue;

                var sprite = texture.Value.ValueKind == JsonValueKind.Object
                    ? texture.Value.GetProperty("base").GetString()
                    : texture.Value.GetString();
                if (sprite == null) continue;

                values.TryAdd(texture.Name, new SortedSet<string>());
                sources.TryAdd(texture.Name, new SortedSet<string>());
                values[texture.Name].Add(sprite);
                sources[texture.Name].Add(file + " -> " + sprite);
            }
        }

        foreach (var path in Directory.GetFiles(Path.Combine(Assets, "blocktypes"), "*.json"))
        {
            Claim("blocktypes/" + Path.GetFileName(path), Load("blocktypes", Path.GetFileName(path)));
        }

        // The cabin is an entity rather than a block, and it is the one thing that has to read as part of the
        // machine while never being made of it, so its keys belong in the same ledger.
        Claim("shapes/entity/cabin.json", Load("shapes", "entity", "cabin.json"));

        Assert.NotEmpty(values);

        var split = values.Where(v => v.Value.Count > 1).ToList();
        Assert.True(split.Count == 0,
            "these palette keys resolve to more than one sprite, so two blocks that are supposed to be the " +
            "same material are not: " +
            string.Join("; ", split.Select(v => "#" + v.Key + " [" + string.Join(", ", sources[v.Key]) + "]")));
    }

    private static void AssertDeclares(string what, SortedSet<string> used, params SortedSet<string>[] declared)
    {
        var known = declared.SelectMany(d => d).ToHashSet();
        var missing = used.Where(k => !known.Contains(k)).ToList();
        Assert.True(missing.Count == 0,
            $"{what}: face key(s) {string.Join(", ", missing.Select(k => "#" + k))} are declared nowhere. " +
            $"Declared: {string.Join(", ", known.OrderBy(k => k))}. In game those faces draw the " +
            "unknown-texture checker and the only sign is an error line on the tesselation thread.");
    }

    /// <summary>
    /// Where a texture key is declared on BOTH a blocktype/itemtype and the shape it draws, the two values
    /// have to be the same string - because only one of them is ever used and it is not the shape's.
    /// <c>BlockTextureAtlasManager.ResolveTextureCodes</c> folds a shape's map into the block's with an
    /// <c>Add</c> that skips keys the block already carries (:306-328), so the shape's copy of a shared key
    /// is a shadow: editing it repaints nothing, and the mismatch is invisible in game AND in the model
    /// renderer, whose manifests carry a scene-global map and read neither file.
    /// <para>
    /// Two copies of the palette is the shipped design - the shape's map is what makes a shape renderable on
    /// its own, and <c>shapes/entity/cabin.json</c>'s map is load-bearing because the cabin entity declares
    /// no textures at all - so the fix is to pin them together rather than to delete one.
    /// </para>
    /// </summary>
    private static void AssertShadowCopyAgrees(string definition, string shapeBase, JsonElement root, JsonElement shape)
    {
        var declared = TextureValues(root);

        foreach (var (key, shapeValue) in TextureValues(shape))
        {
            if (!declared.TryGetValue(key, out var definitionValue) || definitionValue == shapeValue) continue;

            Assert.Fail($"{definition} and {shapeBase} both declare \"{key}\" and they disagree: the " +
                        $"definition says {definitionValue}, the shape says {shapeValue}. THE GAME DRAWS " +
                        $"{definitionValue} - ResolveTextureCodes only adds a shape's entry for a key the " +
                        "block did not already declare, so the shape's copy is a shadow and editing it " +
                        "alone is a silent no-op. Change the definition, or change both.");
        }
    }

    /// <summary>
    /// A <c>textures</c> map as key -> texture path. A blocktype writes <c>{ "base": "..." }</c>, a shape
    /// writes the string directly, and a CompositeTexture accepts either, so both forms are read.
    /// </summary>
    private static Dictionary<string, string> TextureValues(JsonElement root)
    {
        var values = new Dictionary<string, string>();
        if (!root.TryGetProperty("textures", out var textures) || textures.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (var entry in textures.EnumerateObject())
        {
            var value = entry.Value.ValueKind == JsonValueKind.String
                ? entry.Value.GetString()
                : entry.Value.TryGetProperty("base", out var basePath) ? basePath.GetString() : null;

            if (value != null) values[entry.Name] = value;
        }

        return values;
    }

    private static SortedSet<string> Declared(JsonElement root) => TextureKeys(root, "textures");

    private static SortedSet<string> TextureKeys(JsonElement root, string property = "textures")
    {
        var keys = new SortedSet<string>();
        if (root.TryGetProperty(property, out var textures) && textures.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in textures.EnumerateObject()) keys.Add(entry.Name);
        }

        return keys;
    }

    private static IEnumerable<string> ShapeBases(JsonElement root)
    {
        if (root.TryGetProperty("shape", out var single) && single.TryGetProperty("base", out var one))
        {
            yield return one.GetString()!;
        }

        if (!root.TryGetProperty("shapeByType", out var byType)) yield break;

        foreach (var entry in byType.EnumerateObject())
        {
            if (entry.Value.TryGetProperty("base", out var many)) yield return many.GetString()!;
        }
    }

    private static SortedSet<string> FaceKeys(JsonElement shape)
    {
        var keys = new SortedSet<string>();

        void Visit(JsonElement elements)
        {
            foreach (var element in elements.EnumerateArray())
            {
                if (element.TryGetProperty("faces", out var faces))
                {
                    foreach (var face in faces.EnumerateObject())
                    {
                        if (!face.Value.TryGetProperty("texture", out var code)) continue;

                        var name = code.GetString();
                        if (name != null && name.StartsWith('#')) keys.Add(name[1..]);
                    }
                }

                if (element.TryGetProperty("children", out var children)) Visit(children);
            }
        }

        Visit(shape.GetProperty("elements"));
        return keys;
    }

    [Fact]
    public void RegisteredClassNamesMatchTheJson()
    {
        // All three footings, one block class, one block entity class. That is the whole reason a station
        // needed no new C#: BEPylonBase reads whichever multiblockStructure its own block carries, so the
        // difference between a plain tower and a station is entirely in these files.
        foreach (var file in new[] { "pylonbase.json", "drivestation.json", "tensionstation.json" })
        {
            var footing = Load("blocktypes", file);
            Assert.Equal("BlockPylonBase", footing.GetProperty("class").GetString());
            Assert.Equal("PylonBase", footing.GetProperty("entityClass").GetString());
        }

        // The tension weight has NEITHER any more, and that is load bearing rather than tidiness. It carried
        // a block entity purely to register itself in a position table so a line could ask whether any
        // weight stood within eight blocks of any of its towers. Putting "TensionWeight" back would
        // resurrect a class the mod no longer registers, which ServerChunk discards on load.
        var weight = Load("blocktypes", "tensionweight.json");
        Assert.False(weight.TryGetProperty("class", out _));
        Assert.False(weight.TryGetProperty("entityClass", out _));

        var pylon = Load("blocktypes", "pylonbase.json");
        Assert.Equal("BlockPylonBase", pylon.GetProperty("class").GetString());

        // MIGRATION, and the reason this is asserted rather than left to the JSON: this string is what
        // discards a pre-footing world's tower block entities on load - ServerChunk.cs:531 logs and drops a
        // block entity whose class will not instantiate - so every legacy tower comes back as inert
        // decoration with no spans and no route state. Putting "PylonHead" back would instead resurrect
        // them four blocks below their own geometry.
        Assert.Equal("PylonBase", pylon.GetProperty("entityClass").GetString());

        // The sheave is inert: one cell of the pattern, no block entity, none of the gameplay attributes.
        var head = Load("blocktypes", "pylonhead.json");
        Assert.Equal("BlockPylonHead", head.GetProperty("class").GetString());
        Assert.False(head.TryGetProperty("entityClass", out _));
        Assert.False(head.GetProperty("attributes").TryGetProperty("multiblockStructure", out _));

        // The bullwheel is the same cell with a block entity on it - not because it is a machine, but
        // because something has to turn its rim. Its BLOCK is a plain one: it was a BlockMPBase, and with
        // the intake gone there was nothing left in the subclass, so there is deliberately no "class" key.
        var wheel = Load("blocktypes", "bullwheel.json");
        Assert.False(wheel.TryGetProperty("class", out _));
        Assert.Equal("Bullwheel", wheel.GetProperty("entityClass").GetString());
        Assert.False(wheel.GetProperty("attributes").TryGetProperty("multiblockStructure", out _));

        Assert.Equal("EntityRopewayCabin", Load("entities", "cabin.json").GetProperty("class").GetString());
    }

    [Fact]
    public void PylonBaseHasTheSideVariantGroupBEPylonBaseReads()
    {
        var groups = Load("blocktypes", "pylonbase.json").GetProperty("variantgroups")
            .EnumerateArray().Select(g => g.GetProperty("code").GetString()).ToList();

        Assert.Contains("side", groups);
    }

    [Fact]
    public void PylonBaseCarriesTheAttributesBEPylonBaseReads()
    {
        var attributes = Load("blocktypes", "pylonbase.json").GetProperty("attributes");

        Assert.Equal(48, attributes.GetProperty("maxSpan").GetDouble());
        Assert.Equal(16, attributes.GetProperty("maxCandidates").GetInt32());

        // maxLineLength is a CORRECTNESS bound, not a taste knob, which is why it is asserted rather than
        // ranged: past it lies the entire truncated-line failure class (docs/KNOWN-ISSUES.md R1-R4). What it
        // does NOT do is make truncation impossible, which is what this used to claim. MaxChunkRadius 12 -
        // 384 blocks, ServerConfig.cs:925 - is a CAP on the loaded radius rather than the radius:
        // ServerMain.cs:2527 takes min(MaxChunkRadius, ceil(Viewdistance / 32)), and the shipped client
        // default of 256 (ClientSettings.cs:1958) makes that min(12, 8) = 8 chunks = 256 blocks. A 320-block
        // line outruns that by 64. What 320 does buy is that a player standing at the MIDDLE of a full-length
        // line is 160 from each end and holds the whole of it - so the truncation is a thing that happens
        // while somebody walks the line, not a thing they cannot get out of.
        var maxLineLength = attributes.GetProperty("maxLineLength").GetDouble();
        Assert.Equal(320, maxLineLength);
        Assert.True(maxLineLength / 2 <= 256, "half a line must fit the loaded window a player at its middle holds");
        Assert.True(attributes.TryGetProperty("multiblockStructure", out _));

        // BEPylonBase.RopePerBlock. At 1.0 a 48-block span is 96 vanilla rope = 576 cattail tops, which is
        // the most expensive thing in the game; DECISIONS.md 3 asked for cheap.
        var ropePerBlock = attributes.GetProperty("ropePerBlock").GetDouble();
        Assert.InRange(ropePerBlock, 0.01, 0.5);
        Assert.Equal(12, SpanMath.RopeCost(48, ropePerBlock));
    }

    /// <summary>
    /// The sheave is the same seen from either end of the line, because the rope runs through its throat in
    /// both directions and nothing about the tower is asymmetric any more. The old shape carried a spur
    /// pointing at the rear gantry; with one gantry that spur is a hint at something that does not exist.
    /// </summary>
    [Theory]
    [InlineData("pylonhead.json")]
    [InlineData("bullwheel.json")]
    public void ThePylonHeadShapeIsSymmetricAlongTheRopeAxis(string shape)
    {
        // Symmetric as a SET, not element by element: the two sheave cheek plates sit at z 3-5 and 11-13
        // and are each other's mirror. So the whole box list has to map onto itself under z -> 16 - z.
        var boxes = Load("shapes", "block", shape).GetProperty("elements").EnumerateArray()
            .Select(e => (e.GetProperty("from"), e.GetProperty("to")))
            .Select(e => (
                X0: e.Item1[0].GetDouble(), Y0: e.Item1[1].GetDouble(), Z0: e.Item1[2].GetDouble(),
                X1: e.Item2[0].GetDouble(), Y1: e.Item2[1].GetDouble(), Z1: e.Item2[2].GetDouble()))
            .ToHashSet();

        Assert.NotEmpty(boxes);
        Assert.Equal(boxes, boxes.Select(b => (b.X0, b.Y0, 16 - b.Z1, b.X1, b.Y1, 16 - b.Z0)).ToHashSet());
    }

    /// <summary>The three footings that carry a tower structure. A station is one of these, not an extra one.</summary>
    private static readonly string[] Footings = { "pylonbase.json", "drivestation.json", "tensionstation.json" };

    private static JsonElement Structure(string footing) =>
        Load("blocktypes", footing).GetProperty("attributes").GetProperty("multiblockStructure");

    private static List<(int X, int Y, int Z, int W)> Offsets(string footing) =>
        Structure(footing).GetProperty("offsets").EnumerateArray()
            .Select(o => (X: o.GetProperty("x").GetInt32(), Y: o.GetProperty("y").GetInt32(),
                          Z: o.GetProperty("z").GetInt32(), W: o.GetProperty("w").GetInt32()))
            .ToList();

    /// <summary>
    /// THE assertion that makes stations affordable, and the reason none of the clearance numbers below had
    /// to be re-derived for one. All three footings carry the SAME fifteen cells in the same order - only
    /// which block each cell wants differs - so the 5-wide passage, <c>SpanMath.TowerClearance</c>, the
    /// cabin's 2.463-block turning sweep against post inner faces at 2.5 and the roof-to-crossarm clearance
    /// are properties of a list that does not move. <see cref="TheCabinFitsThroughTheTower"/> and
    /// <see cref="TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost"/> read pylonbase.json alone and
    /// are true of a station because of this AND of
    /// <see cref="AStationsMachineLegStaysInsideThePostColumn"/> - a shared offset says which CELLS a
    /// station uses and says nothing about how far the blocks in them reach, which on a plain tower is a
    /// question about vanilla logs and on a station is a question about our own shapes.
    /// <para>
    /// The gameplay numbers are pinned alongside for the same reason: they are now written out three times,
    /// and a line whose station has a different <c>maxSpan</c> from its plain towers is a picker that offers
    /// a link one end will refuse.
    /// </para>
    /// </summary>
    [Fact]
    public void AllThreeFootingsShareOneCellList()
    {
        var shell = Offsets("pylonbase.json").Select(o => (o.X, o.Y, o.Z)).ToList();
        var attributes = Load("blocktypes", "pylonbase.json").GetProperty("attributes");

        foreach (var footing in Footings)
        {
            Assert.Equal(shell, Offsets(footing).Select(o => (o.X, o.Y, o.Z)).ToList());

            var theirs = Load("blocktypes", footing).GetProperty("attributes");
            foreach (var key in new[] { "maxSpan", "maxLineLength", "ropePerBlock", "maxCandidates" })
            {
                Assert.Equal(attributes.GetProperty(key).GetDouble(), theirs.GetProperty(key).GetDouble());
            }
        }

        // ...and they really do differ, or this test is asserting that stations are plain towers. Eight of
        // the fifteen cells want a different block on a station: the centre of the crossarm, the two lay
        // shafts, the head, and the four cells of the machine leg.
        foreach (var station in Footings.Skip(1))
        {
            var differing = Offsets("pylonbase.json").Zip(Offsets(station))
                .Count(pair => Wanted("pylonbase.json", pair.First.W) != Wanted(station, pair.Second.W));

            Assert.Equal(8, differing);
        }
    }

    /// <summary>The wildcard a structure's block number resolves to.</summary>
    private static string Wanted(string footing, int number) =>
        Structure(footing).GetProperty("blockNumbers").EnumerateObject()
            .Single(p => p.Value.GetInt32() == number).Name;

    /// <summary>
    /// The premise <see cref="AllThreeFootingsShareOneCellList"/> rests on, and the one thing about a
    /// station the shared cell list does NOT give you. Post inner faces at 2.5 blocks follow from the
    /// offsets alone only while a post column is filled edge to edge and no further, which on a plain tower
    /// is free - those cells hold vanilla logs, planks and stone. A station fills the same column with
    /// shapes of OURS, and a shape of ours may reach outside its own cell: bullwheelrim.json's felloe sweeps
    /// a unit past the cell face in each direction along the passage, and the wrapped wheel is drawn a whole
    /// cell beyond the tower along the line. So the 2.463-against-2.500 turning sweep and every
    /// penetration number measured off it are claims about a station only while its machine leg stays inside
    /// the column, and there is 0.037 blocks of margin to lose.
    /// <para>
    /// Horizontal only. The drive head's gearbox deliberately hangs below its own cell so the shaft coming
    /// up the leg visibly enters it, and nothing about the cabin cares - the head is a crossarm cell at
    /// x = +3, outside the passage. What the cabin passes is the column's side.
    /// </para>
    /// <para>
    /// Rotated boxes are measured by their swept corner about the rotation origin, the same way
    /// <see cref="TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach"/> measures the wheel: an authored
    /// corner is where a box rests, not how far it reaches. The drive housing's chamfer is a 10-unit drum
    /// turned 45 degrees, which sweeps past its own authored face and still stops 0.93 units inside the
    /// cell. That bound is only sound about a VERTICAL axis, so a tilt is refused rather than mismeasured.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("drivestation.json")]
    [InlineData("tensionstation.json")]
    public void AStationsMachineLegStaysInsideThePostColumn(string footing)
    {
        var postX = Offsets("pylonbase.json").Where(o => o.Y < SpanMath.SheaveHeight).Min(o => Math.Abs(o.X));

        // Both post columns, then only the cells that want a block of ours. The other column is the plain
        // tower's own wildcard - vanilla logs, planks and stone, which fill a cell and stop there.
        var leg = Offsets(footing)
            .Where(o => Math.Abs(o.X) == postX && o.Y < SpanMath.SheaveHeight)
            .Select(o => Wanted(footing, o.W))
            .Where(code => code.StartsWith("ropeway:"))
            .Distinct()
            .ToList();

        // The intake or the weight on the ground, and one shaft repeated up the leg. Two exactly, or the
        // loop below is measuring a column that is not the one the multiblock check asks for.
        Assert.Equal(2, leg.Count);

        foreach (var code in leg)
        {
            // Exact codes rather than wildcards, or the leg could be filled by a block whose shape this
            // test never opens.
            Assert.DoesNotContain("*", code);
            Assert.DoesNotContain("@", code);

            var elements = Load("shapes", "block", code["ropeway:".Length..] + ".json")
                .GetProperty("elements").EnumerateArray().ToList();
            Assert.NotEmpty(elements);

            foreach (var element in elements)
            {
                var name = element.GetProperty("name").GetString();
                var from = element.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                var to = element.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

                double Turn(string key) => element.TryGetProperty(key, out var value) ? value.GetDouble() : 0;

                Assert.True(Turn("rotationX") == 0 && Turn("rotationZ") == 0,
                    $"{code} {name} is tilted, so its horizontal footprint is not the swept circle measured here");

                var origin = element.GetProperty("rotationOrigin").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                var swept = 0.0;
                foreach (var x in new[] { from[0], to[0] })
                foreach (var z in new[] { from[2], to[2] })
                {
                    swept = Math.Max(swept, Math.Sqrt((x - origin[0]) * (x - origin[0]) + (z - origin[2]) * (z - origin[2])));
                }

                var turns = Turn("rotationY") != 0;
                foreach (var axis in new[] { 0, 2 })
                {
                    var low = turns ? origin[axis] - swept : from[axis];
                    var high = turns ? origin[axis] + swept : to[axis];

                    Assert.True(low >= 0 && high <= 16,
                        $"{code} {name} reaches {low:0.###}..{high:0.###} on axis {axis} of its own cell, so a "
                        + "station's leg is not the one-block column the passage half-width is derived from. "
                        + "The cabin sweeps to 2.463 blocks off the tower centre line against a face at 2.500.");
                }
            }
        }
    }

    /// <summary>
    /// ONE MACHINE LEG, AT MOST ONE STATION. <c>MultiblockStructure</c> has no notion of ownership -
    /// <c>InCompleteBlockCount</c> asks only whether the block at each offset matches a wildcard - so before
    /// <c>BEPylonBase.OwnTheHeadCell</c> a <c>drivestation-north</c> at the origin shared its whole machine
    /// leg with a <c>drivestation-east</c> 4.243 blocks away at (3, 0, -3), with a <c>drivestation-west</c> at
    /// (3, 0, +3) and with a <c>drivestation-south</c> at (6, 0, 0). Both structures validated, so
    /// <c>DriveSpeedOn</c> ran two lines at full speed off one mill while <c>DeclareLoad</c> charged at most
    /// one of them for the haul - free speed AND unpaid load, which is what <c>RopewayPower.PoolSpeed</c>'s
    /// own comment calls the one thing a load model must never do.
    /// <para>
    /// The enumeration is the derivation, not a list of the three placements: every facing at every footing
    /// separation the offsets can reach, through the same rotate-then-round transform
    /// <c>MultiblockStructure.InitForUse</c> applies, off the same map <c>BEPylonBase.Init</c> hands it. What
    /// it asserts is that no placement sharing the leg's GROUND cell - the drive housing or the tension
    /// weight, which is the cell <c>Intake</c> resolves and both bugs run through - can be satisfied by one
    /// set of blocks. Deleting the narrowing puts the three placements straight back and this fails on the
    /// first of them.
    /// </para>
    /// <para>
    /// "One block satisfies both" is string equality because every code either structure names is exact or a
    /// <c>-*</c> family wildcard, and two different families are never satisfied by one block. A shared cell
    /// wanting the SAME wildcard on both sides really is shared, which is why the plain post columns still
    /// overlap freely afterwards - two towers may share a leg of logs, and always could.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("drivestation.json")]
    [InlineData("tensionstation.json")]
    public void ASharedMachineLegSatisfiesAtMostOneStation(string footing)
    {
        var offsets = Offsets(footing);

        Dictionary<(int X, int Y, int Z), string> Cells(int ox, int oz, string side)
        {
            var structure = new MultiblockStructure();
            foreach (var pair in Structure(footing).GetProperty("blockNumbers").EnumerateObject())
            {
                structure.BlockNumbers[new AssetLocation(pair.Name)] = pair.Value.GetInt32();
            }

            // The production narrowing, called rather than re-stated - a copy of the rule here would pass
            // while the shipped one did nothing.
            BEPylonBase.OwnTheHeadCell(structure, side);
            var codes = structure.BlockNumbers.ToDictionary(p => p.Value, p => p.Key.ToString());

            var rad = BEPylonBase.RotationFor(side) * Math.PI / 180;
            return offsets.ToDictionary(
                o => (X: (int)Math.Round(o.X * Math.Cos(rad) + o.Z * Math.Sin(rad)) + ox,
                      Y: o.Y,
                      Z: (int)Math.Round(-o.X * Math.Sin(rad) + o.Z * Math.Cos(rad)) + oz),
                o => codes[o.W]);
        }

        // The one cell on the ground the station itself owns: the drive housing or the tension weight. The
        // other y = 0 cell is the plain post column's vanilla wildcard.
        var legFoot = offsets.Single(o => o.Y == 0 && Wanted(footing, o.W).StartsWith("ropeway:"));
        var intake = (X: legFoot.X, Y: legFoot.Y, Z: legFoot.Z);

        var a = Cells(0, 0, "north");
        var reached = 0;

        foreach (var side in new[] { "north", "east", "south", "west" })
        foreach (var dx in Enumerable.Range(-8, 17))
        foreach (var dz in Enumerable.Range(-8, 17))
        {
            if (side == "north" && dx == 0 && dz == 0) continue;

            var b = Cells(dx, dz, side);

            // A footing standing inside the other's own cells is not a build either of them completes.
            if (a.ContainsKey((dx, 0, dz)) || b.ContainsKey((0, 0, 0))) continue;

            var shared = a.Keys.Intersect(b.Keys).ToList();
            if (!shared.Contains(intake)) continue;

            reached++;
            Assert.True(shared.Any(cell => a[cell] != b[cell]),
                $"a {footing[..^5]} facing north at the origin and one facing {side} at ({dx}, 0, {dz}) share "
                + $"{shared.Count} cells including the machine leg's own {a[intake]}, and every one of them is "
                + "satisfied by the same block - so both structures validate off one leg, one mill drives two "
                + "lines at full speed and at most one of them is ever charged for the haul.");
        }

        // Or the loop asserted nothing: the placements still EXIST, they simply cannot both be finished.
        Assert.True(reached >= 3, $"found only {reached} placements sharing a {footing[..^5]}'s machine leg");
    }

    /// <summary>
    /// <c>BEPylonBase.Intake</c> finds the drive by BLOCK NUMBER rather than by a hardcoded offset, so this
    /// attribute is the whole handshake between the C# lookup and the JSON. Point it at the wrong number and
    /// a finished drive station drives nothing, silently, with every panel on the tower reading correctly.
    /// </summary>
    [Fact]
    public void TheDriveIntakeCellIsTheOneTheStationNames()
    {
        var attributes = Load("blocktypes", "drivestation.json").GetProperty("attributes");
        var number = attributes.GetProperty("driveIntakeCell").GetInt32();

        Assert.Equal("ropeway:drivehousing", Wanted("drivestation.json", number));

        // And it is a real cell of the structure, at the foot of the machine leg where an axle can reach it
        // - not up on the crossarm, which is the layout the bullwheel trial died on.
        var cell = Offsets("drivestation.json").Single(o => o.W == number);
        Assert.Equal(0, cell.Y);

        // The other two footings name none, which is what makes Intake free on a plain tower and is why a
        // tension station cannot quietly become a drive.
        foreach (var footing in new[] { "pylonbase.json", "tensionstation.json" })
        {
            Assert.False(Load("blocktypes", footing).GetProperty("attributes")
                .TryGetProperty("driveIntakeCell", out _));
        }

        // The tensioner's own flag, the mirror of the above: one attribute, on one blocktype.
        Assert.True(Load("blocktypes", "tensionstation.json").GetProperty("attributes")
            .GetProperty("tensioner").GetBoolean());
        Assert.False(attributes.TryGetProperty("tensioner", out _));
    }

    [Theory]
    [InlineData("pylonbase.json")]
    [InlineData("drivestation.json")]
    [InlineData("tensionstation.json")]
    public void MultiblockOffsetsAreTheTowerShellAndNothingElse(string footing)
    {
        var structure = Structure(footing);

        var numbers = structure.GetProperty("blockNumbers").EnumerateObject()
            .Select(p => p.Value.GetInt32()).ToHashSet();

        var offsets = Offsets(footing);

        // One crossarm of seven on two posts of four, plus the footing that owns them: 16 cells, 15 offsets.
        // It was five and 14, giving a 3-wide passage. The widening IS the corner fix - it takes post
        // penetration from 1.000 to 0.033 blocks at a 45-degree corner - and it is priced in metal: 6 braces
        // is two crafts, so 2 metal plates a tower instead of 1. See pylonbase.json's "//passagewidth".
        Assert.Equal(15, offsets.Count);
        Assert.Equal(offsets.Count, offsets.Select(o => (o.X, o.Y, o.Z)).Distinct().Count());

        // The controller is the footing itself and must not be one of the cells it checks.
        Assert.DoesNotContain(offsets, o => o.X == 0 && o.Y == 0 && o.Z == 0);

        // Every w must resolve, or MultiblockStructure.InCompleteBlockCount throws on BlockCodes[w].
        Assert.All(offsets, o => Assert.Contains(o.W, numbers));

        // One block deep. The 4-long cabin passes through a 1-deep frame; a second gantry is a tunnel.
        Assert.All(offsets, o => Assert.Equal(0, o.Z));

        // The 5-wide, 4-tall archway the cabin travels through has to stay empty.
        Assert.DoesNotContain(offsets, o => o.X is >= -2 and <= 2 && o.Y is >= 0 and <= 3);

        // The sheave is the crossarm's centre cell, directly SheaveHeight above the footing - AnchorOf,
        // BEPylonBase's cable mesh and BlockPylonHead's block-info lookup all assume exactly this cell.
        Assert.Contains(offsets, o => o.X == 0 && o.Y == SpanMath.SheaveHeight && o.Z == 0);

        // Posts reach the ground the footing stands on. Starting them a cell higher leaves the legs hanging
        // in the air, which is what "posts three tall" would have produced.
        foreach (var x in new[] { -3, 3 })
        {
            for (var y = 0; y < SpanMath.SheaveHeight; y++)
            {
                Assert.Contains(offsets, o => o.X == x && o.Y == y && o.Z == 0);
            }
        }
    }

    /// <summary>
    /// The whole restructure in one assertion: with the controller on the ground the cabin has to pass
    /// through its own tower without clipping the footing it sits on or the crossarm it hangs under. Three
    /// numbers own that fit - <see cref="SpanMath.SheaveHeight"/>, the cabin's <c>hangDrop</c> and the
    /// authored shapes - and any one of them moving alone breaks it silently, in a way only visible by
    /// riding through a tower and watching the floor cut the plinth.
    /// </summary>
    [Fact]
    public void TheCabinFitsThroughTheTower()
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        Assert.Equal(EntityRopewayCabin.DefaultHangDrop, hangDrop);

        var (_, _, elements) = CabinBounds();
        double Bottom(string element) => Find(elements, element).GetProperty("from")[1].GetDouble() / 16;
        double Top(string element) => Find(elements, element).GetProperty("to")[1].GetDouble() / 16;

        // Heights in blocks above the footing block's own bottom face, which is the ground the tower stands on.
        var anchor = SpanMath.SheaveHeight + 0.5;
        var cabinFloor = anchor - hangDrop + Bottom("floor");
        var cabinRoof = anchor - hangDrop + Top("roof");
        // The jaw is four plates round the rope, so its clamped centre - not the top of anything - is what
        // has to land on the anchor. Taking the midpoint of the two clear faces also fails if the jaw is
        // authored lopsided, which a single-face measurement would let through.
        var jawCentre = anchor - hangDrop + (Top("jawbottom") + Bottom("jawtop")) / 2;

        var footingTop = Load("shapes", "block", "pylonbase.json").GetProperty("elements").EnumerateArray()
            .Max(e => e.GetProperty("to")[1].GetDouble()) / 16;
        // The cabin passes under the WHOLE crossarm, and the cell directly over its centre line is the pylon
        // head, not a brace. Deriving this from brace.json alone was accidentally right only because both
        // shapes currently reach y=0; lower the head on its own and the test would still pass while the
        // cabin ate the sheave housing. Take whichever hangs lowest.
        // The lay shaft is here because a station puts one at local x = +1, half a block off the cabin's own
        // centre line. The drive head and the tension head are deliberately NOT: they sit at x = +3, outside
        // the cabin's 1.4375 half-width, and the drive head's gearbox hangs below its own cell on purpose so
        // the shaft coming up the leg visibly enters it.
        var authoredUnderside = SpanMath.SheaveHeight
            + new[] { "brace.json", "pylonhead.json", "bullwheel.json", "layshaft.json" }
                .Select(shape => Load("shapes", "block", shape).GetProperty("elements").EnumerateArray()
                    .Min(e => e.GetProperty("from")[1].GetDouble()))
                .Min() / 16;

        // What hangs lowest over the cabin is the DRAWN station rail, and it has to be in this measurement
        // by name. With the head's own rail plate deleted, min(from[1]) over the four crossarm shapes is the
        // cell floor - so this number would move 3.75 -> 4.00 and the assert below would still pass, with a
        // quarter block MORE room than the cabin actually has. Passing with more room is exactly how a real
        // regression hides, which is why the margin is pinned rather than merely compared.
        var crossarmUnderside = Math.Min(
            authoredUnderside, anchor - BEPylonBase.RailDrop - BEPylonBase.RailHalfDepth);

        Assert.True(cabinFloor > footingTop,
            $"the cabin floor at {cabinFloor} cuts through the footing, which tops out at {footingTop}");
        Assert.True(cabinRoof < crossarmUnderside,
            $"the cabin roof at {cabinRoof} cuts through the crossarm, whose underside is at {crossarmUnderside}");

        // A sixteenth of a block, stated. It was 0.3125 until the crossarm's foot plate came down to the
        // block boundary to meet the posts, and the station rail wanted the headroom more than the floor
        // wanted the clearance under it.
        Assert.Equal(0.25, crossarmUnderside - cabinRoof, 3);

        // Half a block of air between the footing and the cabin passing over it. It was 0.75 while hangDrop
        // sat low in its window; the station rails wanted the room above the roof more than the floor wanted
        // it below, and this is the quarter block that paid for them.
        Assert.Equal(0.5, cabinFloor - footingTop, 3);

        // The jaw has to close ON the rope, not near it, or the cabin visibly hangs from nothing. Its clamp
        // line lands exactly on the anchor - the centre of the sheave block, and the point the cable is
        // drawn from. This is what the split sheave cheeks bought: with a solid housing there was nowhere
        // at rope height for a jaw to be, and the old "grip" was a boss 0.56 block below its own cable.
        Assert.Equal(anchor, jawCentre, 3);

        // And it closes to 0.04 unit of the rope's own surface. Wider and it reads as approaching; any
        // narrower and the two surfaces z-fight.
        var jawGap = (Bottom("jawtop") - Top("jawbottom")) / 2;
        Assert.Equal(BEPylonBase.CableRadius + 0.04 / 16, jawGap, 4);

        // THE TIGHTEST CLEARANCE IN THE MACHINE, and until now nothing measured it: the sheave housing's
        // soffit over the closed jaw. 0.20 units - 0.0125 blocks - tighter than the rail's 0.1-unit
        // engagement, the throat's 0.029 and the passage's 0.037, and it is what every cabin passing every
        // tower threads. crossarmUnderside above cannot see it: it takes min(from[1]) over the whole shape,
        // which is the cell floor, so dropping both soffits from 10.6 to 9.0 - driving the housing 1.4 units
        // THROUGH the jaw of every cabin on the line - left the suite green.
        // Everything at or above the cell's own centre, which is the rope's centreline (AnchorOf is the
        // footing plus SheaveHeight plus half a block): a shape below that line is the throat the jaw hangs
        // in, and a shape above it is over the rope. The lowest such face on either head is the soffit.
        var jawTop = anchor - hangDrop + Top("jawtop");
        var soffit = SpanMath.SheaveHeight
            + new[] { "pylonhead.json", "bullwheel.json" }
                .Select(shape => Load("shapes", "block", shape).GetProperty("elements").EnumerateArray()
                    .Select(e => e.GetProperty("from")[1].GetDouble())
                    .Where(y => y >= 8)
                    .Min())
                .Min() / 16;

        Assert.True(soffit > jawTop,
            $"the sheave housing's soffit at {soffit} is inside the closed jaw, which tops out at {jawTop}");
        Assert.Equal(0.2 / 16, soffit - jawTop, 4);
    }

    /// <summary>
    /// THE PITCH TERM <see cref="TheCabinFitsThroughTheTower"/> never had, and the reason it could not catch
    /// the cabin eating its own crossarm: every height in that test is a constant, so it measures ONE cabin
    /// PARKED at ONE tower. Departing a tower the cabin rises with the rope while the crossarm does not, and
    /// arriving down into one its floor falls while the footing does not - so the fit is a function of pitch
    /// and the guard was a photograph of it at zero.
    /// <para>
    /// This sweeps 0 to 89 degrees and asserts that the tower's own blocks stay out of the cabin everywhere at
    /// or under <see cref="SpanMath.PassablePitchTan"/> and are inside it above - the second half is what stops
    /// the constant drifting upward to whatever the geometry happens to allow. Every input is read off the
    /// shipped shapes, the shipped multiblock and the shipped hang, so shortening the cabin, thinning a slab,
    /// raising the crossarm or lengthening the hanger all land here first.
    /// </para>
    /// <para>
    /// Run against the geometry before <c>PassablePitchTan</c> existed, with the naive assertion that the cabin
    /// clears at every pitch, this failed at <b>7.25 degrees</b>: <c>crossarm 0.182 plinth 0.190 rail -0.004</c>,
    /// deepening to <c>-0.943 / -0.907 / -0.905</c> at 30 degrees. Those are the numbers below.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCabinFitsThroughTheTowerAtEveryPitch()
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();
        double Bottom(string e) => Find(elements, e).GetProperty("from")[1].GetDouble() / 16;
        double Top(string e) => Find(elements, e).GetProperty("to")[1].GetDouble() / 16;
        double HalfLength(string e) => Math.Max(
            Math.Abs(Find(elements, e).GetProperty("from")[0].GetDouble()),
            Math.Abs(Find(elements, e).GetProperty("to")[0].GetDouble())) / 16;

        // The shape is what SpanMath's two cabin constants claim it is - they are used by the corridor sweep,
        // which has no assets to read.
        Assert.Equal(SpanMath.CabinHalfLength, HalfLength("roof"));
        Assert.Equal(SpanMath.CabinHalfHeight, Top("roof"));
        Assert.Equal(SpanMath.CabinHalfHeight, -Bottom("floor"));

        var anchor = SpanMath.SheaveHeight + 0.5;
        var origin = anchor - hangDrop;

        // The three things over and under the passage, all derived: the footing's own plinth, the lowest face
        // any crossarm cell hangs to, and the drawn station rail, which is not a block and follows the rope.
        var plinth = Load("shapes", "block", "pylonbase.json").GetProperty("elements").EnumerateArray()
            .Max(e => e.GetProperty("to")[1].GetDouble()) / 16;
        var crossarm = SpanMath.SheaveHeight
            + new[] { "brace.json", "pylonhead.json", "bullwheel.json", "layshaft.json" }
                .Select(s => Load("shapes", "block", s).GetProperty("elements").EnumerateArray()
                    .Min(e => e.GetProperty("from")[1].GetDouble())).Min() / 16;
        var rail = anchor - BEPylonBase.RailDrop - BEPylonBase.RailHalfDepth;

        // The crossarm row is one cell deep along the travel axis, so the cabin's tail still overlaps it until
        // the cabin is half its own length plus half that cell past the tower - and by then the roof has risen
        // by that whole distance times the pitch. The plinth is the same cell and the same arithmetic on the
        // way down. The rail is not a cell: it runs ALONG the span at a fixed drop under the rope, so what the
        // level roof meets is the rail over the cabin's own tail, half a cabin back and that much lower.
        double Crossarm(double tan) => crossarm - (origin + Top("roof")) - (HalfLength("roof") + 0.5) * tan;
        double Plinth(double tan) => origin + Bottom("floor") - plinth - (HalfLength("floor") + 0.5) * tan;
        double Rail(double tan) => rail - (origin + Top("roof")) - HalfLength("roof") * tan;

        var clear = new List<string>();
        var clipped = new List<string>();
        for (var deg = 0.0; deg <= 89.0; deg += 0.25)
        {
            var tan = Math.Tan(deg * Math.PI / 180);
            var worst = Math.Min(Crossarm(tan), Plinth(tan));
            var line = $"{deg:0.00} deg: crossarm {Crossarm(tan):0.000} plinth {Plinth(tan):0.000} rail {Rail(tan):0.000}";

            if (tan <= SpanMath.PassablePitchTan && worst < 0) clear.Add(line);
            if (tan > SpanMath.PassablePitchTan + 0.02 && worst >= 0) clipped.Add(line);
        }

        Assert.True(clear.Count == 0,
            "the cabin drives through its own tower at a pitch the mod calls passable:\n" + string.Join("\n", clear.Take(6)));
        Assert.True(clipped.Count == 0,
            "SpanMath.PassablePitchTan is lower than the geometry needs - raise it and say why:\n" + string.Join("\n", clipped.Take(6)));

        // Where each one actually runs out, pinned. The crossarm is the binding one and PassablePitchTan is
        // exactly it; the plinth mirrors it a quarter of a degree later because the floor slab is 1/16 shorter
        // than the roof; the drawn rail grazes 4 degrees earlier because it hangs 0.25 over the roof and tips
        // with the rope. Anything that moves one of these three moves a number here.
        double Threshold(System.Func<double, double> clearance)
        {
            double lo = 0, hi = 89;
            for (var i = 0; i < 60; i++)
            {
                var mid = (lo + hi) / 2;
                if (clearance(Math.Tan(mid * Math.PI / 180)) >= 0) lo = mid; else hi = mid;
            }

            return lo;
        }

        Assert.Equal(11.310, Threshold(Crossarm), 3);
        Assert.Equal(11.592, Threshold(Plinth), 3);
        Assert.Equal(7.125, Threshold(Rail), 3);
        Assert.Equal(Math.Atan(SpanMath.PassablePitchTan) * (180 / Math.PI), Threshold(Crossarm), 3);

        // And what it costs on the mod's own headline case, a 30 degree hillside: the roof reaches the
        // crossarm's underside 0.866 blocks out of the tower and does not leave the cell row until 2.5, so it
        // is inside the brace, the sheave and a station's lay shaft for 1.634 blocks of travel, 0.943 deep at
        // the worst. This is the measurement KNOWN-ISSUES quotes.
        var tan30 = Math.Tan(30 * Math.PI / 180);
        var enters = (crossarm - (origin + Top("roof"))) / tan30;
        Assert.Equal(0.866, enters, 3);
        Assert.Equal(1.634, HalfLength("roof") + 0.5 - enters, 3);
        Assert.Equal(-0.943, Crossarm(tan30), 3);
    }

    /// <summary>
    /// THE SAFETY ARGUMENT for <see cref="EntityRopewayCabin.SquareTo"/>, and it is tight. A cabin stopped at
    /// a tower turns to that tower's passage axis, in place, about its own origin - which is the tower's
    /// centre line. So it sweeps a CIRCLE of its own half-diagonal, not its half-width: sqrt(2.0^2 +
    /// 1.4375^2) = 2.463 blocks, against post inner faces at 2.5. Margin 0.037 blocks, and it exists only
    /// because the passage was widened from 3 to 5; at posts x = +/-2 the cabin would sweep 0.463 blocks
    /// through them, so this test is what stops that revert being made silently.
    /// <para>
    /// Both numbers are derived, not typed: the half-diagonal from the shipped cabin shape and the inner face
    /// from the shipped multiblock offsets. Widen the cabin, narrow the passage, or lower the crossarm and
    /// this fails with the number it failed by. Only the parts of the cabin that are actually BESIDE a post
    /// are measured - the hanger reaches above the post tops into the throat, which
    /// <see cref="EveryPartOfTheHangerClearsTheSheaveThroatAtAnyYaw"/> owns.
    /// </para>
    /// <para>
    /// It is the STOPPED half of a pair, and it survived the path being bent for two reasons. Squaring up is
    /// still a thing that happens - <c>StationYaw</c> is a parked cabin's rule and not the reverted moving
    /// one - and 2.463 against 2.500 is also what lets the cabin hold ANY yaw on the tower's centre line,
    /// which is exactly what the bend leans on as it swings through the apex. The moving half is
    /// <see cref="TheBentPathNeverDrivesTheCabinDeeperIntoAPostThanTheStraightOneDid"/>, which measures the
    /// swept path rather than a pose.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost()
    {
        var offsets = Load("blocktypes", "pylonbase.json").GetProperty("attributes")
            .GetProperty("multiblockStructure").GetProperty("offsets").EnumerateArray()
            .Select(o => (X: o.GetProperty("x").GetInt32(), Y: o.GetProperty("y").GetInt32()))
            .ToList();

        // The posts: everything below the crossarm. Their inner faces are half a block in from their centres.
        var posts = offsets.Where(o => o.Y < SpanMath.SheaveHeight).ToList();
        Assert.NotEmpty(posts);
        var passageHalf = posts.Min(o => Math.Abs(o.X)) - 0.5;

        // Heights in blocks above the footing's bottom face, then in the cabin shape's own units about its
        // origin - the cabin hangs hangDrop below the sheave, which is SheaveHeight + 0.5 up.
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var origin = SpanMath.SheaveHeight + 0.5 - hangDrop;
        var postTop = (posts.Max(o => o.Y) + 1 - origin) * 16;
        var postBottom = (posts.Min(o => o.Y) - origin) * 16;

        var (_, _, elements) = CabinBounds();
        var beside = elements.Where(e =>
            e.Element.GetProperty("from")[1].GetDouble() < postTop &&
            e.Element.GetProperty("to")[1].GetDouble() > postBottom).ToList();

        Assert.NotEmpty(beside);

        var worst = ("", 0.0);
        foreach (var (name, element) in beside)
        {
            double Reach(int axis) => Math.Max(
                Math.Abs(element.GetProperty("from")[axis].GetDouble()),
                Math.Abs(element.GetProperty("to")[axis].GetDouble()));

            var corner = Math.Sqrt(Reach(0) * Reach(0) + Reach(2) * Reach(2)) / 16;
            if (corner > worst.Item2) worst = (name, corner);
        }

        Assert.True(worst.Item2 < passageHalf,
            $"{worst.Item1} swings {worst.Item2:0.####} blocks off the yaw axis against a passage whose posts " +
            $"start at {passageHalf:0.####}. A cabin stopped at a tower turns in place to square up, so it " +
            "would sweep through the post. Widen the passage, narrow the cabin, or drop the square-up.");

        // The margin, stated. It is 0.037 blocks - a third of the cabin's own wall thickness - so this is a
        // pass by inspection of one number and not by comfortable clearance.
        Assert.Equal(2.463, worst.Item2, 3);
        Assert.Equal(0.037, passageHalf - worst.Item2, 3);
    }

    /// <summary>
    /// THE acceptance criterion for the bend, and the one thing that fails if it regresses. A cabin is driven
    /// through a corner tower twice - once on the straight chord with the plain leg bearing, which is what
    /// shipped, and once on the bent path with the tangent yaw - and the deeper of the two into a post wins.
    /// The bend may never be the deeper one, at any of nine corner-and-facing combinations, and at the two
    /// combinations it was built to improve it has to actually improve them.
    /// <para>
    /// AND THERE IS A FOURTH CORNER, which is the LIMIT of that claim rather than a case of it. Past about
    /// 125 degrees of turn the bend is worse, and the tenth row here is the worst of them: a 164.6 degree
    /// hairpin with the tower 45 degrees off the bisector goes 0.529 blocks of post on the straight path to a
    /// full 1.000 - straight through - on the bent one. It is pinned in that direction on purpose. Nothing in
    /// <c>RopewayLinkService</c> constrains the angle between two spans, so those corners are buildable, and
    /// the sentence in <see cref="RopewayLine.DirectionAt"/>'s tombstone used to say the tangent law is never
    /// worse ANYWHERE. It is scoped now, and this row is what stops the word coming back.
    /// </para>
    /// <para>
    /// Everything is read off the shipped assets: the cabin's half-extents from its own shape, the passage
    /// half-width from the multiblock offsets, the yaw lag from the constant that cancels it. So this fails
    /// the moment anyone widens the bend window past <see cref="SpanMath.TrimForTowers"/>, puts a facing term
    /// back into the yaw, swaps the through-anchor curve for an inscribed fillet that cuts the corner, or
    /// lengthens the cabin - each of which was measured doing exactly that. Reference implementation and raw
    /// run: <c>docs/agentic/ingest/cablecar/renders/corner/laws.py</c> and <c>MEASUREMENTS.txt</c>, which
    /// reproduce the two penetration numbers <c>docs/KNOWN-ISSUES.md</c> records for the shipped law.
    /// </para>
    /// <para>
    /// PSI is the whole story of the three columns: the angle between the tower's four-way facing and the
    /// corner's bisector. At psi = 45 - what a player gets by default at a right angle between two cardinal
    /// spans - the cabin's ORIGIN travels down the post column, and no curve confined to the last four blocks
    /// can move a path that is already wrong twenty blocks out. The bend does not claim to fix that and this
    /// test does not pretend it does; it asserts only that the bend never makes anything worse, which at
    /// psi = 45 means both laws bury the cabin equally.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBentPathNeverDrivesTheCabinDeeperIntoAPostThanTheStraightOneDid()
    {
        var (min, max, _) = CabinBounds();
        var halfLength = Math.Max(Math.Abs(min[0]), max[0]) / 16;
        var halfWidth = Math.Max(Math.Abs(min[2]), max[2]) / 16;

        var passageHalf = Load("blocktypes", "pylonbase.json").GetProperty("attributes")
            .GetProperty("multiblockStructure").GetProperty("offsets").EnumerateArray()
            .Where(o => o.GetProperty("y").GetInt32() < SpanMath.SheaveHeight)
            .Min(o => Math.Abs(o.GetProperty("x").GetInt32())) - 0.5;

        // Each corner is a real three-tower line: in along +Z, out at the turn. The angles are what the
        // integers give rather than what they are called - 30.03, 164.6 - which is the honest version of the
        // question, since towers stand on integers. NeverWorse marks the corners the claim covers.
        var corners = new (string Name, BlockPos Far, bool NeverWorse)[]
        {
            ("90 deg", new BlockPos(60, 64, 0), true),
            ("45 deg", new BlockPos(60, 64, 60), true),
            ("30 deg", new BlockPos(100, 64, 173), true),
            ("hairpin", new BlockPos(16, 64, -58), false)
        };

        var report = new List<string>();
        var failures = new List<string>();
        var hairpin = new List<double>();

        foreach (var (name, far, neverWorse) in corners)
        {
            var line = RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, -60), new(0, 64, 0), far })!;
            var turn = Math.Atan2(far.X, far.Z);

            foreach (var psi in new[] { 0.0, 22.5, 45.0 })
            {
                var facing = turn / 2 + psi * GameMath.DEG2RAD;
                var straight = Penetration(line, facing, halfLength, halfWidth, passageHalf, bent: false);
                var bent = Penetration(line, facing, halfLength, halfWidth, passageHalf, bent: true);

                report.Add($"turn {turn * GameMath.RAD2DEG,5:0.0} psi {psi,4}: straight {straight:0.000} bent {bent:0.000}");
                if (neverWorse && bent > straight + 1e-6) failures.Add(report[^1]);
                if (!neverWorse && psi == 45.0) hairpin.AddRange(new[] { straight, bent });
            }
        }

        Assert.True(failures.Count == 0,
            "the bend drives the cabin deeper into a post than the straight path did:\n  "
            + string.Join("\n  ", failures) + "\n\nfull table:\n  " + string.Join("\n  ", report));

        // THE LIMIT, pinned in the direction it actually goes. A hairpin's bisector is nearly perpendicular
        // to both legs, so arriving on it points a 4-block cabin BROADSIDE across its own passage, where the
        // straight path at least kept it along a leg. If a later change makes this pass cleanly that is good
        // news and this assert is how you find out - update the tombstone, KNOWN-ISSUES and the TryLink
        // warning's threshold with it rather than deleting the row.
        Assert.True(hairpin[0] < 0.6 && hairpin[1] >= 0.999,
            $"the hairpin no longer measures 0.529 straight / 1.000 bent - it is {hairpin[0]:0.000} / "
            + $"{hairpin[1]:0.000}. The tangent law is scoped to turns under about 125 degrees BECAUSE of "
            + "this row; if it moved, the scoping moved with it.\n\nfull table:\n  " + string.Join("\n  ", report));

        // The two cells the bend exists for. Straight measures 0.034 and 0.033 blocks of post at these, and
        // they are pinned rather than merely "not worse" because a bend that stopped bending would satisfy
        // everything above and nothing here.
        var right = RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, -60), new(0, 64, 0), new(60, 64, 0) })!;
        Assert.True(Penetration(right, Math.PI / 4, halfLength, halfWidth, passageHalf, bent: true) <= 0.001,
            "a right-angle corner with the tower ON the bisector must come out clean");

        var half = RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, -60), new(0, 64, 0), new(60, 64, 60) })!;
        Assert.True(
            Penetration(half, Math.PI / 8 + 22.5 * GameMath.DEG2RAD, halfLength, halfWidth, passageHalf, bent: true) <= 0.005,
            "a 45 degree corner 22.5 degrees off the bisector must come out better than the straight path's 0.033");

        // ...and they stay improvements at every speed the drive ladder reaches, which is the half the
        // measurement used to hardwire away. YawLead is ONE number and the real lag is speed/6 blocks, so the
        // cancellation is exact at 2.2 blocks a second and approximate at the 1.2 to 6.0 the mill decides.
        // Sweeping the lag while the lead stays put is what proves the mistune costs nothing: the worst cell
        // over that whole range measures 0.022 blocks against a straight path of 0.033 to 0.126, so 0.025 is
        // a ceiling with the measurement just under it rather than a number with room to rot.
        foreach (var speed in new[] { 1.2, 1.6, 2.2, 3.0, 4.4, 6.0 })
        {
            var ease = speed / 6;
            Assert.True(
                Penetration(right, Math.PI / 4, halfLength, halfWidth, passageHalf, bent: true, ease) <= 0.025,
                $"the right-angle corner needs the lead tuned for {speed} blocks a second");
            Assert.True(
                Penetration(half, Math.PI / 8 + 22.5 * GameMath.DEG2RAD, halfLength, halfWidth, passageHalf, true, ease)
                <= 0.025,
                $"the 45 degree corner needs the lead tuned for {speed} blocks a second");
        }
    }

    /// <summary>
    /// How far into a post column the cabin gets on one pass through the middle tower of a three-tower line,
    /// in blocks; 1.0 is straight through it. The cabin is a rectangle swept along the path, yawed by a
    /// heading that is passed through the client's own first-order rotation easing - without that the
    /// measurement is of a cabin that turns instantly, which is the flattering case and not the rendered one.
    /// <para>
    /// <paramref name="bent"/> false is the shipped law exactly: the chord, and the leg's bearing. True is
    /// this change: <c>PositionAt</c>, and <c>DirectionAt</c> read <c>YawLead</c> ahead, which is what
    /// <c>EntityRopewayCabin.Place</c> does.
    /// </para>
    /// <para>
    /// <paramref name="ease"/> is the client's lag in blocks of travel and defaults to <c>YawLead</c>, which
    /// is the speed the lead was tuned at. Passing them equal makes the cancellation exact, so it must NOT be
    /// the only way this is measured: the real lag is <c>speed / 6</c> and the drive ladder spans 1.2 to 6.0
    /// blocks a second, so a caller that always ties the two together hardwires away the very error the lead
    /// exists to model.
    /// </para>
    /// </summary>
    private static double Penetration(
        RopewayLine line, double facing, double halfLength, double halfWidth, double passageHalf, bool bent,
        double ease = EntityRopewayCabin.YawLead)
    {
        var tower = line.Cumulative[1];
        var anchor = line.Anchors[1];
        var lead = EntityRopewayCabin.YawLead;

        // The tower's own frame: across the passage (the post axis) and along it.
        var acrossX = Math.Cos(facing);
        var acrossZ = -Math.Sin(facing);

        const int steps = 3000;
        const double reach = 14.0;
        var stride = 2 * reach / steps;

        var worst = 0.0;
        var yaw = double.NaN;

        for (var i = 0; i <= steps; i++)
        {
            var s = -reach + stride * i;
            var point = bent ? line.PositionAt(tower + s) : Chord(line, tower + s);
            var ahead = bent ? line.DirectionAt(tower + s + lead) : null;
            var heading = bent ? Math.Atan2(ahead!.X, ahead.Z) : Bearing(line, tower + s);

            // EntityBehaviorInterpolatePosition.LerpRotation, to first order and in blocks of travel.
            yaw = double.IsNaN(yaw) ? heading : yaw + Wrap(heading - yaw) * stride / ease;

            var alongX = Math.Sin(yaw);
            var alongZ = Math.Cos(yaw);
            var sideX = Math.Cos(yaw);
            var sideZ = -Math.Sin(yaw);

            var corners = new (double X, double Z)[4];
            var c = 0;
            foreach (var (su, sv) in new[] { (1, 1), (1, -1), (-1, -1), (-1, 1) })
            {
                var wx = point.X - anchor.X + su * halfLength * alongX + sv * halfWidth * sideX;
                var wz = point.Z - anchor.Z + su * halfLength * alongZ + sv * halfWidth * sideZ;
                corners[c++] = (wx * acrossX + wz * acrossZ, wx * Math.Sin(facing) + wz * Math.Cos(facing));
            }

            // Both posts, by mirroring the cabin rather than the column: they stand at across = +/-passageHalf
            // outward, ONE BLOCK deep, over the single block of passage the tower actually occupies.
            //
            // That outer face caps the metric at 1.000 = "at least straight through", which means every cell
            // where both laws bury the cabin compares equal and carries no information. Removing it was tried
            // and is wrong: with the column unbounded the number stops being a depth and becomes distance past
            // an inner face into open air, so a cabin whose outgoing leg simply RUNS ALONG the post axis reads
            // 13.5 blocks - the length of its traverse - and two hopeless cells start comparing as though one
            // were better. The post is one block thick; a depth into it cannot exceed one block. The answer to
            // the saturation is a better choice of rows, which is what the hairpin above is.
            foreach (var mirror in new[] { 1.0, -1.0 })
            {
                var clipped = Clip(corners.Select(p => (mirror * p.X, p.Z)).ToList(),
                    passageHalf, passageHalf + 1, -0.5, 0.5);

                foreach (var p in clipped) worst = Math.Max(worst, p.X - passageHalf);
            }
        }

        return worst;
    }

    /// <summary>An angle folded into (-pi, pi].</summary>
    private static double Wrap(double angle)
    {
        return angle - 2 * Math.PI * Math.Floor((angle + Math.PI) / (2 * Math.PI));
    }

    /// <summary>The straight chord and the plain leg bearing: <c>PositionAt</c>/<c>DirectionAt</c> as they were.</summary>
    private static Vec3d Chord(RopewayLine line, double travelled)
    {
        var i = line.AnchorIndexAt(travelled);
        var t = (travelled - line.Cumulative[i]) / (line.Cumulative[i + 1] - line.Cumulative[i]);
        var a = line.Anchors[i];
        var b = line.Anchors[i + 1];

        return new Vec3d(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    private static double Bearing(RopewayLine line, double travelled)
    {
        var i = line.AnchorIndexAt(travelled);

        return Math.Atan2(line.Anchors[i + 1].X - line.Anchors[i].X, line.Anchors[i + 1].Z - line.Anchors[i].Z);
    }

    /// <summary>Sutherland-Hodgman against an axis-aligned box. Empty when the polygon misses it entirely.</summary>
    private static List<(double X, double Z)> Clip(List<(double X, double Z)> poly, double x0, double x1, double z0, double z1)
    {
        foreach (var (axis, limit, keepAbove) in new[] { (0, x0, true), (0, x1, false), (1, z0, true), (1, z1, false) })
        {
            var clipped = new List<(double X, double Z)>();

            for (var i = 0; i < poly.Count; i++)
            {
                var p = poly[(i + poly.Count - 1) % poly.Count];
                var q = poly[i];

                double Coord((double X, double Z) v) => axis == 0 ? v.X : v.Z;
                bool Inside((double X, double Z) v) => keepAbove ? Coord(v) >= limit : Coord(v) <= limit;

                (double X, double Z) Cross()
                {
                    var f = (limit - Coord(p)) / (Coord(q) - Coord(p));
                    return (p.X + f * (q.X - p.X), p.Z + f * (q.Z - p.Z));
                }

                if (Inside(q))
                {
                    if (!Inside(p)) clipped.Add(Cross());
                    clipped.Add(q);
                }
                else if (Inside(p))
                {
                    clipped.Add(Cross());
                }
            }

            poly = clipped;
            if (poly.Count == 0) return poly;
        }

        return poly;
    }

    private static JsonElement Find(List<(string Name, JsonElement Element)> elements, string name)
    {
        return elements.First(e => e.Name == name).Element;
    }

    /// <summary>
    /// The slot rule. The head's throat is fixed to one of four cardinals (the <c>side</c> variant) and the
    /// cabin's yaw is not, so every part of the hanger that reaches the rail band or higher has to fit that
    /// throat at EVERY yaw: its furthest CORNER from the yaw axis, not its half-width, is what must clear.
    /// A hanger authored as the long flat arm the eye prefers - 8 x 2.5 units - reaches 3.71 off the axis at
    /// 45 degrees against a 2.6-unit throat and goes straight through the crossarm's foot plate. That is why
    /// the blade is narrow, and why the guide rollers sit on the yaw axis rather than out at the rails.
    /// The shoulder is exempt because it stays below the rails, and that is asserted rather than assumed.
    /// </summary>
    [Theory]
    [InlineData("pylonhead.json")]
    [InlineData("bullwheel.json")]
    public void EveryPartOfTheHangerClearsTheSheaveThroatAtAnyYaw(string shape)
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();
        var head = Load("shapes", "block", shape).GetProperty("elements").EnumerateArray().ToList();

        double HeadEdge(string element, string corner) =>
            head.First(e => e.GetProperty("name").GetString() == element).GetProperty(corner)[0].GetDouble();

        // Half the clear gap between the two sheave cheeks, measured from the head cell's own centre line,
        // and the underside of the rail band in the cabin shape's own coordinates.
        var slot = (HeadEdge("sheavecheekeast", "from") - HeadEdge("sheavecheekwest", "to")) / 2;

        // PINNED to the runtime rail, and it used to be min(from[1]) over the head shape. That read -4.0
        // while the head authored a rail plate hanging below its own cell and reads 0 now that it does not,
        // which would quietly move this cut-off from 24 to 28 units and drop the GUIDE ROLLERS - the tightest
        // fit in the machine, 0.029 units of margin - out of the set of parts this test measures at all. A
        // green suite, an unmeasured roller, and nothing anywhere to say the subject had changed.
        var railBottom = (hangDrop - BEPylonBase.RailDrop - BEPylonBase.RailHalfDepth) * 16;

        var reaching = elements.Where(e => e.Element.GetProperty("to")[1].GetDouble() > railBottom).ToList();
        Assert.NotEmpty(reaching);
        Assert.DoesNotContain(reaching, e => e.Name == "hangershoulder");

        // The rollers are IN, stated rather than left to the arithmetic above. This is the assert that fails
        // if the band ever moves off them again.
        Assert.Contains(reaching, e => e.Name == "rollereast");
        Assert.Contains(reaching, e => e.Name == "rollerwest");

        foreach (var (name, element) in reaching)
        {
            double Reach(int axis) => Math.Max(
                Math.Abs(element.GetProperty("from")[axis].GetDouble()),
                Math.Abs(element.GetProperty("to")[axis].GetDouble()));

            var corner = Math.Sqrt(Reach(0) * Reach(0) + Reach(2) * Reach(2));
            Assert.True(corner <= slot,
                $"{name}: its far corner is {corner:0.###} units off the yaw axis against a {slot:0.###}-unit " +
                "throat, so it fouls the head at some yaw");
        }
    }

    [Fact]
    public void BothCabinSeatsAreNonControllableAndAnySeatMountable()
    {
        // REVIEW-01 C1: a controlled seat suppresses EntityBehaviorInterpolatePosition for the controlling
        // client. Non-controllable seats still board fine: EntityBehaviorSeatable.mountAnySeat
        // (VSSurvivalMod/EntityBehaviorSeatable.cs:244-255) falls back to a second loop with no CanControl
        // check when the first one finds nothing. interactMountAnySeat is what reaches that fallback, so
        // both halves of this pairing are load bearing.
        var configs = Load("entities", "cabin.json").GetProperty("behaviorConfigs");
        var seatable = configs.GetProperty("seatable");

        Assert.True(seatable.GetProperty("interactMountAnySeat").GetBoolean());

        var seats = seatable.GetProperty("seats").EnumerateArray().ToList();
        Assert.Equal(2, seats.Count);
        Assert.All(seats, s => Assert.False(s.GetProperty("controllable").GetBoolean()));

        // EntityBehaviorSelectionBoxes indexes seats by list order, so the two lists must agree.
        var boxes = configs.GetProperty("selectionboxes").GetProperty("selectionBoxes")
            .EnumerateArray().Select(b => b.GetString()).ToList();

        Assert.Equal(seats.Select(s => s.GetProperty("apName").GetString()).ToList(), boxes);
    }

    /// <summary>
    /// A wrong or missing player anim code fails SILENTLY - AnimManager.StartAnimation resolves the string
    /// against the PLAYER's AnimationsByMetaCode and simply does nothing if it misses, which is exactly how
    /// the dead mountAnimations map left the rider standing for four rounds. So: both seats must name an
    /// animation, and the eye it sits behind must land in the glazing band - above the bench top the rider
    /// is sat on, below the underside of the roof slab. eyeHeight 1.4 put it inside the roof.
    /// </summary>
    [Fact]
    public void BothCabinSeatsSitTheRiderWithTheEyeInTheGlazing()
    {
        var (_, _, elements) = CabinBounds();
        var roofBottom = Find(elements, "roof").GetProperty("from")[1].GetDouble() / 16;
        var benchTop = Find(elements, "seatfront").GetProperty("to")[1].GetDouble() / 16;

        var apY = new Dictionary<string, double>();
        foreach (var (_, element) in elements)
        {
            if (!element.TryGetProperty("attachmentpoints", out var points)) continue;

            foreach (var point in points.EnumerateArray())
            {
                apY[point.GetProperty("code").GetString()!] =
                    (element.GetProperty("from")[1].GetDouble() + double.Parse(point.GetProperty("posY").GetString()!)) / 16;
            }
        }

        var seats = Load("entities", "cabin.json").GetProperty("behaviorConfigs").GetProperty("seatable")
            .GetProperty("seats").EnumerateArray().ToList();

        Assert.NotEmpty(seats);
        foreach (var seat in seats)
        {
            var apName = seat.GetProperty("apName").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(seat.GetProperty("animation").GetString()),
                $"{apName} names no sit animation, so its rider stands");

            var eye = apY[apName] + seat.GetProperty("eyeHeight").GetDouble();
            Assert.True(eye > benchTop && eye < roofBottom,
                $"{apName}: eye at {eye:0.###} is outside the glazing band {benchTop:0.###}..{roofBottom:0.###}");
        }
    }

    /// <summary>
    /// The rider does not land on his attachment point. sitboatidle's frame-0 keyframe carries
    /// <c>LowerTorso offsetX 6.2</c> (game/shapes/entity/humanoid/seraph.json), which Animation.cs:211 turns
    /// into a pose translation, and with that element's own rest roll the seated backside ends up
    /// <c>+4.59 .. +9.85</c> model units toward shape +X of wherever the rider's ORIGIN is - the constants
    /// below, and the only part of the pose that has to land on wood. The knob that moves that origin is the
    /// seat's <c>riderOffset</c>, in BLOCKS, applied in the mount's own shape space
    /// (EntityRideableSeat.SeatPosition: RotateY(yaw+90), then Translate(RiderOffset), then the AP).
    ///
    /// Three numbers therefore have to agree - the AP's x, the pan's x extent, and riderOffset - and nothing
    /// pinned them together. An interior rebuild narrowed the pans 18 -> 14 and moved the APs +/-15 -> +/-18
    /// with all 96 tests green, which put the contact patch 2.85 units off the pan on BOTH benches while the
    /// extent, swept-circle and eye-in-glazing tests carried on passing. Move any one of the three alone and
    /// this is what fails. Calibration: boat-sailed.json:53-54, vanilla's only sitboatidle bench, is a
    /// 10-deep plank with its AP dead centre and riderOffset -0.5 - and without that offset vanilla's own
    /// rider misses its own plank by 2.7 units, which is what fixes the sign.
    /// </summary>
    [Fact]
    public void TheSeatedRidersContactPatchLandsOnItsPan()
    {
        const double buttNear = 4.59;
        const double buttFar = 9.85;

        var (_, _, elements) = CabinBounds();

        // The AP's owning element IS the pan the rider sits on, so the pairing is read, not assumed.
        var pans = new Dictionary<string, (string Name, double From, double To, double ApX)>();
        foreach (var (name, element) in elements)
        {
            if (!element.TryGetProperty("attachmentpoints", out var points)) continue;

            foreach (var point in points.EnumerateArray())
            {
                var from = element.GetProperty("from")[0].GetDouble();
                pans[point.GetProperty("code").GetString()!] =
                    (name, from, element.GetProperty("to")[0].GetDouble(),
                        from + double.Parse(point.GetProperty("posX").GetString()!));
            }
        }

        var seats = Load("entities", "cabin.json").GetProperty("behaviorConfigs").GetProperty("seatable")
            .GetProperty("seats").EnumerateArray().ToList();

        Assert.Equal(2, seats.Count);
        foreach (var seat in seats)
        {
            var apName = seat.GetProperty("apName").GetString()!;
            var (pan, panFrom, panTo, apX) = pans[apName];

            var offset = seat.TryGetProperty("riderOffset", out var riderOffset) &&
                         riderOffset.TryGetProperty("x", out var offsetX)
                ? offsetX.GetDouble() * 16
                : 0;

            var near = apX + offset + buttNear;
            var far = apX + offset + buttFar;

            Assert.True(near >= panFrom && far <= panTo,
                $"{apName}: seated at x {near:0.##}..{far:0.##} (AP {apX:0.##}, riderOffset {offset:0.##} units), " +
                $"which is off {pan} at {panFrom:0.##}..{panTo:0.##}");
        }
    }

    /// <summary>
    /// Entity shapes are authored along X: EntityShapeRenderer adds +90 degrees to Pos.Yaw before building
    /// the model matrix (EntityShapeRenderer.cs:808), so the model's X axis lands on the entity's heading,
    /// and every vanilla entity whose long axis IS its heading - raft 4.5 x 2.25, arapaima 1.90 x 0.58,
    /// pike 1.35 x 0.56 - is long in X. EntityRopewayCabin.Place sets Yaw = Atan2(dir.X, dir.Z), the same
    /// convention. Built along Z the cabin flies sideways and presents its 4-block side to the tower's
    /// 3-block passage, which it does not fit through.
    /// </summary>
    [Fact]
    public void TheCabinIsBuiltAlongTheTravelAxis()
    {
        var (min, max, _) = CabinBounds();

        Assert.Equal(4.0, (max[0] - min[0]) / 16, 3);
        Assert.Equal(2.875, (max[2] - min[2]) / 16, 3);

        // SpanMath.ClearanceRadius certifies a 3-wide corridor along the SPAN, and that is the binding
        // limit on cabin width - not the tower, whose posts now sit at x = +/-3 and leave five free cells.
        // Widening the tower did not widen what the line will carry, and must not be read as having done.
        Assert.True((max[2] - min[2]) / 16 <= 2 * SpanMath.ClearanceRadius + 1,
            "the cabin is wider than the corridor its spans are certified over");
    }

    /// <summary>
    /// EntityRopewayCabin.SetSelectionBox hardcodes the box because hitboxSize is a Vec2f that cannot
    /// describe a body hanging below its own Pos. The box must be YAW-INVARIANT: Entity.SelectionBox is
    /// world-axis-aligned and nothing in Entity.IntersectsRay rotates it, while the cabin's world footprint
    /// does turn with the line's bearing. So it is checked against the model's LONGEST horizontal
    /// half-extent on both axes, not against each axis separately - a box that merely matches the model
    /// bounds fits an east-west line and is transposed on a north-south one, and comparing axis-by-axis in
    /// model space passes for either transposition. A future rectangular "optimisation" fails here.
    /// </summary>
    [Fact]
    public void TheHardcodedSelectionBoxCircumscribesTheCabinAtAnyYaw()
    {
        var (min, max, _) = CabinBounds();

        var cabin = new EntityRopewayCabin();
        cabin.SetSelectionBox(0f, 0f);
        var box = cabin.SelectionBox;

        Assert.True(box.X1 == box.Z1 && box.X2 == box.Z2,
            "selection box is not square in x/z, so it cannot be right at every yaw");

        // Half-extents about Pos, in blocks. The model is centred horizontally on the shape origin.
        var reach = Math.Max(
            Math.Max(Math.Abs(min[0]), Math.Abs(max[0])),
            Math.Max(Math.Abs(min[2]), Math.Abs(max[2]))) / 16;

        Assert.True(-box.X1 >= reach && box.X2 >= reach,
            "selection box is smaller than the cabin's longest horizontal half-extent");
        Assert.True(box.Y1 <= min[1] / 16, "selection box does not reach the bottom of the model");

        // And the top. The old mast stopped at 2.00 blocks, inside a box that topped out at 2.05; the jaw
        // reaches 2.40, and for a while the top 0.35 block of the hanger was not clickable at all.
        Assert.True(box.Y2 >= max[1] / 16, "selection box does not reach the top of the model");
    }

    /// <summary>Both seats must stay on the cabin's centre line, one fore and one aft of the mast.</summary>
    [Fact]
    public void TheSeatAttachmentPointsStayOnTheCentreLine()
    {
        var (_, _, elements) = CabinBounds();
        var seats = new Dictionary<string, double[]>();

        foreach (var (_, element) in elements)
        {
            if (!element.TryGetProperty("attachmentpoints", out var points)) continue;

            // Attachment point coordinates are relative to the owning element's `from` corner
            // (ShapeElement.GetLocalTransformMatrix translates by From), with the model's own axes.
            var from = element.GetProperty("from");
            foreach (var point in points.EnumerateArray())
            {
                seats[point.GetProperty("code").GetString()!] = new[]
                {
                    from[0].GetDouble() + double.Parse(point.GetProperty("posX").GetString()!),
                    from[1].GetDouble() + double.Parse(point.GetProperty("posY").GetString()!),
                    from[2].GetDouble() + double.Parse(point.GetProperty("posZ").GetString()!)
                };
            }
        }

        Assert.Equal(2, seats.Count);
        Assert.All(seats.Values, p => Assert.Equal(0, p[2], 3));

        // Fore and aft along the travel axis, not stacked on top of each other.
        Assert.True(seats["SeatAP"][0] * seats["Seat2AP"][0] < 0, "both seats are on the same side of the mast");
    }

    /// <summary>
    /// The cargo contract, and the reason it is asserted rather than left to the JSON: vanilla indexes
    /// <c>wearableSlots</c> with the SELECTION BOX index, not the inventory index
    /// (<c>EntityBehaviorAttachable.GetInteractionHelp</c> hands <c>es.SelectionBoxIndex - 1</c> straight to
    /// <c>AttachableInteractionHelp</c>, which subscripts the slot array with it). The two lists therefore
    /// have to name the same attachment points in the same order or merely LOOKING at the cabin throws -
    /// which is what boat-raft.json:84 warns modders about in as many words. Free here because the cargo
    /// slots ARE the seats; one line of drift and it is not.
    /// </summary>
    [Fact]
    public void TheCargoSlotsAreTheBenchesAndIndexAlignWithTheSelectionBoxes()
    {
        var configs = Load("entities", "cabin.json").GetProperty("behaviorConfigs");
        var attachable = configs.GetProperty("attachable");
        var slots = attachable.GetProperty("wearableSlots").EnumerateArray().ToList();

        var boxes = configs.GetProperty("selectionboxes").GetProperty("selectionBoxes")
            .EnumerateArray().Select(b => b.GetString()).ToList();

        Assert.Equal(boxes, slots.Select(s => s.GetProperty("attachmentPointCode").GetString()).ToList());

        // Two, because there are two benches and a loaded bench cannot be sat on
        // (EntityBehaviorSeatable.CanSitOn). Freight competing with passengers is the design, not a limit
        // that wants raising - and it is the only cap on capacity we own, since slot counts come from the
        // container itself.
        Assert.Equal(2, slots.Count);

        foreach (var slot in slots)
        {
            // Without this an empty bench stops being boardable: EntityBehaviorAttachable.OnInteract only
            // hands the click back to seatable at :180-184 when the flag is set.
            Assert.True(slot.GetProperty("emptyInteractPassThrough").GetBoolean(),
                $"{slot.GetProperty("attachmentPointCode").GetString()} would swallow the click that boards the cabin");

            // Vanilla's own cargo list minus the crate. boat-sailed.json:143 and :178 read
            // ["seat", "chest", "basket", "crate"] and these benches are those squares; basket and chest
            // both carry BoatableGenericTypedContainer, a HeldBag subclass that overrides only
            // GetQuantitySlots, so both answer the same verb and the interaction help stays true of both.
            // The crate is the one exclusion: BlockCrate carries CollectibleBehaviorBoatableCrate, which
            // overrides OnInteract without calling base, so a crate has no dialog at all - a plain click
            // takes one item out and Ctrl empties it AND detaches it in the same click.
            Assert.Equal(new[] { "basket", "chest" },
                slot.GetProperty("forCategoryCodes").EnumerateArray().Select(c => c.GetString()).ToArray());
        }

        // dropContentsOnDeath must stay OFF: on Die(Death) it drops the container with its `backpack` tree
        // intact while the held-bag despawn hook spills the same goods loose, and re-attaching that container
        // elsewhere keeps the tree. EntityRopewayCabin.Die unloads on every reason, Death included.
        Assert.False(attachable.TryGetProperty("dropContentsOnDeath", out _),
            "dropContentsOnDeath double-drops on the Death path and the unload guard already covers it");
    }

    /// <summary>
    /// Order in both behavior lists. Behind <c>seatable</c> the cargo click is eaten by the mount
    /// (<c>Entity.OnInteract</c> breaks on <c>PreventSubsequent</c>); missing from the SERVER list, the
    /// behavior's <c>ToBytes</c>/<c>FromBytes</c> never run and the cargo is gone on reload with no error.
    /// </summary>
    [Theory]
    [InlineData("client")]
    [InlineData("server")]
    public void TheAttachableBehaviorSitsBetweenSelectionBoxesAndSeatable(string side)
    {
        var codes = Load("entities", "cabin.json").GetProperty(side).GetProperty("behaviors")
            .EnumerateArray().Select(b => b.GetProperty("code").GetString()).ToList();

        Assert.Contains("attachable", codes);
        Assert.InRange(codes.IndexOf("attachable"), codes.IndexOf("selectionboxes") + 1, codes.IndexOf("seatable") - 1);
    }

    /// <summary>
    /// <c>stepParentTo</c> needs a real element to hang the container's shape on - a missing one is
    /// attached-but-invisible cargo, which looks exactly like the attach having failed. And the element has
    /// to start at the bench pan's top, because a container's own shape sits on its parent's floor.
    /// </summary>
    [Fact]
    public void EveryCargoSlotStepParentsToAnElementSittingOnItsBench()
    {
        var (_, _, elements) = CabinBounds();
        var benchTop = Find(elements, "seatfront").GetProperty("to")[1].GetDouble();

        var slots = Load("entities", "cabin.json").GetProperty("behaviorConfigs").GetProperty("attachable")
            .GetProperty("wearableSlots").EnumerateArray();

        foreach (var slot in slots)
        {
            var name = slot.GetProperty("stepParentTo").GetProperty("").GetProperty("elementName").GetString()!;
            Assert.Contains(elements, e => e.Name == name);

            var pad = Find(elements, name);
            Assert.Equal(benchTop, pad.GetProperty("from")[1].GetDouble(), 3);

            // A container is a block: the pad has to be a 16-unit cube or the shape it parents lands at the
            // wrong scale against the cabin instead of sitting in it.
            for (var axis = 0; axis < 3; axis++)
            {
                Assert.Equal(16, pad.GetProperty("to")[axis].GetDouble() - pad.GetProperty("from")[axis].GetDouble(), 3);
            }

            // Invisible: the cargo you see is the container, never the mount point.
            Assert.All(pad.GetProperty("faces").EnumerateObject(),
                f => Assert.False(f.Value.GetProperty("enabled").GetBoolean(), $"{name}.{f.Name} would render a grey box"));
        }
    }

    private static (double[] Min, double[] Max, List<(string Name, JsonElement Element)> Elements) CabinBounds()
    {
        var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
        var max = new[] { double.MinValue, double.MinValue, double.MinValue };
        var elements = new List<(string, JsonElement)>();

        void Visit(JsonElement parent, bool root)
        {
            foreach (var element in parent.EnumerateArray())
            {
                // The root element is a face-less 1x1x1 pivot node at the shape origin, not geometry.
                if (!root)
                {
                    elements.Add((element.GetProperty("name").GetString()!, element));
                    for (var axis = 0; axis < 3; axis++)
                    {
                        min[axis] = Math.Min(min[axis], element.GetProperty("from")[axis].GetDouble());
                        max[axis] = Math.Max(max[axis], element.GetProperty("to")[axis].GetDouble());
                    }
                }

                if (element.TryGetProperty("children", out var children)) Visit(children, false);
            }
        }

        Visit(Load("shapes", "entity", "cabin.json").GetProperty("elements"), true);
        return (min, max, elements);
    }

    /// <summary>
    /// The crossarm's centre cell tells a station from a plain tower, and the split is exclusive both ways.
    /// The bullwheel used to be an accepted ALTERNATIVE on any tower, back when it was decoration that
    /// happened to spin; it is now the driven wheel of a station whose whole crossarm is a drive train, with
    /// a hub axle running out of it to the lay shaft next door. Accepting one on a plain tower would put a
    /// wheel joined to nothing on a tower that drives nothing, and accepting the plain sheave on a station
    /// would leave the shaft run ending in mid air.
    /// </summary>
    [Fact]
    public void AStationWearsTheBullwheelAndAPlainTowerWearsTheHead()
    {
        Regex Centre(string footing)
        {
            var number = Offsets(footing)
                .Single(o => o.X == 0 && o.Z == 0 && o.Y == SpanMath.SheaveHeight).W;

            // WildcardUtil anchors an @-pattern as ^...$ (RegexCache.IsMatch), and * means "anything".
            return new Regex("^" + Wanted(footing, number).Replace("@", "").Replace("*", ".*") + "$");
        }

        var plain = Centre("pylonbase.json");
        Assert.Matches(plain, "ropeway:pylonhead-north");
        Assert.DoesNotMatch(plain, "ropeway:bullwheel-west");
        Assert.DoesNotMatch(plain, "ropeway:brace-north");

        foreach (var station in Footings.Skip(1))
        {
            var centre = Centre(station);
            Assert.Matches(centre, "ropeway:bullwheel-west");
            Assert.DoesNotMatch(centre, "ropeway:pylonhead-north");
            Assert.DoesNotMatch(centre, "ropeway:brace-north");
        }
    }

    /// <summary>
    /// ...and because it is a swap, the cabin has to fit it exactly as it fits the sheave. The throat the
    /// hanger blade rides up is now the only surface of the head shapes the cabin touches, so it is asserted
    /// IDENTICAL rather than merely compatible - a bullwheel authored a quarter unit narrower is a cabin
    /// that catches on one tower out of a line of twelve.
    /// <para>
    /// The station rails used to be pinned here too, box for box on both shapes. Neither shape authors any
    /// now: <c>TURNING-SPEC.md</c> §4 asked for all twelve rail elements out, the flares went first and the
    /// two straight plates followed, and the rail is drawn on the path from <see cref="BEPylonBase.BuildRun"/>
    /// for its whole length. That half MOVED rather than being dropped - see
    /// <see cref="TheDrawnRailIsTheBarTheGuideRollersRideIn"/>, which asks the same question of the runtime
    /// cross-section that this one used to ask of the authored one.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBullwheelKeepsTheSheavesThroat()
    {
        (double[] From, double[] To) Box(string shape, string element) =>
            Load("shapes", "block", shape).GetProperty("elements").EnumerateArray()
                .Where(e => e.GetProperty("name").GetString() == element)
                .Select(e => (e.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray(),
                              e.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray()))
                .Single();

        // The throat: the two cheeks' facing x planes. Their height and depth may grow - a bullwheel is a
        // bigger wheel, and drawing one is the whole point - but the gap between them may not move.
        Assert.Equal(Box("pylonhead.json", "sheavecheekwest").To[0], Box("bullwheel.json", "sheavecheekwest").To[0], 3);
        Assert.Equal(Box("pylonhead.json", "sheavecheekeast").From[0], Box("bullwheel.json", "sheavecheekeast").From[0], 3);

        // And neither shape has any rail left to disagree about. Not a tidying assert: the run starts at the
        // tower centre now, so a plate put back would sit inside it rather than beside it.
        foreach (var shape in new[] { "pylonhead.json", "bullwheel.json" })
        {
            Assert.DoesNotContain(Load("shapes", "block", shape).GetProperty("elements").EnumerateArray(),
                e => e.GetProperty("name").GetString()!.StartsWith("rail"));
        }
    }

    /// <summary>
    /// The station rail is entirely a runtime cross-section now, so the thing it has to agree with is the
    /// thing that rides in it. <c>TURNING-SPEC.md</c> §4 asked for all twelve authored rail elements out of
    /// the head and the wheel; the eight flares went first and the two straight plates have followed, and
    /// this test is where their half of the old <c>TheBullwheelKeepsTheSheavesThroatAndStationRails</c>
    /// moved to when that one lost its rail source and became
    /// <see cref="TheBullwheelKeepsTheSheavesThroat"/>.
    /// <para>
    /// The plates were kept for one round because a cabin STOPPED at a tower squares to the tower's own
    /// cardinal rather than to the path, so a cardinal fixture is right under the sheave at any yaw. That
    /// argument survives their deletion: the drawn rail's inner face is 2.600 units from the path at every
    /// point of the run and is never NEARER than that to the anchor - a curve bends the offset away, not
    /// toward - against a roller whose worst corner reach about the yaw axis is 2.571. What killed them is
    /// the other half nobody had measured: a cabin PASSING a corner tower rides the drawn run, which at a
    /// right angle leaves the plate's axis by 1.33 units and buries the whole roller in the plate's metal.
    /// </para>
    /// <para>
    /// So the four numbers are pinned against the rollers and the crossarm instead of against an authored
    /// box, and a unit of drift in any of them is a cabin whose rollers do not touch its rail - which reads
    /// in game as nothing at all, because the two are the same texture and the rail is 0.1 units bigger.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDrawnRailIsTheBarTheGuideRollersRideIn()
    {
        var hangDrop = Load("entities", "cabin.json").GetProperty("attributes").GetProperty("hangDrop").GetDouble();
        var (_, _, elements) = CabinBounds();

        // The slot, in the CABIN shape's own units: the rollers are authored about the cabin's origin, which
        // hangs hangDrop under the anchor the rail is measured down from.
        var band = (hangDrop - BEPylonBase.RailDrop) * 16;
        var inner = BEPylonBase.RailOffset * 16 - BEPylonBase.RailHalfWidth * 16;

        foreach (var roller in new[] { "rollerwest", "rollereast" })
        {
            var from = Find(elements, roller).GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            var to = Find(elements, roller).GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

            // Vertically INSIDE the band, both ends, or the roller is riding the rail's own edge.
            Assert.True(from[1] > band - BEPylonBase.RailHalfDepth * 16 && to[1] < band + BEPylonBase.RailHalfDepth * 16,
                $"{roller} spans {from[1]}..{to[1]} against a rail band of "
                + $"{band - BEPylonBase.RailHalfDepth * 16}..{band + BEPylonBase.RailHalfDepth * 16}");

            // Laterally: 0.1 unit - 0.00625 blocks - of play per side, which is the tightest fit in the whole
            // machine and the reason the rail cannot be left on a cardinal while the cabin turns off it.
            Assert.Equal(0.1, inner - Math.Max(Math.Abs(from[2]), Math.Abs(to[2])), 4);

            // ...and the rail never FOULS it either, at any yaw. A parked cabin turns in place about the
            // anchor, and the drawn rail is never nearer the anchor than its own offset, so the roller's
            // corner is what has to fit rather than its face.
            var corner = Math.Sqrt(
                Math.Max(from[0] * from[0], to[0] * to[0]) + Math.Max(from[2] * from[2], to[2] * to[2]));
            Assert.True(corner < inner,
                $"{roller}'s far corner is {corner:0.###} units off the yaw axis against a {inner:0.###}-unit slot");
        }

        // The band's TOP FACE IS THE CROSSARM'S SOFFIT exactly, which is why the run is drawn a hair
        // shallower than nominal. Without that phase a rail running along under the braces at a badly-faced
        // corner is one plane with them for the whole 3.5-block reach of the crossarm.
        Assert.Equal(0.5, BEPylonBase.RailDrop - BEPylonBase.RailHalfDepth, 4);
    }

    [Fact]
    public void EveryLangKeyTheCodeAsksForIsShipped()
    {
        var lang = JsonDocument.Parse(File.ReadAllText(Path.Combine(Assets, "lang", "en.json")))
            .RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        var used = Directory.GetFiles(Path.Combine(ModRoot, "src"), "*.cs")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(ropeway:[a-zA-Z0-9\\-_]+)\"")
                .Select(m => m.Groups[1].Value))
            .Distinct()
            .Where(k => k != "ropeway:cabin" && k != "ropeway:haulrope") // item codes, not lang keys
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, k => Assert.Contains(k, lang));
    }
}
