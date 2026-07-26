using System.Text.Json;

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
        Assert.Contains("""{ code: "material", states: ["wood", "stone", "iron"] }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""{ code: "hub", states: ["iron"] }""", fullSizeBlocktype, StringComparison.Ordinal);
        Assert.Contains("""{ code: "material", states: ["iron"] }""", compactBlocktype, StringComparison.Ordinal);
        Assert.DoesNotContain("bronze", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("meteoriciron", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("steel", fullSizeBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bronze", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("meteoriciron", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("steel", compactBlocktype, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bronze", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("meteoriciron", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("steel", activeLanguage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blockinfo-shaft", activeLanguage, StringComparison.Ordinal);
        Assert.Equal(4, FlywheelPowerModSystem.ReleasedRendererCodes.Length);
        Assert.Equal(4, FlywheelPowerModSystem.ReleasedRendererCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            FlywheelPowerModSystem.ReleasedRendererCodes.Take(3),
            rendererCode => Assert.Contains(rendererCode, fullSizeBlocktype, StringComparison.Ordinal));
        Assert.Contains(FlywheelPowerModSystem.ReleasedRendererCodes[3], compactBlocktype, StringComparison.Ordinal);
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
        JsonElement[] discBands = elements.EnumerateArray()
            .Where(element => element.GetProperty("name").GetString()!.StartsWith("DiscBand", StringComparison.Ordinal))
            .ToArray();
        JsonElement hub = elements.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "Hub");

        double minY = discBands.Min(element => element.GetProperty("from")[1].GetDouble());
        double maxY = discBands.Max(element => element.GetProperty("to")[1].GetDouble());
        double minZ = discBands.Min(element => element.GetProperty("from")[2].GetDouble());
        double maxZ = discBands.Max(element => element.GetProperty("to")[2].GetDouble());
        double minX = discBands.Min(element => element.GetProperty("from")[0].GetDouble());
        double maxX = discBands.Max(element => element.GetProperty("to")[0].GetDouble());

        Assert.Equal(16d, maxY - minY);
        Assert.Equal(16d, maxZ - minZ);
        Assert.Equal(1d, maxX - minX);
        Assert.Equal(8d, hub.GetProperty("to")[1].GetDouble() - hub.GetProperty("from")[1].GetDouble());
        Assert.Equal(1.44d, hub.GetProperty("to")[0].GetDouble() - hub.GetProperty("from")[0].GetDouble(), 2);
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
}
