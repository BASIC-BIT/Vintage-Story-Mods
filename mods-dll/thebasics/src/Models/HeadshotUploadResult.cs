using System.ComponentModel;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotUploadResult
{
    [ProtoMember(1)]
    [DefaultValue(true)]
    public bool Success { get; set; } = true;

    [ProtoMember(2)]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(4)]
    public HeadshotMetadata Metadata { get; set; }
}
