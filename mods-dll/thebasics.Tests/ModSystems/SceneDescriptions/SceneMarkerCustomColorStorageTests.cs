using FluentAssertions;
using ProtoBuf;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerCustomColorStorageTests
{
    [Fact]
    public void AppliedEditsClonesAndDefaultsKeepCustomRgbWithoutCopyingContentIntoDefaults()
    {
        var edited = new SceneDescriptionData
        {
            Title = "Watchtower",
            Body = "A distant light.",
            Color = SceneMarkerColor.Blue,
            CustomColorHex = "#a153cf",
            AuthorUid = "editor",
            LockItemCode = "ui",
            ReadStamp = 50,
            SymbolIconName = "wpStar1"
        }.Normalize();
        var data = new SceneDescriptionData { AuthorUid = "creator", ReadStamp = 10 };
        data.ApplyText(edited);
        data.CustomColorHex.Should().Be("#A153CF");
        data.Color.Should().Be(SceneMarkerColor.Blue);
        data.AuthorUid.Should().Be("creator");
        data.ReadStamp.Should().Be(10);
        data.IsLocked.Should().BeFalse();
        data.Clone().Should().BeEquivalentTo(data);
        var defaults = data.AppearanceDefaults();
        defaults.CustomColorHex.Should().Be("#A153CF");
        defaults.Color.Should().Be(SceneMarkerColor.Blue);
        defaults.SymbolIconName.Should().Be("wpStar1");
        defaults.Title.Should().BeEmpty();
        defaults.Body.Should().BeEmpty();
        defaults.AuthorUid.Should().BeEmpty();
        defaults.ReadStamp.Should().Be(0);
    }

    [Theory]
    [InlineData("#A153CF", true, "#A153CF")]
    [InlineData("#a153cf", true, "#A153CF")]
    [InlineData("#000000", true, "#000000")]
    [InlineData("", true, "")]
    [InlineData(null, true, "")]
    [InlineData("#abc", false, "")]
    [InlineData("#A153CF80", false, "")]
    [InlineData("#A153CG", false, "")]
    [InlineData(" #A153CF", false, "")]
    [InlineData("#A153CF ", false, "")]
    [InlineData("A153CF", false, "")]
    [InlineData("navy", false, "")]
    public void PersistedMalformedColorsFallBackWithoutChangingTheirPreset(string? value, bool valid, string expected)
    {
        SceneDescriptionData.IsValidCustomColor(value!).Should().Be(valid);
        var tree = new TreeAttribute();
        tree.SetInt("sceneColor", (int)SceneMarkerColor.Green);
        if (value != null) tree.SetString("sceneCustomColor", value);
        var data = SceneDescriptionData.ReadFrom(tree);
        data.CustomColorHex.Should().Be(expected);
        data.Color.Should().Be(SceneMarkerColor.Green);
    }

    [Theory]
    [InlineData(SceneMarkerColor.Gold)]
    [InlineData(SceneMarkerColor.Parchment)]
    [InlineData(SceneMarkerColor.Blue)]
    [InlineData(SceneMarkerColor.Green)]
    [InlineData(SceneMarkerColor.Red)]
    public void CustomPacketKeepsLegacyFieldFifteenAndLegacyPacketsNeedNoCustomColor(SceneMarkerColor color)
    {
        var packet = new SceneDescriptionEditPacket { Color = (int)color, CustomColorHex = "#A153CF" };
        var bytes = SerializerUtil.Serialize(packet);
        SerializerUtil.Deserialize<SceneDescriptionEditPacket>(bytes).CustomColorHex.Should().Be("#A153CF");
        SerializerUtil.Deserialize<LegacyColorPacket>(bytes).Color.Should().Be((int)color);
        var oldBytes = SerializerUtil.Serialize(new LegacyColorPacket { Color = (int)color });
        var oldPacket = SerializerUtil.Deserialize<SceneDescriptionEditPacket>(oldBytes);
        oldPacket.CustomColorHex.Should().BeEmpty();
        oldPacket.Color.Should().Be((int)color);
        Enum.GetValues<SceneMarkerColor>().Should().HaveCount(5);
    }

    [ProtoContract]
    private sealed class LegacyColorPacket
    {
        [ProtoMember(15)] public int Color { get; set; }
    }
}
