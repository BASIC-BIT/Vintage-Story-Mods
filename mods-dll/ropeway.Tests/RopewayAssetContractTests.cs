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
    // RopewayLinkService.HaulRopeCode, BlockPylonHead.CabinItemCode, RopewayLinkService.CabinEntityCode.
    [InlineData("itemtypes/haulrope.json", "haulrope")]
    [InlineData("itemtypes/cabin.json", "cabin")]
    [InlineData("entities/cabin.json", "cabin")]
    [InlineData("blocktypes/pylonhead.json", "pylonhead")]
    [InlineData("blocktypes/brace.json", "brace")]
    public void CodesTheGameplayCodeHardcodesExist(string file, string expectedCode)
    {
        Assert.Equal(expectedCode, Load(file.Split('/')).GetProperty("code").GetString());
    }

    [Fact]
    public void RegisteredClassNamesMatchTheJson()
    {
        var pylon = Load("blocktypes", "pylonhead.json");
        Assert.Equal("BlockPylonHead", pylon.GetProperty("class").GetString());
        Assert.Equal("PylonHead", pylon.GetProperty("entityClass").GetString());

        Assert.Equal("EntityRopewayCabin", Load("entities", "cabin.json").GetProperty("class").GetString());
    }

    [Fact]
    public void PylonHeadHasTheSideVariantGroupBEPylonHeadReads()
    {
        var groups = Load("blocktypes", "pylonhead.json").GetProperty("variantgroups")
            .EnumerateArray().Select(g => g.GetProperty("code").GetString()).ToList();

        Assert.Contains("side", groups);
    }

    [Fact]
    public void PylonHeadCarriesTheAttributesBEPylonHeadReads()
    {
        var attributes = Load("blocktypes", "pylonhead.json").GetProperty("attributes");

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

        // BEPylonHead.RopePerBlock. At 1.0 a 48-block span is 96 vanilla rope = 576 cattail tops, which is
        // the most expensive thing in the game; DECISIONS.md 3 asked for cheap.
        var ropePerBlock = attributes.GetProperty("ropePerBlock").GetDouble();
        Assert.InRange(ropePerBlock, 0.01, 0.5);
        Assert.Equal(12, SpanMath.RopeCost(48, ropePerBlock));
    }

    [Fact]
    public void PylonHeadShapeIsAsymmetricAlongTheRearGantryAxis()
    {
        // All four side variants are the same shape rotated. If every element is symmetric in z the
        // player cannot see which way the head faces and builds the 21-block rear gantry on the wrong
        // side of it, with nothing but the block info panel to say so.
        var elements = Load("shapes", "block", "pylonhead.json").GetProperty("elements").EnumerateArray()
            .Select(e => (From: e.GetProperty("from")[2].GetDouble(), To: e.GetProperty("to")[2].GetDouble()))
            .ToList();

        Assert.Contains(elements, e => Math.Abs(e.From + e.To - 16.0) > 0.001);
    }

    [Fact]
    public void MultiblockOffsetsAreTheTowerShellAndNothingElse()
    {
        var structure = Load("blocktypes", "pylonhead.json").GetProperty("attributes").GetProperty("multiblockStructure");

        var numbers = structure.GetProperty("blockNumbers").EnumerateObject()
            .Select(p => p.Value.GetInt32()).ToHashSet();

        var offsets = structure.GetProperty("offsets").EnumerateArray()
            .Select(o => (X: o.GetProperty("x").GetInt32(), Y: o.GetProperty("y").GetInt32(),
                          Z: o.GetProperty("z").GetInt32(), W: o.GetProperty("w").GetInt32()))
            .ToList();

        Assert.Equal(21, offsets.Count);
        Assert.Equal(offsets.Count, offsets.Select(o => (o.X, o.Y, o.Z)).Distinct().Count());

        // The controller is the pylon head itself and must not be one of the cells it checks.
        Assert.DoesNotContain(offsets, o => o.X == 0 && o.Y == 0 && o.Z == 0);

        // Every w must resolve, or MultiblockStructure.InCompleteBlockCount throws on BlockCodes[w].
        Assert.All(offsets, o => Assert.Contains(o.W, numbers));

        // The 3 wide x 4 long x 3 tall passage the cabin travels through has to stay empty.
        Assert.DoesNotContain(offsets, o => o.X is >= -1 and <= 1 && o.Y is >= -3 and <= -1 && o.Z is >= 0 and <= 3);
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
