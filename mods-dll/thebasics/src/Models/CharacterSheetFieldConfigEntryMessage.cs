using System.ComponentModel;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public record CharacterSheetFieldConfigEntryMessage
{
    [ProtoMember(1)]
    public string OriginalId { get; set; }

    [ProtoMember(2)]
    public string Id { get; set; }

    [ProtoMember(3)]
    public string Label { get; set; }

    [ProtoMember(4)]
    public string Description { get; set; }

    [ProtoMember(5)]
    public string Type { get; set; }

    [ProtoMember(6)]
    [DefaultValue(true)]
    public bool Optional { get; set; } = true;

    [ProtoMember(7)]
    public string Options { get; set; }

    [ProtoMember(8)]
    public string BindTo { get; set; }

    [ProtoMember(9)]
    public string MaxLength { get; set; }

    [ProtoMember(10)]
    public string Visibility { get; set; }

    [ProtoMember(11)]
    [DefaultValue(true)]
    public bool ShowInLook { get; set; } = true;

    [ProtoMember(12)]
    public string EditorRows { get; set; }

    [ProtoMember(13)]
    public string LayoutSection { get; set; }

    [ProtoMember(14)]
    public string Width { get; set; }

    [ProtoIgnore]
    public bool AutoGenerateId { get; set; }
}
