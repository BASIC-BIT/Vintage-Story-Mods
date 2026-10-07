using System;
using Cairo;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A native dropdown whose expanded options include a smaller description.</summary>
public sealed class GuiElementDescribedDropDown : GuiElementDropDown
{
    private readonly DescribedListMenu describedMenu;

    public GuiElementDescribedDropDown(ICoreClientAPI api, string[] codes, string[] names, string[] descriptions,
        int selected, Action<string, bool> onselect, ElementBounds bounds, CairoFont font)
        : base(api, codes, ValidateNames(codes, names, descriptions, selected), selected,
            (value, on) => onselect?.Invoke(value, on), bounds, font, false)
    {
        var menuBounds = listMenu.Bounds;
        listMenu.Dispose();
        listMenu = describedMenu = new DescribedListMenu(api, codes, names, descriptions, selected,
            (value, on) =>
            {
                SetSelectedValue(value);
                onSelectionChanged?.Invoke(value, on);
            }, menuBounds, font.Clone().WithFontSize(17));
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        base.ComposeElements(context, surface);
        if (describedMenu.IsOpened) describedMenu.OpenDescribed();
    }

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        if (describedMenu.IsOpened)
        {
            describedMenu.OnMouseDown(api, args);
            return;
        }
        if (args.Handled || !IsPositionInside(args.X, args.Y)) return;
        describedMenu.OpenDescribed();
        api.Gui.PlaySound("menubutton");
        args.Handled = true;
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!enabled) return;
        if (HasFocus && !describedMenu.IsOpened &&
            (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter || args.KeyCode == (int)GlKeys.Space))
        {
            describedMenu.OpenDescribed();
            args.Handled = true;
            return;
        }
        base.OnKeyDown(api, args);
    }

    private static string[] ValidateNames(string[] codes, string[] names, string[] descriptions, int selected)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(descriptions);
        if (codes.Length == 0 || codes.Length != names.Length || codes.Length != descriptions.Length)
            throw new ArgumentException("Codes, names, and descriptions must contain the same nonzero number of options.");
        if (selected < 0 || selected >= codes.Length) throw new ArgumentOutOfRangeException(nameof(selected));
        for (var i = 0; i < codes.Length; i++)
        {
            if (string.IsNullOrEmpty(codes[i]) || string.IsNullOrEmpty(names[i]) || descriptions[i] == null)
                throw new ArgumentException("Each option needs a code, a name, and a description.");
        }
        return names;
    }

    private sealed class DescribedListMenu : GuiElementListMenu
    {
        private readonly string[] plainNames;
        private readonly string[] describedNames;
        private readonly double closedY;

        public DescribedListMenu(ICoreClientAPI api, string[] codes, string[] names, string[] descriptions, int selected,
            SelectionChangedDelegate onselect, ElementBounds bounds, CairoFont font)
            : base(api, codes, names, selected, onselect, bounds, font, false)
        {
            plainNames = names;
            describedNames = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                describedNames[i] = VtmlUtils.EscapeVtml(names[i]) +
                    "<br><font size=\"13\" color=\"#dddddd\">" + VtmlUtils.EscapeVtml(descriptions[i]) + "</font>";
            closedY = bounds.fixedY;
            unscaledLineHeight = 58;
        }

        public void OpenDescribed()
        {
            Bounds.fixedY = closedY;
            Bounds.CalcWorldBounds();
            var rowHeight = unscaledLineHeight * Scale * RuntimeEnv.GUIScale;
            var below = api.Render.FrameHeight - Bounds.renderY - Bounds.InnerHeight;
            var above = Bounds.renderY;
            var openAbove = below < Values.Length * rowHeight && above > below;
            var available = openAbove ? above : below;
            var rows = Math.Max(1, Math.Min(Values.Length, (int)Math.Floor(available / rowHeight)));
            var viewportHeight = rows * rowHeight;
            MaxHeight = (int)Math.Ceiling(viewportHeight / RuntimeEnv.GUIScale);
            if (openAbove) Bounds.fixedY -= (viewportHeight + Bounds.InnerHeight) / RuntimeEnv.GUIScale;

            // The native collapsed-value renderer and typeahead must always see the plain labels.
            Names = describedNames;
            try { base.Open(); }
            finally { Names = plainNames; }
            HoveredIndex = SelectedIndex;
            EnsureItemVisible(SelectedIndex);
        }

        public override bool IsPositionInside(int x, int y) => expanded && visibleBounds.PointInside(x, y);

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (!expanded || args.X < Bounds.renderX || args.X > Bounds.renderX + expandedBoxWidth) return;
            if (!visibleBounds.PointInside(args.X, args.Y))
            {
                expanded = false;
                args.Handled = true;
                api.Gui.PlaySound("menubutton");
                return;
            }
            if (Bounds.renderX + expandedBoxWidth - args.X < GuiElement.scaled(10))
            {
                scrollbar.OnMouseDown(api, args);
                args.Handled = true;
                return;
            }

            // Native ListMenu uses the row height as its top offset, which rejects part of row one.
            var rowHeight = unscaledLineHeight * Scale * RuntimeEnv.GUIScale;
            var index = (int)Math.Floor((args.Y - Bounds.renderY - Bounds.InnerHeight + scrollOffY) / rowHeight);
            args.Handled = true;
            if (index < 0 || index >= Values.Length) return;
            SelectedIndex = index;
            expanded = false;
            api.Gui.PlaySound("toggleswitch");
            onSelectionChanged?.Invoke(Values[index], true);
        }
    }
}
