using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetSaveRequest
{
    [ProtoMember(1)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)]
    public bool IsAdminAction { get; set; }

    [ProtoMember(3)]
    public IList<CharacterSheetFieldValueMessage> Fields { get; set; } = new List<CharacterSheetFieldValueMessage>();
}
