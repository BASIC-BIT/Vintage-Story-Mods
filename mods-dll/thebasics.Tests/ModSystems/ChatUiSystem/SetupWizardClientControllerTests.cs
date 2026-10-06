using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardClientControllerTests
{
    [Fact]
    public void CaptureOpenSendsExplicitReadOnlyQaRequest()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safeChannel = new SafeClientNetworkChannel(channel, api,
            new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);

        controller.OpenForCapture();

        channel.Received(1).SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request =>
            request.Kind == SetupWizardRequestKind.Open && request.RequestId > 0 && request.CaptureOnly));
    }

    [VisualTheory]
    [InlineData(1.0)]
    public void DeferredInvitationTracksViewedOnlyOnceItOpens(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var api = Substitute.For<ICoreClientAPI>();
        api.Gui.Returns(host.Api.Gui);
        api.Render.Returns(host.Api.Render);
        api.Input.Returns(host.Api.Input);
        api.Settings.Returns(host.Api.Settings);
        api.World.Returns(host.Api.World);
        api.Logger.Returns(host.Api.Logger);
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safeChannel = new SafeClientNetworkChannel(channel, api,
            new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);
        using var otherDialog = new SetupWizardInvitationDialog(host.Api, null!, null!);
        otherDialog.TryOpen().Should().BeTrue();
        var invitation = new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Invitation, RunId = new string('d', 32), Success = true
        };

        controller.HandleResult(invitation);

        channel.DidNotReceive().SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>());
        otherDialog.TryClose().Should().BeTrue();
        typeof(SetupWizardClientController).GetMethod("TryShowInvitation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(controller, null);
        host.Api.Gui.LoadedGuis.Should().ContainSingle(dialog =>
            dialog is SetupWizardInvitationDialog && dialog.IsOpened());
        channel.Received(1).SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request =>
            request.Kind == SetupWizardRequestKind.Track && request.RunId == invitation.RunId &&
            request.StepId == "invitation" && request.JourneyAction == "viewed" && request.ChoiceId == null));

        controller.HandleResult(invitation);

        channel.Received(1).SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>());
    }

    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReopenReauthorizesAndRefreshesCleanValuesWithoutReplacingDraftOrPendingSave(bool savePending)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var api = Substitute.For<ICoreClientAPI>();
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safeChannel = new SafeClientNetworkChannel(channel, api,
            new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);
        var draft = new SetupWizardDraft([new() { Key = "EnableChatter", Value = "1" },
            new() { Key = "TpaCooldownInGameHours", Value = "0.5" }]);
        draft.Set("EnableChatter", "0");
        var dialog = new SetupWizardDialog(host.Api, draft, "initial-run", true, null!, null!, null!, layoutOnly: true);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!
            .SetValue(controller, dialog);
        if (savePending) draft.CreateSaveRequest(9, "initial-run");

        controller.Open();

        controller.CurrentDialog.Should().BeSameAs(dialog);
        draft.Get("EnableChatter").Should().Be("0");
        if (savePending)
        {
            channel.DidNotReceive().SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>());
            draft.IsSavePending.Should().BeTrue();
            return;
        }
        channel.Received(1).SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request =>
            request.Kind == SetupWizardRequestKind.Open && request.RequestId == 1 && !request.CaptureOnly));
        controller.HandleResult(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open, RequestId = 1, RunId = "refreshed-run", Success = true,
            Values = [new() { Key = "EnableChatter", Value = "1" }, new() { Key = "TpaCooldownInGameHours", Value = "3" }]
        });
        dialog.RunId.Should().Be("refreshed-run");
        draft.Get("EnableChatter").Should().Be("0");
        draft.GetOriginal("EnableChatter").Should().Be("1");
        draft.Get("TpaCooldownInGameHours").Should().Be("3");
    }
}
