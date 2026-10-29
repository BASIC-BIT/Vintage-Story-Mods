using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Setup navigation with fixed category rows and one scrollable page branch.</summary>
public sealed class GuiElementSetupSidebar : GuiElementControl
{
    private const double Padding = 7;
    private const double OverviewHeight = 34;
    private const double CategoryHeight = 31;
    private const double PageHeight = 28;
    private const double TrackWidth = 10;
    private readonly Action<string> navigate;
    private readonly List<NavigationRow> rows = new();
    private readonly HashSet<string> dirtyPages = new(StringComparer.Ordinal);
    private readonly HashSet<string> dirtyTopics = new(StringComparer.Ordinal);
    private readonly TextDrawUtil textUtil = new();
    private readonly CairoFont categoryFont = CairoFont.WhiteSmallText().WithFontSize(15);
    private readonly CairoFont pageFont = CairoFont.WhiteSmallText().WithFontSize(14);
    private LoadedTexture texture;
    private string pageId = "hub", topicId, hovered, pressed, focused = "hub";
    private double branchTop, branchHeight, branchContentHeight, scrollOffset;
    private double dragStartY, dragStartOffset;
    private bool dragging, ownsMouseGesture, composed, needsTexture = true, disposed;
    private bool categoryFontReady, pageFontReady;

    public GuiElementSetupSidebar(ICoreClientAPI api, ElementBounds bounds, Action<string> navigate)
        : base(api, bounds)
    {
        this.navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        texture = new LoadedTexture(api);
        MouseOverCursor = "normal";
    }

    public override bool Focusable => enabled;
    public override double DrawOrder => 0.5;
    internal double ScrollOffset => scrollOffset;
    internal double MaxScrollOffset => Math.Max(0, branchContentHeight - branchHeight);
    internal ElementBounds BranchBounds => LocalBounds(24, branchTop, LogicalWidth - 31, branchHeight);
    internal string FocusedDestination => focused;

    public void SetState(string pageId, string topicId, IReadOnlyCollection<string> changedKeys)
    {
        var selectionChanged = this.pageId != pageId || this.topicId != topicId;
        this.pageId = pageId ?? "hub";
        this.topicId = topicId;
        dirtyPages.Clear();
        dirtyTopics.Clear();
        var changed = new HashSet<string>(changedKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var page in SetupWizardCatalog.Pages)
        {
            if (!page.SettingKeys.Any(changed.Contains) && !(page.Id == "chat.languageeditor" && changed.Contains("Languages"))) continue;
            dirtyPages.Add(page.Id);
            dirtyTopics.Add(page.TopicId);
        }
        if (selectionChanged)
        {
            scrollOffset = 0;
            focused = this.pageId == "hub" ? "hub" : SetupWizardCatalog.TryGetPage(this.pageId, out _)
                ? this.pageId : topicId != null ? "topic:" + topicId : "hub";
        }
        if (composed)
        {
            Layout();
            if (selectionChanged) Reveal(focused);
        }
        needsTexture = true;
    }

    internal bool IsDestinationDirty(string destination) => destination.StartsWith("topic:", StringComparison.Ordinal)
        ? dirtyTopics.Contains(destination[6..]) : dirtyPages.Contains(destination);

    internal ElementBounds DestinationBounds(string destination)
    {
        var row = rows.FirstOrDefault(candidate => candidate.Destination == destination);
        return row == null ? null : LocalBounds(row.IsPage ? 27 : Padding,
            row.Y - (row.IsPage ? scrollOffset : 0), LogicalWidth - (row.IsPage ? 45 : Padding * 2), row.Height);
    }

    internal bool IsDestinationVisible(string destination)
    {
        var row = rows.FirstOrDefault(candidate => candidate.Destination == destination);
        if (row == null) return false;
        return !row.IsPage || row.Y - scrollOffset >= branchTop - 0.01 &&
            row.Y + row.Height - scrollOffset <= branchTop + branchHeight + 0.01;
    }

    private double LogicalWidth => Bounds.InnerWidth / RuntimeEnv.GUIScale;
    private double LogicalHeight => Bounds.InnerHeight / RuntimeEnv.GUIScale;

    private ElementBounds LocalBounds(double x, double y, double width, double height)
    {
        var bounds = ElementBounds.Fixed(x, y, Math.Max(1, width), Math.Max(1, height)).WithParent(Bounds);
        bounds.CalcWorldBounds();
        return bounds;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        Layout();
        Reveal(focused);
        composed = true;
        needsTexture = true;
    }

