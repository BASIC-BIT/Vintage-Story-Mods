using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsCharacterSheetFieldConfigResultMessage
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public List<CharacterSheetFieldConfigEntryMessage> Fields { get; set; } = new();
}
