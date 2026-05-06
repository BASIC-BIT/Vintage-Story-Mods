using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetFieldViewMessage
{
    [ProtoMember(1)]
    public string FieldId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Label { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string Value { get; set; } = string.Empty;

    [ProtoMember(5)]
    public bool Optional { get; set; }

    [ProtoMember(6)]
    public int MaxLength { get; set; }

    [ProtoMember(7)]
    public IList<string> Options { get; set; } = new List<string>();

    [ProtoMember(8)]
    public bool CanEdit { get; set; }

    [ProtoMember(9)]
    public string Visibility { get; set; } = string.Empty;

    [ProtoMember(10)]
    public int EditorRows { get; set; }
}
