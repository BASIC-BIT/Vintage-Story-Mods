using System;
using System.Collections.Generic;
using thebasics.Configs;
using thebasics.ModSystems.DiceRolling;
using thebasics.Extensions;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using NSubstitute;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class DiceRollSoundTests
{
    [Fact]
    public void MutedRollerStillMakesSoundForAnEnabledListener()
    {
        var roller = new FakeServerPlayer("roller") { Entity = new EntityPlayer() };
        var listener = new FakeServerPlayer("listener") { Entity = new EntityPlayer() };
        listener.Entity.Pos.X = 2;
        roller.SetDiceRollSoundsEnabled(false);
        var heard = new List<string>();
        var choices = 0;
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { roller, listener },
            (packet, target) => { Assert.Equal(4, packet.Clip); heard.Add(target.PlayerUID); },
            () => { choices++; return 4; });
        Assert.Equal(new[] { "listener" }, heard);
        Assert.Equal(1, choices);
    }

    [Theory]
    [InlineData(7.9, true)]
    [InlineData(8.0, false)]
    [InlineData(8.1, false)]
    public void SoundHasExclusiveEightBlockRadius(double distance, bool heard)
    {
        var roller = Player("roller");
        var listener = Player("listener");
        listener.Entity.Pos.X = distance;
        var sends = 0;
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { listener },
            (_, _) => sends++, () => 1);
        Assert.Equal(heard ? 1 : 0, sends);
    }

    [Fact]
    public void SoundUsesEuclideanDistanceButOnlyWithinTextAudience()
    {
        var roller = Player("roller");
        var diagonal = Player("diagonal");
        diagonal.Entity.Pos.X = 5;
        diagonal.Entity.Pos.Z = 5;
        var excluded = Player("excluded");
        excluded.Entity.Pos.X = 1;
        var distant = Player("distant");
        distant.Entity.Pos.X = 6;
        distant.Entity.Pos.Z = 6;
        var heard = new List<string>();
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { diagonal, distant },
            (_, listener) => heard.Add(listener.PlayerUID), () => 1);
        Assert.Equal(new[] { "diagonal" }, heard);
    }

    [Fact]
    public void DisabledMutedOtherDimensionAndMissingEntityListenersHearNothing()
    {
        var roller = Player("roller");
        var muted = Player("muted");
        muted.SetDiceRollSoundsEnabled(false);
        var otherDimension = Player("dimension");
        otherDimension.Entity.Pos.Dimension = 1;
        var noEntity = new FakeServerPlayer("missing");
        var choices = 0;
        var sends = 0;
        var audience = new IServerPlayer[] { muted, otherDimension, noEntity };
        DiceRollSounds.Deliver(new ModConfig(), roller, audience, (_, _) => sends++, () => { choices++; return 1; });
        Assert.Equal(0, sends);
        Assert.Equal(0, choices);
        DiceRollSounds.Deliver(new ModConfig { EnableDiceRollSounds = false }, roller,
            new IServerPlayer[] { roller }, (_, _) => sends++, () => { choices++; return 1; });
        Assert.Equal(0, sends);
        Assert.Equal(0, choices);
    }

    [Fact]
    public void ActiveSpectatorAndMissingOriginAreSilent()
    {
        var roller = Player("roller");
        roller.WorldData = Substitute.For<IWorldPlayerData>();
        roller.WorldData.CurrentGameMode.Returns(EnumGameMode.Spectator);
        roller.ConnectionState = EnumClientState.Playing;
        var listener = Player("listener");
        var choices = 0;
        var sends = 0;
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { listener },
            (_, _) => sends++, () => { choices++; return 1; });
        roller.ConnectionState = EnumClientState.Connected;
        roller.Entity = null!;
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { listener },
            (_, _) => sends++, () => { choices++; return 1; });
        Assert.Equal(0, sends);
        Assert.Equal(0, choices);
    }

    [Fact]
    public void OneClipAndCapturedOriginAreSharedEvenWhenRollerMoves()
    {
        var roller = Player("roller");
        roller.Entity.Pos.X = 3;
        roller.Entity.Pos.Y = 4;
        roller.Entity.Pos.Z = 5;
        var first = Player("first");
        var second = Player("second");
        var packets = new List<(string Recipient, int Clip, double X, double InternalY, double Z, int Dimension)>();
        var choices = 0;
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { first, second },
            (packet, recipient) => packets.Add((recipient.PlayerUID, packet.Clip, packet.X, packet.InternalY, packet.Z, packet.Dimension)),
            () => { choices++; roller.Entity.Pos.X = 100; return 9; });
        Assert.Equal(1, choices);
        Assert.Equal(2, packets.Count);
        Assert.Equal(("first", 9, 3d, 4d, 5d, 0), packets[0]);
        Assert.Equal(("second", 9, 3d, 4d, 5d, 0), packets[1]);
    }

    [Fact]
    public void FailedPacketDoesNotPreventLaterListener()
    {
        var roller = Player("roller");
        var first = Player("first");
        var second = Player("second");
        var received = new List<string>();
        DiceRollSounds.Deliver(new ModConfig(), roller, new IServerPlayer[] { first, second },
            (_, recipient) =>
            {
                if (recipient == first) throw new InvalidOperationException("network");
                received.Add(recipient.PlayerUID);
            }, () => 1);
        Assert.Equal(new[] { "second" }, received);
    }

    private static FakeServerPlayer Player(string uid) => new(uid) { Entity = new EntityPlayer() };

    [Fact]
    public void DiceMuteIsPerListenerAndDefaultsOn()
    {
        var muted = new FakeServerPlayer("muted");
        var other = new FakeServerPlayer("other");
        Assert.True(muted.GetDiceRollSoundsEnabled());
        muted.SetDiceRollSoundsEnabled(false);
        Assert.False(muted.GetDiceRollSoundsEnabled());
        Assert.True(other.GetDiceRollSoundsEnabled());
        muted.SetDiceRollSoundsEnabled(true);
        Assert.True(muted.GetDiceRollSoundsEnabled());
    }

    [Fact]
    public void MalformedStoredPreferenceDefaultsOn()
    {
        var player = new FakeServerPlayer();
        player.SetModdata("thebasics-dice-roll-sounds-enabled", []);
        Assert.True(player.GetDiceRollSoundsEnabled());
    }
}
