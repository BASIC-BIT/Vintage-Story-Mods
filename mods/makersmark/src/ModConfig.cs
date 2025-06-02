namespace makersmark
{
    public class ModConfig
    {
        public bool SendServerSaveAnnouncement { get; set; }
        public bool SendServerSaveFinishedAnnouncement { get; set; }

        public string TEXT_ServerSaveAnnouncement { get; set; }
        public string TEXT_ServerSaveFinished { get; set; }

        public int ProximityChatNormalBlockRange { get; set; }
        public int ProximityChatYellBlockRange { get; set; }
        public int ProximityChatWhisperBlockRange { get; set; }

        public ModConfig()
        {
            SendServerSaveAnnouncement = true;
            SendServerSaveFinishedAnnouncement = false;
            TEXT_ServerSaveAnnouncement = "Server save has started - expect lag for a few seconds.";
            TEXT_ServerSaveFinished = "Server save has finished.";
            ProximityChatNormalBlockRange = 15;
            ProximityChatYellBlockRange = 60;
            ProximityChatWhisperBlockRange = 5;
        }
    }
}
