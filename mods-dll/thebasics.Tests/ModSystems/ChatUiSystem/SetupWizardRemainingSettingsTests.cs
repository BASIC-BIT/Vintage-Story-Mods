using TheBasics.GuiPreview;
using System.Reflection;
using thebasics.Configs;
using Vintagestory.API.Common;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardRemainingSettingsTests
{
    public static TheoryData<string, string, string> Settings => new()
    {
        { "chat.tabs", "ProximityChatName", "Nearby" },
        { "chat.tabs", "ProximityChatTabPosition", "AfterGeneral" },
        { "chat.language", "UseNicknameInGlobalOOC", "0" },
        { "teleport.homes", "SetHomeCommandPrivilege", "custom.home" },
        { "teleport.spawn", "SetSpawnCommandPrivilege", "custom.spawn" },
        { "notifications.deaths", "EnableNearbyDeathMessagesInProximityChat", "0" },
        { "roleplay.descriptions", "MaxEnvironmentPlacementDistance", "42" },
        { "server.retention", "ChatHistoryFlushIntervalMilliseconds", "1250" },
        { "server.stats", "PlayerStatDistanceTravelledTimer", "1500" },
        { "server.stats", "PlayerStatToggles.BlockBreaks", "0" },
        { "server.stats", "PlayerStatToggles.Deaths", "0" },
        { "server.stats", "PlayerStatToggles.DistanceTravelled", "0" },
        { "server.stats", "PlayerStatToggles.NpcKills", "0" },
        { "server.stats", "PlayerStatToggles.PlayerKills", "0" },
        { "server.integrations", "EnableTh3EssentialsDiscordRelay", "0" },
        { "server.diagnostics", "DebugMode", "1" }
    };

    [Theory]
    [MemberData(nameof(Settings))]
    public void EveryMigratedSettingUsesTheSharedDraftReviewAndSave(string pageId, string key, string value)
    {
        Assert.True(SetupWizardCatalog.TryGetPage(pageId, out var page));
        Assert.Contains(key, page.SettingKeys);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var original = draft.Get(key);
        if (original == value) value = value == "0" ? "1" : "0";
        draft.Set(key, value);
        Assert.Contains(key, draft.ChangedKeys);

        var request = draft.CreateSaveRequest(1, "task-2");
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        Assert.True(new SetupWizardState(config).TryValidatePatch(config, request, out _, out _, out _, out var errors), string.Join("; ", errors));
        Assert.Contains(request.Values, item => item.Key == key && item.Value == value);
        Assert.Contains(request.OriginalValues, item => item.Key == key && item.Value == original);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void DraftPreviewsShowNamesOrderingAndPlacementReachWithoutUsingProviders(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-chat-tabs", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.Draft.Set("ProximityChatName", "Nearby");
        wizard.Draft.Set("ProximityChatTabPosition", "First");
        wizard.ShowPage("chat.tabs");
        host.RenderGuiDialog(wizard, 0);
        var chat = Assert.IsType<SetupWizardChatFocusElement>(wizard.SingleComposer["preview-chat-focus"]);
        var tabs = chat.Composer.GetHorizontalTabs("preview-chat-tabs");
        Assert.Equal(new[] { "Nearby", "General" }, tabs.tabs.Select(tab => tab.Name));
        Assert.Equal(0, tabs.activeElement);
        wizard.Draft.Set("MaxEnvironmentPlacementDistance", "5");
        wizard.ShowPage("roleplay.descriptions");
        host.RenderGuiDialog(wizard, 0);
        var first = host.Canvas.GetRgbaPixels();
        Assert.Contains("out of", Text(wizard.SingleComposer.GetRichtext("preview-placement-range")));
        wizard.SingleComposer.GetTextInput("MaxEnvironmentPlacementDistance").SetValue("20");
        host.RenderGuiDialog(wizard, 0);
        Assert.Contains("within", Text(wizard.SingleComposer.GetRichtext("preview-placement-range")));
        Assert.False(first.SequenceEqual(host.Canvas.GetRgbaPixels()));
        foreach (var page in new[] { "server.integrations", "server.diagnostics", "server.retention" })
        {
            wizard.ShowPage(page);
            Assert.Null(wizard.SingleComposer.GetElement("preview-diagram"));
            Assert.Null(wizard.SingleComposer.GetElement("preview-text"));
        }
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static string Text(GuiElementRichtext text) => string.Join(" ", text.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void AllNewControlsArePresentAndReviewShowsEveryEditedValue(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-chat-default", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        foreach (var row in Settings)
        {
            var (page, key, value) = row.Data;
            wizard.ShowPage(page);
            Assert.NotNull(wizard.SingleComposer.GetElement(key));
            if (wizard.Draft.Get(key) == value) value = value == "0" ? "1" : "0";
            wizard.Draft.Set(key, value);
        }
        wizard.ShowPage("review");
        host.RenderGuiDialog(wizard, 0);
        var elements = (Dictionary<string, GuiElement>)typeof(GuiComposer).GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wizard.SingleComposer)!;
        var text = string.Join(" ", elements.Values.OfType<GuiElementRichtext>().SelectMany(element => element.Components.OfType<RichTextComponent>()).Select(component => component.DisplayText));
        Assert.Contains("Nearby", text);
        Assert.Contains("custom.home", text);
        Assert.Contains("custom.spawn", text);
        Assert.Contains("1250", text);
        Assert.Contains("1500", text);
        Assert.Contains("After restart", text);
        Assert.Equal(16, wizard.Draft.CreateSaveRequest(2, "review").Values.Count);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void LongFormsScrollWithoutMovingTheirPreviewAndRetainDisabledValues(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-server-stats", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var form = wizard.SingleComposer.GetScrollbar("settings-scroll");
        Assert.NotNull(form);
        var preview = wizard.SingleComposer["preview-diagram"].Bounds;
        var top = preview.renderY;
        var input = wizard.SingleComposer.GetTextInput("PlayerStatDistanceTravelledTimer");
        wizard.SingleComposer.FocusElement(input.TabIndex);
        Assert.True(form.CurrentYPosition > 0);
        form.ScrollToBottom();
        var label = Assert.IsType<SetupWizardViewportRichtext>(wizard.SingleComposer["setting-label-PlayerStatDistanceTravelledTimer"]);
        Assert.Same(input.InsideClipBounds, label.InsideClipBounds);
        Assert.Equal(23, input.Bounds.renderY / scale - label.Bounds.renderY / scale, 1);
        host.RenderGuiDialog(wizard, 0);
        Assert.InRange(input.Bounds.renderY + input.Bounds.OuterHeight, 0, 650);
        Assert.Equal(top, preview.renderY);
        var scroll = form.CurrentYPosition;
        input.SetValue("1500");
        wizard.ShowPage("server.stats");
        Assert.Equal(scroll, wizard.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition);
        wizard.Draft.Set("PlayerStatSystem", "0");
        wizard.ShowPage("server.stats");
        Assert.Equal("1500", wizard.Draft.Get("PlayerStatDistanceTravelledTimer"));
        Assert.False(((GuiElementControl)wizard.SingleComposer["PlayerStatDistanceTravelledTimer"]).Enabled);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }
}
