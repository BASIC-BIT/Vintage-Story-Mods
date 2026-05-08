using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SaveNotifications
{
    public class SaveNotificationsSystem : BaseBasicModSystem
    {
        private bool _saveInProgress;
        private bool _gameWorldSaveSubscribed;
        private bool _serverResumeSubscribed;

        protected override void BasicStartServerSide()
        {
            RefreshSaveNotificationSubscriptions();
        }

        protected override void OnConfigReloaded(System.Collections.Generic.IReadOnlySet<string> changedKeys)
        {
            if (changedKeys.Contains(nameof(Config.SendServerSaveAnnouncement)) ||
                changedKeys.Contains(nameof(Config.SendServerSaveFinishedAnnouncement)))
            {
                RefreshSaveNotificationSubscriptions();
            }
        }

        private void RefreshSaveNotificationSubscriptions()
        {
            if (_gameWorldSaveSubscribed)
            {
                API.Event.GameWorldSave -= Event_GameWorldSave;
                _gameWorldSaveSubscribed = false;
            }

            if (_serverResumeSubscribed)
            {
                API.Event.ServerResume -= Event_SaveFinished;
                _serverResumeSubscribed = false;
            }

            if (Config.SendServerSaveAnnouncement || Config.SendServerSaveFinishedAnnouncement)
            {
                API.Event.GameWorldSave += Event_GameWorldSave;
                _gameWorldSaveSubscribed = true;
            }

            if (Config.SendServerSaveFinishedAnnouncement)
            {
                API.Event.ServerResume += Event_SaveFinished;
                _serverResumeSubscribed = true;
            }
        }

        public override void Dispose()
        {
            if (API?.Event != null && _gameWorldSaveSubscribed)
            {
                API.Event.GameWorldSave -= Event_GameWorldSave;
                _gameWorldSaveSubscribed = false;
            }

            if (API?.Event != null && _serverResumeSubscribed)
            {
                API.Event.ServerResume -= Event_SaveFinished;
                _serverResumeSubscribed = false;
            }

            base.Dispose();
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
