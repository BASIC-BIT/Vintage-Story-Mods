using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotUploadRequest
{
    [ProtoMember(1)]
    public byte[] PngBytes { get; set; }

    /// <summary>
    /// When set by an admin, the upload targets this player instead of the sender.
    /// Server validates the admin privilege before honoring this.
    /// </summary>
    [ProtoMember(2)]
    public string AdminTargetPlayerUid { get; set; } = string.Empty;
}
