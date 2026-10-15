using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class GuiElementSetupSidebarTests
{
    [VisualTheory]
    [InlineData(1600, 1000, 1.0)]
    [InlineData(1600, 1000, 1.25)]
    [InlineData(1280, 720, 1.0)]
    [InlineData(1280, 720, 1.25)]
    public void HubDescriptionsWrapFullyInsideAllEightCards(int width, int height, double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        using var wizard = new SetupWizardDialog(host.Api, draft, "sidebar-card-fit", true,
            null!, null!, null!, layoutOnly: true);
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0);
        foreach (var topic in SetupWizardCatalog.Topics)
        {
            var card = Assert.IsType<GuiElementSetupTopicCard>(wizard.SingleComposer["topic-" + topic.Id]);
            Assert.InRange(card.DescriptionFontSize, 12, 14);
            Assert.True(card.DescriptionDrawBottom <= card.Bounds.OuterHeight - 7 * scale,
                $"{topic.Id} description bottom {card.DescriptionDrawBottom} exceeds card height {card.Bounds.OuterHeight} at {width}x{height}, scale {scale}.");
            if (width == 1280 && scale == 1.25 && topic.Id is "notifications" or "roleplay")
                Assert.True(card.DescriptionFontSize < 14, topic.Id + " must shrink its wrapped description to fit the compact card.");
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EveryCatalogPageIsDiscoverableAndSelectionRevealsIt(double scale)
    {
        using var host = CreateHost(scale);
        using var composer = Composer(host);
        var sidebar = Sidebar(host);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        foreach (var page in SetupWizardCatalog.Pages)
        {
            sidebar.SetState(page.Id, page.TopicId, []);
            Assert.NotNull(sidebar.DestinationBounds(page.Id));
            Assert.True(sidebar.IsDestinationVisible(page.Id), page.Id);
            foreach (var topic in SetupWizardCatalog.Topics)
            {
                Assert.True(sidebar.IsDestinationVisible("topic:" + topic.Id));
                var bounds = sidebar.DestinationBounds("topic:" + topic.Id);
                Assert.True(bounds.renderY >= sidebar.Bounds.renderY);
                Assert.True(bounds.renderY + bounds.InnerHeight <= sidebar.Bounds.renderY + sidebar.Bounds.InnerHeight);
            }
        }
        Render(host, composer);
        Assert.Equal(40, SetupWizardCatalog.Pages.Count);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void MouseActivatesOnlyOnMatchingLeftReleaseAndUsesPointer(double scale)
    {
        using var host = CreateHost(scale);
        var navigated = new List<string>();
        using var composer = Composer(host);
        var sidebar = Sidebar(host, navigated.Add);
        sidebar.SetState("chat.basics", "chat", []);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        var leaf = sidebar.DestinationBounds("chat.basics");
        var (x, y) = Center(leaf);
        var externalDrag = new MouseEvent(x, y, EnumMouseButton.Left, 0);
        sidebar.OnMouseMove(host.Api, externalDrag);
        Assert.False(externalDrag.Handled);
        sidebar.OnMouseUp(host.Api, externalDrag);
        Assert.False(externalDrag.Handled);
        Assert.Empty(navigated);
        host.SetMouse(x, y);
        var down = new MouseEvent(x, y, EnumMouseButton.Left, 0);
        composer.OnMouseDown(down);
        Assert.True(down.Handled);
        Assert.Empty(navigated);
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.Equal(new[] { "chat.basics" }, navigated);
        Click(host, composer, sidebar.DestinationBounds("topic:map"));
        Click(host, composer, sidebar.DestinationBounds("hub"));
        Assert.Equal(new[] { "chat.basics", "topic:map", "hub" }, navigated);
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(850, 650, EnumMouseButton.Left, 0));
        Assert.Equal(3, navigated.Count);
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Right, 0));
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Right, 0));
        Assert.Equal(3, navigated.Count);
        host.SetMouse(x, y);
        Render(host, composer);
        Assert.Equal("normal", composer.MouseOverCursor);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void InnerWheelScrollsPagesWithoutMovingHeadersAndKeepsOutsideWheelUnhandled(double scale)
    {
        using var host = CreateHost(scale);
        using var composer = Composer(host);
        var sidebar = Sidebar(host);
        sidebar.SetState("teleport.tools", "teleportation", []);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        var headers = SetupWizardCatalog.Topics.ToDictionary(topic => topic.Id,
            topic => sidebar.DestinationBounds("topic:" + topic.Id).renderY);
        Assert.False(sidebar.IsDestinationVisible("teleport.warmup"));
        var (x, y) = Center(sidebar.BranchBounds);
        host.SetMouse(x, y);
        var wheel = new MouseWheelEventArgs { delta = -3, deltaPrecise = -3 };
        composer.OnMouseWheel(wheel);
        Assert.True(wheel.IsHandled);
        Assert.True(sidebar.ScrollOffset > 0);
        foreach (var topic in SetupWizardCatalog.Topics)
            Assert.Equal(headers[topic.Id], sidebar.DestinationBounds("topic:" + topic.Id).renderY);
        var offset = sidebar.ScrollOffset;
        host.SetMouse(850, 650);
        wheel = new MouseWheelEventArgs { delta = -1, deltaPrecise = -1 };
        composer.OnMouseWheel(wheel);
        Assert.False(wheel.IsHandled);
        Assert.Equal(offset, sidebar.ScrollOffset);
        // Refreshing dirty dots does not jump the branch back to the selected leaf.
        sidebar.SetState("teleport.tools", "teleportation", ["AllowPlayerTpa"]);
        Assert.Equal(offset, sidebar.ScrollOffset);
        Render(host, composer);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ShortBranchesStayCompactAndAllHeadersFitCompactHeight(double scale)
    {
        using var host = CreateHost(scale);
        using var composer = Composer(host);
        var sidebar = Sidebar(host, height: 422);
        sidebar.SetState("map.visibility", "map", []);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        Assert.Equal(28 * scale, sidebar.BranchBounds.InnerHeight, 3);
        Assert.Equal(0, sidebar.MaxScrollOffset);
        var map = sidebar.DestinationBounds("topic:map");
        var characters = sidebar.DestinationBounds("topic:characters");
        Assert.Equal((31 + 28) * scale, characters.renderY - map.renderY, 3);
        sidebar.SetState("teleport.warmup", "teleportation", []);
        Assert.True(sidebar.IsDestinationVisible("teleport.warmup"));
        foreach (var topic in SetupWizardCatalog.Topics)
        {
            var bounds = sidebar.DestinationBounds("topic:" + topic.Id);
            Assert.True(bounds.renderY + bounds.InnerHeight <= sidebar.Bounds.renderY + sidebar.Bounds.InnerHeight + 0.01);
        }
        Render(host, composer);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void KeyboardMovesFocusRevealsLeavesAndActivatesWithEnterOrSpace(double scale)
    {
        using var host = CreateHost(scale);
        var navigated = new List<string>();
        using var composer = Composer(host);
        var sidebar = Sidebar(host, navigated.Add, 422);
        sidebar.SetState("teleport.tools", "teleportation", []);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        Assert.True(composer.FocusElement(sidebar.TabIndex));
        for (var i = 0; i < 10; i++) Press(composer, GlKeys.Down);
        Assert.Equal("teleport.warmup", sidebar.FocusedDestination);
        Assert.True(sidebar.IsDestinationVisible("teleport.warmup"));
        Assert.Empty(navigated);
        Press(composer, GlKeys.Enter);
        Assert.Equal(new[] { "teleport.warmup" }, navigated);
        Press(composer, GlKeys.Home);
        Press(composer, GlKeys.Space);
        Assert.Equal("hub", navigated.Last());
        Press(composer, GlKeys.End);
        Press(composer, GlKeys.Up);
        Press(composer, GlKeys.KeypadEnter);
        Assert.Equal("topic:roleplay", navigated.Last());
        Render(host, composer);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SharedSettingsMarkEveryAffectedPageAndCategoryWithoutChangingSelection(double scale)
    {
        using var host = CreateHost(scale);
        using var composer = Composer(host);
        var sidebar = Sidebar(host);
        sidebar.SetState("teleport.homes", "teleportation", ["HomeSpawnRequireTemporalGear"]);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        Assert.True(sidebar.IsDestinationDirty("teleport.homes"));
        Assert.True(sidebar.IsDestinationDirty("teleport.spawn"));
        Assert.True(sidebar.IsDestinationDirty("topic:teleportation"));
        Assert.False(sidebar.IsDestinationDirty("teleport.tools"));
        Assert.False(sidebar.IsDestinationDirty("topic:chat"));
        sidebar.SetState("teleport.homes", "teleportation", []);
        Assert.False(sidebar.IsDestinationDirty("topic:teleportation"));
        Assert.True(sidebar.IsDestinationVisible("teleport.homes"));
        Render(host, composer);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void TrackClickAndThumbDragScrollOnlyTheBranchAndConsumeOutsideRelease(double scale)
    {
        using var host = CreateHost(scale);
        var navigated = new List<string>();
        using var composer = Composer(host);
        var sidebar = Sidebar(host, navigated.Add, 422);
        sidebar.SetState("teleport.tools", "teleportation", []);
        composer.AddInteractiveElement(sidebar, "sidebar").Compose();
        var branch = sidebar.BranchBounds;
        var trackX = (int)(sidebar.Bounds.renderX + sidebar.Bounds.InnerWidth - 12 * scale);
        var startY = (int)(branch.renderY + 8 * scale);
        host.SetMouse(trackX, startY);
        composer.OnMouseDown(new MouseEvent(trackX, startY, EnumMouseButton.Left, 0));
        var move = new MouseEvent(trackX, (int)(branch.renderY + branch.InnerHeight + 100 * scale), EnumMouseButton.Left, 0);
        composer.OnMouseMove(move);
        Assert.True(move.Handled);
        Assert.Equal(sidebar.MaxScrollOffset, sidebar.ScrollOffset);
        var release = new MouseEvent(850, 650, EnumMouseButton.Left, 0);
        composer.OnMouseUp(release);
        Assert.True(release.Handled);
        Assert.Empty(navigated);
        composer.OnMouseDown(new MouseEvent(trackX, startY, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(trackX, startY, EnumMouseButton.Left, 0));
        Assert.True(sidebar.ScrollOffset < sidebar.MaxScrollOffset);
        Render(host, composer);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 900, scale);
    private static GuiComposer Composer(PreviewHost host) => host.Api.Gui.CreateCompo("sidebar", ElementBounds.Fixed(0, 0, 900, 700));
    private static GuiElementSetupSidebar Sidebar(PreviewHost host, Action<string>? navigate = null, double height = 502) =>
        new(host.Api, ElementBounds.Fixed(20, 30, 236, height), navigate ?? (_ => { }));
    private static (int X, int Y) Center(ElementBounds bounds) =>
        ((int)(bounds.renderX + bounds.InnerWidth / 2), (int)(bounds.renderY + bounds.InnerHeight / 2));
    private static void Click(PreviewHost host, GuiComposer composer, ElementBounds bounds)
    {
        var (x, y) = Center(bounds);
        host.SetMouse(x, y);
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
    private static void Press(GuiComposer composer, GlKeys key)
    {
        var args = new KeyEvent { KeyCode = (int)key };
        composer.OnKeyDown(args, true);
        Assert.True(args.Handled);
    }
    private static void Render(PreviewHost host, GuiComposer composer)
    {
        host.Canvas.Clear(30, 30, 30, 255);
        composer.Render(0);
    }
}
