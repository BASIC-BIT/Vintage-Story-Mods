using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterExtensionSnapshot
{
    [ProtoMember(1)]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    public byte[] Data { get; set; }
}
