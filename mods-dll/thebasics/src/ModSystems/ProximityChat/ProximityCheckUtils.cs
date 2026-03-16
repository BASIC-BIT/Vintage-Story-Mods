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

    public bool CanSeePlayer(IServerPlayer player1, IServerPlayer player2)
    {
        if (player1.PlayerUID == player2.PlayerUID)
        {
            return true; // Player can always see themselves
        }
        // TODO: Implement FOV check to ensure player1 is looking at player2
        return VisibilityUtils.HasLineOfSight(API.World, player1.Entity, player2.Entity, failOpen: false);
    }
}
