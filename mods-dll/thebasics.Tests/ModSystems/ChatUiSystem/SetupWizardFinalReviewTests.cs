using TheBasics.GuiPreview;
using System.Text.Json;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardFinalReviewTests
{
    public SetupWizardFinalReviewTests() => LangTestHelper.EnsureEnglish();
    [Theory]
    [InlineData("Talk volume", "ChatterModeVolume.Normal")]
    [InlineData("Talk pitch", "ChatterModePitch.Normal")]
    [InlineData("Whisper volume", "ChatterModeVolume.Whisper")]
    [InlineData("Yell pitch", "ChatterModePitch.Yell")]
    [InlineData("Talk gain", "RPTTS_ModeGain.Normal")]
    [InlineData("Talk distance falloff", "RPTTS_ModeFalloff.Normal")]
    [InlineData("Talk size", "ProximityChatDefaultFontSize.Normal")]
    [InlineData("Talk clear sound path", "RequireClearSoundPathForSpeech.Normal")]
    public void FriendlyAndRawSearchReachTheSameScalar(string query, string key)
    {
        var entries = new SetupWizardSearchPanel().Index(new(SetupWizardCaptureScenes.DefaultValues()));
        var friendly = Assert.Single(ConfigAdminSearch.Find(entries, query));
        var raw = Assert.Single(ConfigAdminSearch.Find(entries, key));
        Assert.Equal(key, friendly.Key);
        Assert.Equal(raw, friendly);
        Assert.DoesNotContain("Normal", friendly.Label);
    }

    [Fact]
    public void PartialSearchNeverInventsAValue()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues().Where(v => v.Key != "DebugMode"));
        Assert.DoesNotContain(new SetupWizardSearchPanel().Index(draft), result => result.Key == "DebugMode");
        Assert.Throws<ArgumentException>(() => draft.Set("DebugMode", "1"));
        var config = new thebasics.Configs.ModConfig();
        var loaded = new Dictionary<string, string> { ["ProximityChatName"] = "Server name" };
        var entries = ConfigAdminSearch.Build(config, new Dictionary<string, string>(), loaded);
        Assert.Equal("Server name", Assert.Single(entries, entry => entry.Destination == ConfigSearchDestination.Setting).Value);
    }

    [Fact]
    public void CopySeparatesInputFromHistoryAndExplainsFalloff()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ProseNicknameToken", "@self");
        Assert.DoesNotContain("Type:", SetupWizardChatTextEditor.Preview("chat.wording", draft));
        Assert.Contains("Pip", SetupWizardChatTextEditor.Preview("chat.wording", draft));
        var text = SetupWizardVoicePreview.Text(draft, "chat.tts", null);
        Assert.Contains("controls how audio fades with distance", text);
        Assert.Contains("unknown", text);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void PartialSnapshotKeepsSupportedControlsAndSave(double scale)
    {
        using var host = Host(scale);
        var absent = new[] { "DebugMode", "ProximityChatModeVerbs.Normal", "ChatDelimiters.Bold.Start", "PlayerStatToggles.Deaths", "ManageMapPlayerVisibility", "ServerSaveAnnouncementAsNotification" };
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues().Where(v => !absent.Contains(v.Key)));
        using var wizard = new SetupWizardDialog(host.Api, draft, "partial", true, () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true);
        foreach (var key in absent)
        {
            wizard.ShowPage(SetupWizardCatalog.FindPageForSetting(key).Id);
            Assert.Null(wizard.SingleComposer.GetElement(key));
            Assert.NotNull(wizard.SingleComposer.GetElement("unavailable-" + key));
            if (key == "DebugMode") Assert.Null(wizard.SingleComposer.GetElement("preview-unavailable"));
            if (key is "DebugMode" or "ProximityChatModeVerbs.Normal") Capture(host, wizard, scale, "partial-" + key);
        }
        wizard.ShowPage("chat.wording");
        wizard.SingleComposer.GetTextInput("ProximityChatModeVerbs.Whisper").SetValue("murmurs");
        wizard.ShowPage("chat.shortcuts");
        Assert.Contains("unavailable", SetupWizardChatTextEditor.Result(draft, "Italic"));
        wizard.SingleComposer.GetTextInput("ChatDelimiters.Bold.End").SetValue("[/bold]");
        wizard.ShowSearch("DebugMode"); Assert.Empty(wizard.SearchPanel.Results);
        var request = draft.CreateSaveRequest(1, "partial");
        Assert.Equal(2, request.Values.Count);
        Assert.Contains(request.Values, v => v.Key == "ProximityChatModeVerbs.Whisper" && v.Value == "murmurs");
        Assert.DoesNotContain(request.Values, v => absent.Contains(v.Key));
        Assert.Equal(request.Values.Select(v => v.Key), request.OriginalValues.Select(v => v.Key));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void PrivilegePopupConsumesFirstEscapeAndSecondReturnsToSearch(double scale)
    {
        using var host = Host(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ProximityChatName", "Retained draft");
        using var wizard = new SetupWizardDialog(host.Api, draft, "popup", true, () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true);
        wizard.TryOpen(); wizard.ShowSearch("ChatHistoryPermission");
        Assert.True(wizard.FocusSearchResult(Assert.Single(wizard.SearchPanel.Results)));
        var picker = Assert.IsType<GuiElementPrivilegePicker>(wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        wizard.SingleComposer.FocusElement(picker.TabIndex);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Down });
        Assert.True(picker.IsSuggestionsOpen);
        Capture(host, wizard, scale, "privilege-popup-open");
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.False(picker.IsSuggestionsOpen);
        Assert.Same(picker, wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        Capture(host, wizard, scale, "privilege-popup-closed");
        Assert.Equal("Retained draft", draft.Get("ProximityChatName"));
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.NotNull(wizard.SingleComposer.GetElement("search-result-0"));
        Assert.Equal("ChatHistoryPermission", wizard.SearchPanel.Query);
        Assert.Equal("Retained draft", draft.Get("ProximityChatName"));
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);

    private static void Capture(PreviewHost host, SetupWizardDialog wizard, double scale, string name)
    {
        var root = Environment.GetEnvironmentVariable("FINAL_FIX_CAPTURE_ROOT");
        if (root == null) return;
        host.RenderGuiDialog(wizard, 0);
        var directory = Path.Combine(root, "scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        host.Canvas.SavePng(Path.Combine(directory, name + ".png"));
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(new {
            width = 1280, height = 720, scale,
            sourceTreeHash = Environment.GetEnvironmentVariable("FINAL_FIX_SOURCE_HASH"),
            assemblySha256 = SetupWizardCaptureIdentity.HashFile(typeof(SetupWizardDialog).Assembly.Location),
            environment = host.EnvironmentIdentity,
            coverage = "standalone production GUI, no game or persistence acceptance"
        }));
    }
}