    private void Layout()
    {
        rows.Clear();
        rows.Add(new NavigationRow("hub", "Overview", Padding, OverviewHeight, false));
        var pages = SetupWizardCatalog.Pages.Where(page => page.TopicId == topicId).ToArray();
        branchContentHeight = pages.Length * PageHeight;
        var available = Math.Max(0, LogicalHeight - Padding * 2 - OverviewHeight - 9 - CategoryHeight * SetupWizardCatalog.Topics.Count);
        branchHeight = Math.Min(branchContentHeight, available);
        branchTop = 0;
        var y = Padding + OverviewHeight + 9;
        foreach (var topic in SetupWizardCatalog.Topics)
        {
            rows.Add(new NavigationRow("topic:" + topic.Id, topic.Title, y, CategoryHeight, false));
            y += CategoryHeight;
            if (topic.Id != topicId || pages.Length == 0) continue;
            branchTop = y;
            foreach (var page in pages)
                rows.Add(new NavigationRow(page.Id, PageLabel(page), y + Array.IndexOf(pages, page) * PageHeight, PageHeight, true));
            y += branchHeight;
        }
        scrollOffset = Math.Clamp(scrollOffset, 0, MaxScrollOffset);
    }

    private void Reveal(string destination)
    {
        var row = rows.FirstOrDefault(candidate => candidate.Destination == destination && candidate.IsPage);
        if (row == null || branchHeight <= 0) return;
        var relativeY = row.Y - branchTop;
        if (relativeY < scrollOffset) scrollOffset = relativeY;
        if (relativeY + row.Height > scrollOffset + branchHeight)
            scrollOffset = relativeY + row.Height - branchHeight;
        scrollOffset = Math.Clamp(scrollOffset, 0, MaxScrollOffset);
        needsTexture = true;
    }

    private static string PageLabel(SetupWizardPage page) => page.Id switch
    {
        "chat.languageaccess" => "Language access",
        "chat.signlanguage" => "Sign language",
        "chat.obfuscation" => "Distant speech",
        "chat.tabs" => "Chat tabs",
        "teleport.tools" => "Player visits and places",
        "teleport.utilities" => "Travel commands",
        "appearance.names" => "Player nametags",
        _ => page.Title
    };

    private static SetupTopicCardIcon TopicIcon(string id) => id switch
    {
        "chat" => SetupTopicCardIcon.Chat,
        "teleportation" => SetupTopicCardIcon.Teleportation,
        "notifications" => SetupTopicCardIcon.Notifications,
        "appearance" => SetupTopicCardIcon.PlayerDisplay,
        "map" => SetupTopicCardIcon.Map,
        "characters" => SetupTopicCardIcon.Characters,
        "roleplay" => SetupTopicCardIcon.Roleplay,
        _ => SetupTopicCardIcon.ServerTools
    };

