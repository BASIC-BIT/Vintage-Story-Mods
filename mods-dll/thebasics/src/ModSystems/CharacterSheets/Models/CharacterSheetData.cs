using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.CharacterSheets.Models;

[ProtoContract]
public class CharacterSheetData
{
    [ProtoMember(1)]
    public IDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
}
