using System.Runtime.CompilerServices;
using thebasics.Configs;
using Vintagestory.API.Datastructures;
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
    [InlineData(0)]
    [InlineData(77)]
    public void NativeHudUpdate_ReplacesComponentsPreservesHeldScrollAndTogglesLive(int groupId)
    {
        var apiField = AccessTools.Field(typeof(ChatUiModSystem), "_api");
        var configField = AccessTools.Field(typeof(ChatUiModSystem), "_config");
        var previousApi = apiField.GetValue(null);
        var previousConfig = configField.GetValue(null);
        var api = Substitute.For<ICoreClientAPI>();
        // Initialize only the native HUD/world state used by UpdateText; GPU uploads stay mocked.
        var game = (ClientMain)RuntimeHelpers.GetUninitializedObject(typeof(ClientMain));
        game.currentGroupid = groupId;
        var history = new LimitedList<string>(100);
        history.Add("<strong>restart<br>");
        for (var i = 0; i < 20; i++) history.Add($"<font color=\"#ffffff\">plain-marker-{i}</font>");
        game.ChatHistoryByPlayerGroup = new Dictionary<int, LimitedList<string>> { [groupId] = history };
        api.World.Returns(game);
        var hud = (HudDialogChat)RuntimeHelpers.GetUninitializedObject(typeof(HudDialogChat));
        AccessTools.Field(typeof(HudDialogChat), "game").SetValue(hud, game);
        hud.Composers = new GuiDialog.DlgComposers(hud);
        var font = CairoFont.WhiteDetailText().WithFontSize(17f);
        var oldComponent = Substitute.ForPartsOf<RichTextComponent>(api, "old", font);
        using var historyComposer = new GuiComposer(api, ElementBounds.FixedSize(220, 600).WithEmptyParent(), "history");
        var richtext = new GuiElementRichtext(api, [oldComponent], ElementBounds.FixedSize(220, 1));
        historyComposer.AddInteractiveElement(richtext, "chathistory");
        hud.Composers["chat-group-" + groupId] = historyComposer;
        var scrollChanges = new List<float>();
        using var chatComposer = new GuiComposer(api, ElementBounds.FixedSize(10, 80).WithEmptyParent(), "chat");
        var scrollbar = new GuiElementCompactScrollbar(api, scrollChanges.Add, ElementBounds.FixedSize(10, 80));
        chatComposer.AddInteractiveElement(scrollbar, "scrollbar");
        hud.Composers["chat"] = chatComposer;
        scrollbar.Bounds.CalcWorldBounds();
        scrollbar.SetHeights(80, 80);
        scrollChanges.Clear();
        var original = AccessTools.Method(typeof(HudDialogChat), "UpdateText");
        var harmony = new Harmony("thebasics.tests.native-hud-" + Guid.NewGuid());
        var config = new ModConfig { IsolateChatMessageFormatting = true };
        try
        {
            apiField.SetValue(null, api);
            configField.SetValue(null, config);
            harmony.Patch(original,
                prefix: new HarmonyMethod(typeof(ChatUiModSystem), nameof(ChatUiModSystem.HudDialogChat_UpdateText_Prefix)),
                postfix: new HarmonyMethod(typeof(ChatUiModSystem), nameof(ChatUiModSystem.HudDialogChat_UpdateText_Postfix)));
            original.Invoke(hud, null);

            oldComponent.Received(1).Dispose();
            richtext.Components.OfType<RichTextComponent>().Single(c => c.DisplayText == "plain-marker-0")
                .Font.FontWeight.Should().Be(FontWeight.Normal);
            richtext.Bounds.fixedHeight.Should().BeGreaterThan(80);
            scrollChanges.Last().Should().Be((float)richtext.Bounds.fixedHeight + 5 - 80);
            LoadedTexture texture = null!;
            api.Gui.ReceivedWithAnyArgs().LoadOrUpdateCairoTexture(null!, true, ref texture);

            scrollbar.mouseDownOnScrollbarHandle = true;
            scrollbar.SetScrollbarPosition(0);
            scrollChanges.Clear();
            history.Add("<font color=\"#ffffff\">later-marker</font>");
            original.Invoke(hud, null);
            scrollChanges.Should().ContainSingle().Which.Should().Be(0);
            richtext.Components.OfType<RichTextComponent>().Single(c => c.DisplayText == "later-marker")
                .Font.FontWeight.Should().Be(FontWeight.Normal);

            config.IsolateChatMessageFormatting = false;
            original.Invoke(hud, null);
            richtext.Components.OfType<RichTextComponent>().Single(c => c.DisplayText.Contains("later-marker"))
                .Font.FontWeight.Should().Be(FontWeight.Bold);

            config.IsolateChatMessageFormatting = true;
            scrollbar.mouseDownOnScrollbarHandle = false;
            scrollChanges.Clear();
            original.Invoke(hud, null);
            richtext.Components.OfType<RichTextComponent>().Single(c => c.DisplayText == "later-marker")
                .Font.FontWeight.Should().Be(FontWeight.Normal);
            scrollChanges.Last().Should().Be((float)richtext.Bounds.fixedHeight + 5 - 80);
        }
        finally
        {
            harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
            apiField.SetValue(null, previousApi);
            configField.SetValue(null, previousConfig);
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
        var messages = new[] { "<strong", "a &lt; b &gt; c &lt;strong&gt;literal&lt;/strong&gt; &amp;", "next-marker" };

        var components = ChatUiModSystem.BuildChatHistoryComponents(api, messages, baseFont).OfType<RichTextComponent>().ToArray();

        string.Concat(components.Select(c => c.DisplayText)).Should().Be("\r\na < b > c <strong>literal</strong> &amp;\r\nnext-marker");
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
            var messages = new[] { "<strong", "<font size=\"24\">large-marker</font> <a href=\"https://example.com\">link-marker</a>", "<thebasicshistorytest>custom</thebasicshistorytest> next-marker" };
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

    [Theory]
    [InlineData(220, 1f)]
    [InlineData(700, 1f)]
    [InlineData(220, 1.5f)]
    [InlineData(700, 1.5f)]
    public void OrdinaryHistory_RendersIdenticallyToVanilla(int width, float scale)
    {
        RuntimeEnv.GUIScale = scale;
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = CairoFont.WhiteDetailText().WithFontSize(17f);
        string[][] histories =
        [
            [], ["", ""], ["single message"],
            ["hello world", "another ordinary message", "last message"],
            ["<strong>Alice</strong> says a long sentence with enough ordinary words to wrap in the narrow chat window.", "<i>Bob waves hello.</i>", "(OOC) plain message"],
            ["<font size=\"24\" color=\"#ff0000\">Large announcement</font>", "<font size=\"12\">Quiet speech</font>", "normal speech"],
            ["leading and trailing spaces  ", "  second message  ", "a &lt; b &gt; c"]
        ];
        foreach (var history in histories)
        {
            var vanilla = RenderHistory(api, VtmlUtil.Richtextify(api, string.Join("\r\n", history), baseFont.Clone()), width);
            var isolated = RenderHistory(api, ChatUiModSystem.BuildChatHistoryComponents(api, history, baseFont), width);

            isolated.Height.Should().Be(vanilla.Height, "valid history should retain its height: {0}", string.Join(" | ", history));
            isolated.Pixels.Should().Equal(vanilla.Pixels, "valid history should retain its pixels: {0}", string.Join(" | ", history));
        }
    }

    [Theory]
    [InlineData(1f, 1d)]
    [InlineData(1.5f, 0d)]
    public void MultilineEmptyHistory_HasDocumentedNativeRoundingDifference(float scale, double heightDifference)
    {
        RuntimeEnv.GUIScale = scale;
        var api = Substitute.For<ICoreClientAPI>();
        var baseFont = CairoFont.WhiteDetailText().WithFontSize(17f);
        var history = new[] { "<i>first<br>second</i>", "", "third\r\nfourth", "" };
        var vanilla = RenderHistory(api, VtmlUtil.Richtextify(api, string.Join("\r\n", history), baseFont.Clone()), 220);
        var isolated = RenderHistory(api, ChatUiModSystem.BuildChatHistoryComponents(api, history, baseFont), 220);

        // Native layout rounds at component boundaries. Independent parsing adds boundaries,
        // producing a measured one-pixel shift in this case at scale 1.
        (isolated.Height - vanilla.Height).Should().Be(heightDifference);
        isolated.Pixels.SequenceEqual(vanilla.Pixels).Should().Be(heightDifference == 0);
    }
    private static (double Height, byte[] Pixels) RenderHistory(ICoreClientAPI api, RichTextComponentBase[] components, int width)
    {
        var bounds = ElementBounds.FixedSize(width, 600).WithEmptyParent();
        using var richtext = new GuiElementRichtext(api, components, bounds);
        richtext.CalcHeightAndPositions();
        var height = bounds.fixedHeight;
        bounds.CalcWorldBounds();
        using var surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(bounds.InnerWidth), Math.Max(1, (int)Math.Ceiling(bounds.InnerHeight)));
        using var context = new Context(surface);
        richtext.ComposeFor(bounds, context, surface);
        surface.Flush();
        return (height, surface.Data.ToArray());
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
