using ProtoBuf;

namespace thebasics.Configs
{
    [ProtoContract]
    public class ChatDelimiters
    {
        [ProtoAfterDeserialization]
        private void OnDeserialized()
        {
            InitializeDefaultsIfNeeded();
        }

        public void InitializeDefaultsIfNeeded()
        {
            Bold ??= new ChatDelimiter("+", "+");
            Italic ??= new ChatDelimiter("|", "|");
            Emote ??= new ChatDelimiter("*");
            Environmental ??= new ChatDelimiter("!");
            OOC ??= new ChatDelimiter("(", ")");
            GlobalOOC ??= new ChatDelimiter("((", "))");
            Quote ??= new ChatDelimiter("\"", "\"");
            SignLanguageQuote ??= new ChatDelimiter("'", "'");
        }

        [ProtoMember(1)]
        public ChatDelimiter Bold { get; set; }

        [ProtoMember(2)]
        public ChatDelimiter Italic { get; set; }

        [ProtoMember(3)]
        public ChatDelimiter Emote { get; set; }

        [ProtoMember(4)]
        public ChatDelimiter Environmental { get; set; }

        [ProtoMember(5)]
        public ChatDelimiter OOC { get; set; }

        [ProtoMember(6)]
        public ChatDelimiter GlobalOOC { get; set; }

        [ProtoMember(7)]
        public ChatDelimiter Quote { get; set; }

        [ProtoMember(8)]
        public ChatDelimiter SignLanguageQuote { get; set; }
    }
}