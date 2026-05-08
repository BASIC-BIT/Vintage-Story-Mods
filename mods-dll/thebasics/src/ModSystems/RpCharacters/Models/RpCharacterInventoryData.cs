using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterInventoryData
{
    [ProtoMember(1)]
    public string ClassName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public byte[] TreeData { get; set; }
}
