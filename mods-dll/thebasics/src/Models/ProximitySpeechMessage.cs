using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ProximitySpeechMessage
{
    [ProtoMember(1)]
    public string Text { get; set; }

    [ProtoMember(2)]
    public float Gain { get; set; }

    [ProtoMember(3)]
    public float Falloff { get; set; }
}
