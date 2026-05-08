using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ConfigAdminSettingValue
{
    [ProtoMember(1)]
    public string Key { get; set; }

    [ProtoMember(2)]
    public string Value { get; set; }
}
