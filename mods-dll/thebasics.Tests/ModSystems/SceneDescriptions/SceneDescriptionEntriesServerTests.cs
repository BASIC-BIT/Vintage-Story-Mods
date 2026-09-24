using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection(AnalyticsServiceTestCollection.Name)]
public class SceneDescriptionEntriesServerTests
{
    [Fact]
    public void ClaimMemberCanAddAnIndependentServerAuthoredEntryWithoutChangingTheFirst()
    {
        var (marker, _, world) = CreateMarker();
        var first = marker.Entries.Primary;
        first.Data.LockItemCode = "ui";
        var writer = Player("second", "Second Writer");

        var added = marker.TryAddEntry(writer);

        added.Should().NotBeNull();
        marker.Entries.Entries.Should().HaveCount(2);
        marker.Entries.Primary.Should().BeSameAs(first);
        first.Data.Body.Should().Be("Original text");
        first.Data.IsLocked.Should().BeTrue();
        added!.Id.Should().NotBe(first.Id);
        added.Data.AuthorUid.Should().Be(writer.PlayerUID);
        added.Data.AuthorName.Should().Be(writer.PlayerName);
        added.Data.IsLocked.Should().BeFalse();
        world.DidNotReceive().SpawnItemEntity(Arg.Any<ItemStack>(), Arg.Any<Vec3d>(), Arg.Any<Vec3d>());
    }

