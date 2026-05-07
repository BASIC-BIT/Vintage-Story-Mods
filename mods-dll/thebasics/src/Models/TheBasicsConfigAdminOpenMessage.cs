using System.Collections.Generic;
using ProtoBuf;
using thebasics.Configs;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsConfigAdminOpenMessage
{
    [ProtoMember(1)]
    public ModConfig Config { get; set; }

    [ProtoMember(2)]
    public List<ConfigAdminSettingValue> Values { get; set; } = new();

    [ProtoMember(3)]
    public List<string> ReviewedKeys { get; set; } = new();

    [ProtoMember(4)]
    public string StatusMessage { get; set; }
}
