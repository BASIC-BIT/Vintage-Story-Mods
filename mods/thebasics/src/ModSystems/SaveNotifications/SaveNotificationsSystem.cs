using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SaveNotifications
{
    public class SaveNotificationsSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            if (Config.SendServerSaveAnnouncement)
            {
                API.Event.GameWorldSave += Event_GameWorldSave;
            }

            if (Config.SendServerSaveFinishedAnnouncement)
            {
                API.Event.ServerResume += Event_SaveFinished;
            }
        }

        private void Event_GameWorldSave()
        {
            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.Config.TEXT_ServerSaveAnnouncement,
                EnumChatType.Notification);
        }

        private void Event_SaveFinished()
        {
            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.Config.TEXT_ServerSaveFinished,
                EnumChatType.Notification);
        }
    }
}