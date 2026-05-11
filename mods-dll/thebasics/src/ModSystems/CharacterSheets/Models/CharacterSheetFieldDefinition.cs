using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.CharacterSheets.Models;

[ProtoContract]
public class CharacterSheetFieldDefinition
{
    [ProtoMember(1)]
    public string Id { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Label { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Type { get; set; } = CharacterSheetFieldTypes.String;

    [ProtoMember(4)]
    public bool Optional { get; set; } = true;

    [ProtoMember(5)]
    public IList<string> Options { get; set; } = new List<string>();

    [ProtoMember(6)]
    public string BindTo { get; set; } = string.Empty;

    [ProtoMember(7)]
    public int MaxLength { get; set; }

    [ProtoMember(8)]
    public string Visibility { get; set; } = CharacterSheetFieldVisibilities.Public;

    [ProtoMember(9)]
    public bool ShowInLook { get; set; } = true;

    [ProtoMember(10)]
    public int EditorRows { get; set; }

    /// <summary>
    /// Where the field renders in the bio dialog. See <see cref="CharacterSheetLayoutSections"/>.
    /// Empty defaults to body (the scrollable section).
    /// </summary>
    [ProtoMember(11)]
    public string LayoutSection { get; set; } = CharacterSheetLayoutSections.Body;
}
