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

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public SceneDescriptionKind Kind { get; set; } = SceneDescriptionKind.Environmental;

    public string AuthorUid { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public SceneDescriptionData Normalize()
    {
        Title = NormalizeText(Title, MaxTitleLength, singleLine: true);
        Body = NormalizeText(Body, MaxBodyLength, singleLine: false);
        AuthorUid = NormalizeText(AuthorUid, 128, singleLine: true);
        AuthorName = NormalizeText(AuthorName, 128, singleLine: true);

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
