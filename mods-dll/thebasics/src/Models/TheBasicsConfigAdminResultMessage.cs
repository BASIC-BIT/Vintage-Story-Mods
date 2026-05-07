using System.Collections.Generic;
using ProtoBuf;
using thebasics.Configs;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsConfigAdminResultMessage
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public ModConfig Config { get; set; }

    [ProtoMember(4)]
    public List<ConfigAdminSettingValue> Values { get; set; } = new();

    [ProtoMember(5)]
    public List<string> ReviewedKeys { get; set; } = new();

    [ProtoMember(6)]
    public List<string> LiveAppliedKeys { get; set; } = new();

    [ProtoMember(7)]
    public List<string> RestartRequiredKeys { get; set; } = new();
}
