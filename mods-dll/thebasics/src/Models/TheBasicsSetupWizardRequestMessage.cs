using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

public enum SetupWizardRequestKind
{
    Open,
    InviteStart,
    InviteDismiss,
    Save,
    Track,
    Closed
}

[ProtoContract]
public sealed class TheBasicsSetupWizardRequestMessage
{
    [ProtoMember(1)] public SetupWizardRequestKind Kind { get; set; }
    [ProtoMember(2)] public long RequestId { get; set; }
    [ProtoMember(3)] public string RunId { get; set; }
    [ProtoMember(4)] public List<ConfigAdminSettingValue> Values { get; set; } = new();
    [ProtoMember(5)] public List<ConfigAdminSettingValue> OriginalValues { get; set; } = new();
    [ProtoMember(6)] public string StepId { get; set; }
    [ProtoMember(7)] public string ChoiceId { get; set; }
    [ProtoMember(8)] public string JourneyAction { get; set; }
}
