using ProtoBuf;

namespace thebasics.Configs
{
    [ProtoContract]
    public class ChatDelimiter
    {
        [ProtoMember(1)]
        public string Start { get; set; } = "";

        [ProtoMember(2)]
        public string End { get; set; } = "";

        public ChatDelimiter() { }

        public ChatDelimiter(string start, string end = "")
        {
            Start = start;
            End = end;
        }

        public bool HasEnd => !string.IsNullOrEmpty(End);
    }
}