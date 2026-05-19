using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsCharacterSheetFieldConfigSaveMessage
{
    [ProtoMember(1)]
    public List<CharacterSheetFieldConfigEntryMessage> Fields { get; set; } = new();

    [ProtoMember(2)]
    public bool ReloadFromDisk { get; set; }
}
