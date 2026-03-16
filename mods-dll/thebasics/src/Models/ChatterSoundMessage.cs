using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ChatterSoundMessage
{
    [ProtoMember(1)]
    public long EntityId { get; set; }

    [ProtoMember(2)]
    public int TalkType { get; set; }

    [ProtoMember(3)]
    public int NoteCount { get; set; }

    [ProtoMember(4)]
    public float Volume { get; set; }

    [ProtoMember(5)]
    public float Pitch { get; set; }
}
