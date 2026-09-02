using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionDataTests
{
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
    public void Formatter_EscapesPlayerTextAndStylesEnvironmentalNarration()
    {
        var formatted = SceneDescriptionFormatter.ToVtml(new SceneDescriptionData
        {
            Title = "<b>Old Mill</b>",
            Body = "A <script> turns.\nDust hangs here.",
        });

        formatted.Should().Be("<strong>&lt;b&gt;Old Mill&lt;/b&gt;</strong><br><i>A &lt;script&gt; turns.<br>Dust hangs here.</i>");
    }

    [Fact]
    public void Formatter_DoesNotItalicizeOocNotices()
    {
        var formatted = SceneDescriptionFormatter.ToVtml(
            new SceneDescriptionData
            {
                Body = "Scene paused <until tomorrow>.",
                Kind = SceneDescriptionKind.OocNotice,
            },
            "[OOC]");

        formatted.Should().StartWith("<strong>[OOC]</strong> ");
        formatted.Should().Contain("Scene paused &lt;until tomorrow&gt;.");
        formatted.Should().NotContain("<i>");
    }
}
