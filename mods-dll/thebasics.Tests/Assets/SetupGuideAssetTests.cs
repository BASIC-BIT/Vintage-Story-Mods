using System.Text.Json;
using Vintagestory.API.Common;

namespace thebasics.Tests.Assets;

public class SetupGuideAssetTests
{
    [Fact]
    public void GuideDescriptorHasUniqueEquipmentInMatchingNativeSlots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string path;
        do
        {
            Assert.NotNull(directory);
            path = Path.Combine(directory.FullName, "mods-dll", "thebasics", "assets", "thebasics", "config", "setup-guide.json");
            directory = directory.Parent;
        } while (!File.Exists(path));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var guide = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(guide.GetProperty("name").GetString()));
        var slots = new HashSet<int>();
        foreach (var item in guide.GetProperty("gear").EnumerateArray())
        {
            var code = new AssetLocation(item.GetProperty("code").GetString());
            var parts = code.Path.Split('-', 3);
            Assert.Equal("game", code.Domain);
            Assert.Equal(3, parts.Length);
            Assert.Equal("clothes", parts[0]);
            var category = Enum.Parse<EnumCharacterDressType>(parts[1], ignoreCase: true);
            var slot = item.GetProperty("slot").GetInt32();
            Assert.Equal((int)category, slot);
            Assert.True(slots.Add(slot), $"Duplicate guide gear slot {slot}.");
        }
        Assert.NotEmpty(slots);
        Assert.NotEmpty(guide.GetProperty("skinParts").EnumerateObject());
        Assert.NotEmpty(guide.GetProperty("animations").EnumerateArray());
    }
}
