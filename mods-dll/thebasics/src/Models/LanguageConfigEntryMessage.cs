using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public record LanguageConfigEntryMessage
{
    [ProtoMember(1)]
    public string OriginalName { get; set; }

    [ProtoMember(2)]
    public string Name { get; set; }

    [ProtoMember(3)]
    public string Description { get; set; }

    [ProtoMember(4)]
    public string Prefix { get; set; }

    [ProtoMember(5)]
    public string Syllables { get; set; }

    [ProtoMember(6)]
    public string Color { get; set; }

    [ProtoMember(7)]
    public bool Default { get; set; }

    [ProtoMember(8)]
    public bool Hidden { get; set; }

    [ProtoMember(9)]
    public string GrantedToClasses { get; set; }

    [ProtoMember(10)]
    public string GrantedToModels { get; set; }

    [ProtoMember(11)]
    public string GrantedToModelGroups { get; set; }

    [ProtoMember(12)]
    public string GrantedToTraits { get; set; }
}
