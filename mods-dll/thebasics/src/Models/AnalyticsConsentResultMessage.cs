using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class AnalyticsConsentResultMessage
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public string ConsentLevel { get; set; }
}
