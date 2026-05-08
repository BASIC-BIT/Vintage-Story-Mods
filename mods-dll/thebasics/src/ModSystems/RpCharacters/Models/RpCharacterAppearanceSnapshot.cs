using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterAppearanceSnapshot
{
    [ProtoMember(1)]
    public string CharacterClass { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<string> ExtraTraits { get; set; } = new List<string>();

    [ProtoMember(3)]
    public byte[] SkinConfig { get; set; }

    [ProtoMember(4)]
    public string VoiceType { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string VoicePitch { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string SkinModel { get; set; } = string.Empty;

    [ProtoMember(7)]
    public bool DidSelectSkin { get; set; }
}
