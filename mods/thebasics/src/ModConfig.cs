namespace thebasics
{
    public class ModConfig
    {
        public bool SendServerSaveAnnouncement { get; set; } = true;
        public bool SendServerSaveFinishedAnnouncement { get; set; } = false;

        public string TEXT_ServerSaveAnnouncement { get; set; } =
            "Server save has started - expect lag for a few seconds.";
        public string TEXT_ServerSaveFinished { get; set; } =
            "Server save has finished.";

        public int ProximityChatNormalBlockRange { get; set; } = 35;
        public int ProximityChatYellBlockRange { get; set; } = 90;
        public int ProximityChatWhisperBlockRange { get; set; } = 5;
        public int ProximityChatSignBlockRange { get; set; } = 15;
    } 
}
