using System.Collections.Generic;
using System.ComponentModel;
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class CharacterSheetViewMessage
{
    public const string ErrorCodeDisabled = "disabled";

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

    /// <summary>
    /// The target player's current headshot metadata, when one exists and the viewer can see it.
    /// Bytes are not embedded here — clients fetch them lazily via HeadshotFetchRequest using the hash.
    /// </summary>
    [ProtoMember(14)]
    public HeadshotMetadata Headshot { get; set; }

    /// <summary>
    /// True when the viewer is permitted to upload/replace the target's headshot.
    /// </summary>
    [ProtoMember(15)]
    public bool CanEditHeadshot { get; set; }

    [ProtoMember(16)]
    public string ErrorCode { get; set; } = string.Empty;
}
