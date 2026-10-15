using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class ConfigAdminPrivilegePickerTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void WizardReceivesServerDescriptionsAndKeepsTypedCustomPermissionAfterRefreshingSession(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = new SetupWizardDialog(host.Api, draft, "privilege-test", true,
            () => { }, (_, _, _) => { }, () => { }, layoutOnly: true);
        var session = new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            Success = true,
            RunId = "privilege-test",
            IsDedicated = true,
            Values = SetupWizardCaptureScenes.DefaultValues(),
            PrivilegeSuggestions =
            [
                new() { Code = "custom.server", Description = "Manage custom tools" },
                new() { Code = "chat", Description = "Send chat messages" }
            ]
        };
        wizard.SetSession(session);
        wizard.ShowPage("server.history");
        var picker = Assert.IsType<GuiElementPrivilegePicker>(wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        Assert.Contains(Suggestions(picker), entry => entry.Code == "custom.server" && entry.Description == "Manage custom tools");
        Assert.True(wizard.SingleComposer.FocusElement(picker.TabIndex));
        picker.SetValue(string.Empty);

        Type(wizard.SingleComposer, "Custom.Typed by hand");

        Assert.Equal("Custom.Typed by hand", draft.Get("ChatHistoryPermission"));
        Assert.Equal("Custom.Typed by hand", picker.Value);
        session.PrivilegeSuggestions = [new() { Code = "newly.registered", Description = "A new server privilege" }];
        wizard.SetSession(session);
        picker = Assert.IsType<GuiElementPrivilegePicker>(wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        Assert.Equal("Custom.Typed by hand", picker.Value);
        Assert.True(draft.IsDirty);
        Assert.Contains(Suggestions(picker), entry => entry.Code == "newly.registered" && entry.Description == "A new server privilege");
        Assert.Contains(Suggestions(picker), entry => entry.Code == "Custom.Typed by hand");
        Assert.True(wizard.SingleComposer.FocusElement(picker.TabIndex));
        Press(wizard.SingleComposer, GlKeys.Down);
        AssertPickerCursors(host, wizard, picker, scale);
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void AdvancedEditorUsesPickerForPrivilegesAndPreservesCustomDraftThroughRecomposition(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new Dictionary<string, string>
        {
            ["ChatHistoryPermission"] = "custom.unregistered",
            ["CharacterSheetSetPermission"] = "chat",
            ["TEXT_ServerSaveAnnouncement"] = "Saving soon."
        };
        var changes = new List<(string Key, string Value)>();
        using var dialog = CreateDialog(host, draft, changes);
        changes.Clear();
        var pickers = Pickers(dialog);
        Assert.Equal(new[] { "CharacterSheetSetPermission", "ChatHistoryPermission" }, pickers.Keys.Order());
        Assert.Equal("custom.unregistered", pickers["ChatHistoryPermission"].Value);
        host.RenderGuiDialog(dialog, 0);

        var history = pickers["ChatHistoryPermission"];
        Assert.True(dialog.SingleComposer.FocusElement(dialog.SingleComposer.GetElement("input-2").TabIndex));
        var x = (int)(history.Bounds.renderX + history.Bounds.InnerWidth - 10 * scale);
        var y = (int)(history.Bounds.renderY + history.Bounds.InnerHeight / 2);
        host.SetMouse(x, y);
        dialog.SingleComposer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.True(history.IsSuggestionsOpen);
        AssertPickerCursors(host, dialog, history, scale);
        Press(dialog.SingleComposer, GlKeys.Home);
        Press(dialog.SingleComposer, GlKeys.Enter);

        Assert.Equal("chat", draft["ChatHistoryPermission"]);
        Assert.Equal(new[] { ("ChatHistoryPermission", "chat") }, changes);
        dialog.Recompose();
        Assert.Equal("chat", Pickers(dialog)["ChatHistoryPermission"].Value);
        Assert.Equal("Saving soon.", draft["TEXT_ServerSaveAnnouncement"]);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ScrollingClosesSuggestionsWithoutChangingPermission(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new Dictionary<string, string>
        {
            ["ChatHistoryPermission"] = "custom.unregistered",
            ["CharacterSheetSetPermission"] = "chat"
        };
        var changes = new List<(string Key, string Value)>();
        using var dialog = CreateDialog(host, draft, changes);
        changes.Clear();
        var picker = Pickers(dialog)["ChatHistoryPermission"];
        Assert.True(dialog.SingleComposer.FocusElement(dialog.SingleComposer.GetElement("input-2").TabIndex));
        Press(dialog.SingleComposer, GlKeys.Down);
        Assert.True(picker.IsSuggestionsOpen);

        dialog.SingleComposer.GetScrollbar("settings-scrollbar").ScrollToBottom();

        Assert.False(picker.IsSuggestionsOpen);
        Assert.Equal("custom.unregistered", picker.Value);
        Assert.Empty(changes);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static void AssertPickerCursors(PreviewHost host, GuiDialog dialog, GuiElementPrivilegePicker picker, double scale)
    {
        var middleY = (int)(picker.Bounds.renderY + picker.Bounds.InnerHeight / 2);
        host.SetMouse((int)(picker.Bounds.renderX + picker.Bounds.InnerWidth - 8 * scale), middleY);
        host.RenderGuiDialog(dialog, 0);
        Assert.Equal("normal", dialog.MouseOverCursor);
        host.SetMouse((int)(picker.Bounds.renderX + 30 * scale), middleY);
        host.RenderGuiDialog(dialog, 0);
        Assert.Equal("textselect", dialog.MouseOverCursor);
        var menu = (GuiElementListMenu)typeof(GuiElementPrivilegePicker)
            .GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
        var popup = (ElementBounds)typeof(GuiElementListMenu)
            .GetField("visibleBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(menu)!;
        host.SetMouse((int)(popup.renderX + 30 * scale), (int)(popup.renderY + 30 * scale));
        host.RenderGuiDialog(dialog, 0);
        Assert.Equal("normal", dialog.MouseOverCursor);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);

    private static ScrollableConfigAdminDialog CreateDialog(PreviewHost host, Dictionary<string, string> values, List<(string, string)> changes)
    {
        var settings = new JsonDialogSettings
        {
            Code = "privilege-picker-test",
            Alignment = EnumDialogArea.CenterMiddle,
            SizeMultiplier = 0.9,
            Padding = 16,
            OnGet = key => values.GetValueOrDefault(key, string.Empty),
            OnSet = (key, value) => { values[key] = value; changes.Add((key, value)); },
            Rows =
            [
                new(new DialogElement { Code = "group-permissions", Type = EnumDialogElementType.Text, Text = "Permissions", Width = 720, Height = 30 }),
                new(new DialogElement { Code = "ChatHistoryPermission", Type = EnumDialogElementType.Input, Label = "Read history", Width = 720, Height = 28 }),
                new(new DialogElement { Code = "CharacterSheetSetPermission", Type = EnumDialogElementType.Input, Label = "Edit character sheet", Width = 720, Height = 28 }),
                new(new DialogElement { Code = "TEXT_ServerSaveAnnouncement", Type = EnumDialogElementType.Input, Label = "Save announcement", Width = 720, Height = 28 }),
                new(new DialogElement { Code = "save", Type = EnumDialogElementType.Button, Text = "Save", Width = 150, Height = 34 })
            ]
        };
        return new ScrollableConfigAdminDialog(settings, host.Api, false,
        [
            new ConfigAdminPrivilegeSuggestion { Code = "chat", Description = "Send chat messages" },
            new ConfigAdminPrivilegeSuggestion { Code = "commandplayer", Description = "Manage other players" },
            new ConfigAdminPrivilegeSuggestion { Code = "custom.server", Description = "A registered server privilege" }
        ]);
    }

    private static Dictionary<string, GuiElementPrivilegePicker> Pickers(ScrollableConfigAdminDialog dialog) =>
        (Dictionary<string, GuiElementPrivilegePicker>)typeof(ScrollableConfigAdminDialog)
            .GetField("_privilegePickers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;

    private static IEnumerable<ConfigAdminPrivilegeSuggestion> Suggestions(GuiElementPrivilegePicker picker) =>
        (IEnumerable<ConfigAdminPrivilegeSuggestion>)typeof(GuiElementPrivilegePicker)
            .GetField("suggestions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;

    private static void Type(GuiComposer composer, string value)
    {
        foreach (var character in value) composer.OnKeyPress(new KeyEvent { KeyChar = character, KeyCode = character });
    }

    private static void Press(GuiComposer composer, GlKeys key)
    {
        var args = new KeyEvent { KeyCode = (int)key };
        composer.OnKeyDown(args, true);
        Assert.True(args.Handled);
    }
}
