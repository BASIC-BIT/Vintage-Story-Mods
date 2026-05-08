using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterInventorySnapshot
{
    [ProtoMember(1)]
    public List<RpCharacterInventoryData> Inventories { get; set; } = new List<RpCharacterInventoryData>();

    [ProtoMember(2)]
    public int ActiveHotbarSlotNumber { get; set; }

    [ProtoMember(3)]
    public bool Available { get; set; }
}
