using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class AnalyticsConsentPromptMessage
{
    [ProtoMember(1)]
    public string CurrentConsentLevel { get; set; }

    [ProtoMember(2)]
    public int ConsentVersion { get; set; }

    [ProtoMember(3)]
    public string CommandName { get; set; }
}
