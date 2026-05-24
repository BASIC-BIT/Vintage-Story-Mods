using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class AnalyticsConsentChoiceMessage
{
    [ProtoMember(1)]
    public string ConsentLevel { get; set; }
}
