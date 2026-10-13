using FluentAssertions;
using ProtoBuf;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardProtocolTests
{
    [Fact]
    public void NewTopicSettingsRoundtripFromSnapshotThroughValidatedSaveAndDraftRefresh()
    {
        var current = new ModConfig();
        current.InitializeDefaultsIfNeeded();
        var state = new SetupWizardState(current);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var edits = new Dictionary<string, string>
        {
            ["OverheadChatBubbleMode"] = "Off",
            ["MapPlayerRenderDistance"] = "64",
            ["EnableRpCharacterSlots"] = "1",
            ["MaxRpCharacterSlots"] = "4",
            ["EnableDiceRolling"] = "0",
            ["EnableChatHistory"] = "0"
        };
        foreach (var (key, value) in edits)
        {
            ConfigAdminSettingRegistry.TryGet(key, out var setting).Should().BeTrue();
            draft.Get(key).Should().Be(setting.GetValue(current));
            draft.Set(key, value);
        }

        var request = Serializer.DeepClone(draft.CreateSaveRequest(126, "all-topics"));
        request.Values.Select(value => value.Key).Should().BeEquivalentTo(edits.Keys);
        request.OriginalValues.Select(value => value.Key).Should().BeEquivalentTo(edits.Keys);
        state.TryValidatePatch(current, request, out var saved, out var changed, out var conflicts, out var errors).Should().BeTrue();
        errors.Should().BeEmpty();
        conflicts.Should().BeEmpty();
        changed.Should().BeEquivalentTo(edits.Keys);
        foreach (var (key, value) in edits)
        {
            ConfigAdminSettingRegistry.TryGet(key, out var setting).Should().BeTrue();
            setting.GetValue(saved).Should().Be(value);
            setting.GetValue(current).Should().Be(draft.GetOriginal(key));
        }

        var result = Serializer.DeepClone(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.SaveResult,
            RequestId = request.RequestId,
            RunId = request.RunId,
            Success = true,
            Values = ConfigAdminSettingRegistry.Settings.Where(setting => SetupWizardCatalog.IsSettingKey(setting.Key))
                .Select(setting => new ConfigAdminSettingValue { Key = setting.Key, Value = setting.GetValue(saved) }).ToList(),
            RestartRequiredKeys = state.GetPendingRestartKeys(saved).ToList()
        });
        result.RestartRequiredKeys.Should().Contain("EnableRpCharacterSlots");
        draft.MergeSaveResult(result).Should().BeTrue();
        draft.IsDirty.Should().BeFalse();
        draft.IsSavePending.Should().BeFalse();
        foreach (var (key, value) in edits)
        {
            draft.Get(key).Should().Be(value);
            draft.GetOriginal(key).Should().Be(value);
        }
    }

    [Fact]
    public void RequestRoundtrip_PreservesPatchOriginalsAndJourneyFields()
    {
        var request = Serializer.DeepClone(new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = 123,
            RunId = "run-1",
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }],
            StepId = "review",
            JourneyAction = "save_requested",
            ChoiceId = "off"
        });

        request.Kind.Should().Be(SetupWizardRequestKind.Save);
        request.RequestId.Should().Be(123);
        request.RunId.Should().Be("run-1");
        request.Values.Single().Value.Should().Be("0");
        request.OriginalValues.Single().Value.Should().Be("1");
        request.StepId.Should().Be("review");
        request.JourneyAction.Should().Be("save_requested");
        request.ChoiceId.Should().Be("off");
    }

    [Fact]
    public void ResultRoundtrip_PreservesConflictAndRestartState()
    {
        var result = Serializer.DeepClone(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.SaveResult,
            RequestId = 124,
            RunId = "run-2",
            Success = false,
            Message = "Changed by another administrator.",
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            RestartRequiredKeys = ["DisableRPChat"],
            IsDedicated = true,
            RuntimeApplyFailed = true,
            ConflictKeys = ["EnableChatter"]
        });

        result.Kind.Should().Be(SetupWizardResultKind.SaveResult);
        result.RequestId.Should().Be(124);
        result.RunId.Should().Be("run-2");
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Changed by another administrator.");
        result.Values.Single().Value.Should().Be("0");
        result.RestartRequiredKeys.Should().Equal("DisableRPChat");
        result.IsDedicated.Should().BeTrue();
        result.RuntimeApplyFailed.Should().BeTrue();
        result.ConflictKeys.Should().Equal("EnableChatter");
    }

    [Fact]
    public void CaptureOpenRoundtrip_PreservesExplicitQaMode()
    {
        var request = Serializer.DeepClone(new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Open, RequestId = 125, CaptureOnly = true
        });

        request.Kind.Should().Be(SetupWizardRequestKind.Open);
        request.RequestId.Should().Be(125);
        request.CaptureOnly.Should().BeTrue();
    }
}
