using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SleepNotifier
{
    public class SleepNotifierSystem : BaseBasicModSystem
    {
        public int LastSleepingCount { get; set; }
        private long _slowTickListenerId;
        private bool _saveGameLoadedSubscribed;

        protected override void BasicStartServerSide()
        {
            RefreshSleepNotificationSubscription();
        }

        protected override void OnConfigReloaded(System.Collections.Generic.IReadOnlySet<string> changedKeys)
        {
            if (changedKeys.Contains(nameof(Config.EnableSleepNotifications)))
            {
                RefreshSleepNotificationSubscription();
            }
        }

        private void RefreshSleepNotificationSubscription()
        {
            if (_saveGameLoadedSubscribed)
            {
                API.Event.SaveGameLoaded -= Event_SaveGameLoaded;
                _saveGameLoadedSubscribed = false;
            }

            if (_slowTickListenerId != 0)
            {
                API.Event.UnregisterGameTickListener(_slowTickListenerId);
                _slowTickListenerId = 0;
            }

            if (!Config.EnableSleepNotifications)
            {
                return;
            }

            API.Event.SaveGameLoaded += Event_SaveGameLoaded;
            _saveGameLoadedSubscribed = true;
            _slowTickListenerId = API.Event.RegisterGameTickListener(SlowServerTick, 200);
        }

        public override void Dispose()
        {
            if (API?.Event != null && _saveGameLoadedSubscribed)
            {
                API.Event.SaveGameLoaded -= Event_SaveGameLoaded;
                _saveGameLoadedSubscribed = false;
            }

            if (API?.Event != null && _slowTickListenerId != 0)
            {
                API.Event.UnregisterGameTickListener(_slowTickListenerId);
                _slowTickListenerId = 0;
            }

            base.Dispose();
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
                curSleepingCount < totalPlayers &&
                curSleepingCount > LastSleepingCount &&
                IsAboveSleepingThreshold(curSleepingCount, totalPlayers) &&
                !IsAboveSleepingThreshold(LastSleepingCount, totalPlayers)
               )
            {
                API.BroadcastMessageToAllGroups(Config.TEXT_SleepNotification, EnumChatType.AllGroups);
            }

            LastSleepingCount = curSleepingCount;
        }

        public bool IsAboveSleepingThreshold(int curSleepingCount, int totalPlayers)
        {
            return ((double)curSleepingCount / (double)totalPlayers) >= Config.SleepNotificationThreshold;
        }

        private int GetSleepingCount()
        {
            return API.World.AllOnlinePlayers
                .Count((player) =>
                {
                    var splr = player as IServerPlayer;

                    return splr != null &&
                           splr.ConnectionState == EnumClientState.Playing &&
                           player.Entity != null &&
                           player.Entity.MountedOn != null &&
                           player.Entity.MountedOn is BlockEntityBed;
                });
        }
    }
}
