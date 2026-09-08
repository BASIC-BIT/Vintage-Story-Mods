using System;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.SceneDescriptions;

public enum SceneDescriptionKind
{
    Environmental,
    OocNotice,
}

public enum SceneDescriptionDisplay { WhenTargeted, AlwaysNearby, OnInteraction }

public enum SceneMarkerAppearance { Stone, Model, Billboard, Hybrid }
public enum SceneMarkerSymbol { Exclamation, Question, Information }
public enum SceneMarkerColor { Gold, Parchment, Blue, Green }

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
    public SceneDescriptionDisplay Display { get; set; }

    public string AuthorUid { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string LockItemCode { get; set; } = string.Empty;

    public SceneMarkerAppearance Appearance { get; set; } = SceneMarkerAppearance.Billboard;
    public SceneMarkerSymbol Symbol { get; set; }
    public float IconDistance { get; set; } = 24;
    public bool UnlimitedIconDistance { get; set; }
    public float TextDistance { get; set; } = 8;
    public bool UnlimitedTextDistance { get; set; }
    public float HeightOffset { get; set; }
    public SceneMarkerColor Color { get; set; }

    public float GetTextOpacity(double distance) => GetOpacity(distance, TextDistance, UnlimitedTextDistance);

    public float GetIconOpacity(double distance)
        => GetOpacity(distance, IconDistance, UnlimitedIconDistance);

    private static float GetOpacity(double distance, float range, bool unlimited)
    {
        if (!double.IsFinite(distance) || distance < 0) return 0;
        if (unlimited) return 1;
        // Fade through the outer quarter of the configured radius.
        return (float)Math.Clamp((range - distance) / (range * 0.25), 0, 1);
    }

    public bool ShouldShowDescription(bool targeted) => !string.IsNullOrWhiteSpace(Body) &&
        (Display == SceneDescriptionDisplay.AlwaysNearby || (Display == SceneDescriptionDisplay.WhenTargeted && targeted));

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
        Display = edited.Display;
        Appearance = edited.Appearance;
        Symbol = edited.Symbol;
        IconDistance = edited.IconDistance;
        UnlimitedIconDistance = edited.UnlimitedIconDistance;
        TextDistance = edited.TextDistance;
        UnlimitedTextDistance = edited.UnlimitedTextDistance;
        HeightOffset = edited.HeightOffset;
        Color = edited.Color;
        Normalize();
    }

    public SceneDescriptionData Normalize()
    {
        Title = NormalizeText(Title, MaxTitleLength, singleLine: true);
        Body = NormalizeText(Body, MaxBodyLength, singleLine: false);
        AuthorUid = NormalizeText(AuthorUid, 128, singleLine: true);
        AuthorName = NormalizeText(AuthorName, 128, singleLine: true);
        LockItemCode = NormalizeText(LockItemCode, 256, singleLine: true);
        // Retired experimental modes migrate to the billboard without changing content or ownership.
        Appearance = SceneMarkerAppearance.Billboard;
        if (!Enum.IsDefined(Display)) Display = SceneDescriptionDisplay.WhenTargeted;
        if (!Enum.IsDefined(Symbol)) Symbol = SceneMarkerSymbol.Exclamation;
        IconDistance = float.IsFinite(IconDistance) ? Math.Clamp(IconDistance, 1, 1024) : 24;
        TextDistance = float.IsFinite(TextDistance) ? Math.Clamp(TextDistance, 1, 1024) : 8;
        HeightOffset = float.IsFinite(HeightOffset) ? Math.Clamp(HeightOffset, -0.5f, 4) : 0;
        if (!Enum.IsDefined(Color)) Color = SceneMarkerColor.Gold;

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
            Display = Display,
            AuthorUid = AuthorUid,
            AuthorName = AuthorName,
            LockItemCode = LockItemCode,
            Appearance = Appearance,
            Symbol = Symbol,
            IconDistance = IconDistance,
            UnlimitedIconDistance = UnlimitedIconDistance,
            TextDistance = TextDistance,
            UnlimitedTextDistance = UnlimitedTextDistance,
            HeightOffset = HeightOffset,
            Color = Color,
        };
    }

    internal SceneDescriptionData AppearanceDefaults() => new SceneDescriptionData
    {
        Symbol = Symbol, Color = Color, Display = Display, IconDistance = IconDistance,
        UnlimitedIconDistance = UnlimitedIconDistance, TextDistance = TextDistance,
        UnlimitedTextDistance = UnlimitedTextDistance, HeightOffset = HeightOffset,
    }.Normalize();

    internal void WriteTo(ITreeAttribute attributes)
    {
        Normalize();
        attributes.SetString(TitleAttribute, Title);
        attributes.SetString(BodyAttribute, Body);
        attributes.SetInt(KindAttribute, (int)Kind);
        attributes.SetInt("sceneDisplay", (int)Display);
        attributes.SetString(AuthorUidAttribute, AuthorUid);
        attributes.SetString(AuthorNameAttribute, AuthorName);
        attributes.SetString(LockItemAttribute, LockItemCode);
        attributes.SetInt("sceneAppearance", (int)Appearance);
        attributes.SetInt("sceneSymbol", (int)Symbol);
        attributes.SetFloat("sceneIconDistance", IconDistance);
        attributes.SetBool("sceneIconUnlimited", UnlimitedIconDistance);
        attributes.SetFloat("sceneTextDistance", TextDistance);
        attributes.SetBool("sceneTextUnlimited", UnlimitedTextDistance);
        attributes.SetFloat("sceneHeightOffset", HeightOffset);
        attributes.SetInt("sceneColor", (int)Color);
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
            Display = (SceneDescriptionDisplay)attributes.GetInt("sceneDisplay"),
            Kind = (SceneDescriptionKind)attributes.GetInt(KindAttribute, (int)SceneDescriptionKind.Environmental),
            AuthorUid = attributes.GetString(AuthorUidAttribute, string.Empty),
            AuthorName = attributes.GetString(AuthorNameAttribute, string.Empty),
            LockItemCode = attributes.GetString(LockItemAttribute, string.Empty),
            Appearance = (SceneMarkerAppearance)attributes.GetInt("sceneAppearance"),
            Symbol = (SceneMarkerSymbol)attributes.GetInt("sceneSymbol"),
            IconDistance = attributes.GetFloat("sceneIconDistance", 24),
            UnlimitedIconDistance = attributes.GetBool("sceneIconUnlimited"),
            TextDistance = attributes.GetFloat("sceneTextDistance", attributes.GetFloat("sceneIconDistance", 24)),
            UnlimitedTextDistance = attributes.GetBool("sceneTextUnlimited", attributes.GetBool("sceneIconUnlimited")),
            HeightOffset = attributes.GetFloat("sceneHeightOffset"),
            Color = (SceneMarkerColor)attributes.GetInt("sceneColor"),
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
