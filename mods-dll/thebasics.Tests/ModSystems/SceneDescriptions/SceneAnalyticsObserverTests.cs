using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection(AnalyticsServiceTestCollection.Name)]
public class SceneAnalyticsObserverTests
{
    [Fact]
    public void ClientRequiresLivePermissionAndDedupesSuccessfulOpens()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var channel = Substitute.For<IClientNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        channel.Connected.Returns(true);
        using var observer = new SceneAnalyticsObserver();
        observer.StartClient(api);
        var pos = new BlockPos(0, 0, 0, 0);
        observer.ReaderOpened(pos);
        channel.DidNotReceive().SendPacket(Arg.Any<SceneObservationMessage>());
        observer.SetPermission(true);
        observer.ReaderOpened(pos);
        observer.ReaderOpened(pos);
        channel.Received(1).SendPacket(Arg.Any<SceneObservationMessage>());
        api.World.ElapsedMilliseconds.Returns(2500L);
        observer.ReaderOpened(new BlockPos(1, 0, 0, 0));
        channel.Received(1).SendPacket(Arg.Any<SceneObservationMessage>());
        observer.SetPermission(true);
        observer.SetPermission(false);
        observer.ReaderOpened(new BlockPos(2, 0, 0, 0));
        channel.Received(1).SendPacket(Arg.Any<SceneObservationMessage>());
    }

    [Fact]
    public void ClientDedupesReaderOpensPerSelectedEntry()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var channel = Substitute.For<IClientNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        channel.Connected.Returns(true);
        using var observer = new SceneAnalyticsObserver();
        observer.StartClient(api);
        observer.SetPermission(true);
        var pos = new BlockPos(0, 0, 0, 0);

        observer.ReaderOpened(pos, "first");
        observer.ReaderOpened(pos, "second");
        observer.ReaderOpened(pos, "first");

        channel.Received(2).SendPacket(Arg.Any<SceneObservationMessage>());
        channel.Received(1).SendPacket(Arg.Is<SceneObservationMessage>(message => message.EntryId == "first"));
        channel.Received(1).SendPacket(Arg.Is<SceneObservationMessage>(message => message.EntryId == "second"));
    }

    [Fact]
    public void ServerRejectsUnloadedWrongDimensionFarAndRevokedObservationsAndDedupesPerViewer()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink, playerPseudonymizer: _ => new string('c', 64));
        try
        {
            using var observer = new SceneAnalyticsObserver();
            observer.StartServer(api);
            var pos = new BlockPos(0, 0, 0, 0);
            var marker = new SceneDescriptionBlockEntity { Pos = pos };
            marker.Data.Body = "private";
            api.World.BlockAccessor.GetBlockEntity(Arg.Any<BlockPos>()).Returns(marker);
            var one = new FakeServerPlayer("one") { Entity = new EntityPlayer() };
            var two = new FakeServerPlayer("two") { Entity = new EntityPlayer() };
            one.Entity.Pos.SetPos(0.5, 0.5, 0.5);
            two.Entity.Pos.SetPos(0.5, 0.5, 0.5);
            var message = new SceneObservationMessage();
            api.World.BlockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns((IWorldChunk)null!);
            observer.Receive(one, message);
            sink.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
            api.World.BlockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns(Substitute.For<IWorldChunk>());
            observer.Receive(one, new SceneObservationMessage { Dimension = 1 });
            one.Entity.Pos.SetPos(50, 50, 50);
            observer.Receive(one, message);
            sink.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
            one.Entity.Pos.SetPos(0.5, 0.5, 0.5);
            observer.Receive(one, message);
            observer.Receive(one, message);
            observer.Receive(two, message);
            sink.Received(2).Track("feature used", Arg.Is<IDictionary<string, object>>(p => (string)p["action"] == "reader_opened" && !p.ContainsKey("pseudonymous_player_id")));
            // Distinct held items intentionally share a per-viewer cooldown, without content fingerprints.
            one.InventoryManager = Substitute.For<IPlayerInventoryManager>();
            var firstHeld = new ItemStack(new SceneDescriptionBlock());
            new SceneDescriptionData { Title = "Title only" }.WriteTo(firstHeld.Attributes);
            one.InventoryManager.ActiveHotbarSlot.Returns(new DummySlot(firstHeld));
            observer.Receive(one, new SceneObservationMessage { Held = true });
            var secondHeld = new ItemStack(new SceneDescriptionBlock());
            new SceneDescriptionData { Body = "second" }.WriteTo(secondHeld.Attributes);
            one.InventoryManager.ActiveHotbarSlot.Returns(new DummySlot(secondHeld));
            observer.Receive(one, new SceneObservationMessage { Held = true });
            sink.Received(1).Track("feature used", Arg.Is<IDictionary<string, object>>(p => (string)p["scene_read_source"] == "held"));
            sink.IsEnabled.Returns(false);
            api.World.ElapsedMilliseconds.Returns(30000L);
            observer.Receive(one, message);
            sink.Received(3).Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
        }
        finally { AnalyticsService.Shutdown(); }
    }

    [Fact]
    public void PlacedReaderOpenAttributesTheSelectedEntryAndRejectsUnknownOrAmbiguousIds()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink, playerPseudonymizer: _ => new string('c', 64));
        try
        {
            using var observer = new SceneAnalyticsObserver();
            observer.StartServer(api);
            var pos = new BlockPos(0, 0, 0, 0);
            var marker = new SceneDescriptionBlockEntity { Pos = pos };
            marker.Data.Title = "First description";
            marker.Data.Display = SceneDescriptionDisplay.AlwaysNearby;
            var second = marker.Entries.Add(new SceneDescriptionData
            {
                Body = "Second description",
                Display = SceneDescriptionDisplay.OnInteraction,
                LockItemCode = "ui",
            })!;
            api.World.BlockAccessor.GetBlockEntity(Arg.Any<BlockPos>()).Returns(marker);
            api.World.BlockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns(Substitute.For<IWorldChunk>());
            var player = new FakeServerPlayer("reader") { Entity = new EntityPlayer() };
            player.Entity.Pos.SetPos(0.5, 0.5, 0.5);

            var selected = new SceneObservationMessage { EntryId = second.Id };
            observer.Receive(player, selected);
            observer.Receive(player, selected);
            observer.Receive(player, new SceneObservationMessage { EntryId = marker.Entries.Primary.Id });
            observer.Receive(player, new SceneObservationMessage { EntryId = "deleted-entry" });
            observer.Receive(player, new SceneObservationMessage());

            sink.Received(1).Track("feature used", Arg.Is<IDictionary<string, object>>(properties =>
                (string)properties["action"] == "reader_opened" &&
                (string)properties["scene_display_mode"] == "on_interaction" &&
                (bool)properties["scene_locked"]));
            sink.Received(1).Track("feature used", Arg.Is<IDictionary<string, object>>(properties =>
                (string)properties["action"] == "reader_opened" &&
                (string)properties["scene_display_mode"] == "always_nearby" &&
                !(bool)properties["scene_locked"]));
            sink.Received(2).Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
        }
        finally { AnalyticsService.Shutdown(); }
    }

    [Fact]
    public void MultiEntryBubbleDoesNotAttributeTheSharedSummaryToAnEntry()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink, playerPseudonymizer: _ => new string('c', 64));
        try
        {
            using var observer = new SceneAnalyticsObserver();
            observer.StartServer(api);
            var pos = new BlockPos(0, 0, 0, 0);
            var marker = new SceneDescriptionBlockEntity { Pos = pos };
            marker.Data.Title = "First description";
            marker.Data.Display = SceneDescriptionDisplay.AlwaysNearby;
            marker.Entries.Add(new SceneDescriptionData { Title = "Second description" });
            api.World.BlockAccessor.GetBlockEntity(Arg.Any<BlockPos>()).Returns(marker);
            api.World.BlockAccessor.GetChunkAtBlockPos(Arg.Any<BlockPos>()).Returns(Substitute.For<IWorldChunk>());
            var player = new FakeServerPlayer("reader") { Entity = new EntityPlayer() };
            player.Entity.Pos.SetPos(0.5, 0.5, 0.5);

            observer.Receive(player, new SceneObservationMessage { Bubble = true });
            observer.Receive(player, new SceneObservationMessage { Bubble = true, EntryId = marker.Entries.Primary.Id });

            sink.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
        }
        finally { AnalyticsService.Shutdown(); }
    }

    [Fact]
    public void HeldReaderOpenAttributesTheSelectedEntry()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        api.Network.RegisterChannel(Arg.Any<string>()).Returns(channel);
        channel.RegisterMessageType<SceneObservationMessage>().Returns(channel);
        channel.RegisterMessageType<SceneObservationPermission>().Returns(channel);
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink, playerPseudonymizer: _ => new string('c', 64));
        try
        {
            using var observer = new SceneAnalyticsObserver();
            observer.StartServer(api);
            var entries = new SceneDescriptionEntries();
            entries.Primary.Data.Title = "First description";
            entries.Primary.Data.Display = SceneDescriptionDisplay.AlwaysNearby;
            var second = entries.Add(new SceneDescriptionData { Body = "Second description", Display = SceneDescriptionDisplay.OnInteraction })!;
            var stack = new ItemStack(new SceneDescriptionBlock());
            entries.WriteTo(stack.Attributes);
            var player = new FakeServerPlayer("reader") { Entity = new EntityPlayer(), InventoryManager = Substitute.For<IPlayerInventoryManager>() };
            player.Entity.Pos.SetPos(0.5, 0.5, 0.5);
            player.InventoryManager.ActiveHotbarSlot.Returns(new DummySlot(stack));

            observer.Receive(player, new SceneObservationMessage { Held = true, EntryId = second.Id });
            observer.Receive(player, new SceneObservationMessage { Held = true, EntryId = "deleted-entry" });
            observer.Receive(player, new SceneObservationMessage { Held = true });

            sink.Received(1).Track("feature used", Arg.Is<IDictionary<string, object>>(properties =>
                (string)properties["action"] == "reader_opened" &&
                (string)properties["scene_read_source"] == "held" &&
                (string)properties["scene_display_mode"] == "on_interaction"));
            sink.Received(1).Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
        }
        finally { AnalyticsService.Shutdown(); }
    }
}
