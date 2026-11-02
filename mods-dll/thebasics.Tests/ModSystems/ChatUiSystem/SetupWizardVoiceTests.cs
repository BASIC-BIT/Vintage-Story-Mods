using System.Reflection;
using NSubstitute;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Tests.GuiPreview;
using thebasics.Tests.Support;
using TheBasics.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardVoiceTests
{
    [Theory]
    [InlineData("chat.voices", 8)]
    [InlineData("chat.tts", 6)]
    public void PagesExposeAllControls(string id, int count)
    {
        Assert.True(SetupWizardCatalog.TryGetPage(id, out var page));
        Assert.Equal(count, page.SettingKeys.Count);
    }

    [Theory]
    [InlineData(ProximityChatMode.Whisper)]
    [InlineData(ProximityChatMode.Normal)]
    [InlineData(ProximityChatMode.Yell)]
    public void EditedValuesReachProductionChatterAndSpeechPacket(ProximityChatMode mode)
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ChatterModeVolume." + mode, "1.234567");
        draft.Set("ChatterModePitch." + mode, "0.712345");
        draft.Set("ChatterSelfVolumeMultiplier", "0.312345");
        draft.Set("RPTTS_ModeGain." + mode, "3.123456");
        draft.Set("RPTTS_ModeFalloff." + mode, "9.123456");
        var request = draft.CreateSaveRequest(5, "task-5");
        Assert.Contains(request.Values, value => value.Key == "ChatterModePitch." + mode && value.Value == "0.712345");
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        Assert.True(new SetupWizardState(config).TryValidatePatch(config, request, out _, out _, out _, out var errors), string.Join("; ", errors));
        Assert.Empty(ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values));
        var player = new FakeServerPlayer { Entity = new EntityPlayer { EntityId = 42 } };
        var context = new MessageContext { SendingPlayer = player };
        context.SetMetadata(MessageContext.CHAT_MODE, mode);
        context.SetFlag(MessageContext.IS_SPEECH); context.SetSpeechText("Meet me by the gate.");
        var chatter = ChatterMessagePlanner.CreateBaseMessage(player, context, config, 20);
        Assert.Equal(1.234567f, chatter.Volume); Assert.Equal(.712345f, chatter.Pitch);
        Assert.Equal(1.234567f * .312345f, ChatterMessagePlanner.ForRecipient(chatter, true, config.ChatterSelfVolumeMultiplier).Volume);
        Assert.Same(chatter, ChatterMessagePlanner.ForRecipient(chatter, false, config.ChatterSelfVolumeMultiplier));
        var channel = Substitute.For<IServerNetworkChannel>();
        var system = new RPProximityChatSystem { Config = config };
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(system, channel);
        system.DispatchSpeechForContext(context);
        channel.Received(1).SendPacket(Arg.Is<ProximitySpeechMessage>(m => m.Gain == 3.123456f && m.Falloff == 9.123456f && m.Text == "Meet me by the gate."), player);
        config.ChatterModeVolume.Clear(); config.ChatterModePitch.Clear();
        var fallback = ChatterMessagePlanner.CreateBaseMessage(player, context, config, 20);
        Assert.Equal(.8f, fallback.Volume); Assert.Equal(1f, fallback.Pitch);
        config.RPTTS_ModeGain.Clear(); config.RPTTS_ModeFalloff.Clear();
        system.DispatchSpeechForContext(context);
        channel.Received(1).SendPacket(Arg.Is<ProximitySpeechMessage>(m => m.Gain == config.GetRpttsGain(mode) && m.Falloff == config.GetRpttsFalloff(mode)), player);
    }

    [Theory]
    [InlineData("ChatterModeVolume", "0", "4", "4.01")]
    [InlineData("ChatterModePitch", "0.1", "4", "0.09")]
    [InlineData("RPTTS_ModeGain", "0", "4", "-0.01")]
    [InlineData("RPTTS_ModeFalloff", "0.1", "10", "10.01")]
    public void ModeRangesUseExistingValidators(string prefix, string minimum, string maximum, string invalid)
    {
        foreach (var mode in new[] { "Whisper", "Normal", "Yell" })
        {
            var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
            foreach (var value in new[] { minimum, maximum })
            {
                draft.Set(prefix + "." + mode, value);
                var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
                Assert.Empty(ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values));
            }
            foreach (var value in new[] { invalid, "NaN", "Infinity" })
            {
                draft.Set(prefix + "." + mode, value);
                var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
                Assert.NotEmpty(ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values));
                Assert.Contains("Invalid draft", SetupWizardVoicePreview.Text(draft, prefix.StartsWith("Chatter") ? "chat.voices" : "chat.tts", false));
                Assert.Equal(value, draft.Get(prefix + "." + mode));
            }
        }
    }

    [Fact]
    public void OptionalProviderBoundaryForwardsParametersOnlyWhenReady()
    {
        var type = typeof(thebasics.ModSystems.ChatUiSystem.ChatUiSystem);
        var ready = type.GetField("_usingRptts", BindingFlags.NonPublic | BindingFlags.Static)!;
        var api = type.GetField("_rpttsApi", BindingFlags.NonPublic | BindingFlags.Static)!;
        var method = type.GetMethod("OnProximitySpeechMessage", BindingFlags.NonPublic | BindingFlags.Static)!;
        var oldReady = ready.GetValue(null); var oldApi = api.GetValue(null);
        try
        {
            var fake = new VoiceProvider();
            var message = new ProximitySpeechMessage { Text = "At the gate", Gain = 1.234567f, Falloff = 9.123456f };
            api.SetValue(null, fake); ready.SetValue(null, false);
            method.Invoke(null, new object[] { message }); Assert.Equal(0, fake.Calls);
            ready.SetValue(null, true); api.SetValue(null, null);
            method.Invoke(null, new object[] { message }); Assert.Equal(0, fake.Calls);
            api.SetValue(null, fake); method.Invoke(null, new object[] { message });
            Assert.Equal(1, fake.Calls); Assert.Equal(message.Text, fake.Text);
            Assert.Equal(message.Gain, fake.Gain); Assert.Equal(message.Falloff, fake.Falloff);
            message.Text = " "; method.Invoke(null, new object[] { message }); Assert.Equal(1, fake.Calls);
        }
        finally { ready.SetValue(null, oldReady); api.SetValue(null, oldApi); }
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        Assert.Contains("not active", SetupWizardVoicePreview.Text(draft, "chat.tts", false));
        Assert.Contains("readiness unverified", SetupWizardVoicePreview.Text(draft, "chat.tts", true));
    }

    public class VoiceProvider
    {
        public int Calls; public string? Text; public float? Gain; public float? Falloff;
        public void APIChatMessage(string text, float? gain, float? falloff) { Calls++; Text = text; Gain = gain; Falloff = falloff; }
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void FormsRetainRawDisabledValuesAndShowInvalidFeedback(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-chat-default", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        string Text() => string.Concat(wizard.SingleComposer.GetRichtext("preview-audio").Components.OfType<RichTextComponent>().Select(c => c.DisplayText));
        foreach (var id in new[] { "chat.voices", "chat.tts" })
        {
            wizard.ShowPage(id);
            SetupWizardCatalog.TryGetPage(id, out var page);
            foreach (var key in page.SettingKeys) Assert.NotNull(wizard.SingleComposer.GetElement(key));
            var keyToEdit = id == "chat.voices" ? "ChatterModePitch.Normal" : "RPTTS_ModeFalloff.Normal";
            var composer = wizard.SingleComposer;
            var input = composer.GetTextInput(keyToEdit);
            composer.FocusElement(input.TabIndex);
            input.SetValue("not a number");
            host.RenderGuiDialog(wizard, 0);
            Assert.Contains("Invalid draft", Text()); Assert.DoesNotContain("Talk:", Text());
            Assert.Equal("not a number", wizard.Draft.Get(keyToEdit));
            Assert.Same(composer, wizard.SingleComposer); Assert.True(input.HasFocus);
            input.SetValue("0.71234567");
            Assert.DoesNotContain("Invalid draft", Text());
            wizard.SingleComposer.FocusElement(wizard.SingleComposer.GetElement(page.SettingKeys.Last()).TabIndex);
            Assert.True(wizard.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition > 0);
        }
        wizard.Draft.Set("EnableChatter", "0"); wizard.ShowPage("chat.voices");
        Assert.Contains("off", Text()); Assert.Equal("0.71234567", wizard.Draft.Get("ChatterModePitch.Normal"));
        wizard.Draft.Set("DisableRPChat", "1"); wizard.ShowPage("chat.tts");
        Assert.Contains("unknown in this preview", Text());
        wizard.ShowPage("server.integrations");
        Assert.Contains("not checked", string.Concat(wizard.SingleComposer.GetRichtext("integration-availability").Components.OfType<RichTextComponent>().Select(c => c.DisplayText)));
        Assert.Equal("0.71234567", wizard.Draft.Get("RPTTS_ModeFalloff.Normal"));
    }
}
