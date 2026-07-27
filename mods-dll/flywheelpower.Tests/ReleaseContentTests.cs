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
        JsonElement[] hub = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("Hub", StringComparison.Ordinal))
            .ToArray();
        JsonElement[] bearing = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("BearingCollar", StringComparison.Ordinal))
            .ToArray();
        JsonElement chalkFront = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineFront");
        JsonElement chalkBack = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineBack");
        JsonElement chalkRim = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineRim");

        Assert.Equal(8, spokes.Length);
        Assert.Equal(16, felloes.Length);
        Assert.Equal(16, tyreSegments.Length);
        Assert.Equal(16, hub.Length);
        Assert.Equal(16, bearing.Length);
        Assert.All(spokes, spoke =>
            Assert.Equal("#wood", spoke.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(felloes, felloe =>
            Assert.Equal("#wood", felloe.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(tyreSegments, tyre =>
            Assert.Equal("#wheel", tyre.GetProperty("faces").GetProperty("north").GetProperty("texture").GetString()));
        Assert.All(tyreSegments, tyre =>
            Assert.Equal(3d, tyre.GetProperty("to")[0].GetDouble() - tyre.GetProperty("from")[0].GetDouble(), 2));
        Assert.Equal(1.92d, tyreSegments[0].GetProperty("to")[2].GetDouble() - tyreSegments[0].GetProperty("from")[2].GetDouble(), 2);
        Assert.All(hub, segment =>
            Assert.Equal(4.32d, segment.GetProperty("to")[0].GetDouble() - segment.GetProperty("from")[0].GetDouble(), 2));
        Assert.All(bearing, segment =>
            Assert.Equal(4.8d, segment.GetProperty("to")[0].GetDouble() - segment.GetProperty("from")[0].GetDouble(), 2));
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
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("CompactWheel", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(16, ring.Length);
        Assert.All(
            ring,
            segment => Assert.Equal(
                5.12d,
                segment.GetProperty("to")[0].GetDouble() - segment.GetProperty("from")[0].GetDouble(),
                2));
        for (int index = 0; index < ring.Length; index++)
        {
            Assert.Equal(
                5.365d + index * 0.01d,
                ring[index].GetProperty("from")[0].GetDouble(),
                4);
        }
        Assert.All(
            ring,
            segment => Assert.Equal(
                4.16d,
                segment.GetProperty("to")[2].GetDouble() - segment.GetProperty("from")[2].GetDouble(),
                2));
        Assert.Equal(
            Enumerable.Range(0, 16).Select(index => index * 22.5d),
            ring.Select(segment => segment.GetProperty("rotationX").GetDouble()));
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
        Assert.Contains("LeftBearingHousingLower", fullNames);
        Assert.Contains("LeftBearingHousingUpper", fullNames);
        Assert.Contains("LeftBearingHousingFront", fullNames);
        Assert.Contains("LeftBearingHousingRear", fullNames);
        Assert.Contains("RightBearingHousingLower", fullNames);
        Assert.Contains("RightBearingHousingUpper", fullNames);
        Assert.Contains("RightBearingHousingFront", fullNames);
        Assert.Contains("RightBearingHousingRear", fullNames);
        Assert.Contains("LeftFrontBrace", fullNames);
        Assert.Contains("LeftRearBrace", fullNames);
        Assert.Contains("RightFrontBrace", fullNames);
        Assert.Contains("RightRearBrace", fullNames);
        Assert.DoesNotContain(fullNames, name => name.EndsWith("GreaseCup", StringComparison.Ordinal));
        Assert.DoesNotContain(fullNames, name => name.EndsWith("HoldDown", StringComparison.Ordinal));
        Assert.Equal(-16d, fullElements.Min(element => element.GetProperty("from")[1].GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("Brace", StringComparison.Ordinal)),
            brace =>
            {
                Assert.Equal(-15.25d, brace.GetProperty("from")[1].GetDouble());
                Assert.Equal(6d, brace.GetProperty("to")[1].GetDouble());
            });
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("FrontBrace", StringComparison.Ordinal)),
            brace => Assert.Equal(18d, brace.GetProperty("rotationX").GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.EndsWith("RearBrace", StringComparison.Ordinal)),
            brace => Assert.Equal(-18d, brace.GetProperty("rotationX").GetDouble()));
        Assert.All(
            fullElements.Where(element => element.GetProperty("name").GetString()!.Contains("BearingHousing", StringComparison.Ordinal)),
            housing => Assert.All(
                housing.GetProperty("faces").EnumerateObject(),
                face => Assert.Equal("#wood", face.Value.GetProperty("texture").GetString())));

        JsonElement[] compactElements = ReadShapeElements("compact-flywheel-frame-horizontal.json");
        string[] compactNames = compactElements
            .Select(element => element.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Contains("LeftSleeper", compactNames);
        Assert.Contains("RightSleeper", compactNames);
        Assert.Contains("LeftBearingPost", compactNames);
        Assert.Contains("RightBearingPost", compactNames);
        Assert.Contains("LeftBearingHousingLower", compactNames);
        Assert.Contains("LeftBearingHousingUpper", compactNames);
        Assert.Contains("LeftBearingHousingFront", compactNames);
        Assert.Contains("LeftBearingHousingRear", compactNames);
        Assert.Contains("RightBearingHousingLower", compactNames);
        Assert.Contains("RightBearingHousingUpper", compactNames);
        Assert.Contains("RightBearingHousingFront", compactNames);
        Assert.Contains("RightBearingHousingRear", compactNames);
        Assert.DoesNotContain(compactNames, name => name.EndsWith("GreaseCup", StringComparison.Ordinal));
        Assert.DoesNotContain(compactNames, name => name.EndsWith("HoldDown", StringComparison.Ordinal));
        Assert.All(
            compactElements.Where(element => element.GetProperty("name").GetString()!.Contains("BearingHousing", StringComparison.Ordinal)),
            housing => Assert.All(
                housing.GetProperty("faces").EnumerateObject(),
                face => Assert.Equal("#wood", face.Value.GetProperty("texture").GetString())));

        JsonElement[] axle = ReadShapeElements("flywheel-axle.json");
        double axleMinY = axle.Min(element => element.GetProperty("from")[1].GetDouble());
        double axleMaxY = axle.Max(element => element.GetProperty("to")[1].GetDouble());
        double axleMinZ = axle.Min(element => element.GetProperty("from")[2].GetDouble());
        double axleMaxZ = axle.Max(element => element.GetProperty("to")[2].GetDouble());
        Assert.True(axleMinY > 5.75d);
        Assert.True(axleMaxY < 10.25d);
        Assert.True(axleMinZ > 5.75d);
        Assert.True(axleMaxZ < 10.25d);
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
    public void FullStandCanBePlacedFromItsCenterOrBottomCenterCell()
    {
        BlockPos selected = new(10, 20, 30, 2);

        BlockPos groundTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: false,
            EnumAxis.Y);
        Assert.Equal(new BlockPos(10, 21, 30, 2), groundTarget);

        BlockPos centerTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: false,
            EnumAxis.Z);
        Assert.Equal(selected, centerTarget);

        BlockPos compactTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: true,
            EnumAxis.Y);
        Assert.Equal(selected, compactTarget);
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
