using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.Extensions
{
    public static class EntityExtensions
    {
        public static IServerPlayer GetPlayer(this Entity entity)
        {
            var playerEntity = entity as EntityPlayer;
            
            if (playerEntity == null)
            {
                return null;
            }
            
            return playerEntity.Player as IServerPlayer;
        }
    }
}