using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SleepNotifier
{
    public class SleepNotifierSystem : BaseBasicModSystem
    {
        public double LastSleepingCount;

        protected override void BasicStartServerSide()
        {
            if (Config.EnableSleepNotifications)
            {
                API.Event.SaveGameLoaded += Event_SaveGameLoaded;
                API.Event.RegisterGameTickListener(SlowServerTick, 200);
            }
        }

        private void Event_SaveGameLoaded()
        {
            LastSleepingCount = 0;
        }

        private void SlowServerTick(float dt)
        {
            var curSleepingCount = GetSleepingCount();
            var totalPlayers = API.World.AllOnlinePlayers.Length;

            if (curSleepingCount >= 1 &&
                totalPlayers >= 2 &&
                curSleepingCount > LastSleepingCount &&
                IsAboveSleepingThreshold(curSleepingCount, totalPlayers))
            {
                API.BroadcastMessageToAllGroups(Config.TEXT_SleepNotification, EnumChatType.AllGroups);
            }

            LastSleepingCount = curSleepingCount;
        }

        public bool IsAboveSleepingThreshold(int curSleepingCount, int totalPlayers)
        {
            return ((double) curSleepingCount / (double) totalPlayers) > Config.SleepNotificationThreshold;
        }

        private int GetSleepingCount()
        {
            return API.World.AllOnlinePlayers
                .ToList()
                .Where((player) =>
                {
                    var splr = player as IServerPlayer;

                    return splr != null &&
                           splr.ConnectionState == EnumClientState.Playing &&
                           player.Entity != null &&
                           player.Entity.MountedOn != null &&
                           player.Entity.MountedOn is BlockEntityBed;
                }).Count();
        }
    }
}