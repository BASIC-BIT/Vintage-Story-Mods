using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>Client-side list of link-eligible towers with distance, rope cost and your rope count.</summary>
public sealed class PylonPickerDialog : GuiDialog
{
    private const double DialogWidth = 420;
    private const double ContentWidth = DialogWidth - 48;
    private const int MaxVisibleRows = 8;

    private readonly RopewayModSystem modSystem;

    private BlockPos fromTower;
    private int ropeInInventory;
    private List<TowerCandidate> candidates = new();
    private int scrollIndex;

    public PylonPickerDialog(ICoreClientAPI capi, RopewayModSystem modSystem) : base(capi)
    {
        this.modSystem = modSystem;
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.28;

    public void OnCandidates(TowerCandidatesResponse packet)
    {
        if (packet == null) return;

        fromTower = packet.FromTower;
        ropeInInventory = packet.RopeInInventory;
        candidates = packet.Candidates ?? new List<TowerCandidate>();
        scrollIndex = ClampInt(scrollIndex, 0, LastPageStart(candidates.Count, MaxVisibleRows));

        ComposeDialog();
        TryOpen();
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var ropeBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, 24);
        var listTop = ropeBounds.fixedY + ropeBounds.fixedHeight + 10;
        var listHeight = MaxVisibleRows * 34 + 40;
        var listBounds = ElementBounds.Fixed(0, listTop, ContentWidth, listHeight);
        var buttonY = listBounds.fixedY + listBounds.fixedHeight + 10;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, buttonY + 36).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("ropeway-picker", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("ropeway:dlg-picker-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(listBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddStaticText(Lang.Get("ropeway:dlg-picker-rope", ropeInInventory), CairoFont.WhiteSmallText(), ropeBounds);

        AddCandidateList(composer, listBounds);
        composer.AddSmallButton(Lang.Get("Close"), OnClose, ElementBounds.Fixed(ContentWidth - 120, buttonY, 110, 30));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
    }

    private void AddCandidateList(GuiComposer composer, ElementBounds panelBounds)
    {
        var x = panelBounds.fixedX + 8;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 16;

        if (candidates.Count == 0)
        {
            composer.AddStaticText(Lang.Get("ropeway:dlg-picker-empty"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 46));
            return;
        }

        scrollIndex = ClampInt(scrollIndex, 0, LastPageStart(candidates.Count, MaxVisibleRows));

        for (var i = scrollIndex; i < Math.Min(candidates.Count, scrollIndex + MaxVisibleRows); i++)
        {
            var candidate = candidates[i];
            var label = Lang.Get("ropeway:dlg-picker-row", candidate.Distance, candidate.RopeCost);
            if (candidate.RopeCost > ropeInInventory) label = "[!] " + label;

            composer.AddSmallButton(
                label,
                () => Link(candidate),
                ElementBounds.Fixed(x, y, width, 28),
                EnumButtonStyle.Small,
                "row-" + i);
            y += 34;
        }

        if (candidates.Count > MaxVisibleRows)
        {
            var controlsY = panelBounds.fixedY + panelBounds.fixedHeight - 32;
            composer.AddSmallButton("<", ScrollUp, ElementBounds.Fixed(x, controlsY, 36, 24), EnumButtonStyle.Small);
            composer.AddStaticText(
                PageLabel(scrollIndex, candidates.Count, MaxVisibleRows),
                CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(x + 44, controlsY + 3, 84, 24));
            composer.AddSmallButton(">", ScrollDown, ElementBounds.Fixed(x + 140, controlsY, 36, 24), EnumButtonStyle.Small);
        }
    }

    private bool Link(TowerCandidate candidate)
    {
        // A hint only - the server re-runs every rule before it charges anyone.
        capi.Network.GetChannel(RopewayModSystem.ChannelName).SendPacket(new TowerLinkRequest
        {
            FromTower = fromTower,
            ToTower = candidate.Pos
        });

        TryClose();
        return true;
    }

    private bool ScrollUp()
    {
        scrollIndex = Math.Max(0, scrollIndex - MaxVisibleRows);
        ComposeDialog();
        return true;
    }

    private bool ScrollDown()
    {
        scrollIndex = Math.Min(LastPageStart(candidates.Count, MaxVisibleRows), scrollIndex + MaxVisibleRows);
        ComposeDialog();
        return true;
    }

    private bool OnClose()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }

    private static string PageLabel(int currentOffset, int totalItems, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (totalItems + pageSize - 1) / pageSize);
        var currentPage = Math.Min(totalPages, currentOffset / pageSize + 1);
        return $"{currentPage}/{totalPages}";
    }

    private static int LastPageStart(int totalItems, int pageSize)
    {
        if (totalItems <= 0 || pageSize <= 0) return 0;
        return (totalItems - 1) / pageSize * pageSize;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
