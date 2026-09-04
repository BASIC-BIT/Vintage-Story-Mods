using ProtoBuf;

namespace thebasics.ModSystems.SceneDescriptions;

[ProtoContract]
public sealed class SceneDescriptionEditPacket
{
    [ProtoMember(1)]
    public string Title { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Body { get; set; } = string.Empty;

    [ProtoMember(3)]
    public int Kind { get; set; }
}
