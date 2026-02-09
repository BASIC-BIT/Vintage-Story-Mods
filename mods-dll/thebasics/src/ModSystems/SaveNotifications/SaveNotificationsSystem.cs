using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SaveNotifications
{
    public class SaveNotificationsSystem : BaseBasicModSystem
    {
        // ServerResume does not reliably fire on all server versions/mod stacks.
        // To keep "save finished" usable, we also schedule a delayed callback after GameWorldSave.
        private const int SaveFinishedFallbackDelayMs = 3500;

        private int _saveSeq;
        private int _lastFinishedSeq;

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
            var seq = ++_saveSeq;

            if (Config.SendServerSaveAnnouncement)
            {
                var chatType = Config.ServerSaveAnnouncementAsNotification
                    ? EnumChatType.Notification
                    : EnumChatType.OthersMessage;

                API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Config.TEXT_ServerSaveAnnouncement, chatType);
            }

            if (Config.SendServerSaveFinishedAnnouncement)
            {
                API.Event.RegisterCallback(_ => SendSaveFinishedOnce(seq), SaveFinishedFallbackDelayMs);
            }
        }

        private void Event_SaveFinished()
        {
            // ServerResume can fire without a prior GameWorldSave during server lifecycle.
            if (_saveSeq <= 0)
            {
                return;
            }

            SendSaveFinishedOnce(_saveSeq);
        }

        private void SendSaveFinishedOnce(int seq)
        {
            if (seq <= 0 || seq <= _lastFinishedSeq)
            {
                return;
            }

            _lastFinishedSeq = seq;

            var chatType = Config.ServerSaveFinishedAsNotification
                ? EnumChatType.Notification
                : EnumChatType.OthersMessage;

            API.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Config.TEXT_ServerSaveFinished, chatType);
        }
    }
}
