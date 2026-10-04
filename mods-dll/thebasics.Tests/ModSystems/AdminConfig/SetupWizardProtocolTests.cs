using FluentAssertions;
using ProtoBuf;
using thebasics.Models;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardProtocolTests
{
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
        result.ConflictKeys.Should().Equal("EnableChatter");
    }
}
