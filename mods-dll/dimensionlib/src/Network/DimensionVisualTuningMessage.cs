using ProtoBuf;

namespace DimensionLib.Network;

[ProtoContract]
public sealed class DimensionVisualTuningMessage
{
    [ProtoMember(1)]
    public bool Reset { get; set; }

    [ProtoMember(2)]
    public string PresetId { get; set; }

    [ProtoMember(3)]
    public string Key { get; set; }

    [ProtoMember(4)]
    public float Value { get; set; }

    [ProtoMember(5)]
    public bool Status { get; set; }
}
