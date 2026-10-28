using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardMaintenanceTests
{
    public SetupWizardMaintenanceTests() => LangTestHelper.EnsureEnglish();
    [Fact]
    public void NewFilterUsesSnapshotAndDoesNotReviewVisitsOrEdits()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var panel = new SetupWizardSearchPanel { NewOnly = true };
        panel.Search(draft, "");
        panel.Results.Should().BeEmpty("an older server has no authoritative metadata");
        panel.ReviewedKeys = ConfigAdminSettingRegistry.Settings.Select(s => s.Key).Where(key => key != "EnableChatter").ToArray();
        panel.Search(draft, "");
        panel.Results.Should().ContainSingle().Which.Key.Should().Be("EnableChatter");
        draft.Set("EnableChatter", "0");
        panel.Search(draft, "chatter");
        panel.Results.Should().ContainSingle();
        panel.Search(draft, "teleport");
        panel.Results.Should().BeEmpty();
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void ActionsFiltersConfirmationAndFailedReloadKeepDraft(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var snapshot = new TheBasicsSetupWizardResultMessage { Kind = SetupWizardResultKind.Open, Success = true, RunId = "run", SupportsMaintenance = true,
            Values = SetupWizardCaptureScenes.DefaultValues(), Languages = LanguageConfigAdmin.BuildEntries(config), SheetFields = CharacterSheetFieldConfigAdmin.BuildEntries(config),
            ReviewedKeys = ConfigAdminSettingRegistry.Settings.Select(s => s.Key).Where(key => key != "EnableChatter" && key != "DebugMode").ToList() };
        var draft = new SetupWizardDraft(snapshot.Values, snapshot.Languages, snapshot.SheetFields);
        var requests = new List<SetupWizardRequestKind>();
        using var wizard = new SetupWizardDialog(host.Api, draft, "run", true, () => { }, (_, _, _) => { }, () => { },
            layoutOnly: true, actorFactory: host.CreateGuide, onMaintenance: requests.Add, onHelp: () => { });
        wizard.SetSession(snapshot); wizard.TryOpen();
        wizard.ShowPage("server.diagnostics"); Capture("actions", wizard);
        draft.Set("EnableChatter", "0");
        wizard.RequestMaintenance(SetupWizardRequestKind.Reload);
        var confirm = host.Api.Gui.LoadedGuis.OfType<GuiDialogConfirm>().Single(dialog => dialog.IsOpened());
        Capture("reload-confirmation", confirm);
        confirm.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        requests.Should().BeEmpty(); draft.IsDirty.Should().BeTrue();
        if (confirm.IsOpened()) confirm.TryClose();
        wizard.SearchPanel.NewOnly = true; wizard.ShowSearch(""); Capture("new-settings", wizard);
        wizard.SearchPanel.Results.Should().HaveCount(2);
        wizard.SingleComposer.GetButton("mark-reviewed").Enabled.Should().BeTrue();
        wizard.ShowPage("server.diagnostics");
        wizard.SetRequestFailure("Could not reload the config file. Your edits are still here."); Capture("reload-failed", wizard);
        draft.IsDirty.Should().BeTrue();
        wizard.SetRequestFailure("Disk settings loaded. Some changes failed. Restart the server. Your edits are unsaved."); Capture("reload-partial", wizard);
        wizard.RequestMaintenance(SetupWizardRequestKind.MarkReviewed);
        requests.Should().Equal(SetupWizardRequestKind.MarkReviewed);
        wizard.SetSession(new() { RunId = "run", Values = snapshot.Values, Languages = snapshot.Languages, SheetFields = snapshot.SheetFields, IsDedicated = true, RestartRequiredKeys = ["ProximityChatName"] });
        wizard.SetRequestFailure("Settings saved. Restart needed. See Review changes for details.");
        wizard.ShowPage("review"); Capture("restart-details", wizard);
        wizard.ShowPage("server.diagnostics");
        wizard.SingleComposer.GetButton("reload-disk").Enabled.Should().BeFalse();
        wizard.RequestMaintenance(SetupWizardRequestKind.Reload); requests.Should().HaveCount(1);
        draft.IsDirty.Should().BeTrue();
        host.UnsupportedCalls.Should().BeEmpty();

        void Capture(string name, GuiDialog dialog)
        {
            host.RenderGuiDialog(dialog, 0);
            var root = Environment.GetEnvironmentVariable("TASK7_CAPTURE_ROOT"); if (root == null) return;
            var dir = Path.Combine(root, "scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)); Directory.CreateDirectory(dir);
            host.Canvas.SavePng(Path.Combine(dir, name + ".png"));
            File.WriteAllText(Path.Combine(dir, name + ".json"), JsonSerializer.Serialize(new { width = 1280, height = 720, scale,
                sourceTreeHash = Environment.GetEnvironmentVariable("TASK7_SOURCE_HASH"), assemblySha256 = SetupWizardCaptureIdentity.HashFile(typeof(SetupWizardDialog).Assembly.Location),
                environment = host.EnvironmentIdentity, coverage = "standalone production GUI, no game or persistence acceptance" }));
        }
    }

    [VisualTheory]
    [InlineData("failed")]
    [InlineData("success")]
    [InlineData("partial")]
    public void ReloadResponseDiscardsOnlyOnSuccessAndHelpReturnsSameDialog(string outcome)
    {
        var success = outcome == "success";
        var partial = outcome == "partial";
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720);
        var api = Substitute.For<ICoreClientAPI>(); api.Gui.Returns(host.Api.Gui);
        var channel = Substitute.For<IClientNetworkChannel>(); channel.Connected.Returns(true);
        using var safe = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safe);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()); draft.Set("EnableChatter", "0");
        var wizard = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!.SetValue(controller, wizard);
        typeof(SetupWizardClientController).GetField("_runId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, "run");
        wizard.SetSession(new() { RunId = "run", SupportsMaintenance = true }); wizard.ShowPage("server.diagnostics"); wizard.TryOpen();
        controller.OpenHelp(); wizard.IsOpened().Should().BeFalse();
        using var handbookStandIn = new SetupWizardInvitationDialog(host.Api, null!, null!);
        handbookStandIn.TryOpen(); controller.ReturnFromHelp(); wizard.IsOpened().Should().BeFalse();
        handbookStandIn.TryClose(); controller.ReturnFromHelp();
        wizard.IsOpened().Should().BeTrue(); wizard.CurrentStepId.Should().Be("server.diagnostics");
        controller.Maintain(SetupWizardRequestKind.Reload); wizard.IsOpened().Should().BeFalse();
        var loaded = SetupWizardCaptureScenes.DefaultValues(); loaded.Single(x => x.Key == "DebugMode").Value = "1";
        controller.HandleResult(new() { Kind = partial ? SetupWizardResultKind.ReloadPartiallyApplied : SetupWizardResultKind.MaintenanceResult,
            RequestId = 1, RunId = "run", Success = success, RuntimeApplyFailed = partial,
            SupportsMaintenance = true, Values = loaded, Message = success ? "Settings reloaded from disk." : partial ? "Settings loaded. Restart the server." : "Reload failed." });
        draft.Get("DebugMode").Should().Be(success || partial ? "1" : "0");
        if (partial)
        {
            typeof(SetupWizardDialog).GetField("_runtimeApplyFailed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wizard).Should().Be(true);
            draft.GetOriginal("EnableChatter").Should().Be("1");
        }
        controller.CurrentDialog.Should().BeSameAs(wizard); wizard.IsOpened().Should().BeTrue();
        draft.IsDirty.Should().Be(!success); draft.Get("EnableChatter").Should().Be(success ? "1" : "0");
        controller.Maintain(SetupWizardRequestKind.MarkReviewed);
        controller.HandleResult(new() { Kind = SetupWizardResultKind.RequestFailed, RequestId = 2, RunId = "run", Success = false });
        wizard.IsOpened().Should().BeTrue();
        controller.Maintain(SetupWizardRequestKind.MarkReviewed);
        channel.Received().SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request => request.RequestId == 3));
        controller.HandleResult(new() { Kind = SetupWizardResultKind.Denied, RequestId = 3, RunId = "run", Success = false });
        wizard.IsOpened().Should().BeFalse();
        controller.Maintain(SetupWizardRequestKind.MarkReviewed);
        channel.DidNotReceive().SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request => request.RequestId == 4));
    }
}
