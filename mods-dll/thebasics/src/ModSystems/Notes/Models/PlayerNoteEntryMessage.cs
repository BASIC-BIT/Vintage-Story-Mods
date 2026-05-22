using ProtoBuf;

namespace thebasics.ModSystems.Notes.Models;

[ProtoContract]
public class PlayerNoteEntryMessage
{
    [ProtoMember(1)]
    public string Id { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Kind { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string AuthorPlayerUid { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string AuthorName { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string TargetPlayerName { get; set; } = string.Empty;

    [ProtoMember(7)]
    public string TargetCharacterId { get; set; } = string.Empty;

    [ProtoMember(8)]
    public string TargetCharacterName { get; set; } = string.Empty;

    [ProtoMember(9)]
    public string CreatedUtc { get; set; } = string.Empty;

    [ProtoMember(10)]
    public string UpdatedUtc { get; set; } = string.Empty;

    [ProtoMember(11)]
    public string Text { get; set; } = string.Empty;

    [ProtoMember(12)]
    public string Title { get; set; } = string.Empty;
}