    private void ComposeTexture()
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(Bounds.InnerWidth)), Math.Max(1, (int)Math.Ceiling(Bounds.InnerHeight)));
        using var context = genContext(surface);
        context.SetSourceRGBA(0.08, 0.06, 0.035, 0.26);
        context.Paint();
        context.SetSourceRGBA(0.63, 0.53, 0.36, 0.34);
        context.LineWidth = scaled(1);
        context.MoveTo(surface.Width - scaled(0.5), 0);
        context.LineTo(surface.Width - scaled(0.5), surface.Height);
        context.MoveTo(scaled(13), scaled(Padding + OverviewHeight + 4));
        context.LineTo(surface.Width - scaled(13), scaled(Padding + OverviewHeight + 4));
        context.Stroke();
        foreach (var row in rows)
        {
            context.Save();
            if (row.IsPage)
            {
                context.Rectangle(scaled(24), scaled(branchTop), scaled(LogicalWidth - 31), scaled(branchHeight));
                context.Clip();
            }
            DrawRow(context, row);
            context.Restore();
        }
        if (branchHeight > 0)
        {
            context.SetSourceRGBA(0.63, 0.53, 0.36, 0.35);
            context.LineWidth = scaled(1);
            context.MoveTo(scaled(24), scaled(branchTop));
            context.LineTo(scaled(24), scaled(branchTop + branchHeight));
            context.Stroke();
        }
        if (MaxScrollOffset > 0)
        {
            var track = ScrollTrack();
            var thumb = ScrollThumb();
            context.SetSourceRGBA(0.05, 0.04, 0.025, 0.35);
            RoundRectangle(context, scaled(track.X), scaled(track.Y), scaled(track.Width), scaled(track.Height), scaled(2));
            context.Fill();
            context.SetSourceRGBA(0.66, 0.55, 0.36, dragging ? 1 : 0.70);
            RoundRectangle(context, scaled(thumb.X), scaled(thumb.Y), scaled(thumb.Width), scaled(thumb.Height), scaled(2));
            context.Fill();
        }
        generateTexture(surface, ref texture);
        needsTexture = false;
    }

    private void DrawRow(Context context, NavigationRow row)
    {
        var y = row.Y - (row.IsPage ? scrollOffset : 0);
        var selected = row.Destination == pageId;
        var categorySelected = row.Destination == "topic:" + topicId;
        var x = row.IsPage ? 27 : Padding;
        var width = LogicalWidth - x - (row.IsPage && MaxScrollOffset > 0 ? TrackWidth + Padding : Padding);
        if (selected || hovered == row.Destination || pressed == row.Destination)
        {
            context.SetSourceRGBA(0.65, 0.46, 0.20, selected ? 0.28 : pressed == row.Destination ? 0.24 : 0.13);
            context.Rectangle(scaled(x), scaled(y + 1), scaled(width), scaled(row.Height - 2));
            context.Fill();
        }
        if (selected && row.IsPage)
        {
            context.SetSourceRGBA(0.95, 0.72, 0.35, 1);
            context.Rectangle(scaled(x), scaled(y + 1), scaled(3), scaled(row.Height - 2));
            context.Fill();
        }
        var color = selected || categorySelected ? new[] { 0.98, 0.80, 0.48, 1.0 }
            : row.IsPage ? new[] { 0.77, 0.72, 0.62, 1.0 } : new[] { 0.90, 0.85, 0.74, 1.0 };
        double textX;
        if (row.Destination.StartsWith("topic:", StringComparison.Ordinal))
        {
            context.Save();
            context.Translate(scaled(15), scaled(y + 6));
            context.Scale(scaled(0.53), scaled(0.53));
            context.SetSourceRGBA(color);
            context.LineWidth = 1.8;
            context.LineCap = LineCap.Round;
            context.LineJoin = LineJoin.Round;
            GuiElementSetupTopicCard.DrawIcon(context, TopicIcon(row.Destination[6..]));
            context.Restore();
            textX = 41;
            // The chevron keeps expandable categories distinct from their pages.
            var arrowX = LogicalWidth - 19;
            context.SetSourceRGBA(color[0], color[1], color[2], 0.7);
            context.LineWidth = scaled(1.3);
            if (categorySelected)
            {
                context.MoveTo(scaled(arrowX - 3), scaled(y + 13));
                context.LineTo(scaled(arrowX), scaled(y + 16));
                context.LineTo(scaled(arrowX + 3), scaled(y + 13));
            }
            else
            {
                context.MoveTo(scaled(arrowX - 1), scaled(y + 11));
                context.LineTo(scaled(arrowX + 2), scaled(y + 14));
                context.LineTo(scaled(arrowX - 1), scaled(y + 17));
            }
            context.Stroke();
        }
        else textX = row.IsPage ? 37 : 17;
        var font = row.IsPage ? pageFont : categoryFont;
        font.SetupContext(context);
        if (row.IsPage) pageFontReady = true;
        else categoryFontReady = true;
        context.SetSourceRGBA(color);
        var dirty = IsDestinationDirty(row.Destination);
        var reserve = row.IsPage ? dirty ? 17 : 7 : row.Destination == "hub" ? 7 : 30;
        var maxWidth = Math.Max(1, scaled(x + width - textX - reserve));
        var label = Fit(context, row.Label, maxWidth);
        textUtil.DrawTextLine(context, font, label, scaled(textX), scaled(y + (row.Height - font.UnscaledFontsize) / 2 - 1));
        if (dirty)
        {
            context.SetSourceRGBA(0.97, 0.75, 0.38, 1);
            context.Arc(scaled(x + width - (row.IsPage ? 8 : 27)), scaled(y + row.Height / 2), scaled(2.5), 0, Math.PI * 2);
            context.Fill();
        }
        if (HasFocus && focused == row.Destination)
        {
            context.SetSourceRGBA(0.91, 0.76, 0.47, 0.95);
            context.LineWidth = scaled(1);
            RoundRectangle(context, scaled(x + 1.5), scaled(y + 1.5), scaled(width - 3), scaled(row.Height - 3), scaled(2));
            context.Stroke();
        }
    }

    private static string Fit(Context context, string text, double width)
    {
        if (context.TextExtents(text).XAdvance <= width) return text;
        while (text.Length > 0 && context.TextExtents(text + "…").XAdvance > width) text = text[..^1];
        return text + "…";
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (disposed) return;
        var currentHover = Hit(api.Input.MouseX, api.Input.MouseY)?.Destination;
        if (currentHover != hovered) { hovered = currentHover; needsTexture = true; }
        if (needsTexture) ComposeTexture();
        api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
    }

    private NavigationRow Hit(int x, int y)
    {
        if (!Bounds.PointInside(x, y)) return null;
        var localY = (y - Bounds.renderY) / RuntimeEnv.GUIScale;
        var localX = (x - Bounds.renderX) / RuntimeEnv.GUIScale;
        if (MaxScrollOffset > 0 && ScrollTrack().Contains(localX, localY)) return null;
        return rows.FirstOrDefault(row => localY >= row.Y - (row.IsPage ? scrollOffset : 0) &&
            localY < row.Y + row.Height - (row.IsPage ? scrollOffset : 0) &&
            (!row.IsPage || localY >= branchTop && localY < branchTop + branchHeight && localX >= 24));
    }

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || args.Handled || !Bounds.PointInside(args.X, args.Y)) return;
        ownsMouseGesture = true;
        args.Handled = true;
        if (args.Button != EnumMouseButton.Left) return;
        var x = (args.X - Bounds.renderX) / RuntimeEnv.GUIScale;
        var y = (args.Y - Bounds.renderY) / RuntimeEnv.GUIScale;
        if (MaxScrollOffset > 0 && ScrollTrack().Contains(x, y))
        {
            var thumb = ScrollThumb();
            if (thumb.Contains(x, y))
            {
                dragging = true;
                dragStartY = y;
                dragStartOffset = scrollOffset;
            }
            else SetScroll(scrollOffset + (y < thumb.Y ? -branchHeight : branchHeight));
        }
        else
        {
            pressed = Hit(args.X, args.Y)?.Destination;
            if (pressed != null) focused = pressed;
        }
        needsTexture = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (disposed) return;
        var activate = enabled && ownsMouseGesture && args.Button == EnumMouseButton.Left && pressed != null && Hit(args.X, args.Y)?.Destination == pressed;
        var destination = pressed;
        if (ownsMouseGesture) args.Handled = true;
        ownsMouseGesture = false;
        pressed = null;
        dragging = false;
        needsTexture = true;
        if (activate) Activate(destination);
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || disposed) return;
        if (dragging)
        {
            var travel = branchHeight - ScrollThumb().Height;
            if (travel > 0) SetScroll(dragStartOffset + ((args.Y - Bounds.renderY) / RuntimeEnv.GUIScale - dragStartY) * MaxScrollOffset / travel);
            args.Handled = true;
        }
        else if (ownsMouseGesture) args.Handled = true;
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!enabled || disposed || !Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)) return;
        // The entire sidebar consumes the wheel so a settings field behind it cannot change.
        args.SetHandled();
        var y = (api.Input.MouseY - Bounds.renderY) / RuntimeEnv.GUIScale;
        if (y >= branchTop && y < branchTop + branchHeight)
            SetScroll(scrollOffset - args.deltaPrecise * PageHeight * 2);
    }

    private void SetScroll(double value)
    {
        scrollOffset = Math.Clamp(value, 0, MaxScrollOffset);
        needsTexture = true;
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!enabled || disposed || !HasFocus || args.Handled) return;
        var index = rows.FindIndex(row => row.Destination == focused);
        if (index < 0) index = 0;
        var next = args.KeyCode switch
        {
            (int)GlKeys.Up => Math.Max(0, index - 1),
            (int)GlKeys.Down => Math.Min(rows.Count - 1, index + 1),
            (int)GlKeys.Home => 0,
            (int)GlKeys.End => rows.Count - 1,
            _ => -1
        };
        if (next >= 0)
        {
            focused = rows[next].Destination;
            Reveal(focused);
            needsTexture = true;
            args.Handled = true;
        }
        else if (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter || args.KeyCode == (int)GlKeys.Space)
        {
            args.Handled = true;
            Activate(rows[index].Destination);
        }
    }

    public override void OnFocusGained() { base.OnFocusGained(); needsTexture = true; }
    public override void OnFocusLost()
    {
        base.OnFocusLost();
        pressed = null;
        dragging = false;
        ownsMouseGesture = false;
        needsTexture = true;
    }

    private void Activate(string destination)
    {
        api.Gui.PlaySound("menubutton");
        navigate(destination);
    }

    private Rect ScrollTrack() => new(LogicalWidth - Padding - TrackWidth, branchTop, TrackWidth, branchHeight);
    private Rect ScrollThumb()
    {
        var height = Math.Max(24, branchHeight * branchHeight / Math.Max(1, branchContentHeight));
        height = Math.Min(branchHeight, height);
        var y = branchTop + (branchHeight - height) * (MaxScrollOffset > 0 ? scrollOffset / MaxScrollOffset : 0);
        return new Rect(LogicalWidth - Padding - TrackWidth + 2, y, TrackWidth - 4, height);
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        texture.Dispose();
        if (categoryFontReady) categoryFont.Dispose();
        if (pageFontReady) pageFont.Dispose();
        base.Dispose();
    }

    private sealed record NavigationRow(string Destination, string Label, double Y, double Height, bool IsPage);
    private readonly record struct Rect(double X, double Y, double Width, double Height)
    {
        public bool Contains(double x, double y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    }
}
