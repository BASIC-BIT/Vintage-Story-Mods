using ProtoBuf;
using thebasics.Configs;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsConfigMessage
{
    [ProtoMember(1)]
    public int ProximityGroupId { get; set; }

    // Full config object instead of individual properties
    [ProtoMember(2)]
    public ModConfig Config { get; set; }

    [ProtoMember(4)]
    public bool? SceneMarkersRuntimeEnabled { get; set; }

    [ProtoMember(3)]
    public int? LastSelectedGroupId { get; set; }

    // 0 uses the server default, 1 opens the sheet, 2 keeps it closed.
    [ProtoMember(5)]
    public int CharacterSheetAutoOpenChoice { get; set; }
}
