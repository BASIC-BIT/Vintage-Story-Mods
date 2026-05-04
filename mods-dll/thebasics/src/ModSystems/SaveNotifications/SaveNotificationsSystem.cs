using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SaveNotifications
{
    public class SaveNotificationsSystem : BaseBasicModSystem
    {
        private bool _saveInProgress;

        protected override void BasicStartServerSide()
        {
            if (Config.SendServerSaveAnnouncement || Config.SendServerSaveFinishedAnnouncement)
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
            _saveInProgress = Config.SendServerSaveFinishedAnnouncement;

            if (Config.SendServerSaveAnnouncement)
            {
                var chatType = Config.ServerSaveAnnouncementAsNotification
                    ? EnumChatType.Notification
                    : EnumChatType.OthersMessage;

                API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Config.TEXT_ServerSaveAnnouncement, chatType);
            }
        }

        private void Event_SaveFinished()
        {
            if (!_saveInProgress || !Config.SendServerSaveFinishedAnnouncement)
            {
                return;
            }

            _saveInProgress = false;
            API.Event.RegisterCallback(_ => SendSaveFinishedAnnouncement(), 100, true);
        }

        private void SendSaveFinishedAnnouncement()
        {
            if (Config.SendServerSaveFinishedAnnouncement)
            {
                var chatType = Config.ServerSaveFinishedAsNotification
                    ? EnumChatType.Notification
                    : EnumChatType.OthersMessage;

                API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Config.TEXT_ServerSaveFinished, chatType);
            }
        }
    }
}
