using System;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Native sample chat whose lifecycle does not recompose the surrounding settings.</summary>
public sealed class SetupWizardChatFocusElement : GuiElement
{
    private readonly Action<int> _onSelect;
    private SetupWizardChatFocusPreview.State _state = SetupWizardChatFocusPreview.At(null, 0, true);
    private bool _generalMode;
    private string _tabName = "Proximity";
    private string _tabPosition;
    private bool _disposed;

    public SetupWizardChatFocusElement(ICoreClientAPI api, ElementBounds bounds, Action<int> onSelect)
        : base(api, bounds) => _onSelect = onSelect;

    public GuiComposer Composer { get; private set; }
    public override bool Focusable => !_disposed && _state.IsOpen;

    public void Refresh(SetupWizardDraft draft, double time, bool autoChatOpenSelected,
        bool autoChat = true, int? selectedTabIndex = null)
    {
        if (_disposed) return;
        var previous = _state;
        _state = SetupWizardChatFocusPreview.At(draft, time, autoChatOpenSelected, autoChat, selectedTabIndex);
        if (!_state.IsOpen && HasFocus) OnFocusLost();
        bool generalMode = ConfigAdminSettingDefinition.TryParseBool(draft?.Get("UseGeneralChannelAsProximityChat"), out bool value) && value;
        var tabName = draft?.Get("ProximityChatName");
        if (string.IsNullOrEmpty(tabName)) tabName = "Proximity";
        var tabPosition = draft?.Get("ProximityChatTabPosition");
        bool tabsChanged = generalMode != _generalMode || tabName != _tabName || tabPosition != _tabPosition;
        _tabName = tabName;
        _tabPosition = tabPosition;
        _generalMode = generalMode;
        if (Composer == null) return;
        if (tabsChanged) ComposeChat();
        else if (_state != previous) ApplyState(_state.Messages != previous.Messages);
        SyncPosition();
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        if (_disposed) return;
        Bounds.CalcWorldBounds();
        ComposeChat();
    }

    private void ComposeChat()
    {
        Composer?.Dispose();
        Composer = null;
        double scale = Math.Max(RuntimeEnv.GUIScale, 0.1f);
        // An independent drawing surface avoids making this child part of its parent's autosize calculation.
        var bounds = ElementBounds.Fixed(Bounds.renderX / scale, Bounds.renderY / scale,
            Bounds.InnerWidth / scale, Bounds.InnerHeight / scale);
        var composer = api.Gui.CreateCompo("thebasics-setup-chat-focus-sample", bounds);
        try
        {
            var tabs = _generalMode ? new[] { new GuiTab { Name = "General", DataInt = 0 } }
                : new[] { new GuiTab { Name = "General", DataInt = 0 }, new GuiTab { Name = _tabName, DataInt = 1 } };
            var gameOrder = (GuiTab[])tabs.Clone();
            ChatTabLayout.Arrange(tabs, gameOrder, new thebasics.Configs.ModConfig { ProximityChatTabPosition = _tabPosition, UseGeneralChannelAsProximityChat = _generalMode }, 1);
            SetupWizardDialog.AddChatWindow(composer, api, 0, Math.Max(1, Bounds.fixedHeight - 57), tabs,
                Array.FindIndex(tabs, tab => tab.DataInt == _state.SelectedTabIndex), index => _onSelect?.Invoke(tabs[index].DataInt), _state.Messages);
            Composer = composer.Compose(focusFirstElement: false);
            ApplyState(false);
            if (HasFocus) Composer.GetHorizontalTabs("preview-chat-tabs").OnFocusGained();
        }
        catch
        {
            composer.Dispose();
            Composer = null;
            throw;
        }
    }

    private void ApplyState(bool textChanged)
    {
        Composer.GetHorizontalTabs("preview-chat-tabs").SetValue(Array.FindIndex(Composer.GetHorizontalTabs("preview-chat-tabs").tabs, tab => tab.DataInt == _state.SelectedTabIndex), callhandler: false);
        var history = Composer.GetRichtext("preview-text");
        if (textChanged) history.SetNewText(_state.Messages, CairoFont.WhiteDetailText().WithFontSize(14), null);
        Composer.GetCompactScrollbar("preview-chat-scroll")
            .SetHeights((float)history.InsideClipBounds.fixedHeight, (float)history.Bounds.fixedHeight);
    }

    private void SyncPosition()
    {
        if (Composer == null) return;
        Bounds.CalcWorldBounds();
        double scale = Math.Max(RuntimeEnv.GUIScale, 0.1f);
        var bounds = Composer.Bounds;
        double x = Bounds.renderX / scale, y = Bounds.renderY / scale;
        if (bounds.fixedX == x && bounds.fixedY == y) return;
        bounds.WithFixedPosition(x, y);
        bounds.MarkDirtyRecursive();
        bounds.CalcWorldBounds();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (_disposed || !_state.IsOpen) return;
        SyncPosition();
        Composer?.Render(deltaTime);
    }

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (_disposed || !_state.IsOpen) return;
        SyncPosition();
        Composer?.GetHorizontalTabs("preview-chat-tabs").OnMouseDown(api, args);
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (_disposed || !_state.IsOpen) return;
        SyncPosition();
        Composer?.GetHorizontalTabs("preview-chat-tabs").OnMouseUp(api, args);
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (_disposed || !_state.IsOpen) return;
        SyncPosition();
        Composer?.GetHorizontalTabs("preview-chat-tabs").OnMouseMove(api, args);
    }

    public override void OnFocusGained()
    {
        if (!Focusable) return;
        base.OnFocusGained();
        Composer?.GetHorizontalTabs("preview-chat-tabs").OnFocusGained();
    }

    public override void OnFocusLost()
    {
        Composer?.GetHorizontalTabs("preview-chat-tabs").OnFocusLost();
        base.OnFocusLost();
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (Focusable && HasFocus) Composer?.GetHorizontalTabs("preview-chat-tabs").OnKeyDown(api, args);
    }

    public bool SkipClosedTabNavigation(GuiComposer parent, KeyEvent args)
    {
        if (_disposed || _state.IsOpen || args.Handled || args.KeyCode != (int)GlKeys.Tab || TabIndex < 0) return false;
        int count = parent.MaxTabIndex + 1;
        if (count <= 0) return false;
        int direction = args.ShiftPressed ? -1 : 1;
        var current = parent.CurrentTabIndexElement;
        int next = current == null ? args.ShiftPressed ? count - 1 : 0 : GameMath.Mod(current.TabIndex + direction, count);
        if (next != TabIndex) return false;
        // The native composer consumes Tab even when a reserved index is temporarily unfocusable.
        if (!parent.FocusElement(GameMath.Mod(next + direction, count))) return false;
        args.Handled = true;
        return true;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OnFocusLost();
        Composer?.Dispose();
        Composer = null;
        base.Dispose();
    }
}
