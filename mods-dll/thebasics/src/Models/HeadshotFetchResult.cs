using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotFetchResult
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the server's current headshot for the target player. Empty/null when the player has none.
    /// </summary>
    [ProtoMember(2)]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Null when the client's cached hash already matches, or when no headshot exists.
    /// Otherwise contains the normalized PNG bytes.
    /// </summary>
    [ProtoMember(3)]
    public byte[] PngBytes { get; set; }

    [ProtoMember(4)]
    public int Width { get; set; }

    [ProtoMember(5)]
    public int Height { get; set; }
}
