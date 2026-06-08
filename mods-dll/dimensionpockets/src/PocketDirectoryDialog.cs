using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PocketDimensions;

internal sealed class PocketDirectoryDialog : GuiDialog
{
    private const double DialogWidth = 760;
    private const double ContentWidth = DialogWidth - 48;
    private const double PanelHeight = 360;
    private const int MaxVisibleStacks = 10;
    private const int MaxVisibleLayers = 12;

    private readonly Action _onRefresh;
    private readonly Action<string> _onTeleport;
    private readonly Action<string, string, int, int> _onCreatePocket;
    private PocketDirectoryStateMessage _state = new PocketDirectoryStateMessage { Message = "Loading Pocket Directory..." };
    private string _selectedStackId;

    public PocketDirectoryDialog(ICoreClientAPI capi, Action onRefresh, Action<string> onTeleport, Action<string, string, int, int> onCreatePocket)
        : base(capi)
    {
        _onRefresh = onRefresh;
        _onTeleport = onTeleport;
        _onCreatePocket = onCreatePocket;
        ComposeDialog();
    }

    public override string ToggleKeyCombinationCode => "pocketdirectory";

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.28;

    public void SetState(PocketDirectoryStateMessage state)
    {
        _state = state ?? new PocketDirectoryStateMessage();
        if (_state.Stacks == null)
        {
            _state.Stacks = new List<PocketDirectoryStackMessage>();
        }

        if (string.IsNullOrWhiteSpace(_selectedStackId) || !_state.Stacks.Any(stack => string.Equals(stack.StackId, _selectedStackId, StringComparison.Ordinal)))
        {
            _selectedStackId = _state.Stacks.FirstOrDefault()?.StackId;
        }

        ComposeDialog();
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var helpBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, 42);
        var statusBounds = ElementBounds.Fixed(0, helpBounds.fixedY + helpBounds.fixedHeight + 6, ContentWidth, 24);
        var panelY = statusBounds.fixedY + statusBounds.fixedHeight + 10;
        var leftWidth = 250;
        var panelGap = 16;
        var rightX = leftWidth + panelGap;
        var rightWidth = ContentWidth - rightX;
        var leftPanelBounds = ElementBounds.Fixed(0, panelY, leftWidth, PanelHeight);
        var rightPanelBounds = ElementBounds.Fixed(rightX, panelY, rightWidth, PanelHeight);
        var buttonY = panelY + PanelHeight + 12;
        var bodyHeight = buttonY + 36;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("pocket-directory", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar("Pocket Directory", OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(leftPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddInset(rightPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddStaticText("Browse Pocket Dimensions spaces and teleport to layers your server permissions allow.", CairoFont.WhiteSmallText(), helpBounds)
            .AddStaticText(StatusText(), CairoFont.WhiteSmallText(), statusBounds);

        AddStackList(composer, leftPanelBounds);
        AddLayerList(composer, rightPanelBounds);
        composer.AddSmallButton("Refresh", OnRefresh, ElementBounds.Fixed(0, buttonY, 116, 30));
        if (_state.CanCreatePocket)
        {
            composer.AddSmallButton("New Pocket", OnCreatePocket, ElementBounds.Fixed(126, buttonY, 130, 30));
        }

        composer.AddSmallButton("Close", OnClose, ElementBounds.Fixed(ContentWidth - 120, buttonY, 110, 30));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
    }

    private string StatusText()
    {
        if (!string.IsNullOrWhiteSpace(_state.Message))
        {
            return _state.Success ? _state.Message : "Error: " + _state.Message;
        }

        var count = _state.Stacks?.Count ?? 0;
        if (count == 0)
        {
            return "No accessible pockets yet.";
        }

        var createText = _state.CanCreatePocket ? "You can create pockets." : "Pocket creation is not available to you.";
        return $"{count} accessible pocket stack{(count == 1 ? string.Empty : "s")}. {createText}";
    }

    private void AddStackList(GuiComposer composer, ElementBounds panelBounds)
    {
        var x = panelBounds.fixedX + 8;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 16;
        composer.AddStaticText("Pockets", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22));
        y += 30;

        var stacks = (_state.Stacks ?? new List<PocketDirectoryStackMessage>()).Take(MaxVisibleStacks).ToArray();
        if (stacks.Length == 0)
        {
            composer.AddStaticText("No pockets are visible to this player.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 46));
            return;
        }

        foreach (var stack in stacks)
        {
            var localStack = stack;
            var label = StackButtonText(localStack);
            composer.AddSmallButton(label, () => SelectStack(localStack.StackId), ElementBounds.Fixed(x, y, width, 28), EnumButtonStyle.Normal, "stack-" + Math.Abs((localStack.StackId ?? label).GetHashCode()));
            y += 34;
        }

        var hidden = (_state.Stacks?.Count ?? 0) - stacks.Length;
        if (hidden > 0)
        {
            composer.AddStaticText($"+ {hidden} more hidden by this first UI pass", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22));
        }
    }

    private void AddLayerList(GuiComposer composer, ElementBounds panelBounds)
    {
        var stack = SelectedStack();
        var x = panelBounds.fixedX + 10;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 20;
        composer.AddStaticText(stack?.DisplayName ?? "Layers", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22));
        y += 28;

        if (stack == null)
        {
            composer.AddStaticText("Select a pocket to view its layers.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 42));
            return;
        }

        var owner = string.IsNullOrWhiteSpace(stack.OwnerPlayerName) ? "Server/global" : stack.OwnerPlayerName;
        composer.AddStaticText($"Owner: {owner}. Layers can be created: {(stack.CanCreateLayer ? "yes" : "no")}", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22));
        y += 30;

        var layers = (stack.Layers ?? new List<PocketDirectoryLayerMessage>()).OrderBy(layer => layer.Index).Take(MaxVisibleLayers).ToArray();
        if (layers.Length == 0)
        {
            composer.AddStaticText("No layers are registered for this pocket.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 42));
            return;
        }

        foreach (var layer in layers)
        {
            var localLayer = layer;
            composer.AddStaticText(LayerText(localLayer), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y + 3, width - 104, 24));
            if (localLayer.CanTeleport && !localLayer.IsCurrent && !localLayer.Orphaned)
            {
                composer.AddSmallButton("Teleport", () => Teleport(localLayer.DimensionId), ElementBounds.Fixed(x + width - 96, y, 92, 26), EnumButtonStyle.Normal, "teleport-" + Math.Abs((localLayer.DimensionId ?? string.Empty).GetHashCode()));
            }
            else
            {
                composer.AddStaticText(localLayer.IsCurrent ? "Current" : "Unavailable", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + width - 96, y + 3, 92, 24));
            }

            y += 30;
        }
    }

