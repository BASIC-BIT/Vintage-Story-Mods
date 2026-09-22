using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.DiceRolling;

internal static class DiceRollSounds
{
    internal static void Deliver(ModConfig config, IServerPlayer roller, IReadOnlyList<IServerPlayer> audience,
        Action<DiceRollSoundMessage, IServerPlayer> send, Func<int> chooseClip)
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
            Clip = chooseClip(), X = x, InternalY = internalY, Z = z, Dimension = dimension
        };
        foreach (var recipient in listeners)
        {
            try { send(message, recipient); }
            catch (Exception) { System.Diagnostics.Trace.TraceWarning("Dice roll sound packet delivery failed."); }
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
