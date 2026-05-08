using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterRecord
{
    [ProtoMember(1)]
    public string CharacterId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string DisplayName { get; set; } = string.Empty;

    [ProtoMember(3)]
    public bool Archived { get; set; }

    [ProtoMember(4)]
    public RpCharacterProjectionSnapshot Projection { get; set; } = new RpCharacterProjectionSnapshot();

    [ProtoMember(5)]
    public string CreatedUtc { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string ModifiedUtc { get; set; } = string.Empty;
}
