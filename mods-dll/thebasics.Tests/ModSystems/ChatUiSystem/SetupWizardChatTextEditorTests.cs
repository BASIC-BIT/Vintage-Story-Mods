using TheBasics.GuiPreview;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardChatTextEditorTests
{
    [Fact]
    public void ModesUseProductionVerbsQuotesAndPunctuationDeterministically()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        foreach (var mode in new[] { "Whisper", "Normal", "Yell" })
        {
            draft.Set("ProximityChatModeVerbs." + mode, "declares,remarks");
            draft.Set("ProximityChatModeQuestionVerbs." + mode, "wonders,asks");
            draft.Set("ProximityChatModePunctuation." + mode, "...");
        }
        var preview = SetupWizardChatTextEditor.Preview("chat.wording", draft);
        Assert.Contains("declares", preview); Assert.Contains("wonders", preview); Assert.Contains("Hello...", preview);
        Assert.Equal(preview, SetupWizardChatTextEditor.Preview("chat.wording", draft));
        draft.Set("ProseNicknameToken", "@self");
        draft.Set("ProximityChatModeBabbleVerb", "mumbles");
        draft.Set("AttributeFreeformMessagesToPlayerName", "1");
        var prose = SetupWizardChatTextEditor.Preview("chat.wording", draft);
        Assert.DoesNotContain("Type:", prose);
        Assert.Contains("[PipPlayer] Pip", prose);
        Assert.Contains("mumbles", prose);
        draft.Set("ProximityChatModeVerbs.Normal", "<font>");
        Assert.DoesNotContain("<font>", SetupWizardChatTextEditor.Preview("chat.wording", draft));
    }

    [Fact]
    public void AllPairsRetainDraftsAndUseRegistryValidation()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        foreach (var pair in SetupWizardChatTextEditor.Pairs)
        {
            var key = "ChatDelimiters." + pair + ".Start";
            var original = draft.Get(key);
            draft.Set(key, "<literal>"); draft.Set("ChatDelimiters." + pair + ".End", "");
            Assert.StartsWith("<literal>", SetupWizardChatTextEditor.Typed(draft, pair));
            Assert.DoesNotContain("Invalid", SetupWizardChatTextEditor.Result(draft, pair));
            draft.Set(key, "");
            Assert.Contains("Invalid start", SetupWizardChatTextEditor.Result(draft, pair));
            Assert.Equal("", draft.Get(key));
            draft.Set(key, original); Assert.DoesNotContain(key, draft.ChangedKeys);
        }
    }

    [Theory]
    [InlineData("Bold", "[b]", "[/b]", "by", "")]
    [InlineData("Italic", "by", "", "[i]", "[/i]")]
    public void AccentExamplesUseBothDraftPairs(string pair, string boldStart, string boldEnd, string italicStart, string italicEnd)
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ChatDelimiters.Bold.Start", boldStart);
        draft.Set("ChatDelimiters.Bold.End", boldEnd);
        draft.Set("ChatDelimiters.Italic.Start", italicStart);
        draft.Set("ChatDelimiters.Italic.End", italicEnd);
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        Assert.Empty(ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values));
        var production = new AccentTransformer(new RPProximityChatSystem { Config = config })
            .Transform(new MessageContext { Message = SetupWizardChatTextEditor.Typed(draft, pair) }).Message;
        var text = production.Replace("<strong>", "").Replace("</strong>", "").Replace("<i>", "").Replace("</i>", "");
        Assert.Equal("Meet me  the gate.", text);
        Assert.Equal(text, SetupWizardChatTextEditor.Result(draft, pair));
        var otherKey = "ChatDelimiters." + (pair == "Bold" ? "Italic" : "Bold") + ".Start";
        draft.Set(otherKey, "");
        var errors = ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values);
        Assert.Equal(Assert.Single(errors), SetupWizardChatTextEditor.Result(draft, pair));
        Assert.Equal("", draft.Get(otherKey));
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void ContinuousFormsKeepAllControlsAndRefreshExamplesInPlace(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-shortcuts", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        foreach (var page in new[] { "chat.wording", "chat.shortcuts" })
        {
            wizard.ShowPage(page);
            Assert.True(SetupWizardCatalog.TryGetPage(page, out var definition));
            foreach (var key in definition.SettingKeys) Assert.NotNull(wizard.SingleComposer.GetElement(key));
            var scroll = wizard.SingleComposer.GetScrollbar("settings-scroll");
            var previewTop = wizard.SingleComposer.GetElement("preview-text").Bounds.renderY;
            var lastKey = page == "chat.shortcuts" ? "ChatDelimiters.SignLanguageQuote.End" : "RPTextTogglePermission";
            wizard.SingleComposer.FocusElement(wizard.SingleComposer.GetElement(lastKey).TabIndex);
            Assert.True(scroll.CurrentYPosition > 0);
            scroll.ScrollToBottom(); host.RenderGuiDialog(wizard, 0);
            Assert.Equal(previewTop, wizard.SingleComposer.GetElement("preview-text").Bounds.renderY);
        }
        var composer = wizard.SingleComposer;
        composer.GetTextInput("ChatDelimiters.Italic.Start").SetValue("by");
        composer.GetTextInput("ChatDelimiters.Italic.End").SetValue("");
        Assert.Same(composer, wizard.SingleComposer);
        Assert.Contains("Meet me  the gate.", string.Join("", ((GuiElementRichtext)composer.GetElement("shortcut-Bold-result")).Components.OfType<RichTextComponent>().Select(c => c.DisplayText)));
        var input = composer.GetTextInput("ChatDelimiters.Bold.Start");
        input.SetValue("<new>");
        Assert.Same(composer, wizard.SingleComposer);
        var example = Assert.IsAssignableFrom<GuiElementRichtext>(composer.GetElement("shortcut-Bold-typed"));
        var text = string.Join("", example.Components.OfType<RichTextComponent>().Select(c => c.DisplayText));
        Assert.Contains("<new>", text);
        input.SetValue("");
        Assert.Contains("Invalid", string.Join("", ((GuiElementRichtext)composer.GetElement("shortcut-Bold-result")).Components.OfType<RichTextComponent>().Select(c => c.DisplayText)));
        wizard.Draft.Set("ProseNicknameToken", new string('a', 3000));
        wizard.Draft.Set("RPTextTogglePermission", "custom.role");
        wizard.ShowPage("chat.wording");
        Assert.Equal(new string('a', 3000), wizard.Draft.Get("ProseNicknameToken"));
        Assert.Equal("custom.role", wizard.Draft.Get("RPTextTogglePermission"));
        Assert.Empty(host.RecordedEffects); Assert.Empty(host.UnsupportedCalls);
    }
}
