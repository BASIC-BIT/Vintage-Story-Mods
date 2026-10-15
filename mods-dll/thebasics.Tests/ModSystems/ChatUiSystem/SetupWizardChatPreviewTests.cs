using System.Globalization;
using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

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
        Assert.True(Sidebar(wizard).IsDestinationVisible("chat.basics"));
        AssertNavigationFits(host, wizard);
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
    public void HubCardsRouteFromTheirDescriptionAreaAndFooterActionsShareARow(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var advancedCalls = 0;
        using var wizard = CreateWizard(host, draft, () => advancedCalls++);
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);

        var topics = SetupWizardCatalog.Topics;
        Assert.Equal(8, topics.Count);
        var cards = topics.Select(topic =>
            Assert.IsType<GuiElementSetupTopicCard>(wizard.SingleComposer["topic-" + topic.Id])).ToArray();
        var elements = Elements(wizard);
        Assert.Equal(8, elements.Values.OfType<GuiElementSetupTopicCard>().Count());
        var rows = cards.GroupBy(card => card.Bounds.absY).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal(4, row.Count()));
        var footer = new[] { "all-settings", "close", "review" }.Select(key =>
            Assert.IsAssignableFrom<GuiElementTextButton>(wizard.SingleComposer[key]))
            .OrderBy(button => button.Bounds.absX).ToArray();
        Assert.Equal(new[] { "All settings", "Close", "Review changes" }, footer.Select(button => button.Text));
        var footerY = footer[0].Bounds.absY;
        foreach (var button in footer)
        {
            var bounds = button.Bounds;
            Assert.Equal(footerY, bounds.absY);
            Assert.Equal(footer[0].Bounds.OuterHeight, bounds.OuterHeight);
            Assert.InRange(bounds.absX, 0, host.Canvas.Width);
            Assert.InRange(bounds.absY, 0, host.Canvas.Height);
            Assert.True(bounds.absX + bounds.OuterWidth <= host.Canvas.Width);
            Assert.True(bounds.absY + bounds.OuterHeight <= host.Canvas.Height);
        }
        for (var index = 1; index < footer.Length; index++)
            Assert.True(footer[index - 1].Bounds.absX + footer[index - 1].Bounds.OuterWidth < footer[index].Bounds.absX,
                "Hub footer actions must have a gap between their click targets.");
        foreach (var card in cards)
        {
            Assert.InRange(card.Bounds.absX, 0, host.Canvas.Width);
            Assert.True(card.Bounds.absX + card.Bounds.OuterWidth <= host.Canvas.Width);
            Assert.True(card.Bounds.absY + card.Bounds.OuterHeight < footerY,
                "Topic cards must not overlap the hub footer actions.");
        }
        for (var first = 0; first < cards.Length; first++)
            for (var second = first + 1; second < cards.Length; second++)
            {
                var a = cards[first].Bounds;
                var b = cards[second].Bounds;
                Assert.True(a.absX + a.OuterWidth < b.absX || b.absX + b.OuterWidth < a.absX ||
                    a.absY + a.OuterHeight < b.absY || b.absY + b.OuterHeight < a.absY,
                    "Topic cards must have separate click targets.");
            }

        foreach (var topic in topics)
        {
            wizard.ShowPage("hub");
            host.RenderGuiDialog(wizard, 0);
            var card = Assert.IsType<GuiElementSetupTopicCard>(wizard.SingleComposer["topic-" + topic.Id]);
            ClickElement(host, wizard, card, 0.75);
            Assert.Equal(topic.EntryPageId, wizard.CurrentStepId);
            Assert.True(Sidebar(wizard).IsDestinationVisible(topic.EntryPageId));
            AssertNavigationFits(host, wizard);
        }

        wizard.ShowPage("hub");
        ClickElement(host, wizard, Assert.IsAssignableFrom<GuiElementTextButton>(wizard.SingleComposer["all-settings"]));
        Assert.Equal(1, advancedCalls);
        Assert.False(wizard.IsOpened());

        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        wizard.ShowPage("hub");
        Assert.True(wizard.TryOpen());
        ClickElement(host, wizard, Elements(wizard).Values.OfType<GuiElementTextButton>().Single(button => button.Text == "Close"));
        Assert.True(wizard.IsOpened());
        Assert.True(draft.IsDirty);
        Assert.True(Assert.Single(host.Api.Gui.LoadedGuis.OfType<GuiDialogConfirm>()).IsOpened());
        Assert.Equal(1, advancedCalls);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SidebarCategoriesAndLeavesRestoreTheirPagesAndDraftThroughReview(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var editedValue = draft.Get("EnableChatter");
        var changedKeys = draft.ChangedKeys.ToArray();
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.tabs");
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);
        AssertNavigationFits(host, wizard);
        var remembered = new Dictionary<string, string> { ["chat"] = "chat.tabs" };
        var topics = SetupWizardCatalog.Topics.Skip(1).Concat(SetupWizardCatalog.Topics.Take(1)).ToArray();

        foreach (var topic in topics)
        {
            ClickDestination(host, wizard, "topic:" + topic.Id);
            Assert.Equal(remembered.GetValueOrDefault(topic.Id, topic.EntryPageId), wizard.CurrentStepId);
            var detailPage = SetupWizardCatalog.Pages.Last(page => page.TopicId == topic.Id).Id;
            ScrollSidebarTo(host, wizard, detailPage);
            ClickDestination(host, wizard, detailPage);
            Assert.Equal(detailPage, wizard.CurrentStepId);
            Assert.True(Sidebar(wizard).IsDestinationVisible(detailPage));
            remembered[topic.Id] = detailPage;
            Assert.Equal(editedValue, draft.Get("EnableChatter"));
            Assert.Equal(changedKeys, draft.ChangedKeys);
        }

        foreach (var topic in topics)
        {
            ClickDestination(host, wizard, "topic:" + topic.Id);
            Assert.Equal(remembered[topic.Id], wizard.CurrentStepId);
            Assert.True(Sidebar(wizard).IsDestinationVisible(remembered[topic.Id]));
            Assert.Equal(editedValue, draft.Get("EnableChatter"));
            Assert.Equal(changedKeys, draft.ChangedKeys);
            host.RenderGuiDialog(wizard, 0);
            AssertNavigationFits(host, wizard);
        }

        ClickElement(host, wizard, wizard.SingleComposer["review"]);
        Assert.Equal("review", wizard.CurrentStepId);
        Assert.Equal("Save settings", Assert.IsAssignableFrom<GuiElementTextButton>(wizard.SingleComposer["review"]).Text);
        Assert.True(draft.IsDirty);
        ClickDestination(host, wizard, "hub");
        Assert.Equal("hub", wizard.CurrentStepId);
        ClickDestination(host, wizard, "topic:chat");
        Assert.Equal(remembered["chat"], wizard.CurrentStepId);
        Assert.Equal(changedKeys, draft.ChangedKeys);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1600, 1000, 1.0)]
    [InlineData(1600, 1000, 1.25)]
    [InlineData(1280, 720, 1.0)]
    [InlineData(1280, 720, 1.25)]
    public void SidebarKeepsCategoryHeadersVisibleWhileLongBranchesScrollAndAllPagesFit(int width, int height, double scale)
    {
        using var host = CreateHost(scale, width, height);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("AllowPlayerTpa", "0");
        draft.Set("Teleportation.RegisterHomeCommands", "0");
        draft.Set("Teleportation.RegisterSpawnCommands", "0");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("teleport.tools");
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);
        var headerBounds = SetupWizardCatalog.Topics.ToDictionary(topic => topic.Id,
            topic => BoundsSnapshot(Sidebar(wizard).DestinationBounds("topic:" + topic.Id)));
        var lastPage = SetupWizardCatalog.Pages.Last(page => page.TopicId == "teleportation").Id;
        Assert.False(Sidebar(wizard).IsDestinationVisible(lastPage));
        ScrollSidebarTo(host, wizard, lastPage);
        foreach (var topic in SetupWizardCatalog.Topics)
        {
            Assert.True(Sidebar(wizard).IsDestinationVisible("topic:" + topic.Id));
            Assert.Equal(headerBounds[topic.Id], BoundsSnapshot(Sidebar(wizard).DestinationBounds("topic:" + topic.Id)));
        }
        ClickDestination(host, wizard, lastPage);
        Assert.Equal(lastPage, wizard.CurrentStepId);

        foreach (var page in SetupWizardCatalog.Pages)
        {
            wizard.ShowPage(page.Id);
            host.RenderGuiDialog(wizard, 0);
            Assert.True(Sidebar(wizard).IsDestinationVisible(page.Id), $"Selecting {page.Id} must reveal its sidebar row.");
            AssertNavigationFits(host, wizard);
            foreach (var key in page.SettingKeys)
            {
                var element = wizard.SingleComposer.GetElement(key);
                if (element == null) continue;
                AssertBoundsInsideCanvas(host, element.Bounds, $"{page.Id}: {key}");
                Assert.True(element.Bounds.absX > Sidebar(wizard).Bounds.absX + Sidebar(wizard).Bounds.OuterWidth,
                    $"{key} must stay in the editor to the right of the sidebar.");
                Assert.True(element.Bounds.absY + element.Bounds.OuterHeight < wizard.SingleComposer["close"].Bounds.absY,
                    $"{key} must fit above the shared footer.");
            }
        }
        Assert.Equal(40, SetupWizardCatalog.Pages.Count);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SidebarKeyboardNavigationRestoresFocusAndDirtyIndicatorsWithoutReplacingAnEditedField(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.ranges");
        wizard.TryOpen();
        var composer = wizard.SingleComposer;
        var sidebar = Sidebar(wizard);
        var distance = composer.GetTextInput("ProximityChatModeDistances.Normal");
        Assert.True(composer.FocusElement(distance.TabIndex));
        distance.SetValue("17");
        Assert.Same(composer, wizard.SingleComposer);
        Assert.Same(sidebar, Sidebar(wizard));
        Assert.True(distance.HasFocus);
        Assert.True(sidebar.IsDestinationDirty("chat.ranges"));
        Assert.True(sidebar.IsDestinationDirty("topic:chat"));
        Assert.False(sidebar.IsDestinationDirty("topic:map"));

        Assert.True(composer.FocusElement(sidebar.TabIndex));
        Assert.Equal("chat.ranges", sidebar.FocusedDestination);
        PressWizard(wizard, GlKeys.Down);
        Assert.Equal("chat.obfuscation", sidebar.FocusedDestination);
        Assert.Equal("chat.ranges", wizard.CurrentStepId);
        PressWizard(wizard, GlKeys.Enter);
        Assert.Equal("chat.obfuscation", wizard.CurrentStepId);
        Assert.True(Sidebar(wizard).HasFocus);
        Assert.Equal("chat.obfuscation", Sidebar(wizard).FocusedDestination);
        PressWizard(wizard, GlKeys.Up);
        PressWizard(wizard, GlKeys.Space);
        Assert.Equal("chat.ranges", wizard.CurrentStepId);
        Assert.True(Sidebar(wizard).HasFocus);
        Assert.Equal("17", wizard.SingleComposer.GetTextInput("ProximityChatModeDistances.Normal").GetText());
        Assert.Equal("17", draft.Get("ProximityChatModeDistances.Normal"));
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0, 1.25)]
    public void ChangingGuiScaleRebuildsTheLiveLayoutWithoutLosingItsPageOrDraft(double initialScale, double targetScale)
    {
        using var host = CreateHost(initialScale, 1280, 720);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.ranges");
        wizard.TryOpen();
        wizard.SingleComposer.GetTextInput("ProximityChatModeDistances.Normal").SetValue("17");
        host.RenderGuiDialog(wizard, 0);
        var before = wizard.SingleComposer;
        var beforeFieldWidth = before["ProximityChatModeDistances.Normal"].Bounds.fixedWidth;
        var changedKeys = draft.ChangedKeys.ToArray();

        RuntimeEnv.GUIScale = (float)targetScale;
        host.RenderGuiDialog(wizard, 0);

        Assert.NotSame(before, wizard.SingleComposer);
        Assert.Equal("chat.ranges", wizard.CurrentStepId);
        Assert.True(Sidebar(wizard).IsDestinationVisible("chat.ranges"));
        Assert.True(Sidebar(wizard).IsDestinationDirty("chat.ranges"));
        Assert.Equal("17", wizard.SingleComposer.GetTextInput("ProximityChatModeDistances.Normal").GetText());
        Assert.Equal(changedKeys, draft.ChangedKeys);
        Assert.Equal("17", draft.Get("ProximityChatModeDistances.Normal"));
        Assert.True(wizard.SingleComposer["ProximityChatModeDistances.Normal"].Bounds.fixedWidth < beforeFieldWidth,
            "A larger GUI scale must reduce the editor width in the same client viewport.");
        foreach (var key in SetupWizardCatalog.Pages.Single(page => page.Id == "chat.ranges").SettingKeys)
        {
            var bounds = wizard.SingleComposer[key].Bounds;
            AssertBoundsInsideCanvas(host, bounds, key);
            Assert.True(bounds.absY + bounds.OuterHeight < wizard.SingleComposer["close"].Bounds.absY);
        }
        AssertNavigationFits(host, wizard);
        var recomposed = wizard.SingleComposer;
        host.RenderGuiDialog(wizard, 0);
        Assert.Same(recomposed, wizard.SingleComposer);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void HubKeepsTheDraftCountInTheFooterWhileFailureAndPendingStatusStayAboveIt(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saveCalls = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "hub-status", true,
            () => saveCalls++, null!, null!, layoutOnly: true);
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);

        GuiElementStaticText[] VisibleText() => wizard.SingleComposer.staticElements.Values.OfType<GuiElementStaticText>().ToArray();
        GuiElementRichtext DraftCount() => wizard.SingleComposer.GetRichtext("footer-draft-count");
        var changedKeys = draft.ChangedKeys.ToArray();
        Assert.Equal("1 unsaved change", DisplayedText(DraftCount()));
        var footerBounds = wizard.SingleComposer["close"].Bounds;
        Assert.True(DraftCount().Bounds.absY >= footerBounds.absY);
        Assert.True(DraftCount().Bounds.absY + DraftCount().Bounds.OuterHeight <= footerBounds.absY + footerBounds.OuterHeight);

        const string failure = "Could not save your changes. Try again.";
        wizard.SetRequestFailure(failure);
        host.RenderGuiDialog(wizard, 0);
        var texts = VisibleText();
        var failureText = Assert.Single(texts.Where(text => text.Text == failure));
        Assert.Equal("1 unsaved change", DisplayedText(DraftCount()));
        Assert.True(failureText.Bounds.absY + failureText.Bounds.OuterHeight < wizard.SingleComposer["close"].Bounds.absY,
            "The save failure message must fit above the shared footer.");
        Assert.Equal(changedKeys, draft.ChangedKeys);

        ClickElement(host, wizard, wizard.SingleComposer["review"]);
        var save = Assert.Single(wizard.SingleComposer.staticElements.Values.OfType<GuiElementTextButton>()
            .Where(button => button.Text == "Save settings"));
        ClickElement(host, wizard, save);
        Assert.Equal(1, saveCalls);
        ClickDestination(host, wizard, "hub");
        Assert.Equal("hub", wizard.CurrentStepId);
        host.RenderGuiDialog(wizard, 0);
        texts = VisibleText();
        var pendingText = Assert.Single(texts.Where(text => text.Text == "Saving settings..."));
        Assert.Equal("1 unsaved change", DisplayedText(DraftCount()));
        Assert.DoesNotContain(texts, text => text.Text == failure);
        Assert.True(pendingText.Bounds.absY + pendingText.Bounds.OuterHeight < wizard.SingleComposer["close"].Bounds.absY,
            "The pending-save message must fit above the shared footer.");
        Assert.Equal(changedKeys, draft.ChangedKeys);
        Assert.Equal(1, saveCalls);
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

        foreach (var page in SetupWizardCatalog.Pages.Where(page =>
            page.TopicId is "appearance" or "map" or "characters" or "roleplay" or "server"))
        {
            wizard.ShowPage(page.Id);
            var appearance = page.TopicId == "appearance";
            AssertPreviewElements(wizard, chat: false, diagram: SetupWizardFeaturePreview.Supports(page.Id), layoutGuide: appearance);
            if (appearance)
                Assert.Contains(Elements(wizard).Values.OfType<GuiElementStaticText>(), text => text.Text == "Native 3D Pip omitted");
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
    public void NewTopicDiagramsShowTheirDraftChoices(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ManageMapPlayerVisibility", "1");
        draft.Set("EnableCharacterSheets", "1");
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();
        host.SetMouse(-1, -1);
        var cases = new[]
        {
            (Page: "appearance.bubbles", Key: "OverheadChatBubbleMode", Before: "RpText", After: "Off"),
            (Page: "map.visibility", Key: "MapHideOtherPlayers", Before: "0", After: "1"),
            (Page: "characters.slots", Key: "EnableRpCharacterSlots", Before: "0", After: "1"),
            (Page: "roleplay.dice", Key: "EnableDiceRolling", Before: "1", After: "0"),
            (Page: "server.history", Key: "EnableChatHistory", Before: "1", After: "0")
        };
        foreach (var example in cases)
        {
            draft.Set(example.Key, example.Before);
            wizard.ShowPage(example.Page);
            host.RenderGuiDialog(wizard, 0);
            var before = DiagramPixels(host, wizard);

            draft.Set(example.Key, example.After);
            wizard.ShowPage(example.Page);
            host.RenderGuiDialog(wizard, 0);
            var after = DiagramPixels(host, wizard);
            Assert.False(before.SequenceEqual(after),
                $"The {example.Page} preview must show the change to {example.Key}.");
        }
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SettingsOnlyPagesUseACenteredColumnWithoutAnUnrelatedPreview(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();
        var pages = new[] { "characters.access", "roleplay.scenemarkers", "roleplay.nicknamechanges",
            "roleplay.nicknamerules", "server.retention", "server.notelimits" };
        foreach (var pageId in pages)
        {
            wizard.ShowPage(pageId);
            AssertPreviewElements(wizard, chat: false, diagram: false);
            Assert.True(SetupWizardCatalog.TryGetPage(pageId, out var page));
            Assert.DoesNotContain(Elements(wizard).Values.OfType<GuiElementStaticText>(),
                text => text.Text == "Native 3D Pip omitted");
            foreach (var key in page.SettingKeys)
            {
                var control = wizard.SingleComposer[key];
                Assert.Equal(167, control.Bounds.fixedX);
                Assert.Equal(546, control.Bounds.fixedWidth);
                Assert.InRange(control.Bounds.renderX, 0, host.Canvas.Width);
                Assert.InRange(control.Bounds.renderX + control.Bounds.OuterWidth, 0, host.Canvas.Width);
            }
            host.SetMouse(-1, -1);
            host.RenderGuiDialog(wizard, 0);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void FeaturePicturesStayFocusedOnAppearanceWhenOnlyAccessOrLimitsChange(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        foreach (var key in new[] { "EnableTypingIndicator", "EnableCharacterSheets", "EnableAdminNotes",
            "EnableStructuredAdminNotes", "EnableAdminNoteLedger", "EnablePlayerNotes", "EnableChatHistory", "PlayerStatSystem" })
            draft.Set(key, "1");
        draft.Set("DisableRPChat", "0");
        draft.Set("OverheadChatBubbleMode", "RpText");
        draft.Set("TypingIndicatorMaxRange", "32");
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();
        var cases = new[]
        {
            (Page: "appearance.bubbles", Key: "SpeechBubbleMinimumDisplayMilliseconds", Before: "5000", After: "9000"),
            (Page: "appearance.typing", Key: "TypingIndicatorTimeoutSeconds", Before: "5", After: "10"),
            (Page: "appearance.nametagcolors", Key: "ChangeNametagColorPermission", Before: "chat", After: "controlserver"),
            (Page: "characters.sheets", Key: "CharacterSheetAutoOpenOnCharacterScreen", Before: "0", After: "1"),
            (Page: "server.history", Key: "ChatHistoryPermission", Before: "chat", After: "controlserver"),
            (Page: "server.history", Key: "ChatHistoryManagePermission", Before: "chat", After: "controlserver"),
            (Page: "server.staffnotes", Key: "AdminNotesPermission", Before: "chat", After: "controlserver"),
            (Page: "server.staffnotes", Key: "MaxAdminNotesPerTarget", Before: "20", After: "50"),
            (Page: "server.personalnotes", Key: "PlayerNotesPermission", Before: "chat", After: "controlserver"),
            (Page: "server.personalnotes", Key: "MaxPlayerNotesPerAuthor", Before: "20", After: "50"),
            (Page: "server.stats", Key: "PlayerStatClearPermission", Before: "chat", After: "controlserver")
        };
        foreach (var example in cases)
        {
            draft.Set(example.Key, example.Before);
            wizard.ShowPage(example.Page);
            host.SetMouse(-1, -1);
            host.RenderGuiDialog(wizard, 0);
            var before = DiagramPixels(host, wizard);
            draft.Set(example.Key, example.After);
            wizard.ShowPage(example.Page);
            host.RenderGuiDialog(wizard, 0);
            Assert.True(before.SequenceEqual(DiagramPixels(host, wizard)),
                $"The {example.Page} picture must not repeat the {example.Key} control's value.");
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void MapSampleUsesTheSameLandscapeAndSelfMarkerAcrossVisibilityChoices(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ManageMapPlayerVisibility", "0");
        draft.Set("MapHideOtherPlayers", "0");
        draft.Set("MapPlayerRenderDistance", "1000");
        using var wizard = CreateWizard(host, draft);
        wizard.TryOpen();
        byte[] RenderMap()
        {
            wizard.ShowPage("map.visibility");
            host.SetMouse(-1, -1);
            host.RenderGuiDialog(wizard, 0);
            return DiagramPixels(host, wizard);
        }
        var unmanaged = RenderMap();
        var diagram = Assert.IsType<GuiElementCustomDraw>(wizard.SingleComposer["preview-diagram"]);
        Assert.Equal(310, diagram.Bounds.fixedWidth);
        Assert.Equal(270, diagram.Bounds.fixedHeight);
        var selfMarker = DiagramRegionPixels(host, wizard, 147, 127, 16, 16);
        Assert.True(Enumerable.Range(0, selfMarker.Length / 4).Any(index =>
            selfMarker[index * 4] > 245 && selfMarker[index * 4 + 1] > 245 && selfMarker[index * 4 + 2] > 245),
            "The native self marker should retain its white fill.");
        Assert.True(Enumerable.Range(0, selfMarker.Length / 4).Any(index =>
            selfMarker[index * 4] < 10 && selfMarker[index * 4 + 1] < 10 && selfMarker[index * 4 + 2] < 10),
            "The native self marker should retain its black outline.");
        var self = DiagramRegionPixels(host, wizard, 152, 132, 6, 6);

        draft.Set("MapHideOtherPlayers", "1");
        draft.Set("MapPlayerRenderDistance", "0");
        Assert.True(unmanaged.SequenceEqual(RenderMap()), "Unmanaged maps must ignore the disabled hide and range drafts.");

        draft.Set("ManageMapPlayerVisibility", "1");
        var hidden = RenderMap();
        Assert.False(unmanaged.SequenceEqual(hidden));
        Assert.True(self.SequenceEqual(DiagramRegionPixels(host, wizard, 152, 132, 6, 6)));
        draft.Set("MapHideOtherPlayers", "0");
        Assert.True(hidden.SequenceEqual(RenderMap()), "A zero range and hiding others should both leave only the self marker.");
        draft.Set("MapPlayerRenderDistance", "30");
        var nearby = RenderMap();
        Assert.False(hidden.SequenceEqual(nearby));
        Assert.True(self.SequenceEqual(DiagramRegionPixels(host, wizard, 152, 132, 6, 6)));
        draft.Set("MapPlayerRenderDistance", "80");
        Assert.False(nearby.SequenceEqual(RenderMap()));
        draft.Set("MapPlayerRenderDistance", "-1");
        Assert.True(unmanaged.SequenceEqual(RenderMap()), "An unlimited managed map should show all sample markers.");
        Assert.True(self.SequenceEqual(DiagramRegionPixels(host, wizard, 152, 132, 6, 6)));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void MapMarkerToggleDisablesDependentControlsWithoutLosingDraftValues(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ManageMapPlayerVisibility", "1");
        draft.Set("MapHideOtherPlayers", "0");
        draft.Set("MapPlayerRenderDistance", "1200");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("map.visibility");
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);

        var composer = wizard.SingleComposer;
        var toggle = Assert.IsType<GuiElementToggleButton>(composer["ManageMapPlayerVisibility"]);
        Assert.True(toggle.Toggleable);
        Assert.True(toggle.On);
        Assert.Equal("On", toggle.Text);
        AssertMapControlState(wizard, true, true);

        ClickElement(host, wizard, toggle);
        Assert.Same(composer, wizard.SingleComposer);
        Assert.False(toggle.On);
        Assert.Equal("Off", toggle.Text);
        Assert.Equal("0", draft.Get("ManageMapPlayerVisibility"));
        AssertMapControlState(wizard, false, false);
        var hide = composer.GetDropDown("MapHideOtherPlayers");
        var range = composer.GetTextInput("MapPlayerRenderDistance");
        ClickElement(host, wizard, hide);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.End });
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        Assert.False(hide.listMenu.IsOpened);
        Assert.False(hide.HasFocus);
        ClickElement(host, wizard, range);
        wizard.OnKeyPress(new KeyEvent { KeyChar = '9', KeyCode = (int)GlKeys.Number9 });
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.BackSpace });
        Assert.False(range.HasFocus);
        Assert.Equal("1200", range.GetText());
        Assert.Equal("0", draft.Get("MapHideOtherPlayers"));
        Assert.Equal("1200", draft.Get("MapPlayerRenderDistance"));
        SaveMapControlScreenshot(host, wizard, scale, "off");

        ClickElement(host, wizard, toggle);
        Assert.True(toggle.On);
        Assert.Equal("On", toggle.Text);
        AssertMapControlState(wizard, true, true);
        Assert.Equal("0", hide.SelectedValue);
        Assert.Equal("1200", range.GetText());
        SaveMapControlScreenshot(host, wizard, scale, "on");

        ClickElement(host, wizard, hide);
        Assert.True(hide.listMenu.IsOpened);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.End });
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        Assert.Equal("1", draft.Get("MapHideOtherPlayers"));
        AssertMapControlState(wizard, true, false);
        ClickElement(host, wizard, range);
        wizard.OnKeyPress(new KeyEvent { KeyChar = '9', KeyCode = (int)GlKeys.Number9 });
        Assert.Equal("1200", draft.Get("MapPlayerRenderDistance"));
        SaveMapControlScreenshot(host, wizard, scale, "hidden");

        ClickElement(host, wizard, toggle);
        AssertMapControlState(wizard, false, false);
        ClickElement(host, wizard, toggle);
        AssertMapControlState(wizard, true, false);
        Assert.Equal("1", hide.SelectedValue);
        Assert.Equal("1200", range.GetText());

        ClickElement(host, wizard, hide);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Home });
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        AssertMapControlState(wizard, true, true);
        ClickElement(host, wizard, range);
        Assert.True(range.HasFocus);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.End });
        wizard.OnKeyPress(new KeyEvent { KeyChar = '0', KeyCode = (int)GlKeys.Number0 });
        Assert.Equal("12000", range.GetText());
        Assert.Equal("12000", draft.Get("MapPlayerRenderDistance"));
        Assert.Empty(host.UnsupportedCalls);
    }

    private static void AssertMapControlState(SetupWizardDialog wizard, bool hideEnabled, bool rangeEnabled)
    {
        foreach (var (key, enabled) in new[] { ("MapHideOtherPlayers", hideEnabled), ("MapPlayerRenderDistance", rangeEnabled) })
        {
            var control = Assert.IsAssignableFrom<GuiElementTextBase>(wizard.SingleComposer[key]);
            Assert.Equal(enabled, control.Enabled);
            Assert.Equal(enabled, control.Focusable);
            Assert.Equal(enabled ? 1 : 0.35, control.Font.Color[3], 5);
            var label = Assert.IsType<GuiElementStaticText>(wizard.SingleComposer["setting-label-" + key]);
            Assert.Equal(enabled ? 1 : 0.35, label.Font.Color[3], 5);
        }
    }

    private static void SaveMapControlScreenshot(PreviewHost host, SetupWizardDialog wizard, double scale, string state)
    {
        host.SetMouse(-1, -1);
        wizard.OnMouseMove(new MouseEvent(-1, -1));
        host.RenderGuiDialog(wizard, 0);
        var output = Path.Combine(Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ??
            Path.Combine(AppContext.BaseDirectory, "visual-artifacts"), "wizard-map");
        Directory.CreateDirectory(output);
        host.Canvas.SavePng(Path.Combine(output, state + "-" + scale.ToString("0.##", CultureInfo.InvariantCulture) + ".png"));
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
        Assert.Equal("Save settings", Assert.IsAssignableFrom<GuiElementTextButton>(wizard.SingleComposer["review"]).Text);
        AssertNavigationFits(host, wizard);
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SleepPercentageEditKeepsItsLabelAndUnitsInReview(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("notifications.sleep");
        wizard.TryOpen();

        const string label = "Sleeping players needed (%)";
        Assert.Contains(Elements(wizard).Values.OfType<GuiElementStaticText>(), text => text.Text == label);
        var threshold = wizard.SingleComposer.GetTextInput("SleepNotificationThreshold");
        Assert.Equal("50", threshold.GetText());
        threshold.SetValue("75");
        Assert.Equal("75", threshold.GetText());
        Assert.Equal("0.75", draft.Get("SleepNotificationThreshold"));
        host.RenderGuiDialog(wizard, 0);

        wizard.ShowPage("review");
        host.RenderGuiDialog(wizard, 0);
        var reviewText = Elements(wizard).Values.OfType<GuiElementStaticText>().Select(text => text.Text).ToArray();
        Assert.Contains(label, reviewText);
        Assert.Contains("50%  >  75%", reviewText);
        Assert.DoesNotContain(reviewText, text => text.Contains("0.5", StringComparison.Ordinal) || text.Contains("0.75", StringComparison.Ordinal));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void UnlimitedSpeechDiagramStaysTheSameWhenDistanceFadingIsEnabled(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        foreach (var mode in new[] { "Whisper", "Normal", "Yell" })
            draft.Set("ProximityChatModeDistances." + mode, "-1");
        draft.Set("EnableDistanceObfuscationSystem", "0");
        using var wizard = CreateWizard(host, draft);
        wizard.ShowPage("chat.obfuscation");
        wizard.TryOpen();
        host.SetMouse(-1, -1);
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal(0, Assert.IsType<GuiElementDropDown>(wizard.SingleComposer["EnableDistanceObfuscationSystem"]).listMenu.SelectedIndex);
        var before = host.Canvas.GetRgbaPixels();
        var bounds = Assert.IsType<GuiElementCustomDraw>(wizard.SingleComposer["preview-diagram"]).Bounds;
        var region = ((int)bounds.renderX, (int)bounds.renderY, bounds.OuterWidthInt, bounds.OuterHeightInt);

        draft.Set("EnableDistanceObfuscationSystem", "1");
        wizard.ShowPage("chat.obfuscation");
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal(1, Assert.IsType<GuiElementDropDown>(wizard.SingleComposer["EnableDistanceObfuscationSystem"]).listMenu.SelectedIndex);
        var after = host.Canvas.GetRgbaPixels();
        bounds = Assert.IsType<GuiElementCustomDraw>(wizard.SingleComposer["preview-diagram"]).Bounds;
        Assert.Equal(region, ((int)bounds.renderX, (int)bounds.renderY, bounds.OuterWidthInt, bounds.OuterHeightInt));
        var (left, top, width, height) = region;
        Assert.InRange(left, 0, host.Canvas.Width - 1);
        Assert.InRange(top, 0, host.Canvas.Height - 1);
        Assert.InRange(width, 1, host.Canvas.Width - left);
        Assert.InRange(height, 1, host.Canvas.Height - top);
        for (var y = top; y < top + height; y++)
        {
            var offset = (y * host.Canvas.Width + left) * 4;
            Assert.True(before.AsSpan(offset, width * 4).SequenceEqual(after.AsSpan(offset, width * 4)),
                $"Unlimited speech preview changed inside the diagram on row {y} when distance fading was enabled.");
        }
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
        Assert.Equal("1 of 4 players are asleep.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        var fixedPixels = host.Canvas.GetRgbaPixels();
        host.SetClock(2100);
        host.RenderGuiDialog(wizard, 2.1f);
        Assert.Equal("1 of 4 players are asleep.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        Assert.Equal(fixedPixels, host.Canvas.GetRgbaPixels());

        wizard.ResumePreview();
        host.SetClock(4200);
        host.RenderGuiDialog(wizard, 4.1f);
        Assert.Equal("2 of 4 players are asleep.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal(draft.Get("TEXT_SleepNotification"), PreviewText(wizard));
        Assert.False(fixedPixels.SequenceEqual(host.Canvas.GetRgbaPixels()));
        Assert.True(Sidebar(wizard).IsDestinationVisible("notifications.sleep"));

        foreach (var threshold in new[] { 0d, 0.25, 0.5, 0.75, 1 })
        {
            draft.Set("SleepNotificationThreshold", threshold.ToString(CultureInfo.InvariantCulture));
            for (var sleepers = 0; sleepers < 4; sleepers++)
            {
                wizard.SetPreviewTime(sleepers * 2);
                host.RenderGuiDialog(wizard, 0);
                Assert.Equal($"{sleepers} of 4 players are asleep.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
                var crossed = threshold > 0 && threshold < 1 && sleepers / 4d >= threshold;
                Assert.Equal(crossed ? draft.Get("TEXT_SleepNotification") : "", PreviewText(wizard));
            }
        }
        draft.Set("EnableSleepNotifications", "0");
        draft.Set("SleepNotificationThreshold", "0.75");
        wizard.SetPreviewTime(6);
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal("3 of 4 players are asleep.", DisplayedText(wizard.SingleComposer.GetRichtext("preview-sleep-count")));
        Assert.Equal("", PreviewText(wizard));
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost CreateHost(double scale, int width = 1600, int height = 1000) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);

    private static SetupWizardDialog CreateWizard(PreviewHost host, SetupWizardDraft draft, Action? onAdvanced = null) =>
        new(host.Api, draft, "chat-preview", true, null!, null!, null!, onAdvanced: onAdvanced!, layoutOnly: true);

    private static string PreviewText(SetupWizardDialog wizard) => DisplayedText(wizard.SingleComposer.GetRichtext("preview-text"));

    private static GuiElementSetupSidebar Sidebar(SetupWizardDialog wizard) =>
        Assert.IsType<GuiElementSetupSidebar>(wizard.SingleComposer["setup-sidebar"]);

    private static void AssertNavigationFits(PreviewHost host, SetupWizardDialog wizard)
    {
        var sidebar = Sidebar(wizard);
        AssertBoundsInsideCanvas(host, sidebar.Bounds, "sidebar");
        Assert.True(sidebar.IsDestinationVisible("hub"));
        foreach (var topic in SetupWizardCatalog.Topics)
            Assert.True(sidebar.IsDestinationVisible("topic:" + topic.Id), $"The {topic.Title} category must stay visible.");
        Assert.DoesNotContain(Elements(wizard).Keys, key => key is "topic-tabs" or "page-picker");
        Assert.DoesNotContain(Elements(wizard).Values.OfType<GuiElementTextButton>(),
            button => button.Text is "< Back" or "Next >" or "All topics");
        var footer = new[] { "all-settings", "close", "review" }
            .Select(key => wizard.SingleComposer[key]).OrderBy(element => element.Bounds.absX).ToArray();
        foreach (var element in footer)
        {
            AssertBoundsInsideCanvas(host, element.Bounds, "footer");
            Assert.Equal(footer[0].Bounds.absY, element.Bounds.absY);
        }
        for (var index = 1; index < footer.Length; index++)
            Assert.True(footer[index - 1].Bounds.absX + footer[index - 1].Bounds.OuterWidth < footer[index].Bounds.absX,
                "Shared footer actions need separate click targets.");
    }

    private static void AssertBoundsInsideCanvas(PreviewHost host, ElementBounds bounds, string name)
    {
        Assert.True(bounds.absX >= 0 && bounds.absX + bounds.OuterWidth <= host.Canvas.Width,
            $"{name} must fit horizontally inside the {host.Canvas.Width}-pixel client.");
        Assert.True(bounds.absY >= 0 && bounds.absY + bounds.OuterHeight <= host.Canvas.Height,
            $"{name} must fit vertically inside the {host.Canvas.Height}-pixel client.");
    }

    private static (double X, double Y, double Width, double Height) BoundsSnapshot(ElementBounds bounds) =>
        (bounds.absX, bounds.absY, bounds.OuterWidth, bounds.OuterHeight);

    private static void PressWizard(SetupWizardDialog wizard, GlKeys key)
    {
        var args = new KeyEvent { KeyCode = (int)key };
        wizard.OnKeyDown(args);
        Assert.True(args.Handled);
    }

    private static void ClickDestination(PreviewHost host, SetupWizardDialog wizard, string destination)
    {
        var sidebar = Sidebar(wizard);
        Assert.True(sidebar.IsDestinationVisible(destination), $"{destination} must be visible before clicking.");
        var bounds = sidebar.DestinationBounds(destination);
        var x = (int)(bounds.absX + bounds.OuterWidth / 2);
        var y = (int)(bounds.absY + bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseMove(new MouseEvent(x, y));
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }

    private static void ScrollSidebarTo(PreviewHost host, SetupWizardDialog wizard, string destination)
    {
        for (var attempt = 0; !Sidebar(wizard).IsDestinationVisible(destination) && attempt < 30; attempt++)
        {
            var sidebar = Sidebar(wizard);
            var bounds = sidebar.BranchBounds;
            host.SetMouse((int)(bounds.absX + bounds.OuterWidth / 2),
                (int)(bounds.absY + bounds.OuterHeight / 2));
            wizard.OnMouseWheel(new MouseWheelEventArgs { delta = -1, deltaPrecise = -1 });
        }
        Assert.True(Sidebar(wizard).IsDestinationVisible(destination), $"The branch must scroll to {destination}.");
    }

    private static byte[] DiagramPixels(PreviewHost host, SetupWizardDialog wizard)
    {
        var bounds = Assert.IsType<GuiElementCustomDraw>(wizard.SingleComposer["preview-diagram"]).Bounds;
        var left = (int)bounds.renderX;
        var top = (int)bounds.renderY;
        var width = bounds.OuterWidthInt;
        var height = bounds.OuterHeightInt;
        Assert.InRange(left, 0, host.Canvas.Width - 1);
        Assert.InRange(top, 0, host.Canvas.Height - 1);
        Assert.InRange(width, 1, host.Canvas.Width - left);
        Assert.InRange(height, 1, host.Canvas.Height - top);
        var canvas = host.Canvas.GetRgbaPixels();
        var cropped = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
            Buffer.BlockCopy(canvas, ((top + row) * host.Canvas.Width + left) * 4, cropped, row * width * 4, width * 4);
        return cropped;
    }

    private static byte[] DiagramRegionPixels(PreviewHost host, SetupWizardDialog wizard,
        double x, double y, double width, double height)
    {
        var bounds = Assert.IsType<GuiElementCustomDraw>(wizard.SingleComposer["preview-diagram"]).Bounds;
        var left = (int)Math.Round(bounds.renderX + x * bounds.OuterWidth / bounds.fixedWidth);
        var top = (int)Math.Round(bounds.renderY + y * bounds.OuterHeight / bounds.fixedHeight);
        var pixelWidth = (int)Math.Ceiling(width * bounds.OuterWidth / bounds.fixedWidth);
        var pixelHeight = (int)Math.Ceiling(height * bounds.OuterHeight / bounds.fixedHeight);
        Assert.InRange(left, 0, host.Canvas.Width - pixelWidth);
        Assert.InRange(top, 0, host.Canvas.Height - pixelHeight);
        var canvas = host.Canvas.GetRgbaPixels();
        var cropped = new byte[pixelWidth * pixelHeight * 4];
        for (var row = 0; row < pixelHeight; row++)
            Buffer.BlockCopy(canvas, ((top + row) * host.Canvas.Width + left) * 4,
                cropped, row * pixelWidth * 4, pixelWidth * 4);
        return cropped;
    }

    private static string DisplayedText(GuiElementRichtext text) =>
        string.Join(" ", text.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));

    private static readonly string[] PresentationDescriptions =
    ["Adds a speech verb, such as \"says\", before the dialogue.", "Shows the speaker's name and speech in quotes.", "Shows the speaker's name and message without quotes.",
        "Mix actions and dialogue in one message."];

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

    private static void ClickElement(PreviewHost host, SetupWizardDialog wizard, GuiElement element, double heightFraction = 0.5)
    {
        var x = (int)(element.Bounds.absX + element.Bounds.OuterWidth / 2);
        var y = (int)(element.Bounds.absY + element.Bounds.OuterHeight * heightFraction);
        host.SetMouse(x, y);
        wizard.OnMouseMove(new MouseEvent(x, y));
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }

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
        Assert.Empty(before);
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
