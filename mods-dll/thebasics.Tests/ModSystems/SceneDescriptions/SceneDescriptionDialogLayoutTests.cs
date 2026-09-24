using System.Text.RegularExpressions;
using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionDialogLayoutTests
{
    [Fact]
    public void UnlimitedKeepsTypedDistanceValuesInTheEditorDraft()
    {
        var current = new SceneDescriptionData
        {
            IconDistance = 24,
            TextDistance = 8,
            UnlimitedIconDistance = true,
            UnlimitedTextDistance = true,
        };

        var (iconDistance, textDistance) = SceneDescriptionDialog.DraftDistances(current, "100", "12", nearbyLayout: true);

        iconDistance.Should().Be(100);
        textDistance.Should().Be(12);
        SceneDescriptionDialog.DraftDistances(current, "invalid", "NaN", nearbyLayout: true)
            .Should().Be((24f, 8f), "disabled invalid fields should retain the prior values");
        SceneDescriptionDialog.DraftDistances(current, "2048", "0", nearbyLayout: true)
            .Should().Be((24f, 8f), "disabled out-of-range fields should retain the prior values");
    }

    [Fact]
    public void OutOfRangeActiveDistanceMakesTheNormalizedDraftDirty()
    {
        var current = new SceneDescriptionData { IconDistance = 24, TextDistance = 8 };

        var (iconDistance, textDistance) = SceneDescriptionDialog.DraftDistances(current, "2048", "0", nearbyLayout: true);
        var draft = new SceneDescriptionData { IconDistance = iconDistance, TextDistance = textDistance }.Normalize();

        draft.IconDistance.Should().Be(1024);
        draft.TextDistance.Should().Be(1);
    }

    [Fact]
    public void UnparseableActiveDistanceChangesTheCloseSnapshot()
    {
        var current = new SceneDescriptionData { IconDistance = 24, TextDistance = 8 };

        var loaded = SceneDescriptionDialog.ActiveDistanceSnapshot(current, "24", "8", nearbyLayout: true);
        var invalidIcon = SceneDescriptionDialog.ActiveDistanceSnapshot(current, "abc", "8", nearbyLayout: true);
        var invalidText = SceneDescriptionDialog.ActiveDistanceSnapshot(current, "24", "abc", nearbyLayout: true);

        invalidIcon.Should().NotBe(loaded);
        invalidText.Should().NotBe(loaded);
        current.UnlimitedIconDistance = true;
        SceneDescriptionDialog.ActiveDistanceSnapshot(current, "abc", "8", nearbyLayout: true)
            .Should().Be(SceneDescriptionDialog.ActiveDistanceSnapshot(current, "24", "8", nearbyLayout: true),
                "invalid text in a disabled distance field should not affect the draft");
    }

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
