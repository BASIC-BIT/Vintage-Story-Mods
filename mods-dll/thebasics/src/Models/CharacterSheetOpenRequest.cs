using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetOpenRequest
{
    public const string ModeOwn = "own";
    public const string ModeView = "view";
    public const string ModeLook = "look";
    public const string ModeAdmin = "admin";

    [ProtoMember(1)]
    public string Mode { get; set; } = ModeOwn;

    [ProtoMember(2)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string TargetPlayerName { get; set; } = string.Empty;
}
