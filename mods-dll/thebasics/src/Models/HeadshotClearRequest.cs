using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotClearRequest
{
    /// <summary>
    /// When set to a UID other than the sender's, the server treats this as an admin clear.
    /// </summary>
    [ProtoMember(1)]
    public string AdminTargetPlayerUid { get; set; } = string.Empty;
}
