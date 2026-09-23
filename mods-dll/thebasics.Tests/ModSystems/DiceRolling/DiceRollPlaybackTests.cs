using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NSubstitute;
using ProtoBuf;
using thebasics.Models;
using thebasics.ModSystems.DiceRolling;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

[Collection(thebasics.Tests.ModSystems.AnalyticsServiceTestCollection.Name)]
public class DiceRollPlaybackTests
{
    [Fact]
    public void ValidClipPlaysOnceAtCapturedInternalPositionWithFixedSoundSettings()
    {
        var (api, world, player) = Client();
        player.Entity.Pos.Dimension = 2;
        player.Entity.Pos.X = 11;
        player.Entity.Pos.Y = 4;
        player.Entity.Pos.Z = 6;

        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 9, X = 10, InternalY = 65540, Z = 6, Dimension = 2 });

        var call = Assert.Single(world.Played);
        Assert.Equal("thebasics:sounds/dice/diceroll9", call.Sound.Location!.ToString());
        Assert.Equal(8f, call.Sound.Range);
        Assert.Equal(EnumSoundType.Sound, call.Sound.Type);
        Assert.Equal(1f, call.Sound.Pitch.avg);
        Assert.Equal(0f, call.Sound.Pitch.var);
        Assert.Equal(1f, call.Sound.Volume.avg);
        Assert.Equal((10d, 65540d, 6d, 2), (call.X, call.Y, call.Z, call.Dimension));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void InvalidClipIsSilent(int clip)
    {
        var (api, world, _) = Client();
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = clip });
        Assert.Empty(world.Played);
    }

    [Theory]
    [InlineData(7.99, true)]
    [InlineData(8.0, false)]
    [InlineData(8.01, false)]
    public void UsesCurrentClientPositionWithStrictEightBlockCutoff(double distance, bool heard)
    {
        var (api, world, player) = Client();
        player.Entity.Pos.X = distance;
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 1 });
        Assert.Equal(heard ? 1 : 0, world.Played.Count);
    }

    [Fact]
    public void DifferentDimensionIsSilent()
    {
        var (api, world, player) = Client();
        player.Entity.Pos.Dimension = 1;
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 1, Dimension = 2, InternalY = 65536 });
        Assert.Empty(world.Played);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(0, double.PositiveInfinity, 0)]
    [InlineData(0, 0, double.NegativeInfinity)]
    public void NonfiniteOriginIsSilent(double x, double y, double z)
    {
        var (api, world, _) = Client();
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 1, X = x, InternalY = y, Z = z });
        Assert.Empty(world.Played);
    }

    [Fact]
    public void MissingClientPlayerOrEntityDuringTeardownIsSilent()
    {
        var (api, world, player) = Client();
        world.TestPlayer = null;
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 1 });
        world.TestPlayer = player;
        player.Entity = null!;
        DiceRollSounds.Play(api, new DiceRollSoundMessage { Clip = 1 });
        Assert.Empty(world.Played);
    }

    [Fact]
    public void PacketRoundTripsAllFiveFields()
    {
        var message = new DiceRollSoundMessage { Clip = 7, X = 12.5, InternalY = 65541.25, Z = -3.75, Dimension = 2 };
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        var copy = Serializer.Deserialize<DiceRollSoundMessage>(stream);
        Assert.Equal((7, 12.5, 65541.25, -3.75, 2), (copy.Clip, copy.X, copy.InternalY, copy.Z, copy.Dimension));
    }

    [Fact]
    public void PlaybackFailureDoesNotEscapeHandlerOrWriteToChat()
    {
        var (api, world, _) = Client();
        world.ThrowOnPlay = true;
        var systemType = typeof(thebasics.ModSystems.ChatUiSystem.ChatUiSystem);
        var apiField = systemType.GetField("_api", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousApi = apiField.GetValue(null);
        try
        {
            apiField.SetValue(null, api);
            var handler = systemType.GetMethod("OnDiceRollSoundMessage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(handler);
            handler.Invoke(null, [new DiceRollSoundMessage { Clip = 1 }]);
            api.DidNotReceiveWithAnyArgs().ShowChatMessage(default!);
            api.Logger.Received(1).Warning("Dice roll sound playback failed.");
        }
        finally { apiField.SetValue(null, previousApi); }
    }

    private static (ICoreClientAPI Api, PlaybackWorldProxy World, FakeClientPlayer Player) Client()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var world = DispatchProxy.Create<IClientWorldAccessor, PlaybackWorldProxy>();
        var proxy = (PlaybackWorldProxy)world;
        var player = new FakeClientPlayer();
        api.World.Returns(world);
        proxy.TestPlayer = player;
        player.Entity = new EntityPlayer();
        return (api, proxy, player);
    }

    public class PlaybackWorldProxy : DispatchProxy
    {
        public IClientPlayer? TestPlayer { get; set; }
        public bool ThrowOnPlay { get; set; }
        public List<(SoundAttributes Sound, double X, double Y, double Z, int Dimension)> Played { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Player") return TestPlayer;
            if (targetMethod?.Name == "PlaySoundAt" && args is [SoundAttributes sound, double x, double y, double z, int dimension, ..])
            {
                if (ThrowOnPlay) throw new InvalidOperationException("missing sound asset");
                Played.Add((sound, x, y, z, dimension));
                return 0;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class FakeClientPlayer : IClientPlayer
    {
        public EntityPlayer Entity { get; set; } = null!;
        public IPlayerRole Role { get; set; } = null!;
        public PlayerGroupMembership[] Groups { get; } = [];
        public List<Entitlement> Entitlements { get; } = [];
        public BlockSelection CurrentBlockSelection => null!;
        public EntitySelection CurrentEntitySelection => null!;
        public string PlayerName => "tester";
        public string PlayerUID => "tester";
        public int ClientId => 1;
        public IWorldPlayerData WorldData => null!;
        public IPlayerInventoryManager InventoryManager => null!;
        public string[] Privileges => [];
        public bool ImmersiveFpMode => false;
        public float CameraPitch { get; set; }
        public float CameraRoll { get; set; }
        public float CameraYaw { get; set; }
        public EnumCameraMode CameraMode => default;
        public PlayerGroupMembership[] GetGroups() => Groups;
        public PlayerGroupMembership GetGroup(int groupId) => null!;
        public bool HasPrivilege(string privilegeCode) => false;
        public bool IsInInteractionRangeOf(BlockPos pos, float slack = 0.25f) => false;
        public bool IsInInteractionRangeOf(Entity entity, float slack = 0.25f) => false;
        public void ShowChatNotification(string message) { }
        public void TriggerFpAnimation(EnumHandInteract anim) { }
    }
}
