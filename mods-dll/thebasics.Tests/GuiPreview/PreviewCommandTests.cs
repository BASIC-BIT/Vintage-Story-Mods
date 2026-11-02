using TheBasics.GuiPreview;
using System.Reflection;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using Xunit;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class PreviewCommandTests
{
    [VisualTheory]
    [InlineData("language-color-palette")]
    [InlineData("language-color-custom")]
    [InlineData("admin-chat-colors")]
    [InlineData("admin-chat-colors-custom")]
    [InlineData("admin-nametag-colors")]
    [InlineData("scene-marker-colors")]
    [InlineData("scene-marker-custom-color")]
    [InlineData("scene-marker-custom-picker")]
    [InlineData("scene-marker-custom-icon")]
    [InlineData("scene-marker-custom-locked")]
    public void Color_capture_dialogs_render_through_the_production_entry_points(string scenario)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, 1.25);
        using var scene = PreviewScene.Create(scenario, host);
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
        if (scenario == "scene-marker-custom-picker")
            Assert.Equal(new[] { "sound:menubutton", "sound:menubutton_down" }, host.RecordedEffects);
        else Assert.Empty(host.RecordedEffects);
    }

    [Fact]
    public void Rendering_cannot_label_a_stale_binary_with_current_source_identity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gui-source-" + Guid.NewGuid().ToString("N"));
        var compiled = typeof(SetupWizardDialog).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "GuiCaptureSourceTreeHash")?.Value;
        var wrongHash = compiled == new string('a', 64) ? new string('b', 64) : new string('a', 64);
        try
        {
            Assert.Equal(1, Program.Main(["render", "--game", "missing", "--assets", "missing", "--output", directory,
                "--source-tree-hash", wrongHash]));
            Assert.False(Directory.Exists(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Setup_capture_fixtures_only_apply_wizard_keys()
    {
        Assert.NotEmpty(SetupWizardCaptureScenes.Names);
        Assert.Contains("wizard-wording-bottom", SetupWizardCaptureScenes.Names);
        Assert.Contains("wizard-shortcuts-bottom", SetupWizardCaptureScenes.Names);
        Assert.Equal(SetupWizardCaptureScenes.Names.Length, SetupWizardCaptureScenes.Names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(SetupWizardCaptureScenes.Names,
            PreviewScene.Names.Where(name => name.StartsWith("wizard-", StringComparison.Ordinal)));
        foreach (var scenario in SetupWizardCaptureScenes.Names)
        {
            var values = SetupWizardCaptureScenes.DefaultValues();
            Assert.All(values, value => Assert.True(SetupWizardCatalog.IsSettingKey(value.Key)));
            var draft = new SetupWizardDraft(values, SetupWizardCaptureScenes.DefaultLanguages());
            SetupWizardCaptureScenes.ApplyDraft(draft, scenario);
            Assert.NotEmpty(draft.Values);
            Assert.All(draft.Values, value => Assert.True(SetupWizardCatalog.IsSettingKey(value.Key)));
            SetupWizardCaptureScenes.Validate(scenario, SetupWizardCaptureScenes.DefaultPreviewTime(scenario));
        }
    }

    [Fact]
    public void Color_capture_fixtures_preserve_theme_and_alpha_examples()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(draft, "wizard-nametag-colors-alpha");
        Assert.Equal("#40352980", draft.Get("NametagBackgroundColor"));
        Assert.Equal("#E9DDCEBF", draft.Get("NametagBorderColor"));
        SetupWizardCaptureScenes.ApplyDraft(draft, "wizard-nametag-colors");
        Assert.Equal(string.Empty, draft.Get("NametagBackgroundColor"));
        Assert.Equal(string.Empty, draft.Get("NametagBorderColor"));
        foreach (var name in new[] { "language-color-palette", "language-color-custom", "admin-chat-colors", "admin-chat-colors-custom", "admin-nametag-colors", "scene-marker-colors" })
            Assert.Contains(name, PreviewScene.Names);
    }

    [Fact]
    public void Rendering_cannot_replace_its_own_baseline()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gui-baseline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var baseline = Path.Combine(directory, "language-default.png");
        byte[] original = [1, 2, 3, 4];
        File.WriteAllBytes(baseline, original);
        try
        {
            Assert.Equal(1, Program.Main(["render", "--game", "missing", "--assets", "missing",
                "--output", directory, "--baseline", Path.Combine(directory, ".")]));
            Assert.Equal(original, File.ReadAllBytes(baseline));
        }
        finally { Directory.Delete(directory, true); }
    }
}
