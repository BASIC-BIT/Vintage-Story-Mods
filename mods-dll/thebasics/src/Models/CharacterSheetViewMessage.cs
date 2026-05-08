using System.Collections.Generic;
using System.ComponentModel;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetViewMessage
{
    [ProtoMember(1)]
    [DefaultValue(true)]
    public bool Success { get; set; } = true;

    [ProtoMember(2)]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Title { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string TargetPlayerName { get; set; } = string.Empty;

    [ProtoMember(6)]
    public bool CanEdit { get; set; }

    [ProtoMember(7)]
    public bool IsAdminView { get; set; }

    [ProtoMember(8)]
    public bool IsLookView { get; set; }

    [ProtoMember(9)]
    public IList<CharacterSheetFieldViewMessage> Fields { get; set; } = new List<CharacterSheetFieldViewMessage>();

    [ProtoMember(10)]
    public bool IsSaveResponse { get; set; }

    [ProtoMember(11)]
    public string DisplayName { get; set; } = string.Empty;

    [ProtoMember(12)]
    public bool IsErrorResponse { get; set; }

    [ProtoMember(13)]
    public bool SuppressDialogOpen { get; set; }
}
