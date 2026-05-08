using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ColorOverrideEntry
{
    [ProtoMember(1)]
    public string Key { get; set; }

    [ProtoMember(2)]
    public string Color { get; set; }
}
