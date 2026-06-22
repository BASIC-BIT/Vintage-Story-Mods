using ProtoBuf;

namespace BasicConfig;

[ProtoContract]
public sealed class BasicConfigSettingValue
{
    [ProtoMember(1)]
    public string Key { get; set; }

    [ProtoMember(2)]
    public string Value { get; set; }
}
