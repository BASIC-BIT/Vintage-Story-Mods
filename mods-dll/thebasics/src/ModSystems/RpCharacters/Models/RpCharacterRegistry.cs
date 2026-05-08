using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterRegistry
{
    [ProtoMember(1)]
    public int Version { get; set; } = 1;

    [ProtoMember(2)]
    public List<RpCharacterRecord> Characters { get; set; } = new List<RpCharacterRecord>();
}
