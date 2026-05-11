using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsLanguageConfigSaveMessage
{
    [ProtoMember(1)]
    public List<LanguageConfigEntryMessage> Languages { get; set; } = new();

    [ProtoMember(2)]
    public bool ReloadFromDisk { get; set; }
}
