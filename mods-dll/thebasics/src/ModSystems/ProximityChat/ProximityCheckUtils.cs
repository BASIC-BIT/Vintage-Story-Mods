using System.Collections.Generic;
using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using thebasics.Utilities;

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
    
    private int GetFloodFillDistance(IServerPlayer player1, IServerPlayer player2, int maxDistance)
    {
        HashSet<BlockPos> visited = new HashSet<BlockPos>();
        Queue<(BlockPos, int)> toVisit = new Queue<(BlockPos, int)>();

        BlockPos startPos = player1.Entity.ServerPos.AsBlockPos;
        BlockPos targetPos = player2.Entity.ServerPos.AsBlockPos;

        toVisit.Enqueue((startPos, 0));

        while (toVisit.Count > 0)
        {
            (BlockPos currentPos, int distance) = toVisit.Dequeue();

            if (visited.Contains(currentPos)) continue;
            visited.Add(currentPos);

            if (currentPos.Equals(targetPos)) return distance;

            if (distance < maxDistance)
            {
                foreach (BlockPos neighbor in GetWalkableNeighbors(currentPos))
                {
                    toVisit.Enqueue((neighbor, distance + 1));
                }
            }
        }

        return -1;
    }

    private IEnumerable<BlockPos> GetWalkableNeighbors(BlockPos pos)
    {
        foreach (BlockFacing blockFacing in new[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST, BlockFacing.UP, BlockFacing.DOWN })
        {
            BlockPos neighbor = pos.AddCopy(blockFacing);
            
            Block block = API.World.BlockAccessor.GetBlock(neighbor);

            if (block.Id == 0) yield return neighbor; // Block is air
        }
    }
}
