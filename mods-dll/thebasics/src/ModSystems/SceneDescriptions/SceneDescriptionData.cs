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
public enum SceneMarkerSymbol { Exclamation, Question, Information, Dot, Ring, Diamond }
public enum SceneMarkerEffect { Plain, Hologram }
public enum SceneMarkerColor { Gold, Parchment, Blue, Green, Red }

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
    internal const string ReadStampAttribute = "sceneReadStamp";

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public SceneDescriptionKind Kind { get; set; } = SceneDescriptionKind.Environmental;
    public SceneDescriptionDisplay Display { get; set; }

    public string AuthorUid { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string LockItemCode { get; set; } = string.Empty;

    /// <summary>
    /// Unix milliseconds of the last content change. Read marks are recorded against it, so a
    /// re-placed or re-edited marker reads as unread again for everyone. 0 means never stamped.
    /// </summary>
    public long ReadStamp { get; set; }

    /// <summary>Issues a fresh stamp, never repeating one this marker already carries.</summary>
    internal void Stamp() => ReadStamp = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ReadStamp + 1);

    public SceneMarkerAppearance Appearance { get; set; } = SceneMarkerAppearance.Billboard;
    public SceneMarkerSymbol Symbol { get; set; }
    public float IconDistance { get; set; } = 24;
    public bool UnlimitedIconDistance { get; set; }
    public float TextDistance { get; set; } = 8;
    public bool UnlimitedTextDistance { get; set; }
    public float HeightOffset { get; set; }
    public float IndicatorScale { get; set; } = 1;
    internal float SelectionHalfExtent => 0.44f * IndicatorScale + 0.05f;
    public SceneMarkerColor Color { get; set; }
    public SceneMarkerEffect Effect { get; set; }
    public bool IdleBobbing { get; set; } = true;
    public bool ShowBodyInBubble { get; set; }
    public float BubbleScale { get; set; } = 1;
    internal float BubbleRenderScale => BubbleScale;
    // Zero means none; 1-6 map to SceneMarkerSymbol values plus one.
    public int TitleIcon { get; set; }
    public string TitleIconName { get; set; } = string.Empty;
    internal (string Title, string Body, bool ShowBody, string Icon) BubbleContent => (Title, Body, ShowBodyInBubble, TitleIconName);

    internal static string NormalizeIconName(string value)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length > 256) return string.Empty;
        foreach (var ch in value)
            if (char.IsControl(ch)) return string.Empty;
        return value;
    }

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

    public bool ShouldShowDescription(bool targeted) => (!string.IsNullOrWhiteSpace(Title) || (ShowBodyInBubble && !string.IsNullOrWhiteSpace(Body))) &&
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
        IndicatorScale = edited.IndicatorScale;
        Color = edited.Color;
        Effect = edited.Effect;
        IdleBobbing = edited.IdleBobbing;
        ShowBodyInBubble = edited.ShowBodyInBubble;
        BubbleScale = edited.BubbleScale;
        TitleIcon = edited.TitleIcon;
        TitleIconName = edited.TitleIconName;
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
        IndicatorScale = float.IsFinite(IndicatorScale) ? Math.Clamp(IndicatorScale, 0.25f, 3) : 1;
        // Retain the retired effect field for save/wire compatibility; all indicators now use one style.
        Effect = SceneMarkerEffect.Plain;
        BubbleScale = float.IsFinite(BubbleScale) ? Math.Clamp(BubbleScale, 0.4f, 2) : 1;
        if (TitleIcon < 0 || TitleIcon > Enum.GetValues<SceneMarkerSymbol>().Length) TitleIcon = 0;
        TitleIconName = NormalizeIconName(TitleIconName);
        if (TitleIconName.Length == 0 && TitleIcon > 0) TitleIconName = "thebasics-scene-title-" + TitleIcon;
        TitleIcon = 0; // Consume the legacy selection once so clearing the name stays cleared.
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
            ReadStamp = ReadStamp,
            Appearance = Appearance,
            Symbol = Symbol,
            IconDistance = IconDistance,
            UnlimitedIconDistance = UnlimitedIconDistance,
            TextDistance = TextDistance,
            UnlimitedTextDistance = UnlimitedTextDistance,
            HeightOffset = HeightOffset, IndicatorScale = IndicatorScale,
            Color = Color, Effect = Effect, IdleBobbing = IdleBobbing, ShowBodyInBubble = ShowBodyInBubble, BubbleScale = BubbleScale, TitleIcon = TitleIcon, TitleIconName = TitleIconName,
        };
    }

    internal SceneDescriptionData AppearanceDefaults() => new SceneDescriptionData
    {
        Symbol = Symbol, Color = Color, Effect = Effect, IdleBobbing = IdleBobbing, ShowBodyInBubble = ShowBodyInBubble, BubbleScale = BubbleScale, TitleIcon = TitleIcon, TitleIconName = TitleIconName, Display = Display, IconDistance = IconDistance,
        UnlimitedIconDistance = UnlimitedIconDistance, TextDistance = TextDistance,
        UnlimitedTextDistance = UnlimitedTextDistance, HeightOffset = HeightOffset, IndicatorScale = IndicatorScale,
    }.Normalize();

    /// <summary>
    /// <paramref name="includeReadStamp"/> is opt-in: the block entity's own tree persists and syncs
    /// the stamp, but a picked-up item stack must not carry it, so re-placing clears every read mark.
    /// </summary>
    internal void WriteTo(ITreeAttribute attributes, bool includeReadStamp = false)
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
        attributes.SetFloat("sceneIndicatorScale", IndicatorScale);
        attributes.SetInt("sceneColor", (int)Color);
        attributes.SetInt("sceneEffect", (int)Effect);
        attributes.SetBool("sceneIdleBobbing", IdleBobbing);
        attributes.SetBool("sceneShowBodyInBubble", ShowBodyInBubble);
        attributes.SetFloat("sceneBubbleScale", BubbleScale);
        attributes.SetInt("sceneTitleIcon", TitleIcon);
        attributes.SetString("sceneTitleIconName", TitleIconName);
        if (includeReadStamp) attributes.SetLong(ReadStampAttribute, ReadStamp);
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
            ReadStamp = attributes.GetLong(ReadStampAttribute, 0),
            Appearance = (SceneMarkerAppearance)attributes.GetInt("sceneAppearance"),
            Symbol = (SceneMarkerSymbol)attributes.GetInt("sceneSymbol"),
            IconDistance = attributes.GetFloat("sceneIconDistance", 24),
            UnlimitedIconDistance = attributes.GetBool("sceneIconUnlimited"),
            TextDistance = attributes.GetFloat("sceneTextDistance", attributes.GetFloat("sceneIconDistance", 24)),
            UnlimitedTextDistance = attributes.GetBool("sceneTextUnlimited", attributes.GetBool("sceneIconUnlimited")),
            HeightOffset = attributes.GetFloat("sceneHeightOffset"),
            IndicatorScale = attributes.GetFloat("sceneIndicatorScale", 1),
            Color = (SceneMarkerColor)attributes.GetInt("sceneColor"),
            Effect = (SceneMarkerEffect)attributes.GetInt("sceneEffect"),
            IdleBobbing = attributes.GetBool("sceneIdleBobbing", true),
            ShowBodyInBubble = attributes.GetBool("sceneShowBodyInBubble"),
            BubbleScale = attributes.GetFloat("sceneBubbleScale", 1),
            TitleIcon = attributes.GetInt("sceneTitleIcon"),
            TitleIconName = attributes.GetString("sceneTitleIconName", string.Empty),
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
