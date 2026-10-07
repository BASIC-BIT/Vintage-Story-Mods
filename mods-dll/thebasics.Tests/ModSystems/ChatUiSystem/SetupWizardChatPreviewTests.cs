using System.Globalization;
using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardChatPreviewTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void NativeChatTabsShowTheirDeliveryAndStayInsideThePreview(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("UseGeneralChannelAsProximityChat", "0");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.basics");
        wizard.TryOpen();

        var tabs = wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs");
        Assert.Equal(new[] { "General", "Proximity" }, tabs.tabs.Select(tab => tab.Name));
        Assert.Equal(1, tabs.activeElement);
        Assert.Equal(1, wizard.SingleComposer.GetHorizontalTabs("topic-tabs").activeElement);
        Assert.Contains("Pip says \"Hello, traveler!\"", PreviewText(wizard));
        Assert.Contains("(GOOC)", PreviewText(wizard));
        AssertPreviewBounds(host, wizard);
        AssertDisabledInput(host, wizard);

        host.SetMouse(-1, -1);
        host.RenderGuiDialog(wizard, 0);
        var proximityPixels = host.Canvas.GetRgbaPixels();
        var output = Path.Combine(Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ??
            Path.Combine(AppContext.BaseDirectory, "visual-artifacts"), "wizard-chat");
        Directory.CreateDirectory(output);
        var suffix = scale.ToString("0.##", CultureInfo.InvariantCulture);
        var proximity = Path.Combine(output, "proximity-" + suffix + ".png");
        var general = Path.Combine(output, "general-" + suffix + ".png");
        host.Canvas.SavePng(proximity);

        ClickTab(host, wizard, tabs, 0);
        Assert.Equal(0, tabs.activeElement);
        Assert.Contains("MaraPlayer: Anyone heading north?", PreviewText(wizard));
        Assert.Contains("PipPlayer: Meet you by the gate.", PreviewText(wizard));
        Assert.DoesNotContain("(OOC)", PreviewText(wizard));
        Assert.DoesNotContain("says", PreviewText(wizard));
        host.SetMouse(-1, -1);
        wizard.OnMouseMove(new MouseEvent(-1, -1));
        host.RenderGuiDialog(wizard, 0);
        host.Canvas.SavePng(general);
        AssertPixelChangesStayInChat(host, wizard, proximityPixels, host.Canvas.GetRgbaPixels());
        var diff = ImageComparison.Compare(proximity, general, Path.Combine(output, "tabs-diff-" + suffix + ".png"));
        Assert.False(diff.Matches);
        Assert.True(diff.ChangedPixels > 0);

        ClickTab(host, wizard, tabs, 1);
        Assert.Contains("Pip says \"Hello, traveler!\"", PreviewText(wizard));
        draft.Set("UseGeneralChannelAsProximityChat", "1");
        wizard.ShowPage("chat.basics");
        tabs = wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs");
        Assert.Equal("General", Assert.Single(tabs.tabs).Name);
        Assert.Equal(0, tabs.activeElement);
        Assert.Contains("Pip says \"Hello, traveler!\"", PreviewText(wizard));
        Assert.DoesNotContain("Anyone heading north?", PreviewText(wizard));
        AssertPreviewBounds(host, wizard);
        AssertDisabledInput(host, wizard);
        host.RenderGuiDialog(wizard, 0);
        host.Canvas.SavePng(Path.Combine(output, "general-proximity-" + suffix + ".png"));
        draft.Set("UseNicknameInOOC", "0");
        wizard.ShowPage("chat.language");
        Assert.Contains("(OOC) PipPlayer: One moment.", PreviewText(wizard));
        draft.Set("UseNicknameInOOC", "1");
        wizard.ShowPage("chat.language");
        Assert.Contains("(OOC) Pip: One moment.", PreviewText(wizard));
        draft.Set("UseGeneralChannelAsProximityChat", "0");
        draft.Set("ProximityChatAsDefault", "0");
        wizard.ShowPage("chat.tabs");
        Assert.Equal(0, wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs").activeElement);
        draft.Set("ProximityChatAsDefault", "1");
        wizard.ShowPage("chat.tabs");
        Assert.Equal(1, wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs").activeElement);
        draft.Set("UseGeneralChannelAsProximityChat", "1");
        wizard.ShowPage("chat.tabs");
        Assert.Equal(0, wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs").activeElement);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Same(wizard, Assert.Single(host.Api.Gui.LoadedGuis));
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PresentationUsesEnglishOptionsAndKeepsItsStyleWhenRpFeaturesAreOff(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("UseGeneralChannelAsProximityChat", "1");
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();
        var modes = new[] { "StandardRoleplay", "SimpleSpeech", "PlainProximity", "Prose" };
        var labels = new[] { "Roleplay dialogue", "Quoted chat", "Simple chat", "Storytelling" };

        foreach (var rpDisabled in new[] { false, true })
        {
            draft.Set("DisableRPChat", rpDisabled ? "1" : "0");
            var name = rpDisabled ? "PipPlayer" : "Pip";
            var examples = new[] { name + " says \"Hello, traveler!\"", name + ": \"Hello, traveler!\"",
                name + ": Hello, traveler!", name + " waves. \"Hello, traveler!\"" };
            for (var index = 0; index < modes.Length; index++)
            {
                draft.Set("ProximityChatPresentationMode", modes[index]);
                wizard.ShowPage("chat.basics");
                var mode = Assert.IsType<GuiElementDescribedDropDown>(wizard.SingleComposer["ProximityChatPresentationMode"]);
                Assert.Equal(modes, mode.listMenu.Values);
                Assert.Equal(labels, mode.listMenu.Names);
                Assert.Equal(modes[index], mode.SelectedValue);
                AssertSelectedPresentation(mode, labels[index], PresentationDescriptions[index]);
                Assert.DoesNotContain(Elements(wizard).Values.OfType<GuiElementStaticText>(),
                    text => text.Text == PresentationDescriptions[index]);
                Assert.Contains(examples[index], PreviewText(wizard));
                if (rpDisabled) Assert.DoesNotContain("(GOOC)", PreviewText(wizard));
                else Assert.Contains("(GOOC)", PreviewText(wizard));
                host.RenderGuiDialog(wizard, 0);
                if (!rpDisabled && index == 0)
                {
                    var x = (int)(mode.Bounds.absX + mode.Bounds.OuterWidth / 2);
                    var y = (int)(mode.Bounds.absY + mode.Bounds.OuterHeight / 2);
                    host.SetMouse(x, y);
                    wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
                    wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
                    Assert.True(mode.listMenu.IsOpened);
                    Assert.Equal(labels, mode.listMenu.Names);
                    host.RenderGuiDialog(wizard, 0);
                    var output = Path.Combine(Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ??
                        Path.Combine(AppContext.BaseDirectory, "visual-artifacts"), "wizard-chat");
                    Directory.CreateDirectory(output);
                    host.Canvas.SavePng(Path.Combine(output, "presentation-popup-" + scale.ToString("0.##", CultureInfo.InvariantCulture) + ".png"));
                }
            }
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EachSettingShowsOnlyItsRelevantPreview(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();

        Assert.Equal("hub", wizard.CurrentStepId);
        AssertPreviewElements(wizard, chat: false, diagram: false, layoutGuide: true);
        Assert.Contains(Elements(wizard).Values.OfType<GuiElementStaticText>(), text => text.Text == "Native 3D Pip omitted");
        host.RenderGuiDialog(wizard, 0);

        foreach (var page in new[] { "chat.basics", "chat.language", "chat.tabs" })
        {
            wizard.ShowPage(page);
            AssertPreviewElements(wizard, chat: true, diagram: false);
            AssertDisabledInput(host, wizard);
            host.RenderGuiDialog(wizard, 0);
        }

        foreach (var page in new[] { "chat.ranges", "chat.obfuscation" }
            .Concat(SetupWizardCatalog.Pages.Where(page => page.TopicId == "teleportation").Select(page => page.Id)))
        {
            wizard.ShowPage(page);
            AssertPreviewElements(wizard, chat: false, diagram: true);
            host.RenderGuiDialog(wizard, 0);
        }

        wizard.ShowPage("notifications.sleep");
        AssertPreviewElements(wizard, chat: true, diagram: true);
        AssertDisabledInput(host, wizard);
        Assert.Equal("General", Assert.Single(wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs").tabs).Name);
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SaveAnnouncementModesAndEditsUpdateOnlyTheMockChat(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();

        foreach (var start in new[] { true, false })
        {
            var enabled = start ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement";
            var notification = start ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification";
            var wording = start ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished";
            wizard.ShowPage(start ? "notifications.savestart" : "notifications.savefinish");
            AssertPreviewElements(wizard, chat: true, diagram: false);
            var tabs = wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs");
            Assert.Equal("General", Assert.Single(tabs.tabs).Name);
            Assert.Equal(0, tabs.activeElement);
            AssertDisabledInput(host, wizard);
            var mode = Assert.IsType<GuiElementDropDown>(wizard.SingleComposer["announcement-mode"]);
            Assert.Equal(new[] { "off", "chat", "popup" }, mode.listMenu.Values);
            Assert.Equal(new[] { "Off", "Chat", "Chat notification" }, mode.listMenu.Names);

            SelectAnnouncementMode(host, mode, 0);
            Assert.Equal("0", draft.Get(enabled));
            Assert.Equal("", PreviewText(wizard));
            SelectAnnouncementMode(host, mode, 1);
            Assert.Equal("1", draft.Get(enabled));
            Assert.Equal("0", draft.Get(notification));
            Assert.Equal(draft.Get(wording), PreviewText(wizard));
            var normal = wizard.SingleComposer.GetRichtext("preview-text").Components.OfType<RichTextComponent>().Single().Font.Color.ToArray();

            SelectAnnouncementMode(host, mode, 2);
            Assert.Equal("1", draft.Get(enabled));
            Assert.Equal("1", draft.Get(notification));
            Assert.Equal(draft.Get(wording), PreviewText(wizard));
            var highlighted = wizard.SingleComposer.GetRichtext("preview-text").Components.OfType<RichTextComponent>().Single().Font.Color;
            Assert.Equal(new[] { 224 / 255d, 207 / 255d, 187 / 255d, 204 / 255d }, highlighted);
            Assert.False(normal.SequenceEqual(highlighted));

            wizard.SingleComposer.GetTextInput(wording).SetValue("Saving test <world>!");
            Assert.Equal("Saving test <world>!", draft.Get(wording));
            Assert.Equal("Saving test <world>!", PreviewText(wizard));
            host.RenderGuiDialog(wizard, 0);
        }
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
        Assert.Same(wizard, Assert.Single(host.Api.Gui.LoadedGuis));
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void RestartDetailsStayInReviewWithHumanReadableValues(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        Assert.Equal("0", draft.Get("UseGeneralChannelAsProximityChat"));
        draft.Set("UseGeneralChannelAsProximityChat", "1");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.basics");
        wizard.TryOpen();
        var labels = Elements(wizard).Values.OfType<GuiElementStaticText>().Select(element => element.Text).ToArray();
        Assert.Contains("Use General as proximity chat", labels);
        Assert.DoesNotContain(labels, label => label.Contains("(restart)", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(labels, label => label.Contains("After restart", StringComparison.OrdinalIgnoreCase));

        wizard.ShowPage("review");
        labels = Elements(wizard).Values.OfType<GuiElementStaticText>().Select(element => element.Text).ToArray();
        Assert.Contains("Use General as proximity chat | After restart", labels);
        Assert.Contains("Off  >  On", labels);
        Assert.Equal(4, wizard.SingleComposer.GetHorizontalTabs("topic-tabs").activeElement);
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ResumingAFixedPreviewLetsTheNormalAnimationAdvance(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableSleepNotifications", "1");
        draft.Set("SleepNotificationThreshold", "0.5");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("notifications.sleep");
        wizard.TryOpen();
        wizard.SetPreviewTime(2);
        host.SetClock(0);
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal("1/4 sleeping.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        var fixedPixels = host.Canvas.GetRgbaPixels();
        host.SetClock(2100);
        host.RenderGuiDialog(wizard, 2.1f);
        Assert.Equal("1/4 sleeping.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        Assert.Equal(fixedPixels, host.Canvas.GetRgbaPixels());

        wizard.ResumePreview();
        host.SetClock(4200);
        host.RenderGuiDialog(wizard, 4.1f);
        Assert.Equal("2/4 sleeping.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal(draft.Get("TEXT_SleepNotification"), PreviewText(wizard));
        Assert.False(fixedPixels.SequenceEqual(host.Canvas.GetRgbaPixels()));
        Assert.Equal(3, wizard.SingleComposer.GetHorizontalTabs("topic-tabs").activeElement);

        foreach (var threshold in new[] { 0d, 0.25, 0.5, 0.75, 1 })
        {
            draft.Set("SleepNotificationThreshold", threshold.ToString(CultureInfo.InvariantCulture));
            for (var sleepers = 0; sleepers < 4; sleepers++)
            {
                wizard.SetPreviewTime(sleepers * 2);
                host.RenderGuiDialog(wizard, 0);
                Assert.Equal($"{sleepers}/4 sleeping.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
                var crossed = threshold > 0 && threshold < 1 && sleepers / 4d >= threshold;
                Assert.Equal(crossed ? draft.Get("TEXT_SleepNotification") : "", PreviewText(wizard));
            }
        }
        draft.Set("EnableSleepNotifications", "0");
        draft.Set("SleepNotificationThreshold", "0.75");
        wizard.SetPreviewTime(6);
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal("3/4 sleeping.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 800, scale);

    private static SetupWizardDialog CreateWizard(PreviewHost host, SetupWizardDraft draft) =>
        new(host.Api, draft, "chat-preview", true, null!, null!, null!, layoutOnly: true);

    private static string PreviewText(SetupWizardDialog wizard) => DisplayedText(wizard.SingleComposer.GetRichtext("preview-text"));

    private static string DisplayedText(GuiElementRichtext text) =>
        string.Join(" ", text.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));

    private static readonly string[] PresentationDescriptions =
    ["Names, speech verbs and quoted speech.", "Names and quoted speech.", "Names and unquoted speech.",
        "Mix actions and quoted speech in one line."];

    private static void AssertSelectedPresentation(GuiElementDescribedDropDown mode, string title, string description)
    {
        Assert.Equal(58, mode.Bounds.fixedHeight);
        var components = mode.richTextElem.Components.OfType<RichTextComponent>().ToArray();
        Assert.Contains(components, component => component.DisplayText == title && component.Font.UnscaledFontsize == 17);
        Assert.Contains(components, component => component.DisplayText == description && component.Font.UnscaledFontsize == 13);
        foreach (var line in components.SelectMany(component => component.BoundsPerLine))
        {
            Assert.True(line.X + line.Width + mode.richTextElem.Bounds.drawX <= mode.Bounds.InnerWidth - 20 * mode.Bounds.InnerHeight / 58);
            Assert.InRange(line.Y + line.Height + mode.richTextElem.Bounds.drawY, 0, mode.Bounds.InnerHeight);
        }
    }

    private static void AssertPreviewElements(SetupWizardDialog wizard, bool chat, bool diagram, bool layoutGuide = false)
    {
        var elements = Elements(wizard);
        Assert.Equal(chat, elements.ContainsKey("preview-chat-tabs"));
        Assert.Equal(chat, elements.ContainsKey("preview-chat-input"));
        Assert.Equal(chat, elements.ContainsKey("preview-chat-scroll"));
        Assert.Equal(diagram, elements.ContainsKey("preview-diagram"));
        Assert.False(elements.ContainsKey("pip"));
        Assert.DoesNotContain(elements.Values.OfType<GuiElementStaticText>(), text =>
            text.Text.Contains("Drag Pip to turn", StringComparison.OrdinalIgnoreCase) ||
            (!layoutGuide && text.Text.Contains("Native 3D Pip omitted", StringComparison.Ordinal)));
    }

    private static void SelectAnnouncementMode(PreviewHost host, GuiElementDropDown mode, int index)
    {
        mode.OnFocusGained();
        mode.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Home });
        for (var i = 0; i < index; i++) mode.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Down });
        mode.OnFocusLost();
        Assert.Equal(index, mode.listMenu.SelectedIndex);
    }

    private static Dictionary<string, GuiElement> Elements(SetupWizardDialog wizard) =>
        (Dictionary<string, GuiElement>)typeof(GuiComposer).GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(wizard.SingleComposer)!;

    private static void ClickTab(PreviewHost host, SetupWizardDialog wizard, GuiElementHorizontalTabs tabs, int index)
    {
        var widths = (int[])typeof(GuiElementHorizontalTabs).GetField("tabWidths", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tabs)!;
        var x = (int)(tabs.Bounds.absX + GuiElement.scaled(tabs.unscaledTabSpacing) * (index + 1) + widths.Take(index).Sum() + widths[index] / 2d);
        var y = (int)(tabs.Bounds.absY + tabs.Bounds.InnerHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseMove(new MouseEvent(x, y));
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }

    private static void AssertDisabledInput(PreviewHost host, SetupWizardDialog wizard)
    {
        var input = wizard.SingleComposer.GetChatInput("preview-chat-input");
        Assert.False(input.Enabled);
        Assert.False(input.Focusable);
        var before = input.GetText();
        var x = (int)(input.Bounds.absX + input.Bounds.OuterWidth / 2);
        var y = (int)(input.Bounds.absY + input.Bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnKeyPress(new KeyEvent { KeyChar = 'x', KeyCode = (int)GlKeys.X });
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        Assert.False(input.HasFocus);
        Assert.Equal(before, input.GetText());
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    private static void AssertPreviewBounds(PreviewHost host, SetupWizardDialog wizard)
    {
        var tabs = wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs");
        var input = wizard.SingleComposer.GetChatInput("preview-chat-input");
        var scroll = wizard.SingleComposer.GetCompactScrollbar("preview-chat-scroll");
        var history = wizard.SingleComposer.GetRichtext("preview-text");
        var viewport = Assert.IsAssignableFrom<ElementBounds>(history.InsideClipBounds);
        Assert.InRange(history.Bounds.absX - viewport.absX, -0.1, 0.1);
        Assert.InRange(history.Bounds.absY - viewport.absY, -0.1, 0.1);
        Assert.True(history.Bounds.OuterHeight <= viewport.InnerHeight, "All example chat lines must fit before scrolling.");
        var settings = wizard.SingleComposer["DisableRPChat"].Bounds;
        var footer = Elements(wizard).Values.OfType<GuiElementTextButton>().Single(button => button.Text == "Close").Bounds;
        Assert.True(tabs.Bounds.absY + tabs.Bounds.OuterHeight <= viewport.absY);
        Assert.True(viewport.absY + viewport.OuterHeight <= input.Bounds.absY);
        Assert.True(scroll.Bounds.absY + scroll.Bounds.OuterHeight <= input.Bounds.absY);
        foreach (var bounds in new[] { tabs.Bounds, viewport, scroll.Bounds, input.Bounds })
        {
            Assert.InRange(bounds.absX, 0, host.Canvas.Width);
            Assert.InRange(bounds.absY, 0, host.Canvas.Height);
            Assert.True(bounds.absX + bounds.OuterWidth <= settings.absX);
            Assert.True(bounds.absY + bounds.OuterHeight < footer.absY);
        }
        Assert.Null(input.InsideClipBounds);
    }

    private static void AssertPixelChangesStayInChat(PreviewHost host, SetupWizardDialog wizard, byte[] before, byte[] after)
    {
        var tabs = wizard.SingleComposer.GetHorizontalTabs("preview-chat-tabs").Bounds;
        var input = wizard.SingleComposer.GetChatInput("preview-chat-input").Bounds;
        var changed = 0;
        for (var y = 0; y < host.Canvas.Height; y++)
            for (var x = 0; x < host.Canvas.Width; x++)
            {
                var offset = (y * host.Canvas.Width + x) * 4;
                if (before.AsSpan(offset, 4).SequenceEqual(after.AsSpan(offset, 4))) continue;
                changed++;
                Assert.True(x >= Math.Floor(tabs.absX) - 2 && x <= Math.Ceiling(tabs.absX + tabs.OuterWidth) + 2 &&
                    y >= Math.Floor(tabs.absY) - 2 && y <= Math.Ceiling(input.absY + input.OuterHeight) + 2,
                    $"Chat tab change drew outside the chat preview at ({x}, {y}).");
            }
        Assert.True(changed > 0);
    }
}
