using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

public enum SetupWizardResultKind
{
    Invitation,
    Open,
    SaveResult,
    Denied
}

[ProtoContract]
public sealed class TheBasicsSetupWizardResultMessage
{
    [ProtoMember(1)] public SetupWizardResultKind Kind { get; set; }
    [ProtoMember(2)] public long RequestId { get; set; }
    [ProtoMember(3)] public string RunId { get; set; }
    [ProtoMember(4)] public bool Success { get; set; }
    [ProtoMember(5)] public string Message { get; set; }
    [ProtoMember(6)] public List<ConfigAdminSettingValue> Values { get; set; } = new();
    [ProtoMember(7)] public List<string> RestartRequiredKeys { get; set; } = new();
    [ProtoMember(8)] public bool IsDedicated { get; set; }
    [ProtoMember(9)] public List<string> ConflictKeys { get; set; } = new();
    [ProtoMember(10)] public bool RuntimeApplyFailed { get; set; }
}
