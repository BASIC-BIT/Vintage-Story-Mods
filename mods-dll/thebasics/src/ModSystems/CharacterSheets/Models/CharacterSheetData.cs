using System.Collections.Generic;
using ProtoBuf;
using thebasics.Models;

namespace thebasics.ModSystems.CharacterSheets.Models;

[ProtoContract]
public class CharacterSheetData
{
    [ProtoMember(1)]
    public List<CharacterSheetStoredField> Fields { get; set; } = new List<CharacterSheetStoredField>();

    /// <summary>
    /// Metadata for the player's headshot image. The actual bytes live in a side file
    /// keyed by player+character; this metadata is what travels with the sheet (and so
    /// follows RP character switches automatically).
    /// </summary>
    [ProtoMember(2)]
    public HeadshotMetadata Headshot { get; set; }
}

[ProtoContract]
public class CharacterSheetStoredField
{
    [ProtoMember(1)]
    public string FieldId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Value { get; set; } = string.Empty;
}
