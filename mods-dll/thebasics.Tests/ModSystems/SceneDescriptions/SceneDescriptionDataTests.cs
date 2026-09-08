using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionDataTests
{
    [Theory]
    [InlineData(SceneDescriptionDisplay.WhenTargeted, false, false)]
    [InlineData(SceneDescriptionDisplay.WhenTargeted, true, true)]
    [InlineData(SceneDescriptionDisplay.AlwaysNearby, false, true)]
    [InlineData(SceneDescriptionDisplay.AlwaysNearby, true, true)]
    [InlineData(SceneDescriptionDisplay.OnInteraction, false, false)]
    [InlineData(SceneDescriptionDisplay.OnInteraction, true, false)]
    public void DisplayModeControlsFloatingDescription(SceneDescriptionDisplay display, bool targeted, bool visible)
    {
        var data = new SceneDescriptionData { Display = display, Body = "A quiet clearing." };
        data.ShouldShowDescription(targeted).Should().Be(visible);
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        SceneDescriptionData.ReadFrom(tree).Display.Should().Be(display);
    }

    [Fact]
    public void InvalidAndMissingDisplayModesDefaultToTargeted()
    {
        new SceneDescriptionData { Display = (SceneDescriptionDisplay)500 }.Normalize().Display
            .Should().Be(SceneDescriptionDisplay.WhenTargeted);
        SceneDescriptionData.ReadFrom(new TreeAttribute()).Display.Should().Be(SceneDescriptionDisplay.WhenTargeted);
    }

    [Fact]
    public void InspectorPreviewEscapesMarkupAndLimitsDisplayedText()
    {
        SceneDescriptionFormatter.InspectorPreview("<tag>\nHello").Should().Be("&lt;tag&gt; Hello");
        SceneDescriptionFormatter.InspectorPreview(new string('a', 200)).Should().HaveLength(180).And.EndWith("...");
    }

    [Fact]
    public void FloatingPreviewBoundsLongAndMultilineTextWithoutChangingTheBody()
    {
        var data = new SceneDescriptionData { Body = string.Join("\n", Enumerable.Repeat("line", 100)) };
        SceneDescriptionFormatter.FloatingPreview(data.Body).Split('\n').Should().HaveCount(12);
        data.Body.Split('\n').Should().HaveCount(100);
        SceneDescriptionFormatter.FloatingPreview(new string('x', 1000)).Should().HaveLength(603).And.EndWith("...");
    }
    [Fact]
    public void Normalize_CanonicalizesTextAndInvalidKind()
    {
        var data = new SceneDescriptionData
        {
            Title = "  First\r\nSecond  ",
            Body = "  One\r\nTwo\rThree  ",
            Kind = (SceneDescriptionKind)99,
        };

        data.Normalize();

        data.Title.Should().Be("First Second");
        data.Body.Should().Be("One\nTwo\nThree");
        data.Kind.Should().Be(SceneDescriptionKind.Environmental);
    }

    [Fact]
    public void Normalize_EnforcesStoredTextLimits()
    {
        var data = new SceneDescriptionData
        {
            Title = new string('t', SceneDescriptionData.MaxTitleLength + 1),
            Body = new string('b', SceneDescriptionData.MaxBodyLength + 1),
        };

        data.Normalize();

        data.Title.Should().HaveLength(SceneDescriptionData.MaxTitleLength);
        data.Body.Should().HaveLength(SceneDescriptionData.MaxBodyLength);
    }

    [Fact]
    public void TreeAttributes_RoundTripTextKindAndAuthorMetadata()
    {
        var attributes = new TreeAttribute();
        var original = new SceneDescriptionData
        {
            Title = "Old Mill",
            Body = "The wheel turns without water.",
            Kind = SceneDescriptionKind.OocNotice,
            AuthorUid = "player-1",
            AuthorName = "Example Player",
        };

        original.WriteTo(attributes);
        var restored = SceneDescriptionData.ReadFrom(attributes);

        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Formatter_EscapesPlayerTextWithUnifiedPresentation()
    {
        var formatted = SceneDescriptionFormatter.ToVtml(new SceneDescriptionData
        {
            Title = "<b>Old Mill</b>",
            Body = "A <script> turns.\nDust hangs here.",
        });

        formatted.Should().Be("<strong>&lt;b&gt;Old Mill&lt;/b&gt;</strong><br>A &lt;script&gt; turns.<br>Dust hangs here.");
    }

    [Fact]
    public void Formatter_DoesNotItalicizeOocNotices()
    {
        var formatted = SceneDescriptionFormatter.ToVtml(
            new SceneDescriptionData
            {
                Body = "Scene paused <until tomorrow>.",
                Kind = SceneDescriptionKind.OocNotice,
            });

        formatted.Should().NotContain("[OOC]");
        formatted.Should().Contain("Scene paused &lt;until tomorrow&gt;.");
        formatted.Should().NotContain("<i>");
    }
}
