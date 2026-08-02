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
    [InlineData("blocktypes/brace.json", "brace")]
    public void CodesTheGameplayCodeHardcodesExist(string file, string expectedCode)
    {
        Assert.Equal(expectedCode, Load(file.Split('/')).GetProperty("code").GetString());
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
        // ranged. The server keeps chunks loaded within MaxChunkRadius of a player - default 12 chunks /
        // 384 blocks (ServerConfig.cs:925), only ever raised (ServerMain.cs:789). Chain length upper-bounds
        // the straight-line distance between any two towers, so a line shorter than that radius can never
        // have a tower unload while a rider is on it - and that unload is the precondition for the entire
        // truncated-line failure class (docs/KNOWN-ISSUES.md R1-R4). 320 = 10 chunks, two chunks of margin.
        // Raising this toward 384 removes the margin; past 384 the bugs come back.
        var maxLineLength = attributes.GetProperty("maxLineLength").GetDouble();
        Assert.Equal(320, maxLineLength);
        Assert.True(maxLineLength <= 320, "maxLineLength must stay inside the default server chunk radius");
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
    [Fact]
    public void ThePylonHeadShapeIsSymmetricAlongTheRopeAxis()
    {
        // Symmetric as a SET, not element by element: the two sheave cheek plates sit at z 3-5 and 11-13
        // and are each other's mirror. So the whole box list has to map onto itself under z -> 16 - z.
        var boxes = Load("shapes", "block", "pylonhead.json").GetProperty("elements").EnumerateArray()
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

        // One crossarm of five on two posts of four, plus the footing that owns them: 14 cells, 13 offsets.
        Assert.Equal(13, offsets.Count);
        Assert.Equal(offsets.Count, offsets.Select(o => (o.X, o.Y, o.Z)).Distinct().Count());

        // The controller is the footing itself and must not be one of the cells it checks.
        Assert.DoesNotContain(offsets, o => o.X == 0 && o.Y == 0 && o.Z == 0);

        // Every w must resolve, or MultiblockStructure.InCompleteBlockCount throws on BlockCodes[w].
        Assert.All(offsets, o => Assert.Contains(o.W, numbers));

        // One block deep. The 4-long cabin passes through a 1-deep frame; a second gantry is a tunnel.
        Assert.All(offsets, o => Assert.Equal(0, o.Z));

        // The 3-wide, 4-tall archway the cabin travels through has to stay empty.
        Assert.DoesNotContain(offsets, o => o.X is >= -1 and <= 1 && o.Y is >= 0 and <= 3);

        // The sheave is the crossarm's centre cell, directly SheaveHeight above the footing - AnchorOf,
        // BEPylonBase's cable mesh and BlockPylonHead's block-info lookup all assume exactly this cell.
        Assert.Contains(offsets, o => o.X == 0 && o.Y == SpanMath.SheaveHeight && o.Z == 0);

        // Posts reach the ground the footing stands on. Starting them a cell higher leaves the legs hanging
        // in the air, which is what "posts three tall" would have produced.
        foreach (var x in new[] { -2, 2 })
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
        var mastTip = anchor - hangDrop + Top("mast");

        var footingTop = Load("shapes", "block", "pylonbase.json").GetProperty("elements").EnumerateArray()
            .Max(e => e.GetProperty("to")[1].GetDouble()) / 16;
        var crossarmUnderside = SpanMath.SheaveHeight
            + Load("shapes", "block", "brace.json").GetProperty("elements").EnumerateArray()
                .Min(e => e.GetProperty("from")[1].GetDouble()) / 16;

        Assert.True(cabinFloor > footingTop,
            $"the cabin floor at {cabinFloor} cuts through the footing, which tops out at {footingTop}");
        Assert.True(cabinRoof < crossarmUnderside,
            $"the cabin roof at {cabinRoof} cuts through the crossarm, whose underside is at {crossarmUnderside}");

        // BASIC's figure, and the reason SheaveHeight is 4 rather than 3: three quarters of a block of air
        // between the footing and the cabin passing over it.
        Assert.Equal(0.75, cabinFloor - footingTop, 3);

        // The mast has to reach the sheave throat, or the cabin visibly hangs from nothing. It lands exactly
        // on the anchor - the centre of the sheave block, and the point the cable is drawn from.
        Assert.Equal(anchor, mastTip, 3);
    }

    private static JsonElement Find(List<(string Name, JsonElement Element)> elements, string name)
    {
        return elements.First(e => e.Name == name).Element;
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

        // The tower's posts sit at block x = +/-2, leaving three free cells, and SpanMath.ClearanceRadius
        // certifies exactly that corridor. Anything wider is a cabin no line can legally carry.
        Assert.True((max[2] - min[2]) / 16 <= 2 * SpanMath.ClearanceRadius + 1,
            "the cabin is wider than the passage it travels through");
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