    [Fact]
    public void OpeningAndCancelingTheAddEditorDoesNotLeaveABlankEntry()
    {
        var (marker, player, _) = CreateMarker();

        marker.OnReceivedClientPacket(player, 1008, null);
        marker.Entries.Entries.Should().ContainSingle();

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            CreateNew = true,
        }));
        marker.Entries.Entries.Should().ContainSingle();

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            CreateNew = true,
            Title = "A second account",
            Body = "Another perspective",
        }));
        marker.Entries.Entries.Should().HaveCount(2);
        marker.Entries.Entries[1].Data.Body.Should().Be("Another perspective");
        marker.Entries.Entries[1].Data.AuthorUid.Should().Be(player.PlayerUID);
    }

    [Fact]
    public void AddingRequiresClaimAccessAndProximity()
    {
        var (denied, player, _) = CreateMarker(claim: false);
        denied.TryAddEntry(player).Should().BeNull();
        denied.Entries.Entries.Should().ContainSingle();

        var (distant, distantPlayer, _) = CreateMarker();
        distantPlayer.Entity.Pos.SetPos(50, 50, 50);
        distant.TryAddEntry(distantPlayer).Should().BeNull();
        distant.Entries.Entries.Should().ContainSingle();
    }

    [Fact]
    public void AddStopsAtEightEntriesWithoutChangingEarlierEntries()
    {
        var (marker, player, _) = CreateMarker();
        var first = marker.Entries.Primary;
        for (var index = 1; index < SceneDescriptionEntries.MaxEntries; index++)
            marker.TryAddEntry(player).Should().NotBeNull();

        marker.TryAddEntry(player).Should().BeNull();
        marker.Entries.Entries.Should().HaveCount(SceneDescriptionEntries.MaxEntries);
        marker.Entries.Entries.Select(entry => entry.Id).Should().OnlyHaveUniqueItems();
        marker.Entries.Primary.Should().BeSameAs(first);
        first.Data.Body.Should().Be("Original text");
    }

    [Fact]
    public void SeveralEntriesShowANeutralTargetedCaptionInsteadOfTheFirstBody()
    {
        LangTestHelper.EnsureEnglish();
        var (marker, player, _) = CreateMarker();
        marker.Data.Title = "Private first title";
        marker.Data.ShowBodyInBubble = true;
        marker.Data.Display = SceneDescriptionDisplay.AlwaysNearby;
        marker.DisplayData.Should().BeSameAs(marker.Data);

        marker.TryAddEntry(player).Should().NotBeNull();

        marker.DisplayData.Title.Should().Be(Vintagestory.API.Config.Lang.Get("thebasics:scene-entry-count", 2));
        marker.DisplayData.Body.Should().BeEmpty();
        marker.DisplayData.ShowBodyInBubble.Should().BeFalse();
        marker.DisplayData.Display.Should().Be(SceneDescriptionDisplay.WhenTargeted);
        marker.Data.Title.Should().Be("Private first title");
        marker.Data.Body.Should().Be("Original text");
    }

    [Fact]
    public void TargetedSaveChangesOnlyItsEntryAndRejectsStaleOrAmbiguousIds()
    {
        var (marker, player, _) = CreateMarker();
        var first = marker.Entries.Primary;
        var second = marker.TryAddEntry(player);
        second.Should().NotBeNull();
        second!.Data.Body = "Second text";

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            EntryId = second.Id,
            Body = "Revised second text",
        }));
        first.Data.Body.Should().Be("Original text");
        second.Data.Body.Should().Be("Revised second text");

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            EntryId = "deleted-entry",
            Body = "Stale write",
        }));
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            Body = "Ambiguous legacy write",
        }));
        first.Data.Body.Should().Be("Original text");
        second.Data.Body.Should().Be("Revised second text");
    }

    [Fact]
    public void OnlyTheFirstEntryCanChangeTheSharedIcon()
    {
        var (marker, player, _) = CreateMarker();
        var second = marker.TryAddEntry(player)!;

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            EntryId = second.Id,
            Body = "Second scene",
            Color = (int)SceneMarkerColor.Blue,
            Symbol = (int)SceneMarkerSymbol.Diamond,
        }));

        marker.Data.Body.Should().Be("Original text");
        second.Data.Body.Should().Be("Second scene");
        marker.Data.Color.Should().NotBe(SceneMarkerColor.Blue);
        marker.Data.Symbol.Should().NotBe(SceneMarkerSymbol.Diamond);

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            EntryId = marker.Entries.Primary.Id,
            Body = "Original text",
            Color = (int)SceneMarkerColor.Blue,
            Symbol = (int)SceneMarkerSymbol.Diamond,
        }));
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
        marker.Data.Symbol.Should().Be(SceneMarkerSymbol.Diamond);
        second.Data.Body.Should().Be("Second scene");
    }

    [Fact]
    public void RemovingTheFirstEntryKeepsItsSharedAppearance()
    {
        var (marker, player, _) = CreateMarker();
        var second = marker.TryAddEntry(player)!;
        marker.Data.Color = SceneMarkerColor.Blue;

        marker.TryRemoveEntry(player, marker.Entries.Primary.Id).Should().BeTrue();

        marker.Data.Should().BeSameAs(second.Data);
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
    }

    [Fact]
    public void EmptyPrimaryMustBeWrittenBeforeAdding()
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.Title = string.Empty;
        marker.Data.Body = string.Empty;

        marker.TryAddEntry(player).Should().BeNull();
        marker.OnReceivedClientPacket(player, 1008, null);
        marker.Entries.Entries.Should().ContainSingle();
    }

    [Fact]
    public void ASecondaryLockProtectsTheSharedMarkerFromBreakingAndExplosion()
    {
        var (marker, player, world) = CreateMarker();
        var second = marker.TryAddEntry(player);
        second.Should().NotBeNull();
        second!.Data.LockItemCode = "ui";

        SceneDescriptionBlock.BreakRefused(marker, player).Should().BeTrue();
        marker.Block.GetDrops(world, marker.Pos, player).Should().BeEmpty();
        marker.Block.OnBlockBroken(world, marker.Pos, player);
        marker.Block.OnBlockExploded(world, marker.Pos, marker.Pos, EnumBlastType.RockBlast, "other");
        world.BlockAccessor.DidNotReceive().SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
        marker.Entries.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void UnlockedMarkerDropsOneItemThatRestoresEveryEntry()
    {
        var (marker, player, world) = CreateMarker();
        marker.Data.Title = "First title";
        var second = marker.TryAddEntry(Player("second", "Second Writer"));
        second.Should().NotBeNull();
        second!.Data.Title = "Second title";
        second.Data.Body = "Second text";
        var ids = marker.Entries.Entries.Select(entry => entry.Id).ToArray();

        var drops = marker.Block.GetDrops(world, marker.Pos, player);

        drops.Should().ContainSingle();
        marker.InitializeFromItem(drops[0], Player("mover", "Mover"));
        marker.Entries.Entries.Select(entry => entry.Id).Should().Equal(ids);
        marker.Entries.Entries.Select(entry => entry.Data.Title).Should().Equal("First title", "Second title");
        marker.Entries.Entries.Select(entry => entry.Data.Body).Should().Equal("Original text", "Second text");
        marker.Entries.Entries.Select(entry => entry.Data.AuthorUid).Should().Equal("creator", "second");
    }

    [Fact]
    public void CreativePickCopiesEveryEntryButClearsEveryCopiedLock()
    {
        var (marker, player, world) = CreateMarker();
        marker.Data.LockItemCode = "ui";
        var second = marker.TryAddEntry(player);
        second.Should().NotBeNull();
        second!.Data.Title = "Second title";
        second.Data.LockItemCode = "ui";

        var copied = marker.Block.OnPickBlock(world, marker.Pos);
        var copiedEntries = SceneDescriptionEntries.ReadFrom(copied.Attributes);

        copiedEntries.Entries.Should().HaveCount(2);
        copiedEntries.Entries.Select(entry => entry.Data.Title).Should().Equal(marker.Entries.Entries.Select(entry => entry.Data.Title));
        copiedEntries.Entries.Should().OnlyContain(entry => !entry.Data.IsLocked);
        marker.Entries.Entries.Should().OnlyContain(entry => entry.Data.IsLocked);
    }

    private static FakeServerPlayer Player(string uid, string name)
    {
        var player = new FakeServerPlayer(uid, name) { Entity = new EntityPlayer() };
        player.Entity.Pos.SetPos(0.5, 0.5, 0.5);
        return player;
    }

    private static (SceneDescriptionBlockEntity Marker, FakeServerPlayer Player, IWorldAccessor World) CreateMarker(bool claim = true)
    {
        LangTestHelper.EnsureEnglish();
        var api = Substitute.For<ICoreAPI>();
        var world = Substitute.For<IWorldAccessor>();
        api.Side.Returns(EnumAppSide.Server);
        api.World.Returns(world);
        world.Side.Returns(EnumAppSide.Server);
        var player = Player("creator", "Original Writer");
        var block = new SceneDescriptionBlock { Code = new AssetLocation("thebasics:scene-marker-ground-north") };
        var marker = new SceneDescriptionBlockEntity { Api = api, Pos = new BlockPos(0, 0, 0, 0), Block = block };
        world.BlockAccessor.GetBlockEntity(marker.Pos).Returns(marker);
        world.Claims.TryAccess(Arg.Any<IPlayer>(), marker.Pos, EnumBlockAccessFlags.BuildOrBreak).Returns(claim);
        marker.Data.AuthorUid = player.PlayerUID;
        marker.Data.AuthorName = player.PlayerName;
        marker.Data.Body = "Original text";
        return (marker, player, world);
    }
}
