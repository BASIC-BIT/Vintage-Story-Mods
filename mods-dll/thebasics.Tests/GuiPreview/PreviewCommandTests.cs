using TheBasics.GuiPreview;
using System.Reflection;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using Xunit;

namespace thebasics.Tests.GuiPreview;

public class PreviewCommandTests
{
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
        foreach (var scenario in SetupWizardCaptureScenes.Names)
        {
            var values = SetupWizardCaptureScenes.DefaultValues();
            Assert.All(values, value => Assert.True(SetupWizardCatalog.IsSettingKey(value.Key)));
            var draft = new SetupWizardDraft(values);
            SetupWizardCaptureScenes.ApplyDraft(draft, scenario);
            Assert.NotEmpty(draft.Values);
        }
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
