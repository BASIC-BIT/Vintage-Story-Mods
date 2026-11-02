using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSheetRenderTests
{
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Embedded_sheet_renders_at_its_offset_owner_and_never_left_of_the_wizard(double scale)
    {
        using var host = Host(1800, 1200, scale);
        using var scene = PreviewScene.Create("wizard-sheet-builder-selected", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);
        Save(host, "sheet-offset-" + scale);
        var preview = Assert.IsType<SetupWizardSheetPreview>(wizard.SingleComposer.GetElement("sheet-editor-preview"));
        var contentX = wizard.SingleComposer.GetRichtext("sheet-editor-status").Bounds.renderX - 158 * scale;
        Assert.Equal(contentX, preview.Bounds.renderX, 1);
        Assert.All(preview.Dialog.TemplateSelections.Values, cell => Assert.True(cell.Bounds.renderX >= contentX));
        var pixels = host.Canvas.GetRgbaPixels();
        var left = (int)wizard.SingleComposer.Bounds.renderX - 20;
        for (var y = 0; y < host.Canvas.Height; y++) for (var x = 0; x < left; x++)
        { var offset = (y * host.Canvas.Width + x) * 4; Assert.True(pixels[offset] == 30 && pixels[offset + 1] == 30 && pixels[offset + 2] == 30, $"Unexpected sheet pixel outside wizard at {x},{y}"); }
        Assert.Empty(host.UnsupportedCalls);
    }
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Inspector_native_controls_cannot_draw_or_receive_pointer_input_beyond_the_actual_viewport(double scale)
    {
        using var host = Host(1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-sheet-builder-choice", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(1);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
        var scrollbar = wizard.SingleComposer.GetScrollbar("sheet-editor-inspector-scroll");
        // The choice capture focuses its options; this clipping test needs the original top viewport.
        wizard.SingleComposer.FocusElement(wizard.SingleComposer.GetDropDown("sheet-editor-Visibility").TabIndex);
        host.RenderGuiDialog(wizard, 0); Save(host, "inspector-clip-" + scale);
        var clip = wizard.SingleComposer.GetDropDown("sheet-editor-Visibility").InsideClipBounds;
        Assert.Equal(scrollbar.Bounds.renderY + scrollbar.Bounds.InnerHeight, clip.renderY + clip.InnerHeight, 1);
        var toggle = Assert.IsType<GuiElementSetupToggle>(wizard.SingleComposer.GetElement("sheet-editor-ShowInLook"));
        // Place the toggle across the viewport edge independently of inspector row spacing.
        toggle.Bounds.fixedY += (clip.renderY + clip.InnerHeight - 10 * scale - toggle.Bounds.renderY) / scale;
        toggle.Bounds.MarkDirtyRecursive(); toggle.Bounds.CalcWorldBounds();
        var original = toggle.Value;
        var x = (int)(toggle.Bounds.renderX + toggle.Bounds.InnerWidth / 2); var y = (int)Math.Max(toggle.Bounds.renderY + toggle.Bounds.InnerHeight / 2, clip.renderY + clip.InnerHeight + 2);
        Assert.True(y > clip.renderY + clip.InnerHeight); Assert.True(y < toggle.Bounds.renderY + toggle.Bounds.OuterHeight);
        host.SetMouse(x, y); wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0)); wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.Equal(original, toggle.Value);
        host.Canvas.Clear();
        foreach (var key in new[] { "sheet-editor-Visibility", "sheet-editor-ShowInLook" })
            wizard.SingleComposer.GetElement(key).RenderInteractiveElements(0);
        var pixels = host.Canvas.GetRgbaPixels(); var bottom = (int)Math.Ceiling(clip.renderY + clip.InnerHeight);
        for (var py = bottom + 1; py < host.Canvas.Height; py++) for (var px = 0; px < host.Canvas.Width; px++)
            Assert.Equal(0, pixels[(py * host.Canvas.Width + px) * 4 + 3]);
        wizard.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(0);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
        var advanced = wizard.SingleComposer.GetButton("sheet-editor-advanced");
        wizard.SingleComposer.FocusElement(advanced.TabIndex);
        x = (int)(advanced.Bounds.renderX + advanced.Bounds.InnerWidth / 2); y = (int)(advanced.Bounds.renderY + advanced.Bounds.InnerHeight / 2);
        host.SetMouse(x, y); wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0)); wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        clip = wizard.SingleComposer.GetTextInput("sheet-editor-Label").InsideClipBounds;
        host.Canvas.Clear();
        foreach (var key in new[] { "sheet-editor-Label", "sheet-editor-Description", "sheet-editor-Type", "sheet-editor-Required", "sheet-editor-MaxLength" })
            wizard.SingleComposer.GetElement(key).RenderInteractiveElements(0);
        pixels = host.Canvas.GetRgbaPixels(); bottom = (int)Math.Ceiling(clip.renderY + clip.InnerHeight);
        for (var py = bottom + 1; py < host.Canvas.Height; py++) for (var px = 0; px < host.Canvas.Width; px++)
            Assert.Equal(0, pixels[(py * host.Canvas.Width + px) * 4 + 3]);
        var dropdown = wizard.SingleComposer.GetDropDown("sheet-editor-Type");
        wizard.SingleComposer.FocusElement(dropdown.TabIndex);
        x = (int)(dropdown.Bounds.renderX + dropdown.Bounds.InnerWidth / 2); y = (int)(dropdown.Bounds.renderY + dropdown.Bounds.InnerHeight / 2);
        host.SetMouse(x, y); wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0)); wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.True(dropdown.listMenu.IsOpened);
        host.Canvas.Clear(); dropdown.RenderInteractiveElements(0);
        pixels = host.Canvas.GetRgbaPixels();
        for (var py = bottom + 1; py < host.Canvas.Height; py++) for (var px = 0; px < host.Canvas.Width; px++)
            Assert.Equal(0, pixels[(py * host.Canvas.Width + px) * 4 + 3]);
        Assert.Empty(host.UnsupportedCalls);
    }
    private static PreviewHost Host(int width, int height, double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
    private static void Save(PreviewHost host, string name)
    {
        var root = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, "../../.."));
        Directory.CreateDirectory(Path.Combine(root, ".superpowers/sdd/2026-10-26-character-sheet-builder"));
        host.Canvas.SavePng(Path.Combine(root, ".superpowers/sdd/2026-10-26-character-sheet-builder/task-4-fix2-" + name + ".png"));
    }
}
