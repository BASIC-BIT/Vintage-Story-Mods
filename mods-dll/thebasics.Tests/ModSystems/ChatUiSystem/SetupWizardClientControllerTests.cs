using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardClientControllerTests
{
    [VisualTheory]
    [InlineData(1.0)]
    public void ReopenCompositionFailureKeepsTheDirtyDraftAndAllowsRetry(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.Returns(host.Api.Render);
        api.Settings.Returns(host.Api.Settings);
        var compositions = 0;
        api.Gui.Returns(StrictApiProxy.Create<IGuiAPI>((method, arguments) =>
        {
            if (method.Name == "CreateCompo" && ++compositions == 2)
                throw new InvalidOperationException("reopen composition failed");
            return method.Invoke(host.Api.Gui, arguments);
        }));
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safeChannel = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var changedValue = draft.Get("EnableChatter") == "1" ? "0" : "1";
        draft.Set("EnableChatter", changedValue);
        var dialog = new SetupWizardDialog(api, draft, "initial-run", true, null!, null!, null!, layoutOnly: true);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!
            .SetValue(controller, dialog);
        dialog.TryOpen();
        controller.Open();

        var failure = Record.Exception(() => controller.HandleResult(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            RequestId = 1,
            RunId = "failed-reopen",
            Success = true,
            Values = SetupWizardCaptureScenes.DefaultValues()
        }));

        failure.Should().BeNull();
        controller.CurrentDialog.Should().BeSameAs(dialog);
        dialog.Draft.Should().BeSameAs(draft);
        draft.Get("EnableChatter").Should().Be(changedValue);
        draft.IsDirty.Should().BeTrue();
        dialog.IsOpened().Should().BeFalse();
        host.Api.Gui.LoadedGuis.Should().NotContain(dialog);
        api.Received(1).ShowChatMessage("Could not reopen setup. Your edits are still here. Try /basic setup again.");

        controller.Open();
        controller.HandleResult(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            RequestId = 2,
            RunId = "retried-run",
            Success = true,
            Values = SetupWizardCaptureScenes.DefaultValues()
        });

        compositions.Should().Be(3);
        controller.CurrentDialog.Should().BeSameAs(dialog);
        draft.Get("EnableChatter").Should().Be(changedValue);
        draft.IsDirty.Should().BeTrue();
        dialog.IsOpened().Should().BeTrue();
        host.Api.Gui.LoadedGuis.Should().Contain(dialog);
    }

    [Fact]
    public void PreviewConstructionFailureDoesNotEscapeTheNetworkHandlerAndCanRetry()
    {
        var api = Substitute.For<ICoreClientAPI>();
        api.Assets.Get(Arg.Any<Vintagestory.API.Common.AssetLocation>())
            .Returns(_ => throw new InvalidOperationException("guide fixture unavailable"));
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        using var safeChannel = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);
        controller.OpenForCapture();

        var failure = Record.Exception(() => controller.HandleResult(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            RequestId = 1,
            RunId = "capture-run",
            Success = true,
            Values = SetupWizardCaptureScenes.DefaultValues()
        }));

        failure.Should().BeNull();
        controller.CurrentDialog.Should().BeNull();
        controller.IsCaptureOnly.Should().BeFalse();
        api.Received(1).ShowChatMessage("Could not open setup. Use /tb config advanced to edit your settings.");
        controller.OpenForCapture();
        channel.Received(2).SendPacket(Arg.Is<TheBasicsSetupWizardRequestMessage>(request => request.CaptureOnly));
    }

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
            Kind = SetupWizardResultKind.Invitation,
            RunId = new string('d', 32),
            Success = true
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
        var dialog = new SetupWizardDialog(host.Api, draft, "initial-run", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
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
            Kind = SetupWizardResultKind.Open,
            RequestId = 1,
            RunId = "refreshed-run",
            Success = true,
            Values = [new() { Key = "EnableChatter", Value = "1" }, new() { Key = "TpaCooldownInGameHours", Value = "3" }]
        });
        dialog.RunId.Should().Be("refreshed-run");
        draft.Get("EnableChatter").Should().Be("0");
        draft.GetOriginal("EnableChatter").Should().Be("1");
        draft.Get("TpaCooldownInGameHours").Should().Be("3");
    }

    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimedOutLanguageSaveReadsBackBeforeRetryAndCanRecoverFromFailedReadback(bool readbackTimesOut)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var api = Substitute.For<ICoreClientAPI>();
        var timeouts = new List<Action<float>>();
        api.Event.RegisterCallback(Arg.Do<Action<float>>(callback => timeouts.Add(callback)), 30000);
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        var sent = new List<TheBasicsSetupWizardRequestMessage>();
        channel.When(value => value.SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>()))
            .Do(call => sent.Add(call.Arg<TheBasicsSetupWizardRequestMessage>()));
        using var safeChannel = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safeChannel);
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config));
        var dialog = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!.SetValue(controller, dialog);
        typeof(SetupWizardClientController).GetField("_runId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(controller, "run");
        var save = typeof(SetupWizardClientController).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!;
        draft.Languages[0].Name = "Coastal";
        draft.SheetFields[0].Label = "Saved template";
        dialog.ShowPage("review");
        save.Invoke(controller, null);
        var firstSave = sent.Single(request => request.Kind == SetupWizardRequestKind.Save);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            controller.Open("chat.languageeditor");
            controller.HandleResult(new() { Kind = SetupWizardResultKind.Open, Success = true, RunId = "run", Destination = "characters.builder", Values = SetupWizardCaptureScenes.DefaultValues() });
        }
        dialog.CurrentStepId.Should().Be("characters.builder");
        draft.IsSavePending.Should().BeTrue();
        controller.CurrentDialog.Should().BeSameAs(dialog);
        sent.Count(request => request.Kind == SetupWizardRequestKind.Open).Should().Be(0);
        draft.Languages[0].Name = "Mountain";
        draft.SheetFields[0].Label = "Later template edit";
        timeouts[0](30);

        draft.HasUnconfirmedSave.Should().BeTrue();
        controller.HandleResult(new() { Kind = SetupWizardResultKind.Open, Success = true, RunId = "run", Destination = "chat.languageeditor", Values = SetupWizardCaptureScenes.DefaultValues() });
        draft.HasUnconfirmedSave.Should().BeTrue();
        dialog.ShowPage("review");
        dialog.SingleComposer.GetButton("review").Enabled.Should().BeFalse();
        save.Invoke(controller, null);
        sent.Count(request => request.Kind == SetupWizardRequestKind.Save).Should().Be(1);
        var open = sent.Last(request => request.Kind == SetupWizardRequestKind.Open);
        if (readbackTimesOut)
        {
            timeouts[1](30);
            draft.HasUnconfirmedSave.Should().BeTrue();
            dialog.SingleComposer.GetButton("review").Enabled.Should().BeFalse();
            controller.Open();
            open = sent.Last(request => request.Kind == SetupWizardRequestKind.Open);
        }
        LanguageConfigAdmin.TryApplyEntries(config, firstSave.Languages, out _).Should().BeTrue();
        CharacterSheetFieldConfigAdmin.TryApplyEntries(config, firstSave.SheetFields, out _).Should().BeTrue();

        controller.HandleResult(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            RequestId = open.RequestId,
            RunId = "run",
            Success = true,
            Values = SetupWizardCaptureScenes.DefaultValues(),
            Languages = LanguageConfigAdmin.BuildEntries(config),
            SheetFields = CharacterSheetFieldConfigAdmin.BuildEntries(config)
        });

        draft.HasUnconfirmedSave.Should().BeFalse();
        draft.Languages[0].Name.Should().Be("Mountain");
        draft.Languages[0].OriginalName.Should().Be("Coastal");
        draft.SheetFields[0].Label.Should().Be("Later template edit");
        draft.OriginalSheetFields[0].Label.Should().Be("Saved template");
        dialog.SingleComposer.GetButton("review").Enabled.Should().BeTrue();
        save.Invoke(controller, null);
        sent.Count(request => request.Kind == SetupWizardRequestKind.Save).Should().Be(2);
        sent.Last(request => request.Kind == SetupWizardRequestKind.Save).Languages[0].OriginalName.Should().Be("Coastal");
    }
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void AmbiguousLanguageReadbackRetainsDraftAndExplainsDiscardRecovery(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var api = Substitute.For<ICoreClientAPI>();
        var timeouts = new List<Action<float>>();
        api.Event.RegisterCallback(Arg.Do<Action<float>>(callback => timeouts.Add(callback)), 30000);
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        var sent = new List<TheBasicsSetupWizardRequestMessage>();
        channel.When(value => value.SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>()))
            .Do(call => sent.Add(call.Arg<TheBasicsSetupWizardRequestMessage>()));
        using var safe = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safe);
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(),
            LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config));
        var originalName = draft.Languages[0].OriginalName;
        draft.Languages[0].Name = "Coastal";
        var dialog = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!.SetValue(controller, dialog);
        typeof(SetupWizardClientController).GetField("_runId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(controller, "run");
        var save = typeof(SetupWizardClientController).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!;
        dialog.ShowPage("review");
        save.Invoke(controller, null);
        var request = sent.Single(value => value.Kind == SetupWizardRequestKind.Save);
        LanguageConfigAdmin.TryApplyEntries(config, request.Languages, out _).Should().BeTrue();
        config.Languages[0] = config.Languages[0] with { Description = "Another administrator changed this after the save." };
        draft.Languages[0].Name = "Mountain";
        timeouts[0](30);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var open = sent.Last(value => value.Kind == SetupWizardRequestKind.Open);
            controller.HandleResult(new()
            {
                Kind = SetupWizardResultKind.Open,
                RequestId = open.RequestId,
                RunId = "readback-run",
                Success = true,
                Values = SetupWizardCaptureScenes.DefaultValues(),
                Languages = LanguageConfigAdmin.BuildEntries(config),
                SheetFields = CharacterSheetFieldConfigAdmin.BuildEntries(config)
            });

            controller.CurrentDialog.Should().BeSameAs(dialog);
            draft.HasUnconfirmedSave.Should().BeTrue();
            draft.Languages[0].Name.Should().Be("Mountain");
            draft.Languages[0].OriginalName.Should().Be(originalName);
            dialog.ShowPage("review");
            dialog.SingleComposer.GetButton("review").Enabled.Should().BeFalse();
            host.RenderGuiDialog(dialog, 0);
            var message = dialog.SingleComposer.GetRichtext("setup-message");
            string.Concat(message.Components.OfType<RichTextComponent>().Select(component => component.DisplayText))
                .Should().Contain("Could not confirm").And.Contain("discard this draft").And.Contain("/basic setup");
            (message.Bounds.renderY + message.TotalHeight).Should().BeLessThanOrEqualTo(dialog.SingleComposer.GetButton("close").Bounds.renderY);
            save.Invoke(controller, null);
            sent.Count(value => value.Kind == SetupWizardRequestKind.Save).Should().Be(1);
            if (attempt == 0) controller.Open();
        }
    }

    [VisualTheory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void RepeatedTargetedCommandsDoNotConsumeDelayedSaveResult(bool success, bool navigate)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720);
        var api = Substitute.For<ICoreClientAPI>(); var channel = Substitute.For<IClientNetworkChannel>(); channel.Connected.Returns(true);
        var sent = new List<TheBasicsSetupWizardRequestMessage>();
        channel.When(x => x.SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>())).Do(call => sent.Add(call.Arg<TheBasicsSetupWizardRequestMessage>()));
        using var safe = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safe);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()); draft.Set("EnableChatter", "0");
        var dialog = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!.SetValue(controller, dialog);
        typeof(SetupWizardClientController).GetField("_runId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, "run");
        dialog.ShowPage("review");
        typeof(SetupWizardClientController).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
        var request = sent.Single(x => x.Kind == SetupWizardRequestKind.Save);
        for (var i = 0; navigate && i < 3; i++)
            controller.HandleResult(new() { Kind = SetupWizardResultKind.Open, Success = true, RunId = "run", Destination = "characters.builder", Values = SetupWizardCaptureScenes.DefaultValues() });
        draft.IsSavePending.Should().BeTrue();
        if (navigate) draft.Set("DebugMode", "1");
        controller.HandleResult(new()
        {
            Kind = SetupWizardResultKind.SaveResult,
            RequestId = request.RequestId,
            RunId = "run",
            Success = success,
            RestartRequiredKeys = success ? ["ProximityChatName"] : [],
            Message = "Settings saved.",
            Values = [new() { Key = "EnableChatter", Value = success ? "0" : "1" }]
        });
        draft.IsSavePending.Should().BeFalse(); draft.IsDirty.Should().Be(!success || navigate);
        draft.Get("EnableChatter").Should().Be("0"); controller.CurrentDialog.Should().BeSameAs(dialog);
        dialog.CurrentStepId.Should().Be(navigate ? "characters.builder" : success ? "finish" : "review");
        if (navigate && success)
        {
            string.Concat(dialog.SingleComposer.GetRichtext("setup-message").Components.OfType<RichTextComponent>().Select(c => c.DisplayText))
                .Should().Contain("Restart").And.Contain("Review changes");
            dialog.ShowPage("review");
            string.Concat(dialog.SingleComposer.GetRichtext("review-detail-0").Components.OfType<RichTextComponent>().Select(c => c.DisplayText))
                .Should().Contain("Proximity tab name");
            draft.Get("DebugMode").Should().Be("1"); draft.IsDirty.Should().BeTrue();
        }
    }

    [VisualTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveResultDuringHelpPreservesSearchAndScroll(bool success)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720);
        var api = Substitute.For<ICoreClientAPI>(); api.Gui.Returns(host.Api.Gui);
        var channel = Substitute.For<IClientNetworkChannel>(); channel.Connected.Returns(true);
        var sent = new List<TheBasicsSetupWizardRequestMessage>();
        channel.When(x => x.SendPacket(Arg.Any<TheBasicsSetupWizardRequestMessage>())).Do(call => sent.Add(call.Arg<TheBasicsSetupWizardRequestMessage>()));
        using var safe = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false });
        using var controller = new SetupWizardClientController(api, safe);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()); draft.Set("EnableChatter", "0");
        var dialog = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
        typeof(SetupWizardClientController).GetProperty(nameof(SetupWizardClientController.CurrentDialog))!.SetValue(controller, dialog);
        typeof(SetupWizardClientController).GetField("_runId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, "run");
        dialog.ShowPage("review"); dialog.TryOpen();
        typeof(SetupWizardClientController).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
        var request = sent.Single(x => x.Kind == SetupWizardRequestKind.Save);
        dialog.ShowSearch("chat"); dialog.SearchPanel.Move(dialog.SingleComposer, 9);
        var scroll = dialog.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition;
        scroll.Should().BeGreaterThan(0);
        controller.OpenHelp();
        controller.HandleResult(new()
        {
            Kind = SetupWizardResultKind.SaveResult,
            RequestId = request.RequestId,
            RunId = "run",
            Success = success,
            Values = [new() { Key = "EnableChatter", Value = success ? "0" : "1" }]
        });
        dialog.IsOpened().Should().BeFalse();
        controller.ReturnFromHelp(); dialog.IsOpened().Should().BeTrue();
        dialog.SingleComposer.GetElement("search-result-0").Should().NotBeNull();
        dialog.SearchPanel.Query.Should().Be("chat");
        dialog.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition.Should().Be(scroll);
    }

}
