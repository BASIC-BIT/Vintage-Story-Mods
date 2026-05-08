using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsLanguageConfigOpenRequest
{
    [ProtoMember(1)]
    public bool Request { get; set; } = true;
}
