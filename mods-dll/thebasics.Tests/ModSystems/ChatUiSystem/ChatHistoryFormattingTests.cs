using Cairo;
using HarmonyLib;
using Vintagestory.Client.NoObf;
using FluentAssertions;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using ChatUiModSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class ChatHistoryFormattingTests : IDisposable
{
    private readonly float _originalGuiScale = RuntimeEnv.GUIScale;

    public ChatHistoryFormattingTests()
    {
        LangTestHelper.EnsureEnglish();
        RuntimeEnv.GUIScale = 1;
    }

    public void Dispose() => RuntimeEnv.GUIScale = _originalGuiScale;

    [Fact]
    public void HistoryPatch_InstallsAgainstNativeUpdateText()
    {
        var harmony = new Harmony("thebasics.tests.chat-history-" + Guid.NewGuid());
        var original = AccessTools.Method(typeof(HudDialogChat), "UpdateText");
        try
        {
            harmony.Patch(original,
                prefix: new HarmonyMethod(typeof(ChatUiModSystem), nameof(ChatUiModSystem.HudDialogChat_UpdateText_Prefix)),
                postfix: new HarmonyMethod(typeof(ChatUiModSystem), nameof(ChatUiModSystem.HudDialogChat_UpdateText_Postfix)));
            Harmony.GetPatchInfo(original).Prefixes.Should().Contain(p => p.owner == harmony.Id);
        }
        finally
        {
            harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
        }
    }

    [Theory]
    [InlineData("<strong>Restarting<br>")]
    [InlineData("<font weight=\"bold\" color=\"#ff0000\">Restarting<br>")]
    [InlineData("<i>Restarting<br>")]
    [InlineData("<strong")]
    public void MalformedAnnouncement_DoesNotStyleOrHideLaterMessages(string announcement)
    {
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = new CairoFont(17, "Arial", [1.0, 1.0, 1.0, 1.0]);
        var messages = new[] { announcement, "<strong>Alice</strong> plain-marker<br>", "(OOC) ooc-marker<br>" };

        var components = ChatUiModSystem.BuildChatHistoryComponents(api, messages, baseFont).OfType<RichTextComponent>().ToArray();

        components.Should().Contain(c => c.DisplayText.Contains("Alice") && c.Font.FontWeight == FontWeight.Bold);
        foreach (var marker in new[] { "plain-marker", "ooc-marker" })
        {
            var component = components.Single(c => c.DisplayText.Contains(marker));
            component.Font.FontWeight.Should().Be(FontWeight.Normal);
            component.Font.Slant.Should().Be(FontSlant.Normal);
            component.Font.Color.Should().Equal(baseFont.Color);
        }
    }

    [Fact]
    public void EscapedUserAngles_RemainLiteralText()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = new CairoFont(17, "Arial", [1.0, 1.0, 1.0, 1.0]);
        var messages = new[] { "a &lt; b &gt; c &lt;strong&gt;literal&lt;/strong&gt; &amp;", "next-marker" };

        var components = ChatUiModSystem.BuildChatHistoryComponents(api, messages, baseFont).OfType<RichTextComponent>().ToArray();

        string.Concat(components.Select(c => c.DisplayText)).Should().Be("a < b > c <strong>literal</strong> &amp;\r\nnext-marker");
        components.Should().OnlyContain(c => c.Font.FontWeight == FontWeight.Normal && c.Font.Slant == FontSlant.Normal);
    }

    [Fact]
    public void NativeLinksFontSizesAndOtherModTagConverters_ArePreserved()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = new CairoFont(17, "Arial", [1.0, 1.0, 1.0, 1.0]);
        const string tagName = "thebasicshistorytest";
        VtmlUtil.TagConverters.Add(tagName, (capi, tag, fontStack, didClickLink) =>
            new RichTextComponent(capi, "converted-marker", fontStack.Peek()));
        try
        {
            var messages = new[] { "<font size=\"24\">large-marker</font> <a href=\"https://example.com\">link-marker</a>", "<thebasicshistorytest>custom</thebasicshistorytest> next-marker" };
            var components = ChatUiModSystem.BuildChatHistoryComponents(api, messages, baseFont);

            var link = components.OfType<LinkTextComponent>().Single();
            link.Href.Should().Be("https://example.com");
            link.DisplayText.Should().Be("link-marker");
            components.OfType<RichTextComponent>().Single(c => c.DisplayText == "large-marker").Font.UnscaledFontsize.Should().Be(24);
            components.OfType<RichTextComponent>().Single(c => c.DisplayText == "converted-marker").Font.UnscaledFontsize.Should().Be(17);
            components.OfType<RichTextComponent>().Single(c => c.DisplayText == "next-marker").Font.UnscaledFontsize.Should().Be(17);
        }
        finally
        {
            VtmlUtil.TagConverters.Remove(tagName);
        }
    }
    [Fact]
    public void MessageBoundaries_PreserveMultilineMarkupAndEmptyEntries()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = new CairoFont(17, "Arial", [1.0, 1.0, 1.0, 1.0]);
        var messages = new[] { "<i>first<br>second</i>", "", "<font color=\"#ff0000\"><strong>Alice</strong></font> says hello" };

        var components = ChatUiModSystem.BuildChatHistoryComponents(api, messages, baseFont).OfType<RichTextComponent>().ToArray();

        string.Concat(components.Select(c => c.DisplayText)).Should().Be("first\r\nsecond\r\n\r\nAlicesays hello");
        components.Where(c => c.DisplayText is "first" or "second").Should().OnlyContain(c => c.Font.Slant == FontSlant.Italic);
        components.Single(c => c.DisplayText == "Alice").Font.FontWeight.Should().Be(FontWeight.Bold);
        components.Single(c => c.DisplayText.Contains("says hello")).Font.FontWeight.Should().Be(FontWeight.Normal);
        components.Single(c => c.DisplayText.Contains("says hello")).PaddingLeft.Should().BeGreaterThan(0);
    }
}
