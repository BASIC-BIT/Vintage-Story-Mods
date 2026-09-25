using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneBubbleLayoutTests
{
    [Fact]
    public void MoreTextGrowsBubbleWithoutReducingLetterSize()
    {
        var shortText = SceneBubbleLayout.Size(120, 40, 1, 0.4f);
        var longText = SceneBubbleLayout.Size(360, 1200, 1, 0.4f);
        (shortText.Width / 120).Should().BeApproximately(longText.Width / 360, 0.00001f);
        longText.Width.Should().BeApproximately(0.72f, 0.00001f);
        longText.Height.Should().BeApproximately(2.4f, 0.00001f);
        SceneBubbleLayout.Size(720, 2400, 2, 0.4f).Should().Be(longText);
    }

    [Fact]
    public void ContentCacheKeyChangesWhenExistingIconIsCleared()
    {
        var data = new SceneDescriptionData { TitleIconName = "wpHome" };
        var before = data.BubbleContent;
        data.TitleIconName = "";
        data.BubbleContent.Should().NotBe(before);
    }

    [Fact]
    public void RedUsesNewIdAndSurvivesNormalization()
    {
        ((int)SceneMarkerColor.Red).Should().Be(4);
        new SceneDescriptionData { Color = SceneMarkerColor.Red }.Normalize().Color.Should().Be(SceneMarkerColor.Red);
    }
}
