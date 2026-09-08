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

    [ProtoMember(4)] public int Appearance { get; set; }
    [ProtoMember(5)] public int Symbol { get; set; }
    [ProtoMember(6)] public float IconDistance { get; set; } = 24;
    [ProtoMember(7)] public bool UnlimitedIconDistance { get; set; }
    [ProtoMember(8)] public bool IsLocked { get; set; }
    [ProtoMember(9)] public bool CanManageLock { get; set; }
    [ProtoMember(10)] public bool LockAfterSave { get; set; }
}
