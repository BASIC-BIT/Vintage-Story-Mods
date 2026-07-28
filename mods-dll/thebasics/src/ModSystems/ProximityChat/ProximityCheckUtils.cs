using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

public class ProximityCheckUtils : BaseSubSystem
{
    public ProximityCheckUtils(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api, config)
    {
    }

    public bool CanSeePlayer(IServerPlayer player1, IServerPlayer player2, bool useMultiPointTargets = false)
    {
        if (player1.PlayerUID == player2.PlayerUID)
        {
            return true; // Player can always see themselves
        }
        // TODO: Implement FOV check to ensure player1 is looking at player2
        return VisibilityUtils.HasLineOfSight(
            API.World,
            player1.Entity,
            player2.Entity,
            failOpen: false,
            useMultiPointTargets: useMultiPointTargets);
    }

    /// <summary>
    /// Whether speech from <paramref name="speaker"/> reaches <paramref name="listener"/> unobstructed.
    /// Sound occludes differently from sight: glass and water stop it, foliage does not.
    /// </summary>
    public bool CanHearPlayer(IServerPlayer speaker, IServerPlayer listener)
    {
        if (speaker.PlayerUID == listener.PlayerUID)
        {
            return true;
        }

        return VisibilityUtils.HasLineOfHearing(
            API.World,
            speaker.Entity,
            listener.Entity,
            failOpen: false,
            useMultiPointTargets: true);
    }

    /// <summary>
    /// Sound-occluding blocks between two players, used to inflate effective distance for muffling.
    /// </summary>
    public int CountSoundOccluders(IServerPlayer speaker, IServerPlayer listener)
    {
        if (speaker.PlayerUID == listener.PlayerUID)
        {
            return 0;
        }

        return VisibilityUtils.CountSoundOccluders(API.World, speaker.Entity, listener.Entity);
    }
}
