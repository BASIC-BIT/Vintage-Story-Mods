using System.Text.Json;
using Vintagestory.API.MathTools;

namespace FlywheelPower.Tests;

public sealed class ReleaseContentTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void SlipTransmissionHasNoActiveBlocktypeOrRuntimeRegistration()
    {
        string activeBlocktypes = Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes");
        string modSystem = File.ReadAllText(Path.Combine(ProjectRoot, "src", "FlywheelPowerModSystem.cs"));

        Assert.DoesNotContain(
            Directory.EnumerateFiles(activeBlocktypes, "*.json", SearchOption.TopDirectoryOnly),
            path => Path.GetFileName(path).Equals("sliptransmission.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("RegisterBlockClass(\"BlockSlipTransmission\"", modSystem, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterBlockEntityBehaviorClass(\"MPSlipTransmission\"", modSystem, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sliptransmission",
            File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json")),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "sliptransmission.json")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "lang", "en.sliptransmission.json")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "shapes", "block", "slip-transmission-shaft.json")));
        Assert.False(File.Exists(Path.Combine(ProjectRoot, "assets", "flywheelpower", "shapes", "block", "slip-transmission-shaft.json")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "BlockSlipTransmission.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "BEBehaviorMPSlipTransmission.cs")));
    }

    [Fact]
    public void UnpublishedLegacyAliasesAndUnreferencedShapesAreRemoved()
    {
        string activeBlocktypes = Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes");
        string activeShapes = Path.Combine(ProjectRoot, "assets", "flywheelpower", "shapes", "block");

        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "flywheellegacy.json")));
        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "keyedflywheellegacy.json")));
        Assert.False(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "flywheellegacy.json")));
        Assert.False(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "keyedflywheellegacy.json")));
        Assert.False(File.Exists(Path.Combine(activeShapes, "flywheel-frame.json")));
        Assert.False(File.Exists(Path.Combine(activeShapes, "compact-flywheel-frame.json")));
    }

    [Fact]
    public void ReleaseSurfaceUsesCuratedMaterialsWithDistinctRendererGroups()
    {
        string activeBlocktypes = Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes");
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));
        string fullSizeBlocktype = File.ReadAllText(Path.Combine(activeBlocktypes, "flywheel.json"));
        string compactBlocktype = File.ReadAllText(Path.Combine(activeBlocktypes, "compactflywheel.json"));

        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "keyedflywheel.json")));
        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "keyedcompactflywheel.json")));
        Assert.DoesNotContain("keyedflywheel", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "keyedflywheel.json")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "keyedcompactflywheel.json")));
        Assert.Contains("""{ code: "material", states: ["wood", "iron", "meteoriciron", "steel"] }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""{ code: "hub", states: ["iron", "meteoriciron", "steel"] }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-wood-meteoriciron-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-wood-steel-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-iron-meteoriciron-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-iron-steel-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-meteoriciron-iron-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("\"flywheel-meteoriciron-steel-*\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("flywheel-steel-iron-*", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("flywheel-steel-meteoriciron-*", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""{ code: "material", states: ["wood", "stone", "iron", "meteoriciron", "steel"] }""", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("""{ code: "hub", states: ["iron", "meteoriciron", "steel"] }""", compactBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("bronze", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("""states: ["wood", "stone""", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("*-stone-iron-*", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bronze", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stone", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("meteoriciron", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("steel", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""flywheelpower-full-meteoriciron-meteoricironhub""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""flywheelpower-full-steel-steelhub""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"*-meteoriciron-meteoriciron-*\": {", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"*-steel-steel-*\": {", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"*-meteoriciron-steel-*\": {", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"*-steel-steel-*\": {", compactBlocktype, StringComparison.Ordinal);
        Assert.True(CountOccurrences(fullSizeBlocktype, """metal: { base: "game:block/metal/ingot/meteoriciron" }""") >= 1);
        Assert.True(CountOccurrences(fullSizeBlocktype, """metal: { base: "game:block/metal/ingot/steel" }""") >= 1);
        Assert.True(CountOccurrences(compactBlocktype, """metal: { base: "game:block/metal/ingot/meteoriciron" }""") >= 1);
        Assert.True(CountOccurrences(compactBlocktype, """metal: { base: "game:block/metal/ingot/steel" }""") >= 1);
        Assert.Contains("""axleShape: { base: "flywheelpower:block/flywheel-axle" }""", compactBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("slip-transmission-shaft", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("""bearing: { base: "game:block/metal/tarnished/iron-riveted1" }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""bearing: { base: "game:block/metal/tarnished/iron-riveted1" }""", compactBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("bronze", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("block-flywheel-stone-iron", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("block-compactflywheel-stone", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("meteoriciron", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("steel", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blockinfo-shaft", activeLanguage, StringComparison.Ordinal);
        Assert.Equal(23, FlywheelPowerModSystem.ReleasedRendererCodes.Length);
        Assert.Equal(23, FlywheelPowerModSystem.ReleasedRendererCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            FlywheelPowerModSystem.ReleasedRendererCodes.Take(10),
            rendererCode => Assert.Contains(rendererCode, fullSizeBlocktype, StringComparison.Ordinal));
        Assert.All(
            FlywheelPowerModSystem.ReleasedRendererCodes.Skip(10),
            rendererCode => Assert.Contains(rendererCode, compactBlocktype, StringComparison.Ordinal));
    }

    [Fact]
    public void HubTierMustMeetOrExceedWheelTier()
    {
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("wood", "iron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("stone", "meteoriciron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("iron", "meteoriciron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("meteoriciron", "iron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("iron", "steel"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "steel"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "iron"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "meteoriciron"));
    }

    [Fact]
    public void PlacedFlywheelsRegisterStaticAxisAwareStandMeshes()
    {
        string behaviorSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BEBehaviorMPFlywheel.cs"));
        string fullSizeBlocktype = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes", "flywheel.json"));
        string compactBlocktype = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes", "compactflywheel.json"));

        Assert.Contains("FlywheelStandRenderable", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("public float AngleRad => 0f;", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("manager.AddDeviceForRender(standRenderable);", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("""horizontalStandShape: { base: "flywheelpower:block/flywheel-frame-horizontal" }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""verticalStandShape: { base: "flywheelpower:block/flywheel-frame-vertical" }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""horizontalStandShape: { base: "flywheelpower:block/compact-flywheel-frame-horizontal" }""", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("""verticalStandShape: { base: "flywheelpower:block/compact-flywheel-frame-vertical" }""", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("SetShapeRotation(0f, 0f, 90f, 0f, 0f, 0f);", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("SetShapeRotation(0f, 90f, 0f, 0f, 90f, 0f);", behaviorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalComparisonAppearsOnHeldAndPlacedBlockInfo()
    {
        string fullBlockSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheel.cs"));
        string compactBlockSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockCompactFlywheel.cs"));
        string behaviorSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BEBehaviorMPFlywheel.cs"));
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));

        Assert.Contains("AddExtraHeldItemInfoPostMaterial", fullBlockSource, StringComparison.Ordinal);
        Assert.Contains("AddExtraHeldItemInfoPostMaterial", compactBlockSource, StringComparison.Ordinal);
        Assert.Contains("flywheelpower:blockinfo-physical", fullBlockSource, StringComparison.Ordinal);
        Assert.Contains("flywheelpower:blockinfo-physical", compactBlockSource, StringComparison.Ordinal);
        Assert.Contains("flywheelpower:blockinfo-physical", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("Rotating mass: {0} kg; effective inertia: {1}", activeLanguage, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryPreviewMatchesReleasedFullSizeDimensions()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                ProjectRoot,
                "assets",
                "flywheelpower",
                "shapes",
                "block",
                "flywheel-wheel-coupled.json")));
        JsonElement elements = document.RootElement.GetProperty("elements");
        JsonElement[] spokes = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("WoodSpoke", StringComparison.Ordinal))
            .ToArray();
        JsonElement[] felloes = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("WoodFelloe", StringComparison.Ordinal))
            .ToArray();
        JsonElement[] tyreSegments = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("OuterTyre", StringComparison.Ordinal))
            .ToArray();
        JsonElement hub = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "Hub");
        JsonElement bearing = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "BearingCollar");
        JsonElement chalkFront = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineFront");
        JsonElement chalkBack = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineBack");
        JsonElement chalkRim = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineRim");

        Assert.Equal(8, spokes.Length);
        Assert.Equal(8, felloes.Length);
        Assert.Equal(8, tyreSegments.Length);
        Assert.All(spokes, spoke =>
            Assert.Equal("#wood", spoke.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(felloes, felloe =>
            Assert.Equal("#wood", felloe.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(tyreSegments, tyre =>
            Assert.Equal("#wheel", tyre.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(tyreSegments, tyre =>
            Assert.Equal(3d, tyre.GetProperty("to")[0].GetDouble() - tyre.GetProperty("from")[0].GetDouble(), 2));
        Assert.Equal(1.92d, tyreSegments[0].GetProperty("to")[2].GetDouble() - tyreSegments[0].GetProperty("from")[2].GetDouble(), 2);
        Assert.Equal(8.96d, hub.GetProperty("to")[1].GetDouble() - hub.GetProperty("from")[1].GetDouble(), 2);
        Assert.Equal(4.32d, hub.GetProperty("to")[0].GetDouble() - hub.GetProperty("from")[0].GetDouble(), 2);
        Assert.Equal(6.08d, bearing.GetProperty("to")[1].GetDouble() - bearing.GetProperty("from")[1].GetDouble(), 2);
        Assert.Equal(4.8d, bearing.GetProperty("to")[0].GetDouble() - bearing.GetProperty("from")[0].GetDouble(), 2);
        Assert.True(chalkFront.GetProperty("to")[2].GetDouble() > chalkRim.GetProperty("from")[2].GetDouble());
        Assert.True(chalkBack.GetProperty("to")[2].GetDouble() > chalkRim.GetProperty("from")[2].GetDouble());
        Assert.True(chalkRim.GetProperty("from")[0].GetDouble() <= chalkBack.GetProperty("to")[0].GetDouble());
        Assert.True(chalkRim.GetProperty("to")[0].GetDouble() >= chalkFront.GetProperty("from")[0].GetDouble());
    }

    [Fact]
    public void CompactInventoryPreviewMatchesPointNineTwoMeterRuntimeDiameter()
    {
        JsonElement[] elements = ReadShapeElements("compact-flywheel-wheel-coupled.json");
        JsonElement[] ring = elements
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("PreviewRing", StringComparison.Ordinal))
            .ToArray();

        double minY = ring.Min(element => element.GetProperty("from")[1].GetDouble());
        double maxY = ring.Max(element => element.GetProperty("to")[1].GetDouble());
        double minZ = ring.Min(element => element.GetProperty("from")[2].GetDouble());
        double maxZ = ring.Max(element => element.GetProperty("to")[2].GetDouble());
        double minX = ring.Min(element => element.GetProperty("from")[0].GetDouble());
        double maxX = ring.Max(element => element.GetProperty("to")[0].GetDouble());

        Assert.Equal(14.72d, maxY - minY, 2);
        Assert.Equal(14.72d, maxZ - minZ, 2);
        Assert.Equal(5.12d, maxX - minX, 2);
    }

    [Fact]
    public void MetadataDeclaresUniversalSurvivalDependency()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ProjectRoot, "modinfo.json")));
        JsonElement root = document.RootElement;

        Assert.Equal("0.5.0", root.GetProperty("version").GetString());
        Assert.Equal("universal", root.GetProperty("side").GetString());
        Assert.True(root.TryGetProperty("dependencies", out JsonElement dependencies));
        Assert.True(dependencies.TryGetProperty("game", out _));
        Assert.True(dependencies.TryGetProperty("survival", out _));
        Assert.False(root.TryGetProperty("dependency", out _));
    }

    [Fact]
    public void GroundedFramesIncludeServiceableBearingStandDetails()
    {
        JsonElement[] fullElements = ReadShapeElements("flywheel-frame-horizontal.json");
        string[] fullNames = fullElements
            .Select(element => element.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Contains("LeftGroundSleeper", fullNames);
        Assert.Contains("RightGroundSleeper", fullNames);
        Assert.Contains("LeftBearingCap", fullNames);
        Assert.Contains("RightBearingCap", fullNames);
        Assert.Contains("LeftFrontBrace", fullNames);
        Assert.Contains("LeftRearBrace", fullNames);
        Assert.Contains("RightFrontBrace", fullNames);
        Assert.Contains("RightRearBrace", fullNames);
        Assert.Contains("LeftGreaseCup", fullNames);
        Assert.Contains("RightGreaseCup", fullNames);
        Assert.Contains("FrontLeftHoldDown", fullNames);
        Assert.Equal(-16d, fullElements.Min(element => element.GetProperty("from")[1].GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("FrontBrace", StringComparison.Ordinal)),
            brace => Assert.Equal(18d, brace.GetProperty("rotationX").GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("RearBrace", StringComparison.Ordinal)),
            brace => Assert.Equal(-18d, brace.GetProperty("rotationX").GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("BearingCap", StringComparison.Ordinal)),
            cap => Assert.All(
                cap.GetProperty("faces").EnumerateObject(),
                face => Assert.Equal("#wood", face.Value.GetProperty("texture").GetString())));

        JsonElement[] compactElements = ReadShapeElements("compact-flywheel-frame-horizontal.json");
        string[] compactNames = compactElements
            .Select(element => element.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Contains("LeftSleeper", compactNames);
        Assert.Contains("RightSleeper", compactNames);
        Assert.Contains("LeftBearingPost", compactNames);
        Assert.Contains("RightBearingPost", compactNames);
        Assert.Contains("LeftGreaseCup", compactNames);
        Assert.Contains("RightGreaseCup", compactNames);
        Assert.All(
            compactElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("BearingCap", StringComparison.Ordinal)),
            cap => Assert.All(
                cap.GetProperty("faces").EnumerateObject(),
                face => Assert.Equal("#wood", face.Value.GetProperty("texture").GetString())));
    }

    [Fact]
    public void FullSizePlacementExplainsItsReservedFootprint()
    {
        string multiblockSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "FlywheelMultiblock.cs"));
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));

        Assert.Contains("""failureCode = "flywheelrequiresclearance";""", multiblockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("""failureCode = "notenoughspace";""", multiblockSource, StringComparison.Ordinal);
        Assert.Contains(
            """"placefailure-flywheelrequiresclearance": "Requires a clear 3x3 area around the flywheel plane."""",
            activeLanguage,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StandsRequireGroundAcrossTheirPhysicalFootprints()
    {
        BlockPos center = new(10, 20, 30, 0);

        BlockPos[] horizontal = FlywheelGroundSupport
            .GetFullSizeSupportPositions(center, EnumAxis.X)
            .ToArray();
        Assert.Equal(3, horizontal.Length);
        Assert.All(horizontal, pos => Assert.Equal(18, pos.Y));
        Assert.Equal(new[] { 29, 30, 31 }, horizontal.Select(pos => pos.Z).ToArray());

        BlockPos[] vertical = FlywheelGroundSupport
            .GetFullSizeSupportPositions(center, EnumAxis.Y)
            .ToArray();
        Assert.Equal(9, vertical.Length);
        Assert.All(vertical, pos => Assert.Equal(19, pos.Y));

        string standSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheelStand.cs"));
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));
        Assert.Contains("""failureCode = "flywheelrequiresfoundation";""", standSource, StringComparison.Ordinal);
        Assert.Contains("placefailure-flywheelrequiresfoundation", activeLanguage, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyBlocksRequireAPlacedStandAndReturnBothPartsWhenBroken()
    {
        string fullSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheel.cs"));
        string compactSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockCompactFlywheel.cs"));
        string standSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheelStand.cs"));
        string fullBlocktype = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes", "flywheel.json"));
        string compactBlocktype = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes", "compactflywheel.json"));

        Assert.Contains("""failureCode = "flywheelrequiresstand";""", fullSource, StringComparison.Ordinal);
        Assert.Contains("""failureCode = "flywheelrequiresstand";""", compactSource, StringComparison.Ordinal);
        Assert.Contains("SetBlock(installed.BlockId", standSource, StringComparison.Ordinal);
        Assert.Contains("slot.TakeOut(1)", standSource, StringComparison.Ordinal);
        Assert.Contains("code: \"flywheelstand-full-ud\"", fullBlocktype, StringComparison.Ordinal);
        Assert.Contains("code: \"flywheelstand-compact-ud\"", compactBlocktype, StringComparison.Ordinal);
    }

    [Fact]
    public void SurvivalRecipesExposeTheStagedConstructionChain()
    {
        string recipes = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(ProjectRoot, "assets", "flywheelpower", "recipes", "grid"),
                    "*.json")
                .Select(File.ReadAllText));

        Assert.Contains("game:fat-rendered", recipes, StringComparison.Ordinal);
        Assert.Contains("game:metalplate-iron", recipes, StringComparison.Ordinal);
        Assert.Contains("game:metalplate-meteoriciron", recipes, StringComparison.Ordinal);
        Assert.Contains("game:metalplate-steel", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelbearing-full-", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelweb-full", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelrim-full-", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelstand-full-ud", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelstand-compact-ud", recipes, StringComparison.Ordinal);
        Assert.DoesNotContain("flywheelrim-full-stone", recipes, StringComparison.Ordinal);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln")))
            {
                return Path.Combine(directory.FullName, "mods-dll", "flywheelpower");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the flywheelpower project root.");
    }

    private static JsonElement[] ReadShapeElements(string fileName)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                ProjectRoot,
                "assets",
                "flywheelpower",
                "shapes",
                "block",
                fileName)));
        return document.RootElement
            .GetProperty("elements")
            .EnumerateArray()
            .Select(element => element.Clone())
            .ToArray();
    }

    private static int CountOccurrences(string source, string value)
    {
        return source.Split(value, StringSplitOptions.None).Length - 1;
    }
}
