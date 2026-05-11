using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class HeadshotMetadata
{
    [ProtoMember(1)]
    public string Hash { get; set; } = string.Empty;

    [ProtoMember(2)]
    public long UpdatedAtUnixMs { get; set; }

    [ProtoMember(3)]
    public int Width { get; set; }

    [ProtoMember(4)]
    public int Height { get; set; }

    [ProtoMember(5)]
    public int ByteLength { get; set; }
}
