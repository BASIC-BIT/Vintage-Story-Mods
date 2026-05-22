using ProtoBuf;

namespace thebasics.ModSystems.Notes.Models;

[ProtoContract]
public class TheBasicsNotesOpenRequest
{
    [ProtoMember(1)]
    public string Scope { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string TargetQuery { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string TargetPlayerUid { get; set; } = string.Empty;
}
