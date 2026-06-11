using ProtoBuf;

namespace thebasics.ModSystems.HomeSpawn;

[ProtoContract]
public class HomeSpawnHomeEntry
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public HomeSpawnLocation Location { get; set; }
}
