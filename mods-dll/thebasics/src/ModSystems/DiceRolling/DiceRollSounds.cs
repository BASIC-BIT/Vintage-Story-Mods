using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.DiceRolling;

internal static class DiceRollSounds
{
    internal static void Play(ICoreClientAPI api, DiceRollSoundMessage message)
    {
        if (message == null || message.Clip is < 1 or > 9 ||
            !double.IsFinite(message.X) || !double.IsFinite(message.InternalY) || !double.IsFinite(message.Z)) return;

        var world = api?.World;
        var position = world?.Player?.Entity?.Pos;
        if (position == null || position.Dimension != message.Dimension) return;

        var dx = position.X - message.X;
        var dy = position.InternalY - message.InternalY;
        var dz = position.Z - message.Z;
        if (!(dx * dx + dy * dy + dz * dz < 64)) return;

        var sound = new SoundAttributes(
            new AssetLocation("thebasics", $"sounds/dice/diceroll{message.Clip}"),
            withRandomPitch: false)
        {
            Range = 8f,
            Type = EnumSoundType.Sound
        };
        world.PlaySoundAt(sound, message.X, message.InternalY, message.Z, message.Dimension);
    }

    internal static void Deliver(ModConfig config, IServerPlayer roller, IReadOnlyList<IServerPlayer> audience,
        Action<DiceRollSoundMessage, IServerPlayer> send, Func<int> chooseClip, ILogger logger = null)
    {
        if (config?.EnableDiceRollSounds != true || !SpectatorChatPolicy.ShouldEmitEntityAttachedCues(roller)) return;
        var origin = roller?.Entity?.Pos;
        if (origin == null) return;

        var x = origin.X;
        var y = origin.Y;
        var internalY = origin.InternalY;
        var z = origin.Z;
        var dimension = origin.Dimension;
        var listeners = audience.Where(recipient => IsEligible(recipient, dimension, x, y, z)).ToArray();
        if (listeners.Length == 0) return;

        var message = new DiceRollSoundMessage
        {
            Clip = chooseClip(),
            X = x,
            InternalY = internalY,
            Z = z,
            Dimension = dimension
        };
        foreach (var recipient in listeners)
        {
            try { send(message, recipient); }
            catch (Exception) { logger?.Warning("Dice roll sound packet delivery failed."); }
        }
    }

    private static bool IsEligible(IServerPlayer recipient, int dimension, double x, double y, double z)
    {
        var position = recipient?.Entity?.Pos;
        if (position == null || position.Dimension != dimension || !recipient.GetDiceRollSoundsEnabled()) return false;
        var dx = position.X - x;
        var dy = position.Y - y;
        var dz = position.Z - z;
        return dx * dx + dy * dy + dz * dz < 64;
    }
}
