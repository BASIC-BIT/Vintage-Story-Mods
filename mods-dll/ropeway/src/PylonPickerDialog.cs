using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// Client-side list of this tower's neighbours: the ones it is already linked to, then the ones it could
/// link to, each with distance and rope. Also where a tower gets its name.
/// </summary>
public sealed class PylonPickerDialog : GuiDialog
{
    private const double DialogWidth = 460;
    private const double ContentWidth = DialogWidth - 48;
    private const int MaxVisibleRows = 8;
    private const string NameInputKey = "towername";

    private readonly RopewayModSystem modSystem;

    private BlockPos fromTower;
    private string fromName;
    private string nameDraft = "";
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
        fromName = packet.FromName;
        nameDraft = packet.FromName ?? "";
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
        var nameLabel = ElementBounds.Fixed(0, contentTop, 90, 24);
        var nameInput = ElementBounds.Fixed(94, contentTop - 3, ContentWidth - 94 - 84, 28);
        var nameButton = ElementBounds.Fixed(ContentWidth - 78, contentTop - 3, 78, 28);
        var ropeBounds = ElementBounds.Fixed(0, contentTop + 36, ContentWidth, 24);
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
            .AddStaticText(Lang.Get("ropeway:dlg-picker-name"), CairoFont.WhiteSmallText(), nameLabel)
            .AddTextInput(nameInput, text => nameDraft = text, CairoFont.WhiteSmallText(), NameInputKey)
            .AddSmallButton(Lang.Get("ropeway:dlg-picker-name-save"), OnRename, nameButton)
            .AddInset(listBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddStaticText(Lang.Get("ropeway:dlg-picker-rope", ropeInInventory), CairoFont.WhiteSmallText(), ropeBounds);

        AddCandidateList(composer, listBounds);
        composer.AddSmallButton(Lang.Get("Close"), OnClose, ElementBounds.Fixed(ContentWidth - 120, buttonY, 110, 30));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        SingleComposer.GetTextInput(NameInputKey)?.SetValue(nameDraft);
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
            var name = DisplayName(candidate);

            string label;
            if (candidate.Linked)
            {
                // A linked row is not a candidate that failed - it is a connection, and clicking it cuts
                // that connection for the refund the label states. Different wording and a different button
                // style, so it cannot be misread as a broken link row.
                label = Lang.Get("ropeway:dlg-picker-row-linked", name, candidate.Distance, candidate.RopeCost);
            }
            else
            {
                label = Lang.Get("ropeway:dlg-picker-row", name, candidate.Distance, candidate.RopeCost);
                if (candidate.RopeCost > ropeInInventory) label = "[!] " + label;
            }

            var target = candidate;
            composer.AddSmallButton(
                label,
                () => Activate(target),
                ElementBounds.Fixed(x, y, width, 28),
                candidate.Linked ? EnumButtonStyle.Normal : EnumButtonStyle.Small,
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

    /// <summary>An unnamed tower is called by the bearing you would walk in to reach it, never "unnamed".</summary>
    private string DisplayName(TowerCandidate candidate)
    {
        if (!string.IsNullOrEmpty(candidate.Name)) return candidate.Name;
        if (fromTower == null || candidate.Pos == null) return Lang.Get("ropeway:dir-n");

        return Lang.Get(SpanMath.CompassKey(candidate.Pos.X - fromTower.X, candidate.Pos.Z - fromTower.Z));
    }

    private bool Activate(TowerCandidate candidate)
    {
        // A hint only - the server re-runs every rule before it charges or refunds anyone.
        var channel = capi.Network.GetChannel(RopewayModSystem.ChannelName);

        if (candidate.Linked)
        {
            channel.SendPacket(new TowerUnlinkRequest { FromTower = fromTower, ToTower = candidate.Pos });

            // Unlike a link, the server answers with a fresh list, so the picker stays open on the tower
            // you are standing at and shows what it is connected to now.
            return true;
        }

        channel.SendPacket(new TowerLinkRequest { FromTower = fromTower, ToTower = candidate.Pos });
        TryClose();
        return true;
    }

    private bool OnRename()
    {
        if (fromTower == null) return true;

        // Nothing to say when the text has not moved; the server would sanitise to the same string anyway.
        if (nameDraft != (fromName ?? ""))
        {
            capi.Network.GetChannel(RopewayModSystem.ChannelName)
                .SendPacket(new TowerRenameRequest { Tower = fromTower, Name = nameDraft });
        }

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
