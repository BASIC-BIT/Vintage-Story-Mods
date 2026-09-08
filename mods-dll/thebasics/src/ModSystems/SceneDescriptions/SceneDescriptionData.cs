using System;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.SceneDescriptions;

public enum SceneDescriptionKind
{
    Environmental,
    OocNotice,
}

public sealed class SceneDescriptionData
{
    public const int MaxTitleLength = 80;
    public const int MaxBodyLength = 4096;

    internal const string TitleAttribute = "sceneTitle";
    internal const string BodyAttribute = "sceneBody";
    internal const string KindAttribute = "sceneKind";
    internal const string AuthorUidAttribute = "sceneAuthorUid";
    internal const string AuthorNameAttribute = "sceneAuthorName";
    internal const string LockItemAttribute = "sceneLockItem";

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public SceneDescriptionKind Kind { get; set; } = SceneDescriptionKind.Environmental;

    public string AuthorUid { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string LockItemCode { get; set; } = string.Empty;

    public bool IsLocked => !string.IsNullOrWhiteSpace(LockItemCode);

    internal bool CanEdit(string playerUid, bool hasClaimAccess) =>
        !string.IsNullOrWhiteSpace(playerUid) && hasClaimAccess && !IsLocked;

    internal bool CanLock(string playerUid, bool hasClaimAccess) =>
        CanEdit(playerUid, hasClaimAccess) && playerUid == AuthorUid;

    internal bool CanUnlock(string playerUid, bool isAdmin, bool hasClaimAccess) =>
        IsLocked && CanManage(playerUid, isAdmin, hasClaimAccess);

    internal bool CanBreak(string playerUid, bool isAdmin, bool hasClaimAccess) =>
        !string.IsNullOrWhiteSpace(playerUid) && hasClaimAccess && (!IsLocked || CanManage(playerUid, isAdmin, hasClaimAccess));

    private bool CanManage(string playerUid, bool isAdmin, bool hasClaimAccess) =>
        !string.IsNullOrWhiteSpace(playerUid) && hasClaimAccess && (isAdmin || playerUid == AuthorUid);

    internal void EstablishCreator(string playerUid, string playerName)
    {
        if (string.IsNullOrWhiteSpace(AuthorUid) && !string.IsNullOrWhiteSpace(playerUid))
        {
            AuthorUid = playerUid;
            AuthorName = playerName ?? string.Empty;
        }
    }

    internal void ApplyText(SceneDescriptionData edited)
    {
        Title = edited.Title;
        Body = edited.Body;
        Kind = edited.Kind;
        Normalize();
    }

    public SceneDescriptionData Normalize()
    {
        Title = NormalizeText(Title, MaxTitleLength, singleLine: true);
        Body = NormalizeText(Body, MaxBodyLength, singleLine: false);
        AuthorUid = NormalizeText(AuthorUid, 128, singleLine: true);
        AuthorName = NormalizeText(AuthorName, 128, singleLine: true);
        LockItemCode = NormalizeText(LockItemCode, 256, singleLine: true);

        if (!Enum.IsDefined(Kind))
        {
            Kind = SceneDescriptionKind.Environmental;
        }

        return this;
    }

    public SceneDescriptionData Clone()
    {
        return new SceneDescriptionData
        {
            Title = Title,
            Body = Body,
            Kind = Kind,
            AuthorUid = AuthorUid,
            AuthorName = AuthorName,
            LockItemCode = LockItemCode,
        };
    }

    internal void WriteTo(ITreeAttribute attributes)
    {
        Normalize();
        attributes.SetString(TitleAttribute, Title);
        attributes.SetString(BodyAttribute, Body);
        attributes.SetInt(KindAttribute, (int)Kind);
        attributes.SetString(AuthorUidAttribute, AuthorUid);
        attributes.SetString(AuthorNameAttribute, AuthorName);
        attributes.SetString(LockItemAttribute, LockItemCode);
    }

    internal static SceneDescriptionData ReadFrom(ITreeAttribute attributes)
    {
        if (attributes == null)
        {
            return new SceneDescriptionData();
        }

        return new SceneDescriptionData
        {
            Title = attributes.GetString(TitleAttribute, string.Empty),
            Body = attributes.GetString(BodyAttribute, string.Empty),
            Kind = (SceneDescriptionKind)attributes.GetInt(KindAttribute, (int)SceneDescriptionKind.Environmental),
            AuthorUid = attributes.GetString(AuthorUidAttribute, string.Empty),
            AuthorName = attributes.GetString(AuthorNameAttribute, string.Empty),
            LockItemCode = attributes.GetString(LockItemAttribute, string.Empty),
        }.Normalize();
    }

    private static string NormalizeText(string value, int maxLength, bool singleLine)
    {
        value = (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (singleLine)
        {
            value = value.Replace('\n', ' ');
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
