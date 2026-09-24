using System.Text.RegularExpressions;
using FluentAssertions;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionDialogLayoutTests
{
    [Fact]
    public void SharedAppearanceNoticeLeavesSpaceBeforeLockButton()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        var source = File.ReadAllText(Path.Combine(directory!.FullName,
            "mods-dll/thebasics/src/ModSystems/SceneDescriptions/SceneDescriptionDialog.cs"));
        var notice = Regex.Match(source,
            "scene-entry-shared-appearance.*?ElementBounds.Fixed\\((\\d+),\\s*top,\\s*(\\d+),\\s*(\\d+)\\)",
            RegexOptions.Singleline);

        notice.Success.Should().BeTrue("the shared-appearance notice must have fixed bounds in the dialog");
        var left = int.Parse(notice.Groups[1].Value);
        var width = int.Parse(notice.Groups[2].Value);
        var height = int.Parse(notice.Groups[3].Value);
        left.Should().Be(530);
        width.Should().BeGreaterThanOrEqualTo(200, "the notice needs room to remain legible");
        (left + width).Should().BeLessThanOrEqualTo(740, "the lock button starts at x=750");
        height.Should().Be(40);
    }
}
