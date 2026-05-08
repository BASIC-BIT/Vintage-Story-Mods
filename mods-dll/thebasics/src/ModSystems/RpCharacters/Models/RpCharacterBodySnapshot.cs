using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterBodySnapshot
{
    [ProtoMember(1)]
    public RpCharacterHealthSnapshot Health { get; set; } = new RpCharacterHealthSnapshot();

    [ProtoMember(2)]
    public RpCharacterHungerSnapshot Hunger { get; set; } = new RpCharacterHungerSnapshot();

    [ProtoMember(3)]
    public float Intoxication { get; set; }

    [ProtoMember(4)]
    public float Psychedelic { get; set; }

    [ProtoMember(5)]
    public RpCharacterPositionSnapshot Position { get; set; } = new RpCharacterPositionSnapshot();

    [ProtoMember(6)]
    public RpCharacterPositionSnapshot PositionBeforeFalling { get; set; } = new RpCharacterPositionSnapshot();

    [ProtoMember(7)]
    public RpCharacterSpawnSnapshot Spawn { get; set; } = new RpCharacterSpawnSnapshot();

    [ProtoMember(8)]
    public int Deaths { get; set; }
}
