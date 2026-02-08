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
            var chatType = Config.ServerSaveAnnouncementAsNotification
                ? EnumChatType.Notification
                : EnumChatType.OthersMessage;

            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.Config.TEXT_ServerSaveAnnouncement,
                chatType);
        }

        private void Event_SaveFinished()
        {
            var chatType = Config.ServerSaveFinishedAsNotification
                ? EnumChatType.Notification
                : EnumChatType.OthersMessage;

            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, this.Config.TEXT_ServerSaveFinished,
                chatType);
        }
    }
}
