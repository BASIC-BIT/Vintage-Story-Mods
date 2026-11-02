using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class GuiPopupHoverGuard
{
    // Vanilla tooltips poll the mouse during rendering, independently of handled mouse events.
    // shortcut: private GUI fields require rechecking these regression tests on game API upgrades.
    private static readonly FieldInfo Elements = typeof(GuiComposer).GetField("interactiveElements", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo AutoDisplay = typeof(GuiElementHoverText).GetField("autoDisplay", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void RenderBackground(GuiComposer composer, bool popupOpen, Action render)
    {
        if (composer == null) { render(); return; }
        var elements = ((Dictionary<string, GuiElement>)Elements.GetValue(composer)).Values
            .Select(element => element is ScrollableConfigAdminDialog.ScrollingWidget wrapper ? wrapper.Widget : element).ToArray();
        popupOpen |= elements.Any(element => element is GuiElementDropDown { listMenu.IsOpened: true } ||
            element is GuiElementPrivilegePicker { IsSuggestionsOpen: true });
        if (!popupOpen) { render(); return; }
        var restore = new List<Action>();
        try
        {
            foreach (var element in elements)
            {
                if (element is GuiElementHoverText hover)
                {
                    var automatic = (bool)AutoDisplay.GetValue(hover);
                    var visible = hover.IsVisible;
                    restore.Add(() => { hover.SetAutoDisplay(automatic); hover.SetVisible(visible); });
                    hover.SetAutoDisplay(false);
                    hover.SetVisible(false);
                }
                else if (element is GuiElementColorListPicker swatch)
                {
                    var show = swatch.ShowToolTip;
                    restore.Add(() => swatch.ShowToolTip = show);
                    swatch.ShowToolTip = false;
                }
            }
            render();
        }
        finally { foreach (var reset in restore) reset(); }
    }
}