    private PocketDirectoryStackMessage SelectedStack()
    {
        return (_state.Stacks ?? new List<PocketDirectoryStackMessage>()).FirstOrDefault(stack => string.Equals(stack.StackId, _selectedStackId, StringComparison.Ordinal));
    }

    private static string StackButtonText(PocketDirectoryStackMessage stack)
    {
        var selected = stack?.IsOwner == true ? "* " : string.Empty;
        return selected + (string.IsNullOrWhiteSpace(stack?.DisplayName) ? stack?.StackId ?? "Unnamed" : stack.DisplayName);
    }

    private static string LayerText(PocketDirectoryLayerMessage layer)
    {
        var status = layer.Prepared ? "prepared" : "unprepared";
        if (layer.Orphaned)
        {
            status = "orphaned";
        }

        return $"Layer {FormatLayer(layer.Index)}: {layer.DisplayName} ({status})";
    }

    private static string FormatLayer(int index)
    {
        return index > 0 ? "+" + index : index.ToString();
    }

    private bool SelectStack(string stackId)
    {
        _selectedStackId = stackId;
        ComposeDialog();
        return true;
    }

    private bool Teleport(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _onTeleport?.Invoke(dimensionId);
        }

        return true;
    }

    private bool OnRefresh()
    {
        _onRefresh?.Invoke();
        return true;
    }

    private bool OnCreatePocket()
    {
        new PocketCreateDialog(capi, _onCreatePocket).TryOpen();
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
}
