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
    private const int MaxVisibleStacks = 8;

    private readonly Action _onRefresh;
    private readonly Action<string> _onTeleport;
    private readonly Action<string, string, int, int> _onCreatePocket;
    private readonly Action<string, int, string> _onCreateLayer;
    private readonly Action<string, int, string> _onEditLayer;
    private PocketDirectoryStateMessage _state = new PocketDirectoryStateMessage { Message = "Loading Pocket Directory..." };
    private string _selectedStackId;
    private int _stackScrollIndex;
    private int _layerScrollIndex;

    public PocketDirectoryDialog(
        ICoreClientAPI capi,
        Action onRefresh,
        Action<string> onTeleport,
        Action<string, string, int, int> onCreatePocket,
        Action<string, int, string> onCreateLayer,
        Action<string, int, string> onEditLayer)
        : base(capi)
    {
        _onRefresh = onRefresh;
        _onTeleport = onTeleport;
        _onCreatePocket = onCreatePocket;
        _onCreateLayer = onCreateLayer;
        _onEditLayer = onEditLayer;
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

        var preferredStackId = !string.IsNullOrWhiteSpace(_state.SelectedStackId) ? _state.SelectedStackId : _selectedStackId;
        if (string.IsNullOrWhiteSpace(preferredStackId) || !_state.Stacks.Any(stack => string.Equals(stack.StackId, preferredStackId, StringComparison.Ordinal)))
        {
            preferredStackId = _state.Stacks.FirstOrDefault()?.StackId;
        }

        if (!string.Equals(_selectedStackId, preferredStackId, StringComparison.Ordinal))
        {
            _selectedStackId = preferredStackId;
            _layerScrollIndex = 0;
        }

        KeepSelectedStackVisible();
        KeepCurrentLayerVisible();
        ClampScrollIndexes();

        ComposeDialog();
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var helpBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, 42);
        var locationBounds = ElementBounds.Fixed(0, helpBounds.fixedY + helpBounds.fixedHeight + 4, ContentWidth, 22);
        var statusBounds = ElementBounds.Fixed(0, locationBounds.fixedY + locationBounds.fixedHeight + 4, ContentWidth, 24);
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
            .AddStaticText(CurrentLocationText(), CairoFont.WhiteSmallText(), locationBounds)
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

        var allStacks = _state.Stacks ?? new List<PocketDirectoryStackMessage>();
        _stackScrollIndex = ClampInt(_stackScrollIndex, 0, Math.Max(0, allStacks.Count - MaxVisibleStacks));
        var stacks = allStacks.Skip(_stackScrollIndex).Take(MaxVisibleStacks).ToArray();
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

        if (allStacks.Count > MaxVisibleStacks)
        {
            AddScrollButtons(composer, x, panelBounds.fixedY + panelBounds.fixedHeight - 32, width, ScrollStacksUp, ScrollStacksDown, _stackScrollIndex, allStacks.Count - MaxVisibleStacks);
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

        if (stack.CanCreateLayer)
        {
            composer.AddSmallButton("New Layer", () => CreateLayer(stack), ElementBounds.Fixed(x, y, 112, 26), EnumButtonStyle.Normal, "new-layer-" + Math.Abs((stack.StackId ?? string.Empty).GetHashCode()));
            y += 34;
        }

        var allLayers = (stack.Layers ?? new List<PocketDirectoryLayerMessage>()).OrderBy(layer => layer.Index).ToArray();
        if (allLayers.Length == 0)
        {
            composer.AddStaticText("No layers are registered for this pocket.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 42));
            return;
        }

        var rowHeight = 50;
        var controlsHeight = allLayers.Length > MaxVisibleLayerRows(y, panelBounds) ? 34 : 0;
        var maxRows = MaxVisibleLayerRows(y, panelBounds, controlsHeight);
        _layerScrollIndex = ClampInt(_layerScrollIndex, 0, Math.Max(0, allLayers.Length - maxRows));
        var layers = allLayers.Skip(_layerScrollIndex).Take(maxRows).ToArray();

        foreach (var layer in layers)
        {
            var localLayer = layer;
            composer.AddStaticText(LayerText(localLayer), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y + 3, width - 160, 44));
            if (localLayer.CanTeleport && !localLayer.IsCurrent && !localLayer.Orphaned)
            {
                composer.AddSmallButton("Enter", () => Teleport(localLayer.DimensionId), ElementBounds.Fixed(x + width - 152, y, 70, 26), EnumButtonStyle.Normal, "teleport-" + Math.Abs((localLayer.DimensionId ?? string.Empty).GetHashCode()));
            }
            else
            {
                composer.AddStaticText(localLayer.IsCurrent ? "Current" : "Unavailable", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + width - 152, y + 3, 76, 24));
            }

            if (localLayer.CanEdit)
            {
                composer.AddSmallButton("Edit", () => EditLayer(localLayer), ElementBounds.Fixed(x + width - 74, y, 70, 26), EnumButtonStyle.Normal, "edit-" + Math.Abs((localLayer.DimensionId ?? string.Empty).GetHashCode()));
            }

            y += rowHeight;
        }

        if (allLayers.Length > maxRows)
        {
            AddScrollButtons(composer, x, panelBounds.fixedY + panelBounds.fixedHeight - 32, width, ScrollLayersUp, ScrollLayersDown, _layerScrollIndex, allLayers.Length - maxRows);
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

        return $"Layer {FormatLayer(layer.Index)}: {Truncate(layer.DisplayName, 24)}\n({status})";
    }

    private static string FormatLayer(int index)
    {
        return index > 0 ? "+" + index : index.ToString();
    }

    private bool SelectStack(string stackId)
    {
        _selectedStackId = stackId;
        _layerScrollIndex = 0;
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
        new PocketCreateDialog(capi, _state.DefaultSizeChunks, _state.DefaultSpawnY, _onCreatePocket).TryOpen();
        return true;
    }

    private bool ScrollStacksUp()
    {
        _stackScrollIndex = Math.Max(0, _stackScrollIndex - MaxVisibleStacks);
        ComposeDialog();
        return true;
    }

    private bool ScrollStacksDown()
    {
        var max = Math.Max(0, (_state.Stacks?.Count ?? 0) - MaxVisibleStacks);
        _stackScrollIndex = Math.Min(max, _stackScrollIndex + MaxVisibleStacks);
        ComposeDialog();
        return true;
    }

    private bool ScrollLayersUp()
    {
        _layerScrollIndex = Math.Max(0, _layerScrollIndex - 3);
        ComposeDialog();
        return true;
    }

    private bool ScrollLayersDown()
    {
        var stack = SelectedStack();
        var max = Math.Max(0, (stack?.Layers?.Count ?? 0) - 1);
        _layerScrollIndex = Math.Min(max, _layerScrollIndex + 3);
        ComposeDialog();
        return true;
    }

    private bool CreateLayer(PocketDirectoryStackMessage stack)
    {
        var suggested = SuggestedLayerIndex(stack);
        var hint = $"Target index examples: {FormatLayer(suggested)}, +1, -1. Leave display name blank to use the default layer name.";
        new PocketLayerDetailsDialog(capi, "Create Pocket Layer", hint, showLayerIndex: true, suggested, (index, displayName) =>
        {
            _onCreateLayer?.Invoke(stack.StackId, index, displayName);
        }).TryOpen();
        return true;
    }

    private bool EditLayer(PocketDirectoryLayerMessage layer)
    {
        var stack = SelectedStack();
        if (stack == null || layer == null)
        {
            return true;
        }

        var hint = $"Current name: {layer.DisplayName}. Submit blank to reset this layer to its default name.";
        new PocketLayerDetailsDialog(capi, $"Edit Layer {FormatLayer(layer.Index)}", hint, showLayerIndex: false, layer.Index, (index, displayName) =>
        {
            _onEditLayer?.Invoke(stack.StackId, index, displayName);
        }).TryOpen();
        return true;
    }

    private static int SuggestedLayerIndex(PocketDirectoryStackMessage stack)
    {
        var layers = stack?.Layers ?? new List<PocketDirectoryLayerMessage>();
        return layers.Count == 0 ? 0 : layers.Max(layer => layer.Index) + 1;
    }

    private string CurrentLocationText()
    {
        return string.IsNullOrWhiteSpace(_state.CurrentLocationText)
            ? "Current location: unknown."
            : _state.CurrentLocationText;
    }

    private void KeepSelectedStackVisible()
    {
        var stacks = _state.Stacks ?? new List<PocketDirectoryStackMessage>();
        var index = stacks.FindIndex(stack => string.Equals(stack.StackId, _selectedStackId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        if (index < _stackScrollIndex)
        {
            _stackScrollIndex = index;
        }
        else if (index >= _stackScrollIndex + MaxVisibleStacks)
        {
            _stackScrollIndex = Math.Max(0, index - MaxVisibleStacks + 1);
        }
    }

    private void KeepCurrentLayerVisible()
    {
        var stack = SelectedStack();
        var layers = stack?.Layers?.OrderBy(layer => layer.Index).ToArray();
        if (layers == null)
        {
            return;
        }

        var current = Array.FindIndex(layers, layer => layer.IsCurrent);
        if (current >= 0)
        {
            _layerScrollIndex = current;
        }
    }

    private void ClampScrollIndexes()
    {
        _stackScrollIndex = ClampInt(_stackScrollIndex, 0, Math.Max(0, (_state.Stacks?.Count ?? 0) - MaxVisibleStacks));
        _layerScrollIndex = Math.Max(0, _layerScrollIndex);
    }

    private static void AddScrollButtons(GuiComposer composer, double x, double y, double width, Func<bool> up, Func<bool> down, int current, int max)
    {
        composer.AddSmallButton("Up", () => up(), ElementBounds.Fixed(x, y, 72, 26));
        composer.AddStaticText($"{current + 1} / {max + 1}", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 82, y + 3, width - 164, 24));
        composer.AddSmallButton("Down", () => down(), ElementBounds.Fixed(x + width - 72, y, 72, 26));
    }

    private static int MaxVisibleLayerRows(double currentY, ElementBounds panelBounds, double reservedBottom = 0)
    {
        return Math.Max(1, (int)((panelBounds.fixedY + panelBounds.fixedHeight - currentY - reservedBottom - 6) / 50));
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private static string Truncate(string value, int maxLength)
    {
        value ??= string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
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
