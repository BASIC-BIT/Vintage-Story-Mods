using ProtoBuf;

namespace thebasics.ModSystems.Notes.Models;

[ProtoContract]
public class PersonalNoteLedgerMessage
{
    [ProtoMember(1)]
    public string AuthorPlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string TargetPlayerName { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string TargetCharacterId { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string TargetCharacterName { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string UpdatedUtc { get; set; } = string.Empty;

    [ProtoMember(7)]
    public string Text { get; set; } = string.Empty;
}
