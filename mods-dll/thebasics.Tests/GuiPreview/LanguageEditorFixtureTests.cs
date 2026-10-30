using System.Text.Json;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using TheBasics.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class LanguageEditorFixtureTests
{
    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingOrUnconfirmedSaveDisablesLanguageReset(bool timedOut)
    {
        using var host = Host(1.25);
        using var scene = PreviewScene.Create("wizard-language-edited", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        Assert.True(wizard.SingleComposer.GetButton("language-editor-reset").Enabled);
        wizard.Draft.CreateSaveRequest(1, "run");
        if (timedOut) wizard.Draft.MarkSaveTimedOut();
        wizard.SetPreviewTime(1);
        Assert.False(wizard.SingleComposer.GetButton("language-editor-reset").Enabled);
        Assert.Empty(host.RecordedEffects);
    }

    [Fact]
    public void DefaultLanguageFixturesComeFromTheInitializedConfiguration()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        Assert.Equal(LanguageConfigAdmin.BuildEntries(config), SetupWizardCaptureScenes.DefaultLanguages());
        var first = SetupWizardCaptureScenes.DefaultLanguages();
        first[0].Name = "Changed only in this fixture";
        Assert.NotEqual(first[0].Name, SetupWizardCaptureScenes.DefaultLanguages()[0].Name);
    }

    [Fact]
    public void OtherCaptureScenesKeepTheSuppliedConfiguredLanguages()
    {
        var languages = SetupWizardCaptureScenes.DefaultLanguages();
        languages[0].Name = "Server's configured language";
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), languages);
        SetupWizardCaptureScenes.ApplyDraft(draft, "wizard-chat-default");
        Assert.Equal(languages, draft.Languages);
        Assert.False(draft.IsDirty);
    }

    [VisualTheory]
    [InlineData("wizard-language-configured", 1.0)]
    [InlineData("wizard-language-edited", 1.25)]
    [InlineData("wizard-language-invalid", 1.25)]
    [InlineData("wizard-language-grants", 1.25)]
    public void LanguageScenesRenderConfiguredRowsAndRecordThemInTheManifest(string scenario, double scale)
    {
        using var host = Host(scale);
        using var scene = PreviewScene.Create(scenario, host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        Assert.Equal("chat.languageeditor", wizard.CurrentStepId);
        Assert.Equal(new[] { "Dock Cant", "Quayside" }, wizard.Draft.Languages.Select(row => row.OriginalName));
        Assert.DoesNotContain(wizard.Draft.Languages, row => row.Name is "Common" or "Tradeband");
        Assert.Equal(scenario is "wizard-language-edited" or "wizard-language-invalid", wizard.Draft.IsDirty);

        host.RenderGuiDialog(wizard, 0);
        var pixels = host.Canvas.GetRgbaPixels();
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal(pixels, host.Canvas.GetRgbaPixels());
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
        Assert.False(wizard.Draft.IsSavePending);

        var fixture = JsonSerializer.SerializeToElement(scene.Fixture);
        Assert.Equal(wizard.Draft.Values.Count, fixture.GetProperty("values").GetArrayLength());
        var languages = fixture.GetProperty("languages");
        Assert.Equal(2, languages.GetArrayLength());
        Assert.Equal(wizard.Draft.Languages[0].Syllables, languages[0].GetProperty("Syllables").GetString());
        Assert.Equal(wizard.Draft.Languages[0].Color, languages[0].GetProperty("Color").GetString());
        if (scenario == "wizard-language-invalid")
        {
            Assert.NotEmpty(Text(wizard, "language-editor-errors"));
            Assert.Empty(Text(wizard, "language-editor-understood"));
            Assert.Empty(Text(wizard, "language-editor-unfamiliar"));
        }
        else if (scenario != "wizard-language-grants")
        {
            Assert.Contains("Meet me by the gate.", Text(wizard, "language-editor-understood"));
            Assert.DoesNotContain("Meet me by the gate.", Text(wizard, "language-editor-unfamiliar"));
            Assert.Empty(Text(wizard, "language-editor-errors"));
        }
        else
        {
            Assert.Equal("trader", wizard.SingleComposer.GetTextInput("language-editor-GrantedToClasses").GetText());
            Assert.Equal("seraph", wizard.SingleComposer.GetTextInput("language-editor-GrantedToModels").GetText());
            Assert.Equal("harborfolk", wizard.SingleComposer.GetTextInput("language-editor-GrantedToModelGroups").GetText());
            Assert.Equal("worldly", wizard.SingleComposer.GetTextInput("language-editor-GrantedToTraits").GetText());
        }
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EditingNativeFieldsRefreshesSpeechAndKeepsChangesInTheDraft(double scale)
    {
        using var host = Host(scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);
        var before = Text(wizard, "language-editor-unfamiliar");

        wizard.SingleComposer.GetTextInput("language-editor-Syllables").SetValue("zu");
        wizard.SingleComposer.GetTextInput("language-editor-Color").SetValue("#e6b267");
        wizard.SingleComposer.GetTextInput("language-editor-Prefix").SetValue("Harbor");
        host.RenderGuiDialog(wizard, 0);

        Assert.Equal("zu", wizard.Draft.Languages[0].Syllables);
        Assert.Equal("#e6b267", wizard.Draft.Languages[0].Color);
        Assert.Equal("Harbor", wizard.Draft.Languages[0].Prefix);
        Assert.NotEqual(before, Text(wizard, "language-editor-unfamiliar"));
        Assert.Contains("Zu", Text(wizard, "language-editor-unfamiliar"));
        Assert.Contains(wizard.SingleComposer.GetRichtext("language-editor-understood").Components.OfType<RichTextComponent>(),
            component => Math.Abs(component.Font.Color[0] - 0xe6 / 255d) < 0.01 && Math.Abs(component.Font.Color[1] - 0xb2 / 255d) < 0.01);

        wizard.SingleComposer.GetTextInput("language-editor-Color").SetValue("blue");
        wizard.SingleComposer.GetTextInput("language-editor-Syllables").SetValue("");
        host.RenderGuiDialog(wizard, 0);
        Assert.NotEmpty(Text(wizard, "language-editor-errors"));
        Assert.Empty(Text(wizard, "language-editor-understood"));
        Assert.Empty(Text(wizard, "language-editor-unfamiliar"));
        Assert.Equal("blue", wizard.Draft.Languages[0].Color);
        Assert.Equal("", wizard.Draft.Languages[0].Syllables);
        Assert.False(wizard.Draft.IsSavePending);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.25)]
    public void OpeningAndSwitchingModesPreservesLongConfiguredGrantValues(double scale)
    {
        using var host = Host(scale);
        var languages = SetupWizardCaptureScenes.LanguagesForScenario("wizard-language-configured");
        var grants = string.Join(",", Enumerable.Range(0, 80).Select(index => "harborfolk" + index));
        Assert.True(grants.Length > 512);
        languages[0].GrantedToClasses = grants;
        languages[0].GrantedToTraits = grants;
        languages[0].GrantedToModels = grants;
        languages[0].GrantedToModelGroups = grants;
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), languages);
        using var wizard = new SetupWizardDialog(host.Api, draft, "long-grants", true,
            () => { }, null!, null!, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);
        wizard.TryOpen();
        wizard.ShowLanguageEditor();
        wizard.SingleComposer.GetScrollbar("language-editor-scroll").ScrollToBottom();
        host.RenderGuiDialog(wizard, 0);
        foreach (var key in new[] { "GrantedToClasses", "GrantedToTraits", "GrantedToModels", "GrantedToModelGroups" })
            Assert.Equal(grants, wizard.SingleComposer.GetTextInput("language-editor-" + key).GetText());
        wizard.ShowLanguageEditor();
        wizard.ShowLanguageEditor();
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal(languages, draft.Languages);
        Assert.False(draft.IsDirty);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void StartingSpeakerRulesDisableWithoutLosingIdsAndFitCompactView(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-grants", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var keys = new[] { "GrantedToClasses", "GrantedToTraits", "GrantedToModels", "GrantedToModelGroups" };
        wizard.Draft.Languages[1].Default = true;
        wizard.ShowLanguageEditor();
        wizard.SingleComposer.GetScrollbar("language-editor-scroll").ScrollToBottom();
        var values = keys.Select(key => wizard.SingleComposer.GetTextInput("language-editor-" + key).GetText()).ToArray();
        Check(enabled: false);
        Capture("everyone");
        var toggle = Assert.IsType<GuiElementSetupToggle>(wizard.SingleComposer.GetElement("language-editor-Default"));
        wizard.SingleComposer.FocusElement(toggle.TabIndex);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Space });
        Assert.False(wizard.Draft.Languages[0].Default);
        Check(enabled: true);
        Capture("rules");
        Assert.Equal(values, keys.Select(key => wizard.SingleComposer.GetTextInput("language-editor-" + key).GetText()).ToArray());
        Assert.Equal(new[] { "sound:toggleswitch" }, host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);

        void Check(bool enabled)
        {
            host.RenderGuiDialog(wizard, 0);
            foreach (var key in keys)
            {
                var input = wizard.SingleComposer.GetTextInput("language-editor-" + key);
                Assert.Equal(enabled, input.Enabled);
                Assert.True(input.Bounds.renderY + input.Bounds.OuterHeight < 720);
            }
        }
        void Capture(string state)
        {
            var output = Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT");
            if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output);
            host.Canvas.SavePng(Path.Combine(output, $"starting-speakers-{state}-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png"));
        }
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);

    private static string Text(SetupWizardDialog wizard, string key) => string.Concat(
        wizard.SingleComposer.GetRichtext(key).Components.OfType<RichTextComponent>().Select(component => component.DisplayText));
}
