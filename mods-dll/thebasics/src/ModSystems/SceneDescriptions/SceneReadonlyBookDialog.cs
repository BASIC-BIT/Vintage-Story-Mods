using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

/// <summary>
/// The vanilla book reader plus one button that marks the marker read or unread for this player.
/// </summary>
internal sealed class SceneReadonlyBookDialog : GuiDialogReadonlyBook
{
    private readonly BlockPos _pos;
    private readonly string _titleIconName;
    private long _stamp;
    private bool _read;

    internal BlockPos Pos => _pos;

    // A fresh reader is built per right-click, so drop it from the GUI manager and free its composers on close.
    public override bool UnregisterOnClose => true;

    internal SceneReadonlyBookDialog(ItemStack bookStack, ICoreClientAPI capi, BlockPos pos, long stamp, string titleIconName) : base(bookStack, capi)
    {
        // The base constructor already composed once, before these fields existed.
        _pos = pos;
        _titleIconName = titleIconName ?? "";
        _stamp = stamp;
        _read = SceneReadMarks.IsRead(pos, stamp);
        Compose();
    }

    protected override void Compose()
    {
        if (_pos == null)
        {
            base.Compose();
            return;
        }

        var hasIcon = _titleIconName.Length > 0;
        var hasHeader = hasIcon || !string.IsNullOrEmpty(Title);
        var titleX = hasIcon ? 34 : 0;
        var iconBounds = ElementBounds.Fixed(0, 30, 28, 28);
        var titleBounds = ElementBounds.Fixed(titleX, 30, maxWidth - titleX, 30);
        var lineHeight = font.GetFontExtents().Height * font.LineHeightMultiplier / RuntimeEnv.GUIScale;
        var textBounds = ElementBounds.Fixed(0, hasHeader ? 70 : 30, maxWidth, (maxLines + (Pages.Count > 1 ? 2 : 0)) * lineHeight + 1);
        var prevBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textBounds, 23).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
        var pageBounds = ElementBounds.FixedSize(80, 30).FixedUnder(textBounds, 33).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);
        var nextBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textBounds, 23).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
        var closeBounds = ElementBounds.FixedSize(0, 0).FixedUnder(prevBounds, 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
        var markBounds = ElementBounds.FixedSize(0, 0).FixedUnder(nextBounds, 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
        var backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        backgroundBounds.BothSizing = ElementSizing.FitToChildren;
        backgroundBounds.WithChildren(closeBounds);

        // Assigning SingleComposer replaces the previous one without disposing it.
        SingleComposer?.Dispose();
        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-reader", ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0))
            .AddShadedDialogBG(backgroundBounds)
            .AddDialogTitleBar(Title, () => TryClose())
            .BeginChildElements(backgroundBounds)
            .AddIf(hasIcon)
            .AddSceneDrawing(iconBounds, (ctx, surface, _) => SceneTitleIcons.Draw(capi, ctx, surface, _titleIconName))
            .EndIf()
            .AddIf(hasHeader)
            .AddRichtext("<strong>" + VtmlUtils.EscapeVtml(Title ?? "") + "</strong>", font, titleBounds)
            .EndIf()
            .AddRichtext("", font, textBounds, "text")
            .AddIf(Pages.Count > 1)
            .AddSmallButton(Lang.Get("<"), PreviousPage, prevBounds)
            .EndIf()
            .AddDynamicText("1/1", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), pageBounds, "pageNum")
            .AddIf(Pages.Count > 1)
            .AddSmallButton(Lang.Get(">"), nextPage, nextBounds)
            .EndIf()
            .AddSmallButton(Lang.Get("Close"), () => TryClose(), closeBounds)
            .AddSmallButton(ReadButtonLabel(), OnToggleRead, markBounds, key: "markread")
            .EndChildElements()
            .Compose();
        updatePage();
    }

    private string ReadButtonLabel() => Lang.Get(_read ? "thebasics:scene-mark-unread" : "thebasics:scene-mark-read");

    private bool PreviousPage()
    {
        curPage = System.Math.Max(curPage - 1, 0);
        updatePage();
        return true;
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        Dispose();
    }

    private bool OnToggleRead()
    {
        // The button only relabels once the server confirms the mark; see Refresh.
        capi.Network.SendBlockEntityPacket(_pos, _read ? SceneDescriptionBlockEntity.MarkUnreadPacketId : SceneDescriptionBlockEntity.MarkReadPacketId);
        return true;
    }

    /// <summary>Re-reads the client mark cache after the server replied with the marker's current stamp (0 = unread).</summary>
    internal void Refresh(long stamp)
    {
        if (stamp != 0) _stamp = stamp; // A legacy marker gets its first stamp on first read.
        _read = SceneReadMarks.IsRead(_pos, _stamp);
        // Rebuild rather than relabel: the button bounds were fitted to the previous label.
        Compose();
    }
}
