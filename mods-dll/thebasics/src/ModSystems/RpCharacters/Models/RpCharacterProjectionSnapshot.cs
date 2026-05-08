using System.Collections.Generic;
using System.ComponentModel;
using ProtoBuf;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterProjectionSnapshot
{
    [ProtoMember(1)]
    public CharacterSheetData Sheet { get; set; } = new CharacterSheetData();

    [ProtoMember(2)]
    public string NicknameColor { get; set; }

    [ProtoMember(3)]
    public List<string> Languages { get; set; } = new List<string>();

    [ProtoMember(4)]
    public string DefaultLanguage { get; set; }

    [ProtoMember(5)]
    public ProximityChatMode ChatMode { get; set; } = ProximityChatMode.Normal;

    [ProtoMember(6)]
    [DefaultValue(true)]
    public bool ChatterEnabled { get; set; } = true;
}
