using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    public void CodesTheGameplayCodeHardcodesExist(string file, string expectedCode)
    {
        Assert.Equal(expectedCode, Load(file.Split('/')).GetProperty("code").GetString());
    }

    /// <summary>
    /// The mechanical power hookup, all of which is JSON that C# cannot check for itself. It lives on the
    /// ground-level DRIVE HOUSING, and on nothing else - the footing and the bullwheel are both asserted
    /// clean, because a second consumer anywhere on a tower would let <c>PoolSpeed</c> see one network twice
    /// through two different blocks.
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
        var behaviors = Load("blocktypes", "drivehousing.json").GetProperty("entityBehaviors").EnumerateArray().ToList();
        var consumer = behaviors.Single(b => b.GetProperty("name").GetString() == "MPConsumer");

        var properties = consumer.GetProperty("properties");
        Assert.True(properties.TryGetProperty("mechPartShape", out var shape), "mechPartShape must be present");
        Assert.Equal(JsonValueKind.Null, shape.ValueKind);

        // The behaviour's own default is 0.1. BEDriveHousing rewrites this every second, so what the JSON
        // pins is the state a housing is in before its first tick - which must be the idle one, or a fresh
        // chunk load taxes the network for a second on every drive of a long line.
        Assert.Equal(RopewayPower.IdleResistance, properties.GetProperty("resistance").GetSingle(), 4);

        Assert.False(Load("blocktypes", "pylonbase.json").TryGetProperty("entityBehaviors", out _));

        // The bullwheel is DECORATION. It carried this behaviour for one trial; leaving it on would put the
        // drive back four blocks up, where reaching it cost sixteen vanilla blocks of scaffold.
        Assert.False(Load("blocktypes", "bullwheel.json").TryGetProperty("entityBehaviors", out _));
    }

    /// <summary>
    /// The housing is bound to a line by proximity at lookup time, so a zero radius is a block that can
    /// never be built and a line that can never have a drive - the same rule and the same failure the
    /// tensioner has.
    /// </summary>
    [Fact]
    public void TheDriveHousingCarriesTheAttributesItsBlockEntityReads()
    {
        var block = Load("blocktypes", "drivehousing.json");
        Assert.Equal("BlockDriveHousing", block.GetProperty("class").GetString());
        Assert.Equal("DriveHousing", block.GetProperty("entityClass").GetString());
        Assert.True(block.GetProperty("attributes").GetProperty("towerRadius").GetDouble() > 0);

        // No side variant: the housing connects on every horizontal face, so orientation decides nothing -
        // and a block with no orientation cannot be placed 90 degrees out, which is the failure the
        // crossarm's oriented blocks still carry.
        Assert.False(block.TryGetProperty("variantgroups", out _));
    }

    /// <summary>
    /// The turning half of the bullwheel is a SEPARATE shape, drawn by <c>BullwheelRenderer</c> over the
    /// static one, and it has to stay clear of the block cell the cabin passes through. The sheave throat is
    /// where the cabin's hanger blade rides at every tower; a wheel authored down into it is a cabin that
    /// catches, and nothing in the game would say so - it would just stop.
    /// </summary>
    [Fact]
    public void TheTurningWheelStaysAboveTheCellTheCabinPassesThrough()
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
        var cull = 0.0;
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

            // The same corners, measured from the BLOCK CENTRE instead of the axle, which is the question the
            // frustum sphere asks. Turning about the axle moves a corner round a circle centred on
            // (centre, 8) in the y-z plane, so the furthest it ever gets from (8, 8) is that offset plus its
            // own radius; x rides along unchanged and a yaw about the block's own vertical - which contains
            // both the axle and the block centre - cannot change the distance either. Per element, because
            // the hub is the widest in x and the felloe the furthest out, and neither wins on both.
            var spanX = Math.Max(Math.Abs(from[0] - 8), Math.Abs(to[0] - 8));
            cull = Math.Max(cull, Math.Sqrt(spanX * spanX + (centre - 8 + swept) * (centre - 8 + swept)));
        }

        Assert.True(centre - reach >= 16,
            $"the turning wheel sweeps down to {centre - reach}, inside the crossarm cell the cabin passes through");

        // The other end of the same measurement. Nothing in the game complains when a frustum sphere is too
        // small - the wheel simply vanishes at the edge of the screen on a tower the player is looking at -
        // so the number the renderer culls against is tied to the shape here rather than trusted.
        Assert.True(BullwheelRenderer.CullRadius >= cull / 16,
            $"the turning wheel reaches {cull / 16} blocks from the block centre, outside the {BullwheelRenderer.CullRadius}-block frustum sphere it is culled against");
    }

    [Fact]
    public void TheTensionWeightCarriesTheAttributesItsBlockEntityReads()
    {
        var block = Load("blocktypes", "tensionweight.json");
        Assert.Equal("BlockTensionWeight", block.GetProperty("class").GetString());
        Assert.Equal("TensionWeight", block.GetProperty("entityClass").GetString());

        // The only rule the tensioner has left. It is read at placement AND at lookup, so a zero here is a
        // block that can never be built and a line that can never have one.
        Assert.True(block.GetProperty("attributes").GetProperty("towerRadius").GetDouble() > 0);
    }

    /// <summary>
    /// The mass is authored in the SHAPE now, not drawn by the block entity at a height that meant a charge,
    /// and it has to hang inside the guide rails (2/16 to 46/16) rather than through its own pad or out the
    /// top. Nothing else notices: a mass outside its guide is a render nobody's test suite looks at.
    /// </summary>
    [Fact]
    public void TheHangingMassStaysInsideTheGuideItHangsIn()
    {
        var elements = Load("shapes", "block", "tensionweight.json").GetProperty("elements").EnumerateArray().ToList();
        var mass = elements.Single(e => e.GetProperty("name").GetString() == "mass");

        var from = mass.GetProperty("from").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var to = mass.GetProperty("to").EnumerateArray().Select(v => v.GetDouble()).ToArray();

        Assert.True(from[1] >= 2, $"the mass sinks into its own pad: {from[1]}");
        Assert.True(to[1] <= 46, $"the mass pokes out the top of its guide: {to[1]}");

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

    [Fact]
    public void RegisteredClassNamesMatchTheJson()
    {
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

    [Fact]
    public void MultiblockOffsetsAreTheTowerShellAndNothingElse()
    {
        var structure = Load("blocktypes", "pylonbase.json").GetProperty("attributes").GetProperty("multiblockStructure");

        var numbers = structure.GetProperty("blockNumbers").EnumerateObject()
            .Select(p => p.Value.GetInt32()).ToHashSet();

        var offsets = structure.GetProperty("offsets").EnumerateArray()
            .Select(o => (X: o.GetProperty("x").GetInt32(), Y: o.GetProperty("y").GetInt32(),
                          Z: o.GetProperty("z").GetInt32(), W: o.GetProperty("w").GetInt32()))
            .ToList();

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
        var crossarmUnderside = SpanMath.SheaveHeight
            + new[] { "brace.json", "pylonhead.json", "bullwheel.json" }
                .Select(shape => Load("shapes", "block", shape).GetProperty("elements").EnumerateArray()
                    .Min(e => e.GetProperty("from")[1].GetDouble()))
                .Min() / 16;

        Assert.True(cabinFloor > footingTop,
            $"the cabin floor at {cabinFloor} cuts through the footing, which tops out at {footingTop}");
        Assert.True(cabinRoof < crossarmUnderside,
            $"the cabin roof at {cabinRoof} cuts through the crossarm, whose underside is at {crossarmUnderside}");

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
        // and the underside of the rails in the cabin shape's own coordinates.
        var slot = (HeadEdge("sheavecheekeast", "from") - HeadEdge("sheavecheekwest", "to")) / 2;
        var railBottom = (hangDrop - 0.5) * 16 + head.Min(e => e.GetProperty("from")[1].GetDouble());

        var reaching = elements.Where(e => e.Element.GetProperty("to")[1].GetDouble() > railBottom).ToList();
        Assert.NotEmpty(reaching);
        Assert.DoesNotContain(reaching, e => e.Name == "hangershoulder");

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
    /// The bullwheel is a SWAP for the sheave, not an extra cell: the crossarm's centre cell accepts either,
    /// so a drive tower is the same sixteen cells as every other tower and the drive costs no per-tower
    /// marginal anything (DECISIONS.md 3). Narrow this wildcard back and every existing drive tower reads as
    /// incomplete, silently, with the highlight overlay pointing at a cell that already has a block in it.
    /// </summary>
    [Fact]
    public void TheCrossarmCentreCellTakesEitherASheaveOrABullwheel()
    {
        var structure = Load("blocktypes", "pylonbase.json").GetProperty("attributes").GetProperty("multiblockStructure");

        var centre = structure.GetProperty("offsets").EnumerateArray()
            .Single(o => o.GetProperty("x").GetInt32() == 0 && o.GetProperty("z").GetInt32() == 0
                         && o.GetProperty("y").GetInt32() == SpanMath.SheaveHeight)
            .GetProperty("w").GetInt32();

        var wildcard = structure.GetProperty("blockNumbers").EnumerateObject()
            .Single(p => p.Value.GetInt32() == centre).Name;

        // WildcardUtil anchors an @-pattern as ^...$ (RegexCache.IsMatch), and * means "anything".
        var pattern = new Regex("^" + wildcard.Replace("@", "").Replace("*", ".*") + "$");

        Assert.Matches(pattern, "ropeway:pylonhead-north");
        Assert.Matches(pattern, "ropeway:bullwheel-west");
        Assert.DoesNotMatch(pattern, "ropeway:brace-north");
    }

    /// <summary>
    /// ...and because it is a swap, the cabin has to fit it exactly as it fits the sheave. The throat the
    /// hanger blade rides up and the station rails the guide rollers run inside are the two surfaces the
    /// cabin actually touches, so they are asserted IDENTICAL rather than merely compatible - a bullwheel
    /// authored a quarter unit narrower is a cabin that catches on one tower out of a line of twelve.
    /// </summary>
    [Fact]
    public void TheBullwheelKeepsTheSheavesThroatAndStationRails()
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

        // The rails, whole: the rollers ride inside them and the crossarm-underside clearance is measured
        // off them, so nothing about them may differ at all.
        foreach (var rail in new[] { "railwest", "raileast" })
        {
            Assert.Equal(Box("pylonhead.json", rail).From, Box("bullwheel.json", rail).From);
            Assert.Equal(Box("pylonhead.json", rail).To, Box("bullwheel.json", rail).To);
        }
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
