namespace thebasics
{
    public class ModConfig
    {
        public bool SendServerSaveAnnouncement = true;
        public bool SendServerSaveFinishedAnnouncement = false;

        public string TEXT_ServerSaveAnnouncement = "Server save has started - expect lag for a few seconds.";
        public string TEXT_ServerSaveFinished = "Server save has finished.";

        public int ProximityChatNormalBlockRange = 35;
        public int ProximityChatYellBlockRange = 90;
        public int ProximityChatWhisperBlockRange = 5;
        public int ProximityChatSignBlockRange = 15;
    } 
}
