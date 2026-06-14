using ProtoBuf;
using thebasics.ModSystems.HomeSpawn;

namespace thebasics.ModSystems.Teleportation;

[ProtoContract]
public sealed class TeleportBackEntry
{
    [ProtoMember(1)]
    public HomeSpawnLocation Location { get; set; }

    [ProtoMember(2)]
    public long RecordedUtcTicks { get; set; }
}
