using System.Collections.Generic;
using ProtoBuf;

namespace BasicConfig;

[ProtoContract]
public sealed class BasicConfigOpenMessage
{
    [ProtoMember(1)]
    public string ConfigId { get; set; }

    [ProtoMember(2)]
    public List<BasicConfigSettingValue> Values { get; set; } = new();

    [ProtoMember(3)]
    public List<string> ReviewedKeys { get; set; } = new();

    [ProtoMember(4)]
    public string StatusMessage { get; set; }
}

[ProtoContract]
public sealed class BasicConfigSaveMessage
{
    [ProtoMember(1)]
    public string ConfigId { get; set; }

    [ProtoMember(2)]
    public List<BasicConfigSettingValue> Values { get; set; } = new();

    [ProtoMember(3)]
    public List<string> MarkReviewedKeys { get; set; } = new();

    [ProtoMember(4)]
    public bool ReloadFromDisk { get; set; }
}

[ProtoContract]
public sealed class BasicConfigResultMessage
{
    [ProtoMember(1)]
    public string ConfigId { get; set; }

    [ProtoMember(2)]
    public bool Success { get; set; }

    [ProtoMember(3)]
    public string Message { get; set; }

    [ProtoMember(4)]
    public List<BasicConfigSettingValue> Values { get; set; } = new();

    [ProtoMember(5)]
    public List<string> ReviewedKeys { get; set; } = new();

    [ProtoMember(6)]
    public List<string> LiveAppliedKeys { get; set; } = new();

    [ProtoMember(7)]
    public List<string> RestartRequiredKeys { get; set; } = new();
}
