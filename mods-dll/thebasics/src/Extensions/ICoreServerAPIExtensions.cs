using System;
using System.Linq;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Extensions
{
    public static class ICoreServerAPIExtensions
    {
        public static IServerPlayer GetPlayerByName(this ICoreServerAPI api, string name)
        {
            return api.Server.Players.ToList()
                .Find(findPlayer => String.Equals(findPlayer.PlayerName, name, StringComparison.InvariantCultureIgnoreCase));
        }

        public static IServerPlayer GetPlayerByUID(this ICoreServerAPI api, string name)
        {
            return api.Server.Players.ToList()
                .Find(findPlayer => String.Equals(findPlayer.PlayerUID, name, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}