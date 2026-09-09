using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionDataTests
{
    [Fact]
    public void FloatingBubbleDefaultsToTitleAndReadCueWithoutChangingFullText()
    {
        var data = new SceneDescriptionData { Title = "A <clue>", Body = "Hidden description" };
        data.ShowBodyInBubble.Should().BeFalse();
        SceneDescriptionData.ReadFrom(new TreeAttribute()).ShowBodyInBubble.Should().BeFalse();
        var bubble = SceneDescriptionFormatter.ToFloatingVtml(data, "Right-click to read");
        bubble.Should().Contain("A &lt;clue&gt;").And.Contain("Right-click to read").And.NotContain("Hidden description");
        SceneDescriptionFormatter.ToVtml(data).Should().Contain("Hidden description");
        data.Body.Should().Be("Hidden description");
        data.Title = "";
        SceneDescriptionFormatter.ToFloatingVtml(data, "Right-click to read").Should().Contain("Right-click to read");
        data.Title = "Title only";
        data.Body = "";
        data.ShouldShowDescription(true).Should().BeTrue();
        SceneDescriptionFormatter.ToFloatingVtml(data, "Right-click to read").Should().Be("<strong>Title only</strong>");
    }

    [Fact]
    public void FloatingDescriptionCanBeEnabledAndSaved()
    {
        var data = new SceneDescriptionData { Title = "Scene", Body = "First\nSecond", ShowBodyInBubble = true };
        SceneDescriptionFormatter.ToFloatingVtml(data, "Read").Should().Be("<strong>Scene</strong><br>First<br>Second");
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        SceneDescriptionData.ReadFrom(tree).ShowBodyInBubble.Should().BeTrue();
        data.Clone().ShowBodyInBubble.Should().BeTrue();
        data.AppearanceDefaults().ShowBodyInBubble.Should().BeTrue();
        var edited = new SceneDescriptionData();
        edited.ApplyText(data);
        edited.ShowBodyInBubble.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IdleBobbingRoundTripsIncludingFalseAndDefaultsOnForLegacyMarkers(bool enabled)
    {
        var data = new SceneDescriptionData { IdleBobbing = enabled };
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        SceneDescriptionData.ReadFrom(tree).IdleBobbing.Should().Be(enabled);
        data.Clone().IdleBobbing.Should().Be(enabled);
        data.AppearanceDefaults().IdleBobbing.Should().Be(enabled);
        var edited = new SceneDescriptionData();
        edited.ApplyText(data);
        edited.IdleBobbing.Should().Be(enabled);
        var packet = new SceneDescriptionEditPacket { IdleBobbing = enabled };
        Vintagestory.API.Util.SerializerUtil.Deserialize<SceneDescriptionEditPacket>(
            Vintagestory.API.Util.SerializerUtil.Serialize(packet)).IdleBobbing.Should().Be(enabled);
        SceneDescriptionData.ReadFrom(new TreeAttribute()).IdleBobbing.Should().BeTrue();
        Vintagestory.API.Util.SerializerUtil.Deserialize<SceneDescriptionEditPacket>(Array.Empty<byte>()).IdleBobbing.Should().BeTrue();
    }

    [Theory]
    [InlineData(SceneMarkerSymbol.Dot)]
    [InlineData(SceneMarkerSymbol.Ring)]
    [InlineData(SceneMarkerSymbol.Diamond)]
    public void NewSymbolsAndHologramPersistAndBecomeFreshMarkerDefaults(SceneMarkerSymbol symbol)
    {
        var data = new SceneDescriptionData { Symbol = symbol, Effect = SceneMarkerEffect.Hologram };
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        var restored = SceneDescriptionData.ReadFrom(tree);
        restored.Symbol.Should().Be(symbol);
        restored.Effect.Should().Be(SceneMarkerEffect.Hologram);
        restored.Clone().Effect.Should().Be(SceneMarkerEffect.Hologram);
        restored.AppearanceDefaults().Effect.Should().Be(SceneMarkerEffect.Hologram);
        var edited = new SceneDescriptionData();
        edited.ApplyText(restored);
        edited.Symbol.Should().Be(symbol);
        edited.Effect.Should().Be(SceneMarkerEffect.Hologram);
    }

    [Fact]
    public void MissingAndInvalidEffectsRemainPlainAndShimmerNeverFlashesOff()
    {
        SceneDescriptionData.ReadFrom(new TreeAttribute()).Effect.Should().Be(SceneMarkerEffect.Plain);
        new SceneDescriptionData { Effect = (SceneMarkerEffect)99 }.Normalize().Effect.Should().Be(SceneMarkerEffect.Plain);
        for (var step = 0; step < 600; step++)
        {
            SceneMarkerVisuals.EffectOpacity(SceneMarkerEffect.Hologram, step / 10.0).Should().BeInRange(0.5984f, 0.68f);
            SceneMarkerVisuals.EffectOpacity(SceneMarkerEffect.Plain, step / 10.0).Should().Be(1);
        }
    }

    [Fact]
    public void IndicatorSizePersistsIndependentlyOfHeightAndMigratesAtFullSize()
    {
        var data = new SceneDescriptionData { IndicatorScale = 2, HeightOffset = 3 };
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        var restored = SceneDescriptionData.ReadFrom(tree);
        restored.IndicatorScale.Should().Be(2);
        restored.HeightOffset.Should().Be(3);
        restored.Clone().IndicatorScale.Should().Be(2);
        restored.AppearanceDefaults().IndicatorScale.Should().Be(2);
        restored.ApplyText(new SceneDescriptionData { IndicatorScale = 0.5f, HeightOffset = 3 });
        restored.IndicatorScale.Should().Be(0.5f);
        restored.HeightOffset.Should().Be(3);
        SceneDescriptionData.ReadFrom(new TreeAttribute()).IndicatorScale.Should().Be(1);
        new SceneDescriptionData { IndicatorScale = float.NaN }.Normalize().IndicatorScale.Should().Be(1);
        new SceneDescriptionData { IndicatorScale = 0 }.Normalize().IndicatorScale.Should().Be(0.25f);
        new SceneDescriptionData { IndicatorScale = 100 }.Normalize().IndicatorScale.Should().Be(3);
    }

    [Fact]
    public void AppearanceControlsRoundTripWithoutCopyingContentOrOwnership()
    {
        var data = new SceneDescriptionData { Title = "Private title", Body = "Scene", AuthorUid = "owner", LockItemCode = "ui",
            Symbol = SceneMarkerSymbol.Question, Color = SceneMarkerColor.Green, HeightOffset = 2,
            Display = SceneDescriptionDisplay.AlwaysNearby, IconDistance = 40, TextDistance = 6, UnlimitedTextDistance = true };
        var tree = new TreeAttribute();
        data.WriteTo(tree);
        SceneDescriptionData.ReadFrom(tree).Should().BeEquivalentTo(data);
        var defaults = data.AppearanceDefaults();
        defaults.Title.Should().BeEmpty();
        defaults.Body.Should().BeEmpty();
        defaults.AuthorUid.Should().BeEmpty();
        defaults.IsLocked.Should().BeFalse();
        defaults.Color.Should().Be(SceneMarkerColor.Green);
        defaults.HeightOffset.Should().Be(2);
        defaults.TextDistance.Should().Be(6);
        defaults.UnlimitedTextDistance.Should().BeTrue();
    }

    [Fact]
    public void TextRangeIsIndependentAndOldMarkersRetainTheirSharedRange()
    {
        var data = new SceneDescriptionData { IconDistance = 24, TextDistance = 8 };
        data.GetIconOpacity(10).Should().Be(1);
        data.GetTextOpacity(10).Should().Be(0);
        data.GetTextOpacity(7).Should().Be(0.5f);
        data.UnlimitedTextDistance = true;
        data.GetTextOpacity(100).Should().Be(1);
        data.GetIconOpacity(100).Should().Be(0);
        var old = new TreeAttribute();
        old.SetFloat("sceneIconDistance", 50);
        old.SetBool("sceneIconUnlimited", true);
        SceneDescriptionData.ReadFrom(old).TextDistance.Should().Be(50);
        SceneDescriptionData.ReadFrom(old).UnlimitedTextDistance.Should().BeTrue();
    }

    [Fact]
    public void MalformedAppearanceControlsAreNormalized()
    {
        var data = new SceneDescriptionData { Color = (SceneMarkerColor)99, TextDistance = float.NaN, HeightOffset = float.PositiveInfinity }.Normalize();
        data.Color.Should().Be(SceneMarkerColor.Gold);
        data.TextDistance.Should().Be(8);
        data.HeightOffset.Should().Be(0);
        new SceneDescriptionData { HeightOffset = 100, TextDistance = -2 }.Normalize().HeightOffset.Should().Be(4);
        new SceneDescriptionData { HeightOffset = -100, TextDistance = -2 }.Normalize().TextDistance.Should().Be(1);
    }

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
