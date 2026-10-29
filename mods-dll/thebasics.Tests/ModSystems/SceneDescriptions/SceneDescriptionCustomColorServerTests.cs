using FluentAssertions;
using thebasics.Extensions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public partial class SceneDescriptionServerTests
{
    [Fact]
    public void SavedCustomRgbSurvivesDropsCreativeCopiesAndReplacement()
    {
        var (marker, player, world) = CreateMarker();
        marker.Block.Code = new AssetLocation("thebasics:scene-marker-ground-north");
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            Body = "Custom marker", Color = (int)SceneMarkerColor.Blue, CustomColorHex = "#a153cf"
        }));
        marker.Data.CustomColorHex.Should().Be("#A153CF");
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
        marker.Data.AuthorUid.Should().Be("creator");
        var drop = marker.Block.GetDrops(world, marker.Pos, player).Should().ContainSingle().Which;
        marker.InitializeFromItem(drop, player);
        marker.Data.CustomColorHex.Should().Be("#A153CF");
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
        marker.Data.LockItemCode = "ui";
        var copy = marker.Block.OnPickBlock(world, marker.Pos);
        var copied = SceneDescriptionData.ReadFrom(copy.Attributes);
        copied.CustomColorHex.Should().Be("#A153CF");
        copied.IsLocked.Should().BeFalse();
        marker.Data.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void CustomDefaultsApplyOnlyToFreshMarkersAndOrdinaryEditsDoNotReplaceThem()
    {
        var (marker, player, _) = CreateMarker();
        marker.OnReceivedClientPacket(player, SceneDescriptionBlockEntity.SetAppearanceDefaultsPacketId,
            SerializerUtil.Serialize(new SceneDescriptionEditPacket { Color = (int)SceneMarkerColor.Blue, CustomColorHex = "#a153cf" }));
        player.GetModData<SceneDescriptionEditPacket>("thebasics-scene-appearance").CustomColorHex.Should().Be("#A153CF");
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), player);
        marker.Data.CustomColorHex.Should().Be("#A153CF");
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        { Body = "Other color", Color = (int)SceneMarkerColor.Red, CustomColorHex = "#37B8A5" }));
        player.GetModData<SceneDescriptionEditPacket>("thebasics-scene-appearance").CustomColorHex.Should().Be("#A153CF");
        var pickedUp = new ItemStack(new SceneDescriptionBlock());
        new SceneDescriptionData { Title = "Existing", Color = SceneMarkerColor.Green, CustomColorHex = "#37B8A5" }.WriteTo(pickedUp.Attributes);
        marker.InitializeFromItem(pickedUp, player);
        marker.Data.CustomColorHex.Should().Be("#37B8A5");
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), new FakeServerPlayer("other"));
        marker.Data.CustomColorHex.Should().BeEmpty();
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("#A153CF80")]
    [InlineData("#G153CF")]
    [InlineData(" #A153CF")]
    [InlineData("navy")]
    public void InvalidCustomSavesAndDefaultsLeaveMarkerReadStateAndPreferencesUnchanged(string invalid)
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.CustomColorHex = "#37B8A5";
        marker.Data.Stamp();
        var stamp = marker.Data.ReadStamp;
        const string key = "thebasics-scene-appearance";
        player.SetModData(key, new SceneDescriptionEditPacket { Color = (int)SceneMarkerColor.Green, CustomColorHex = "#A153CF" });
        var packet = SerializerUtil.Serialize(new SceneDescriptionEditPacket
        { Body = "Rejected", Color = (int)SceneMarkerColor.Red, CustomColorHex = invalid, LockAfterSave = true });
        marker.OnReceivedClientPacket(player, 1002, packet);
        marker.Data.Body.Should().Be("Original text");
        marker.Data.CustomColorHex.Should().Be("#37B8A5");
        marker.Data.ReadStamp.Should().Be(stamp);
        marker.Data.IsLocked.Should().BeFalse();
        marker.OnReceivedClientPacket(player, SceneDescriptionBlockEntity.SetAppearanceDefaultsPacketId, packet);
        player.GetModData<SceneDescriptionEditPacket>(key).CustomColorHex.Should().Be("#A153CF");
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void CustomColorCannotBypassLocksClaimsOrEditDistance(bool locked, bool claim, bool farAway)
    {
        var (marker, player, _) = CreateMarker(claim);
        marker.Data.CustomColorHex = "#37B8A5";
        marker.Data.LockItemCode = locked ? "ui" : "";
        if (farAway) player.Entity.Pos.SetPos(50, 50, 50);
        const string key = "thebasics-scene-appearance";
        player.SetModData(key, new SceneDescriptionEditPacket { CustomColorHex = "#37B8A5" });
        var packet = SerializerUtil.Serialize(new SceneDescriptionEditPacket { CustomColorHex = "#A153CF" });
        marker.OnReceivedClientPacket(player, 1002, packet);
        marker.OnReceivedClientPacket(player, SceneDescriptionBlockEntity.SetAppearanceDefaultsPacketId, packet);
        marker.Data.CustomColorHex.Should().Be("#37B8A5");
        marker.Data.IsLocked.Should().Be(locked);
        player.GetModData<SceneDescriptionEditPacket>(key).CustomColorHex.Should().Be("#37B8A5");
    }
}
