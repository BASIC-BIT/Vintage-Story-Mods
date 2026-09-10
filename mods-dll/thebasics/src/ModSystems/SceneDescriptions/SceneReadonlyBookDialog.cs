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
    private readonly long _stamp;
    private bool _read;

    internal SceneReadonlyBookDialog(ItemStack bookStack, ICoreClientAPI capi, BlockPos pos, long stamp) : base(bookStack, capi)
    {
        // The base constructor already composed once, before these fields existed.
        _pos = pos;
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

        var lineHeight = font.GetFontExtents().Height * font.LineHeightMultiplier / RuntimeEnv.GUIScale;
        var textBounds = ElementBounds.Fixed(0, 30, maxWidth, (maxLines + (Pages.Count > 1 ? 2 : 0)) * lineHeight + 1);
        var prevBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textBounds, 23).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
        var pageBounds = ElementBounds.FixedSize(80, 30).FixedUnder(textBounds, 33).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);
        var nextBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textBounds, 23).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
        var closeBounds = ElementBounds.FixedSize(0, 0).FixedUnder(prevBounds, 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
        var markBounds = ElementBounds.FixedSize(0, 0).FixedUnder(nextBounds, 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
        var backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        backgroundBounds.BothSizing = ElementSizing.FitToChildren;
        backgroundBounds.WithChildren(closeBounds);

        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-reader", ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0))
            .AddShadedDialogBG(backgroundBounds)
            .AddDialogTitleBar(Title, () => TryClose())
            .BeginChildElements(backgroundBounds)
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

    private bool OnToggleRead()
    {
        _read = !_read;
        capi.Network.SendBlockEntityPacket(_pos, _read ? SceneDescriptionBlockEntity.MarkReadPacketId : SceneDescriptionBlockEntity.MarkUnreadPacketId);
        // Optimistic: the server's reply carries the authoritative stamp for the client cache.
        SceneReadMarks.SetClientMark(_pos, _read ? _stamp : 0);
        SingleComposer.GetButton("markread").Text = ReadButtonLabel();
        SingleComposer.ReCompose();
        return true;
    }
}
