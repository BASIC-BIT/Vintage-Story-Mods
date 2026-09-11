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
    [ProtoMember(23)] public string SymbolIconName { get; set; } = string.Empty;
    [ProtoMember(6)] public float IconDistance { get; set; } = 24;
    [ProtoMember(7)] public bool UnlimitedIconDistance { get; set; }
    [ProtoMember(8)] public bool IsLocked { get; set; }
    [ProtoMember(9)] public bool CanManageLock { get; set; }
    [ProtoMember(10)] public bool LockAfterSave { get; set; }
    [ProtoMember(11)] public int Display { get; set; }
    [ProtoMember(12)] public float TextDistance { get; set; } = 8;
    [ProtoMember(13)] public bool UnlimitedTextDistance { get; set; }
    [ProtoMember(14)] public float HeightOffset { get; set; }
    [ProtoMember(15)] public int Color { get; set; }
    [ProtoMember(20)] public float BubbleScale { get; set; } = 1;
    [ProtoMember(21)] public int TitleIcon { get; set; }
    [ProtoMember(22)] public string TitleIconName { get; set; } = string.Empty;
    [ProtoMember(19)] public bool ShowBodyInBubble { get; set; }
    [ProtoMember(18), System.ComponentModel.DefaultValue(true)] public bool IdleBobbing { get; set; } = true;
    [ProtoMember(17)] public int Effect { get; set; }
    [ProtoMember(16)] public float IndicatorScale { get; set; } = 1;
}
