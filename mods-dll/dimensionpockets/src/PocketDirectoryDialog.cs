using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PocketDimensions;

internal sealed class PocketDirectoryDialog : GuiDialog
{
    private const double DialogWidth = 560;
    private const double ContentWidth = DialogWidth - 48;
    private const double PanelHeight = 360;
    private const int MaxVisibleStacks = 8;
    private const int MaxStackLabelLength = 52;
    private const int MaxLocationTextLength = 96;

    private readonly Action _onRefresh;
    private readonly Action<string> _onTeleport;
    private readonly Action<string, string, int, int> _onCreatePocket;
    private PocketDirectoryStateMessage _state = new PocketDirectoryStateMessage { Message = "Loading Pocket Directory..." };
    private string _selectedStackId;
    private int _stackScrollIndex;

    public PocketDirectoryDialog(
        ICoreClientAPI capi,
        Action onRefresh,
        Action<string> onTeleport,
        Action<string, string, int, int> onCreatePocket)
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

        var preferredStackId = !string.IsNullOrWhiteSpace(_state.SelectedStackId) ? _state.SelectedStackId : _selectedStackId;
        if (string.IsNullOrWhiteSpace(preferredStackId) || !_state.Stacks.Any(stack => string.Equals(stack.StackId, preferredStackId, StringComparison.Ordinal)))
        {
            preferredStackId = _state.Stacks.FirstOrDefault()?.StackId;
        }

        if (!string.Equals(_selectedStackId, preferredStackId, StringComparison.Ordinal))
        {
            _selectedStackId = preferredStackId;
        }

