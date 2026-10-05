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
