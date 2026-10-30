using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public sealed class ConfigAdminPrivilegeSuggestion
{
    [ProtoMember(1)] public string Code { get; set; }
    [ProtoMember(2)] public string Description { get; set; }
}
