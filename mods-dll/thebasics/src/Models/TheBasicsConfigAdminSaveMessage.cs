using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsConfigAdminSaveMessage
{
    [ProtoMember(1)]
    public List<ConfigAdminSettingValue> Values { get; set; } = new();

    [ProtoMember(2)]
    public List<string> MarkReviewedKeys { get; set; } = new();

    [ProtoMember(3)]
    public bool ReloadFromDisk { get; set; }
}
