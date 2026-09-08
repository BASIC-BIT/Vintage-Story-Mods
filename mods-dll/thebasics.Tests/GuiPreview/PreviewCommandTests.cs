using TheBasics.GuiPreview;
using Xunit;

namespace thebasics.Tests.GuiPreview;

public class PreviewCommandTests
{
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
