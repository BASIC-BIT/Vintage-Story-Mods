using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotFetchRequest
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    /// <summary>
    /// The hash the client already has cached. The server sends bytes only when this hash
    /// differs from the current stored hash; otherwise it returns the same hash with null bytes.
    /// </summary>
    [ProtoMember(2)]
    public string ClientCachedHash { get; set; } = string.Empty;
}
