using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsLanguageConfigOpenMessage
{
    [ProtoMember(1)]
    public List<LanguageConfigEntryMessage> Languages { get; set; } = new();

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public bool Success { get; set; } = true;
}
