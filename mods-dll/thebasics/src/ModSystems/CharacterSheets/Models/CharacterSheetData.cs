using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.CharacterSheets.Models;

[ProtoContract]
public class CharacterSheetData
{
    [ProtoMember(1)]
    public List<CharacterSheetStoredField> Fields { get; set; } = new List<CharacterSheetStoredField>();
}

[ProtoContract]
public class CharacterSheetStoredField
{
    [ProtoMember(1)]
    public string FieldId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Value { get; set; } = string.Empty;
}
