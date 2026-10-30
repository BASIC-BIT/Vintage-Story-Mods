using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Client;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardTextAppearanceTests
{
    [Fact]
    public void FontPreviewUsesProductionSizesAndRetainsValuesWhenDisabled()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ProximityChatClampFontSizes", "30,20,10");
        draft.Set("ProximityChatDefaultFontSize.Normal", "30");
        var config = SetupWizardTextAppearancePreview.Config(draft);
        Assert.Equal(30, DistanceFontSizeTransformer.CalculateFontSize(config, ProximityChatMode.Normal, 0));
        Assert.Equal(10, DistanceFontSizeTransformer.CalculateFontSize(config, ProximityChatMode.Normal, 100));
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = -1;
        Assert.Equal(30, DistanceFontSizeTransformer.CalculateFontSize(config, ProximityChatMode.Normal, 100));
        config.ProximityChatClampFontSizes = [];
        Assert.Equal(30, new DistanceFontSizeTransformer(new thebasics.ModSystems.ProximityChat.RPProximityChatSystem { Config = config }).GetFontSize(null!, null!, ProximityChatMode.Normal));
        draft.Set("EnableDistanceFontSizeSystem", "0");
        Assert.DoesNotContain(SetupWizardTextAppearancePreview.Messages(draft), line => line.Contains("<font size="));
        Assert.Equal("30", draft.Get("ProximityChatDefaultFontSize.Normal"));
        draft.Set("BoldNicknames", "1"); draft.Set("ApplyColorsToNicknames", "0"); draft.Set("ApplyColorsToPlayerNames", "1");
        var messages = SetupWizardTextAppearancePreview.Messages(draft);
        Assert.Contains("<strong>Pip</strong>", messages[0]);
        Assert.DoesNotContain("#91C5DF", messages[0]);
        Assert.Contains("#91C5DF", messages[1]);
    }


    [Theory]
    [InlineData("chat.textappearance", 9)]
    [InlineData("chat.occlusion", 6)]
    public void PagesExposeExactRegistryKeys(string id, int count)
    {
        Assert.True(SetupWizardCatalog.TryGetPage(id, out var page));
        Assert.Equal(count, page.SettingKeys.Count);
        foreach (var key in page.SettingKeys) Assert.True(ConfigAdminSettingRegistry.TryGet(key, out _));
    }
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void ContinuousFormsRetainDisabledValuesAndRevealLastField(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-chat-default", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        foreach (var id in new[] { "chat.textappearance", "chat.occlusion" })
        {
            wizard.ShowPage(id);
            Assert.True(SetupWizardCatalog.TryGetPage(id, out var page));
            foreach (var key in page.SettingKeys) Assert.NotNull(wizard.SingleComposer.GetElement(key));
            if (id == "chat.occlusion") Assert.NotNull(wizard.SingleComposer.GetElement("preview-sight"));
            var previewY = wizard.SingleComposer.GetElement("preview-text").Bounds.renderY;
            wizard.SingleComposer.FocusElement(wizard.SingleComposer.GetElement(page.SettingKeys.Last()).TabIndex);
            Assert.True(wizard.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition > 0);
            host.RenderGuiDialog(wizard, 0);
            Assert.Equal(previewY, wizard.SingleComposer.GetElement("preview-text").Bounds.renderY);
        }
        wizard.Draft.Set("EnableDistanceFontSizeSystem", "0");
        wizard.Draft.Set("ProximityChatDefaultFontSize.Normal", "27");
        wizard.ShowPage("chat.textappearance");
        Assert.Equal("27", wizard.Draft.Get("ProximityChatDefaultFontSize.Normal"));
    }
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void InvalidTypedValuesAndCrossFieldCombinationsShowVisibleFeedback(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-chat-default", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        string Text(string key) => string.Concat(wizard.SingleComposer.GetRichtext(key).Components.OfType<RichTextComponent>().Select(c => c.DisplayText));
        wizard.ShowPage("chat.textappearance");
        var composer = wizard.SingleComposer;
        var input = composer.GetTextInput("ProximityChatDefaultFontSize.Normal");
        composer.FocusElement(input.TabIndex);
        foreach (var invalid in new[] { "129", "not a size" })
        {
            input.SetValue(invalid); host.RenderGuiDialog(wizard, 0);
            Assert.Equal(invalid, wizard.Draft.Get("ProximityChatDefaultFontSize.Normal"));
            Assert.Contains("Invalid", Text("preview-validation"));
            Assert.DoesNotContain("Meet me", Text("preview-text"));
            Assert.Same(composer, wizard.SingleComposer); Assert.True(input.HasFocus);
        }
        input.SetValue("16");
        Assert.Empty(Text("preview-validation")); Assert.Contains("Meet me", Text("preview-text"));
        wizard.ShowPage("chat.occlusion");
        wizard.SingleComposer.GetTextInput("SightPassThroughBlockCodePatterns").SetValue("unqualified");
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal("unqualified", wizard.Draft.Get("SightPassThroughBlockCodePatterns"));
        Assert.Contains("Invalid", Text("preview-sight")); Assert.DoesNotContain("Sight through", Text("preview-sight"));
        Assert.Empty(Text("preview-text"));
        wizard.SingleComposer.GetTextInput("SightPassThroughBlockCodePatterns").SetValue("game:glass-*");
        wizard.SingleComposer.GetTextInput("SightBlockingBlockCodePatterns").SetValue("game:glass-*");
        Assert.Contains("both", Text("preview-sight"));
        wizard.SingleComposer.GetTextInput("SightBlockingBlockCodePatterns").SetValue("");
        wizard.Draft.Set("RequireClearSoundPathForSpeech.Normal", "1");
        wizard.ShowPage("chat.ranges");
        wizard.SingleComposer.GetTextInput("ProximityChatModeDistances.Normal").SetValue("-1");
        wizard.ShowPage("chat.occlusion"); host.RenderGuiDialog(wizard, 0);
        Assert.Contains("Unlimited", Text("preview-sight")); Assert.Empty(Text("preview-text"));
    }

}
