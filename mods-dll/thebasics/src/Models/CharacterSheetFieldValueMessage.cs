using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetFieldValueMessage
{
    [ProtoMember(1)]
    public string FieldId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Value { get; set; } = string.Empty;
}
