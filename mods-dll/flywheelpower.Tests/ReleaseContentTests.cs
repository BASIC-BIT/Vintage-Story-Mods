using System.Text.Json;
using Vintagestory.API.Common;
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
    public void ReleaseSurfaceUsesTieredMaterialsWithDistinctRendererGroups()
    {
        string activeBlocktypes = Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes");
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));
        string fullSizeBlocktype = File.ReadAllText(Path.Combine(activeBlocktypes, "flywheel.json"));
        string compactBlocktype = File.ReadAllText(Path.Combine(activeBlocktypes, "compactflywheel.json"));
        using JsonDocument fullDocument = JsonDocument.Parse(fullSizeBlocktype);
        using JsonDocument compactDocument = JsonDocument.Parse(compactBlocktype);
        JsonElement fullRoot = fullDocument.RootElement;
        JsonElement compactRoot = compactDocument.RootElement;

        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "keyedflywheel.json")));
        Assert.False(File.Exists(Path.Combine(activeBlocktypes, "keyedcompactflywheel.json")));
        Assert.DoesNotContain("keyedflywheel", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "keyedflywheel.json")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "disabled-content", "blocktypes", "keyedcompactflywheel.json")));

        Assert.Equal(FlywheelPowerModSystem.FullWheelMaterials, VariantStates(fullRoot, "material"));
        Assert.Equal(FlywheelPowerModSystem.FullHubMaterials, VariantStates(fullRoot, "hub"));
        Assert.Equal(FlywheelPowerModSystem.CompactWheelMaterials, VariantStates(compactRoot, "material"));
        Assert.Equal(FlywheelPowerModSystem.CompactHubMaterials, VariantStates(compactRoot, "hub"));
        Assert.DoesNotContain("stone", FlywheelPowerModSystem.FullWheelMaterials);
        Assert.DoesNotContain("copper", FlywheelPowerModSystem.FullHubMaterials);
        Assert.DoesNotContain("tinbronze", FlywheelPowerModSystem.FullHubMaterials);

        Assert.Equal(22, fullRoot.GetProperty("attributesByType").EnumerateObject().Count());
        Assert.Equal(46, compactRoot.GetProperty("attributesByType").EnumerateObject().Count());
        Assert.Equal(68, FlywheelPowerModSystem.ReleasedRendererCodes.Length);
        Assert.Equal(68, FlywheelPowerModSystem.ReleasedRendererCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            FlywheelPowerModSystem.ReleasedRendererCodes.Take(22),
            rendererCode => Assert.Contains(rendererCode, fullSizeBlocktype, StringComparison.Ordinal));
        Assert.All(
            FlywheelPowerModSystem.ReleasedRendererCodes.Skip(22),
            rendererCode => Assert.Contains(rendererCode, compactBlocktype, StringComparison.Ordinal));

        AssertTexture(fullRoot, "*-copper-iron-*", "wheel", "game:block/metal/ingot/copper");
        AssertTexture(fullRoot, "*-blackbronze-steel-*", "wheel", "game:block/metal/ingot/blackbronze");
        AssertTexture(compactRoot, "*-wood-copper-*", "metal", "game:block/metal/ingot/copper");
        AssertTexture(compactRoot, "*-wood-copper-*", "bearing", "game:block/metal/ingot/copper");
        AssertTexture(compactRoot, "*-tinbronze-bismuthbronze-*", "wheel", "game:block/metal/ingot/tinbronze");
        AssertTexture(compactRoot, "*-tinbronze-bismuthbronze-*", "metal", "game:block/metal/ingot/bismuthbronze");
        AssertTexture(compactRoot, "*-tinbronze-bismuthbronze-*", "bearing", "game:block/metal/ingot/bismuthbronze");
        Assert.DoesNotContain(
            fullRoot.GetProperty("attributesByType").EnumerateObject(),
            property => property.Name.Contains("-copper-*", StringComparison.Ordinal));

        Assert.Equal(
            "flywheelpower:block/flywheel-axle",
            compactRoot.GetProperty("entityBehaviors")[0]
                .GetProperty("properties")
                .GetProperty("axleShape")
                .GetProperty("base")
                .GetString());
        Assert.DoesNotContain("slip-transmission-shaft", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("block-flywheel-copper-iron", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("block-flywheel-blackbronze-steel", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("block-compactflywheel-wood-copper", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("block-compactflywheel-tinbronze-bismuthbronze", activeLanguage, StringComparison.Ordinal);
        Assert.DoesNotContain("block-flywheel-stone-iron", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("block-compactflywheel-stone", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blockinfo-shaft", activeLanguage, StringComparison.Ordinal);

        using JsonDocument assemblyDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                ProjectRoot,
                "assets",
                "flywheelpower",
                "recipes",
                "grid",
                "flywheel-assembly.json")));
        string[] recipeOutputs = assemblyDocument.RootElement
            .EnumerateArray()
            .Select(recipe => recipe.GetProperty("output").GetProperty("code").GetString()!)
            .ToArray();
        string[] expectedOutputs = ReleasedBlockCodes(compact: false)
            .Concat(ReleasedBlockCodes(compact: true))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedOutputs, recipeOutputs.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(expectedOutputs.Length, recipeOutputs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void HubTierMustMeetOrExceedWheelTier()
    {
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("wood", "copper"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("stone", "copper"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("copper", "tinbronze"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("tinbronze", "bismuthbronze"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("blackbronze", "tinbronze"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("iron", "meteoriciron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("meteoriciron", "iron"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("iron", "steel"));
        Assert.True(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "steel"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("tinbronze", "copper"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("iron", "blackbronze"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "iron"));
        Assert.False(FlywheelPowerModSystem.IsReleasedMaterialCombination("steel", "meteoriciron"));
    }

    [Fact]
    public void ReleasedMetalDensitiesMatchVintageStoryMaterialData()
    {
        Assert.Equal(8960f, FlywheelPhysicalProperties.GetMaterialDensity("copper"));
        Assert.Equal(7600f, FlywheelPhysicalProperties.GetMaterialDensity("tinbronze"));
        Assert.Equal(7900f, FlywheelPhysicalProperties.GetMaterialDensity("bismuthbronze"));
        Assert.Equal(9000f, FlywheelPhysicalProperties.GetMaterialDensity("blackbronze"));
        Assert.Equal(7870f, FlywheelPhysicalProperties.GetMaterialDensity("iron"));
        Assert.Equal(7800f, FlywheelPhysicalProperties.GetMaterialDensity("meteoriciron"));
        Assert.Equal(7820f, FlywheelPhysicalProperties.GetMaterialDensity("steel"));

        FlywheelPhysicalProfile copper = FlywheelPhysicalProperties.ForVariant(
            compact: true,
            "copper",
            "copper");
        FlywheelPhysicalProfile blackBronze = FlywheelPhysicalProperties.ForVariant(
            compact: true,
            "blackbronze",
            "blackbronze");
        FlywheelPhysicalProfile steel = FlywheelPhysicalProperties.ForVariant(
            compact: true,
            "steel",
            "steel");

        Assert.True(copper.RotatingMassKg > steel.RotatingMassKg);
        Assert.True(blackBronze.RotatingMassKg > copper.RotatingMassKg);
        Assert.True(blackBronze.EffectiveInertia > copper.EffectiveInertia);
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
        Assert.Contains("\"base\": \"flywheelpower:block/flywheel-frame-horizontal\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"base\": \"flywheelpower:block/flywheel-frame-vertical\"", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"base\": \"flywheelpower:block/compact-flywheel-frame-horizontal\"", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"base\": \"flywheelpower:block/compact-flywheel-frame-vertical\"", compactBlocktype, StringComparison.Ordinal);
        Assert.Contains("SetShapeRotation(0f, 0f, 90f, 0f, 0f, 0f);", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("SetShapeRotation(0f, 90f, 0f, 0f, 90f, 0f);", behaviorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalComparisonStaysOnHeldItemsWhilePlacedTelemetryIsDebugOnly()
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
        Assert.Contains("if (!FlywheelPowerModSystem.Config.ShowDebugBlockInfo)", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("Estimated rotating mass: {0} kg; effective inertia: {1}", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("Coupling effort: {0}%{1}", activeLanguage, StringComparison.Ordinal);
        Assert.Contains(" (at limit)", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("Stored energy: {0}% of rated safe capacity", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("Shaft speed difference: {0}% of rated speed", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("Approaching rated limit: {0}% of rated speed", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("Overspeed: {0}% of rated speed", activeLanguage, StringComparison.Ordinal);
        Assert.Contains("SpawnOverspeedSmoke(tick)", behaviorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("friction-coupled", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SpawnSlipSparks(tick)", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("SpawnOverspeedSparks(tick)", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("Api.World.SpawnParticles(particles)", behaviorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailedPlacedTelemetryDefaultsOff()
    {
        FlywheelPowerConfig config = new();

        Assert.False(config.ShowDebugBlockInfo);
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
        JsonElement chalkBearingFront = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLineBearingFront");
        JsonElement chalkPlateFront = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "ChalkLinePlateFront");
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
        Assert.Equal(
            chalkBearingFront.GetProperty("to")[2].GetDouble(),
            chalkPlateFront.GetProperty("from")[2].GetDouble());
        Assert.Equal(
            chalkPlateFront.GetProperty("to")[2].GetDouble(),
            chalkFront.GetProperty("from")[2].GetDouble());
        Assert.True(
            chalkBearingFront.GetProperty("from")[0].GetDouble()
            > chalkPlateFront.GetProperty("from")[0].GetDouble());
        Assert.True(
            chalkPlateFront.GetProperty("from")[0].GetDouble()
            > chalkFront.GetProperty("from")[0].GetDouble());
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
    public void CompactStandLeavesClearanceAroundTheInstalledWheel()
    {
        JsonElement[] horizontal = ReadShapeElements("compact-flywheel-frame-horizontal.json");
        JsonElement[] vertical = ReadShapeElements("compact-flywheel-frame-vertical.json");
        double center = 8d;
        double radius = FlywheelModelDimensions.CompactWheelOuterRadius * 16d;
        double halfThickness = FlywheelModelDimensions.CompactWheelHalfThickness * 16d;
        double minimumVisualGap = 0.04d * 16d;

        Assert.All(
            horizontal,
            element =>
            {
                Assert.False(
                    IntersectsAxisAlignedCylinder(element, axis: 0, center, radius, halfThickness),
                    $"{element.GetProperty("name").GetString()} intersects the horizontal compact wheel.");
                Assert.True(
                    HasVisualClearanceFromAxisAlignedCylinder(
                        element,
                        axis: 0,
                        center,
                        radius,
                        halfThickness,
                        minimumVisualGap),
                    $"{element.GetProperty("name").GetString()} sits too close to the horizontal compact wheel.");
            });
        Assert.All(
            vertical,
            element => Assert.False(
                IntersectsAxisAlignedCylinder(element, axis: 1, center, radius, halfThickness),
                $"{element.GetProperty("name").GetString()} intersects the vertical compact wheel."));

        double bearingMin = center - FlywheelModelDimensions.CompactBearingHalfThickness * 16d;
        double bearingMax = center + FlywheelModelDimensions.CompactBearingHalfThickness * 16d;
        JsonElement leftHousing = horizontal.Single(
            element => element.GetProperty("name").GetString() == "LeftBearingHousingLower");
        JsonElement rightHousing = horizontal.Single(
            element => element.GetProperty("name").GetString() == "RightBearingHousingLower");
        Assert.True(leftHousing.GetProperty("to")[0].GetDouble() > bearingMin);
        Assert.True(rightHousing.GetProperty("from")[0].GetDouble() < bearingMax);
    }

    [Fact]
    public void HorizontalStandBasesUseSubstantialGroundedTimbers()
    {
        JsonElement[] full = ReadShapeElements("flywheel-frame-horizontal.json");
        JsonElement[] compact = ReadShapeElements("compact-flywheel-frame-horizontal.json");

        AssertElementHeight(full, "LeftGroundSleeper", expectedBottom: -16d, expectedHeight: 4d);
        AssertElementHeight(full, "RightGroundSleeper", expectedBottom: -16d, expectedHeight: 4d);
        AssertElementHeight(full, "FrontCrossTie", expectedBottom: -14d, expectedHeight: 3.75d);
        AssertElementHeight(full, "RearCrossTie", expectedBottom: -14d, expectedHeight: 3.75d);

        AssertElementHeight(compact, "LeftSleeper", expectedBottom: 0d, expectedHeight: 4d);
        AssertElementHeight(compact, "RightSleeper", expectedBottom: 0d, expectedHeight: 4d);
        AssertElementHeight(compact, "FrontCrossTie", expectedBottom: 0.5d, expectedHeight: 2.5d);
        AssertElementHeight(compact, "RearCrossTie", expectedBottom: 0.5d, expectedHeight: 2.5d);
    }

    [Fact]
    public void FullSizePlacementExplainsItsReservedFootprint()
    {
        string multiblockSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "FlywheelMultiblock.cs"));
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "game", "lang", "en.json"));

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
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "game", "lang", "en.json"));
        Assert.Contains("""failureCode = "flywheelrequiresfoundation";""", standSource, StringComparison.Ordinal);
        Assert.Contains("placefailure-flywheelrequiresfoundation", activeLanguage, StringComparison.Ordinal);
    }

    private static void AssertElementHeight(
        IEnumerable<JsonElement> elements,
        string name,
        double expectedBottom,
        double expectedHeight)
    {
        JsonElement element = Assert.Single(
            elements,
            candidate => candidate.GetProperty("name").GetString() == name);
        double bottom = element.GetProperty("from")[1].GetDouble();
        double top = element.GetProperty("to")[1].GetDouble();

        Assert.Equal(expectedBottom, bottom);
        Assert.Equal(expectedHeight, top - bottom);
    }

    [Fact]
    public void FullStandCanBePlacedFromItsCenterOrBottomCenterCell()
    {
        BlockPos selected = new(10, 20, 30, 2);

        BlockPos groundTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: false,
            EnumAxis.Y,
            EnumAxis.X);
        Assert.Equal(new BlockPos(10, 21, 30, 2), groundTarget);

        BlockPos verticalGroundTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: false,
            EnumAxis.Y,
            EnumAxis.Y);
        Assert.Equal(selected, verticalGroundTarget);

        BlockPos centerTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: false,
            EnumAxis.Z,
            EnumAxis.Z);
        Assert.Equal(selected, centerTarget);

        BlockPos compactTarget = BlockFlywheelStand.ResolvePlacementPosition(
            selected,
            compact: true,
            EnumAxis.Y,
            EnumAxis.Y);
        Assert.Equal(selected, compactTarget);
    }

    [Fact]
    public void SneakingOnAHorizontalSurfaceSelectsVerticalPlacement()
    {
        Assert.Equal(
            EnumAxis.Y,
            BlockFlywheelStand.ResolvePlacementAxis(EnumAxis.Y, playerYaw: 0f, verticalPlacement: true));
        Assert.Equal(
            EnumAxis.Z,
            BlockFlywheelStand.ResolvePlacementAxis(EnumAxis.Z, playerYaw: 0f, verticalPlacement: true));
        Assert.NotEqual(
            EnumAxis.Y,
            BlockFlywheelStand.ResolvePlacementAxis(EnumAxis.Y, playerYaw: 0f, verticalPlacement: false));
    }

    [Theory]
    [InlineData("ns", 0, "ns")]
    [InlineData("ns", 90, "we")]
    [InlineData("we", 270, "ns")]
    [InlineData("we", -90, "ns")]
    [InlineData("ns", 180, "ns")]
    [InlineData("ud", 90, "ud")]
    public void SchematicRotationKeepsPrincipalVariantsAligned(string rotation, int angle, string expected)
    {
        Assert.Equal(expected, FlywheelMultiblock.RotateRotation(rotation, angle));
    }

    [Fact]
    public void EveryPrincipalBlockOverridesSchematicRotation()
    {
        foreach (Type blockType in new[]
                 {
                     typeof(BlockFlywheelStand),
                     typeof(BlockFlywheel),
                     typeof(BlockCompactFlywheel)
                 })
        {
            Assert.Equal(
                blockType,
                blockType.GetMethod(nameof(Block.GetRotatedBlockCode), new[] { typeof(int) })?.DeclaringType);
        }
    }

    [Fact]
    public void MultiblockPartsDelegateOnlyToFlywheelPrincipals()
    {
        BlockFlywheelStand compactStand = new()
        {
            Variant = new Vintagestory.API.Util.RelaxedReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { ["size"] = "compact" })
        };

        Assert.True(BlockFlywheelPart.IsValidPrincipalBlock(new BlockFlywheelStand()));
        Assert.True(BlockFlywheelPart.IsValidPrincipalBlock(new BlockFlywheel()));
        Assert.False(BlockFlywheelPart.IsValidPrincipalBlock(compactStand));
        Assert.False(BlockFlywheelPart.IsValidPrincipalBlock(new BlockCompactFlywheel()));
        Assert.False(BlockFlywheelPart.IsValidPrincipalBlock(new Block()));
        Assert.False(BlockFlywheelPart.IsValidPrincipalBlock(null));
    }

    [Fact]
    public void MultiblockPartDelegationRechecksClaimAccessAtThePrincipal()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheelPart.cs"));

        Assert.Contains(
            "world.Claims.TryAccess(byPlayer, principal, EnumBlockAccessFlags.BuildOrBreak)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "world.Claims.TryAccess(byPlayer, principal, EnumBlockAccessFlags.Use)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("FlywheelMultiblock.IsPartPosition", source, StringComparison.Ordinal);
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
        Assert.Contains(
            "world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)",
            standSource,
            StringComparison.Ordinal);
        Assert.Contains("slot.TakeOut(1)", standSource, StringComparison.Ordinal);
        Assert.Contains("\"code\": \"flywheelstand-full-ud\"", fullBlocktype, StringComparison.Ordinal);
        Assert.Contains("\"code\": \"flywheelstand-compact-ud\"", compactBlocktype, StringComparison.Ordinal);
    }

    [Fact]
    public void EnginePlacementFailuresAreLocalizedInTheGameDomain()
    {
        string engineLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "game", "lang", "en.json"));
        string modLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));

        Assert.Contains("placefailure-flywheelrequiresclearance", engineLanguage, StringComparison.Ordinal);
        Assert.Contains("placefailure-flywheelrequiresfoundation", engineLanguage, StringComparison.Ordinal);
        Assert.Contains("placefailure-flywheelrequiresstand", engineLanguage, StringComparison.Ordinal);
        Assert.DoesNotContain("placefailure-flywheel", modLanguage, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageUsesSortedEntriesAndFixedZipTimestamps()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "package.ps1"));
        string project = File.ReadAllText(Path.Combine(ProjectRoot, "flywheelpower.csproj"));

        Assert.Contains("Sort-Object -Property EntryName -CaseSensitive", source, StringComparison.Ordinal);
        Assert.Contains("$entry.LastWriteTime = $fixedEntryTimestamp", source, StringComparison.Ordinal);
        Assert.Contains("$sourceStream.CopyTo($entryStream)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateEntryFromFile", source, StringComparison.Ordinal);
        Assert.Contains("<Deterministic>true</Deterministic>", project, StringComparison.Ordinal);
        Assert.Contains("<PathMap>$(MSBuildProjectDirectory)=/_/mods-dll/flywheelpower</PathMap>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void SurvivalRecipesExposeTheStagedConstructionChain()
    {
        string recipeDirectory = Path.Combine(ProjectRoot, "assets", "flywheelpower", "recipes", "grid");
        string recipes = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    recipeDirectory,
                    "*.json")
                .Select(File.ReadAllText));

        Assert.Contains("game:fat-rendered", recipes, StringComparison.Ordinal);
        Assert.Contains("bearingfittings-iron", recipes, StringComparison.Ordinal);
        Assert.Contains("bearingfittings-meteoriciron", recipes, StringComparison.Ordinal);
        Assert.Contains("bearingfittings-steel", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelbearing-full-", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelweb-full", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelrim-full-", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelstand-full-ud", recipes, StringComparison.Ordinal);
        Assert.Contains("flywheelstand-compact-ud", recipes, StringComparison.Ordinal);
        Assert.DoesNotContain("flywheelrim-full-stone", recipes, StringComparison.Ordinal);
        Assert.DoesNotContain("flywheelweb-compact", recipes, StringComparison.Ordinal);
        Assert.Contains("game:supportbeam-*", recipes, StringComparison.Ordinal);

        using JsonDocument components = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(recipeDirectory, "flywheel-components.json")));
        JsonElement fullBearing = components.RootElement
            .EnumerateArray()
            .Single(recipe =>
                recipe.GetProperty("output").GetProperty("code").GetString() == "flywheelbearing-full-iron");
        JsonElement compactBearing = components.RootElement
            .EnumerateArray()
            .Single(recipe =>
                recipe.GetProperty("output").GetProperty("code").GetString() == "flywheelbearing-compact-copper");
        Assert.Equal(16, FittingsConsumed(fullBearing));
        Assert.Equal(4, FittingsConsumed(compactBearing));
        Assert.Equal("game:woodenaxle-ud", fullBearing.GetProperty("ingredients").GetProperty("A").GetProperty("code").GetString());
        Assert.Equal("game:fat-rendered", fullBearing.GetProperty("ingredients").GetProperty("L").GetProperty("code").GetString());

        using JsonDocument smithing = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                ProjectRoot,
                "assets",
                "flywheelpower",
                "recipes",
                "smithing",
                "bearingfittings.json")));
        JsonElement fittingsRecipe = Assert.Single(smithing.RootElement.EnumerateArray());
        Assert.Equal(4, fittingsRecipe.GetProperty("output").GetProperty("stacksize").GetInt32());
        string[] fittingPattern = fittingsRecipe.GetProperty("pattern")[0]
            .EnumerateArray()
            .Select(row => row.GetString()!)
            .ToArray();
        Assert.Equal(
            ["_#####_", "##___##", "##___##", "##___##", "##___##", "###_###"],
            fittingPattern);
        Assert.Equal(27, fittingPattern.Sum(row => row.Count(voxel => voxel == '#')));
        Assert.Equal(
            FlywheelPowerModSystem.CompactHubMaterials,
            fittingsRecipe.GetProperty("ingredient")
                .GetProperty("allowedVariants")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());

        using JsonDocument fittingShape = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            ProjectRoot,
            "assets",
            "flywheelpower",
            "shapes",
            "item",
            "bearing-fitting.json")));
        Assert.Equal(
            ["LeftFoot", "RightFoot", "LeftCheek", "RightCheek", "Crown"],
            fittingShape.RootElement.GetProperty("elements")
                .EnumerateArray()
                .Select(element => element.GetProperty("name").GetString()!)
                .ToArray());

        JsonElement stoneBlank = components.RootElement
            .EnumerateArray()
            .Single(recipe =>
                recipe.GetProperty("output").GetProperty("code").GetString() == "flywheelrim-compact-stone");
        Assert.Equal("R_R,RCR", stoneBlank.GetProperty("ingredientPattern").GetString());
        Assert.Equal(
            4,
            stoneBlank.GetProperty("ingredientPattern").GetString()!.Count(character => character == 'R'));
        Assert.Equal(
            1,
            stoneBlank.GetProperty("ingredientPattern").GetString()!.Count(character => character == 'C'));

        using JsonDocument assemblies = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(recipeDirectory, "flywheel-assembly.json")));
        JsonElement[] compactAssemblies = assemblies.RootElement
            .EnumerateArray()
            .Where(recipe => recipe.GetProperty("output").GetProperty("code").GetString()!
                .StartsWith("flywheelpower:compactflywheel-", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(compactAssemblies);
        Assert.All(compactAssemblies, recipe =>
        {
            Assert.Equal("R,B", recipe.GetProperty("ingredientPattern").GetString());
            Assert.False(recipe.GetProperty("ingredients").TryGetProperty("W", out _));
        });
        Assert.All(
            assemblies.RootElement.EnumerateArray().Where(recipe =>
                recipe.GetProperty("output").GetProperty("code").GetString()!
                    .StartsWith("flywheelpower:flywheel-", StringComparison.Ordinal)),
            recipe => Assert.Equal(
                "flywheelpower:flywheelweb-full",
                recipe.GetProperty("ingredients").GetProperty("W").GetProperty("code").GetString()));

        string language = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));
        Assert.DoesNotContain("Tyre", language, StringComparison.Ordinal);
        Assert.DoesNotContain("Wheel Blank", language, StringComparison.Ordinal);
        Assert.DoesNotContain("Wooden Rim", language, StringComparison.Ordinal);
        Assert.Contains("Full-Size Copper Wheel", language, StringComparison.Ordinal);

        static int FittingsConsumed(JsonElement recipe)
        {
            int fittingSlots = recipe.GetProperty("ingredientPattern").GetString()!.Count(character => character == 'F');
            int quantityPerSlot = recipe.GetProperty("ingredients").GetProperty("F").GetProperty("quantity").GetInt32();
            return fittingSlots * quantityPerSlot;
        }
        Assert.Contains("Compact Copper Wheel", language, StringComparison.Ordinal);
    }

    [Fact]
    public void IntermediatePartsUseDedicatedInventoryGroundAndHeldModels()
    {
        string itemtypeDirectory = Path.Combine(ProjectRoot, "assets", "flywheelpower", "itemtypes");
        string[] itemtypeFiles =
        [
            "bearingfittings.json",
            "flywheelbearing.json",
            "flywheelrim.json",
            "flywheelweb.json",
        ];

        foreach (string fileName in itemtypeFiles)
        {
            string text = File.ReadAllText(Path.Combine(itemtypeDirectory, fileName));
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            Assert.True(root.TryGetProperty("guiTransform", out _), $"{fileName} lacks a toolbar transform");
            Assert.True(root.TryGetProperty("groundTransform", out _), $"{fileName} lacks a ground transform");
            Assert.True(root.TryGetProperty("fpHandTransform", out _), $"{fileName} lacks a first-person transform");
            Assert.True(root.TryGetProperty("tpHandTransform", out _), $"{fileName} lacks a third-person transform");
            Assert.DoesNotContain("game:item/plate", text, StringComparison.Ordinal);
            Assert.DoesNotContain("game:item/resource/metalnailsandstrips", text, StringComparison.Ordinal);
        }

        string allItemtypes = string.Join(
            '\n',
            itemtypeFiles.Select(fileName => File.ReadAllText(Path.Combine(itemtypeDirectory, fileName))));
        string[] shapeCodes =
        [
            "flywheelpower:item/bearing-fitting",
            "flywheelpower:item/flywheel-bearing-full",
            "flywheelpower:item/flywheel-bearing-compact",
            "flywheelpower:item/flywheel-web-full",
            "flywheelpower:item/flywheel-rim-full",
            "flywheelpower:item/flywheel-rim-compact",
        ];
        Assert.All(shapeCodes, code => Assert.Contains(code, allItemtypes, StringComparison.Ordinal));

        string shapeDirectory = Path.Combine(ProjectRoot, "assets", "flywheelpower", "shapes", "item");
        string[] shapeFiles = Directory.EnumerateFiles(shapeDirectory, "*.json").ToArray();
        Assert.Equal(6, shapeFiles.Length);
        Assert.All(shapeFiles, path =>
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.NotEmpty(document.RootElement.GetProperty("elements").EnumerateArray());
        });

        string stand = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "assets",
            "flywheelpower",
            "blocktypes",
            "flywheelstand.json"));
        Assert.Contains("guiTransformByType", stand, StringComparison.Ordinal);
        Assert.Contains("groundTransformByType", stand, StringComparison.Ordinal);
        Assert.Contains("fpHandTransform", stand, StringComparison.Ordinal);
        Assert.Contains("tpHandTransformByType", stand, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleasedCollectiblesHaveCompleteDeterministicRepresentationEvidence()
    {
        string blocktypeDirectory = Path.Combine(ProjectRoot, "assets", "flywheelpower", "blocktypes");
        foreach (string fileName in new[] { "flywheel.json", "compactflywheel.json" })
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(blocktypeDirectory, fileName)));
            JsonElement root = document.RootElement;
            Assert.True(root.TryGetProperty("guiTransform", out _), $"{fileName} lacks a toolbar transform");
            Assert.True(root.TryGetProperty("groundTransform", out _), $"{fileName} lacks a ground transform");
            Assert.True(root.TryGetProperty("fpHandTransform", out _), $"{fileName} lacks a first-person transform");
            Assert.True(root.TryGetProperty("tpHandTransform", out _), $"{fileName} lacks a third-person transform");
            Assert.Equal("holdbothhandslarge", root.GetProperty("heldTpIdleAnimation").GetString());
        }

        string manifestDirectory = Path.Combine(ProjectRoot, "model-render");
        string[] collectibleKeys =
        [
            "bearing-fitting", "bearing-compact", "bearing-full",
            "rim-compact", "rim-full", "web-full",
            "stand-compact", "stand-full", "assembly-compact", "assembly-full",
        ];
        string[] contexts = ["gui", "ground", "fp", "seraph"];
        Assert.All(collectibleKeys, key =>
            Assert.All(contexts, context =>
                Assert.True(
                    File.Exists(Path.Combine(manifestDirectory, $"representation-{key}-{context}.json")),
                    $"Missing {context} evidence manifest for {key}")));
    }

    [Fact]
    public void RuntimeRegistrationBandUsesPhysicalScaleUvs()
    {
        string rendererSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "src",
            "FlywheelMechBlockRenderer.cs"));

        Assert.Contains("float u1 = 2f * halfWidth / TextureMeters;", rendererSource, StringComparison.Ordinal);
        Assert.Contains("float v1 = Math.Abs(maxX - minX) / TextureMeters;", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CylinderVertex(maxX, radius, halfAngle, 1f, 1f)",
            rendererSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FullSizeInstallationRevalidatesItsReservedFootprint()
    {
        string standSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlockFlywheelStand.cs"));
        string activeLanguage = File.ReadAllText(Path.Combine(ProjectRoot, "assets", "flywheelpower", "lang", "en.json"));

        Assert.Contains("FlywheelMultiblock.HasIntactReservations", standSource, StringComparison.Ordinal);
        Assert.Contains("flywheelpower:error-damagedstand", standSource, StringComparison.Ordinal);
        Assert.Contains("flywheelpower:error-damagedstand", activeLanguage, StringComparison.Ordinal);
    }

    [Fact]
    public void FullSizeIntermediatePartsUseLargeTwoHandPoseWithoutChangingCompactPose()
    {
        string itemtypeDirectory = Path.Combine(ProjectRoot, "assets", "flywheelpower", "itemtypes");
        (string FileName, string FullPattern, double X, double Y, double Z)[] largeParts =
        [
            ("flywheelbearing.json", "flywheelbearing-full-*", -0.625d, -0.625d, -0.575d),
            ("flywheelrim.json", "flywheelrim-full-*", -0.307d, -0.694d, -0.665d),
            ("flywheelweb.json", "flywheelweb-full", -0.625d, -0.625d, -0.575d),
        ];

        foreach ((string fileName, string fullPattern, double x, double y, double z) in largeParts)
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(itemtypeDirectory, fileName)));
            JsonElement root = document.RootElement;
            JsonElement compactTransform = root.GetProperty("tpHandTransform");
            JsonElement fullTransform = root.GetProperty("tpHandTransformByType").GetProperty(fullPattern);
            JsonElement fullAnimations = root.GetProperty("heldTpIdleAnimationByType");

            Assert.Equal("holdbothhandslarge", fullAnimations.GetProperty(fullPattern).GetString());
            Assert.Equal(0.42d, compactTransform.GetProperty("scale").GetDouble(), 2);
            Assert.Equal(0.84d, fullTransform.GetProperty("scale").GetDouble(), 2);
            Assert.Equal(x, fullTransform.GetProperty("translation").GetProperty("x").GetDouble(), 3);
            Assert.Equal(y, fullTransform.GetProperty("translation").GetProperty("y").GetDouble(), 3);
            Assert.Equal(z, fullTransform.GetProperty("translation").GetProperty("z").GetDouble(), 3);
        }

        using JsonDocument fittings = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(itemtypeDirectory, "bearingfittings.json")));
        Assert.False(fittings.RootElement.TryGetProperty("heldTpIdleAnimationByType", out _));
        Assert.False(fittings.RootElement.TryGetProperty("tpHandTransformByType", out _));
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

    private static string[] VariantStates(JsonElement root, string code)
    {
        return root.GetProperty("variantgroups")
            .EnumerateArray()
            .Single(group => group.GetProperty("code").GetString() == code)
            .GetProperty("states")
            .EnumerateArray()
            .Select(state => state.GetString()!)
            .ToArray();
    }

    private static void AssertTexture(
        JsonElement root,
        string variantPattern,
        string textureCode,
        string expectedBase)
    {
        Assert.Equal(
            expectedBase,
            root.GetProperty("texturesByType")
                .GetProperty(variantPattern)
                .GetProperty(textureCode)
                .GetProperty("base")
                .GetString());
    }

    private static IEnumerable<string> ReleasedBlockCodes(bool compact)
    {
        string block = compact ? "compactflywheel" : "flywheel";
        string[] wheels = compact
            ? FlywheelPowerModSystem.CompactWheelMaterials
            : FlywheelPowerModSystem.FullWheelMaterials;
        string[] hubs = compact
            ? FlywheelPowerModSystem.CompactHubMaterials
            : FlywheelPowerModSystem.FullHubMaterials;
        return wheels.SelectMany(wheel => hubs
            .Where(hub => FlywheelPowerModSystem.IsReleasedMaterialCombination(wheel, hub))
            .Select(hub => $"flywheelpower:{block}-{wheel}-{hub}-ud"));
    }

    private static bool IntersectsAxisAlignedCylinder(
        JsonElement element,
        int axis,
        double center,
        double radius,
        double halfThickness)
    {
        double[] from = element.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        double[] to = element.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        double cylinderMin = center - halfThickness;
        double cylinderMax = center + halfThickness;
        if (to[axis] <= cylinderMin || from[axis] >= cylinderMax)
        {
            return false;
        }

        int radialAxisA = axis == 0 ? 1 : 0;
        int radialAxisB = 2;
        double deltaA = DistanceToInterval(center, from[radialAxisA], to[radialAxisA]);
        double deltaB = DistanceToInterval(center, from[radialAxisB], to[radialAxisB]);
        return deltaA * deltaA + deltaB * deltaB < radius * radius;
    }

    private static bool HasVisualClearanceFromAxisAlignedCylinder(
        JsonElement element,
        int axis,
        double center,
        double radius,
        double halfThickness,
        double minimumGap)
    {
        double[] from = element.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        double[] to = element.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        int radialAxisA = axis == 0 ? 1 : 0;
        int radialAxisB = 2;
        double deltaA = DistanceToInterval(center, from[radialAxisA], to[radialAxisA]);
        double deltaB = DistanceToInterval(center, from[radialAxisB], to[radialAxisB]);
        if (deltaA * deltaA + deltaB * deltaB >= radius * radius)
        {
            return true;
        }

        double cylinderMin = center - halfThickness;
        double cylinderMax = center + halfThickness;
        double axialGap = Math.Max(cylinderMin - to[axis], from[axis] - cylinderMax);
        return axialGap >= minimumGap;
    }

    private static double DistanceToInterval(double value, double min, double max)
    {
        if (value < min)
        {
            return min - value;
        }

        return value > max ? value - max : 0d;
    }
}