        KeepSelectedStackVisible();
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
        var listBounds = ElementBounds.Fixed(0, panelY, ContentWidth, PanelHeight);
        var summaryBounds = ElementBounds.Fixed(0, panelY + PanelHeight + 10, ContentWidth, 48);
        var buttonY = summaryBounds.fixedY + summaryBounds.fixedHeight + 8;
        var bodyHeight = buttonY + 36;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("pocket-directory", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar("Pocket Directory", OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(listBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddStaticText("Browse Pocket Dimensions spaces and enter pockets.", CairoFont.WhiteSmallText(), helpBounds)
            .AddStaticText(CurrentLocationText(), CairoFont.WhiteSmallText(), locationBounds)
            .AddStaticText(StatusText(), CairoFont.WhiteSmallText(), statusBounds);

        AddStackList(composer, listBounds);
        AddSelectedPocketSummary(composer, summaryBounds);
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
        if (!string.IsNullOrWhiteSpace(_state.Message) && !_state.Success)
        {
            return "Error: " + _state.Message;
        }

        var count = _state.Stacks?.Count ?? 0;
        if (count == 0)
        {
            if (string.Equals(_state.Message, "Loading Pocket Directory...", StringComparison.Ordinal))
            {
                return _state.Message;
            }

            return "No accessible pockets yet.";
        }

        // Avoid transient success copy here; after actions, players need stable context or errors.
        var createText = _state.CanCreatePocket ? "You can create new pockets." : "Pocket creation is unavailable.";
        return $"{count} pocket{(count == 1 ? string.Empty : "s")} available. {createText}";
    }

    private void AddStackList(GuiComposer composer, ElementBounds panelBounds)
    {
        var x = panelBounds.fixedX + 8;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 16;
        composer.AddStaticText("Pockets", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22));
        y += 30;

        var allStacks = _state.Stacks ?? new List<PocketDirectoryStackMessage>();
        _stackScrollIndex = ClampInt(_stackScrollIndex, 0, LastPageStart(allStacks.Count, MaxVisibleStacks));
        var stacks = allStacks.Skip(_stackScrollIndex).Take(MaxVisibleStacks).ToArray();
        if (stacks.Length == 0)
        {
            composer.AddStaticText("No pockets are visible to this player.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 46));
            return;
        }

        foreach (var stack in stacks)
        {
            var localStack = stack;
            var selected = string.Equals(localStack.StackId, _selectedStackId, StringComparison.Ordinal);
            var label = StackButtonText(localStack, selected);
            if (selected)
            {
                composer.AddInset(ElementBounds.Fixed(x - 4, y - 4, width + 8, 36), 2, 1.15f);
            }

            composer.AddSmallButton(label, () => SelectStack(localStack.StackId), ElementBounds.Fixed(x, y, width, 28), EnumButtonStyle.Small, "stack-" + Math.Abs((localStack.StackId ?? label).GetHashCode()));
            y += 34;
        }

        if (allStacks.Count > MaxVisibleStacks)
        {
            AddScrollButtons(composer, x, panelBounds.fixedY + panelBounds.fixedHeight - 32, width, ScrollStacksUp, ScrollStacksDown, PageLabel(_stackScrollIndex, allStacks.Count, MaxVisibleStacks));
        }
    }

    private void AddSelectedPocketSummary(GuiComposer composer, ElementBounds bounds)
    {
        var stack = SelectedStack();
        var x = bounds.fixedX;
        var y = bounds.fixedY;
        var width = bounds.fixedWidth;

        if (stack == null)
        {
            composer.AddStaticText("Select a pocket.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y + 8, width, 22));
            return;
        }

        var owner = string.IsNullOrWhiteSpace(stack.OwnerPlayerName) ? "Server/global" : stack.OwnerPlayerName;
        var allSpaces = stack.Layers ?? new List<PocketDirectoryLayerMessage>();
        var availableSpaces = allSpaces.Count(layer => layer.Prepared && !layer.Orphaned);
        var displayName = string.IsNullOrWhiteSpace(stack.DisplayName) ? stack.StackId ?? "Unnamed" : stack.DisplayName;
        var summary = $"{Truncate(displayName, 28)} | {Truncate(owner, 16)} | {availableSpaces}/{allSpaces.Count} spaces";
        composer.AddStaticText(summary, CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width - 146, 22));

        var entry = DirectoryEntry(stack);
        var current = allSpaces.FirstOrDefault(layer => layer.IsCurrent);
        if (current != null)
        {
            composer.AddStaticText("Currently inside this pocket.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y + 22, width - 146, 22));
        }
        else if (entry?.CanTeleport == true && !entry.Orphaned)
        {
            composer.AddSmallButton("Enter Pocket", () => Teleport(entry.DimensionId), ElementBounds.Fixed(x + width - 132, y + 6, 132, 30), EnumButtonStyle.Normal, "enter-pocket-" + Math.Abs((entry.DimensionId ?? stack.StackId ?? string.Empty).GetHashCode()));
        }
        else
        {
            composer.AddStaticText("Unavailable for directory entry.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y + 22, width - 146, 22));
        }
    }

    private PocketDirectoryStackMessage SelectedStack()
    {
        return (_state.Stacks ?? new List<PocketDirectoryStackMessage>()).FirstOrDefault(stack => string.Equals(stack.StackId, _selectedStackId, StringComparison.Ordinal));
    }

    private static string StackButtonText(PocketDirectoryStackMessage stack, bool selected)
    {
        var ownerPrefix = stack?.IsOwner == true ? "* " : string.Empty;
        var selectedPrefix = selected ? "[Selected] " : string.Empty;
        var name = string.IsNullOrWhiteSpace(stack?.DisplayName) ? stack?.StackId ?? "Unnamed" : stack.DisplayName;
        return selectedPrefix + ownerPrefix + Truncate(name, MaxStackLabelLength - selectedPrefix.Length - ownerPrefix.Length);
    }

    private static PocketDirectoryLayerMessage DirectoryEntry(PocketDirectoryStackMessage stack)
    {
        var layers = stack?.Layers ?? new List<PocketDirectoryLayerMessage>();
        return layers.FirstOrDefault(layer => layer.IsCurrent)
            ?? layers.FirstOrDefault(layer => layer.Index == 0 && layer.CanTeleport && !layer.Orphaned)
            ?? layers.FirstOrDefault(layer => layer.CanTeleport && !layer.Orphaned)
            ?? layers.FirstOrDefault(layer => layer.Index == 0)
            ?? layers.FirstOrDefault();
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
        var max = LastPageStart(_state.Stacks?.Count ?? 0, MaxVisibleStacks);
        _stackScrollIndex = Math.Min(max, _stackScrollIndex + MaxVisibleStacks);
        ComposeDialog();
        return true;
    }

    private string CurrentLocationText()
    {
        return string.IsNullOrWhiteSpace(_state.CurrentLocationText)
            ? "Current location: unknown."
            : Truncate(_state.CurrentLocationText, MaxLocationTextLength);
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
            _stackScrollIndex = PageStartForIndex(index, MaxVisibleStacks);
        }
        else if (index >= _stackScrollIndex + MaxVisibleStacks)
        {
            _stackScrollIndex = PageStartForIndex(index, MaxVisibleStacks);
        }
    }

    private void ClampScrollIndexes()
    {
        _stackScrollIndex = ClampInt(_stackScrollIndex, 0, LastPageStart(_state.Stacks?.Count ?? 0, MaxVisibleStacks));
    }

    private static void AddScrollButtons(GuiComposer composer, double x, double y, double width, Func<bool> up, Func<bool> down, string label)
    {
        var controlsWidth = 172;
        var controlsX = x + (width - controlsWidth) / 2;
        composer.AddSmallButton("<", () => up(), ElementBounds.Fixed(controlsX, y, 36, 24), EnumButtonStyle.Small);
        composer.AddStaticText(label, CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(controlsX + 44, y + 3, 84, 24));
        composer.AddSmallButton(">", () => down(), ElementBounds.Fixed(controlsX + controlsWidth - 36, y, 36, 24), EnumButtonStyle.Small);
    }

    private static string PageLabel(int currentOffset, int totalItems, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (totalItems + pageSize - 1) / pageSize);
        var currentPage = Math.Min(totalPages, currentOffset / pageSize + 1);
        return $"Page {currentPage}/{totalPages}";
    }

    private static int LastPageStart(int totalItems, int pageSize)
    {
        if (totalItems <= 0 || pageSize <= 0)
        {
            return 0;
        }

        return ((totalItems - 1) / pageSize) * pageSize;
    }

    private static int PageStartForIndex(int index, int pageSize)
    {
        return pageSize <= 0 ? 0 : Math.Max(0, index / pageSize * pageSize);
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
