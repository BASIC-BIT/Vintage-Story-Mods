using FluentAssertions;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardDraftTests
{
    [Fact]
    public void CreateSaveRequest_SendsOnlyChangedKeysAndTheirOriginalValues()
    {
        var draft = CreateDraft();
        draft.IsDirty.Should().BeFalse();
        draft.Set("EnableChatter", "0");
        draft.Set("TpaCooldownInGameHours", "2");
        draft.Set("EnableChatter", "1");

        var request = draft.CreateSaveRequest(10, "run-a");

        request.Kind.Should().Be(SetupWizardRequestKind.Save);
        request.RequestId.Should().Be(10);
        request.RunId.Should().Be("run-a");
        request.Values.Should().ContainSingle().Which.Should().BeEquivalentTo(Value("TpaCooldownInGameHours", "2"));
        request.OriginalValues.Should().ContainSingle().Which.Should().BeEquivalentTo(Value("TpaCooldownInGameHours", "0.5"));
        draft.ChangedKeys.Should().Equal("TpaCooldownInGameHours");
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("0.5");
        draft.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void DraftAndSaveRequest_DoNotShareMutableInputValues()
    {
        var source = new[] { Value("EnableChatter", "1") };
        var draft = new SetupWizardDraft(source);
        source[0].Value = "0";
        draft.Get("EnableChatter").Should().Be("1");
        draft.Set("EnableChatter", "0");
        var request = draft.CreateSaveRequest(10, "run-a");
        draft.Set("EnableChatter", "1");

        request.Values.Should().ContainSingle().Which.Value.Should().Be("0");
        request.OriginalValues.Should().ContainSingle().Which.Value.Should().Be("1");
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0.5")]
    public void SuccessfulReply_PreservesInflightEditsAndMergesUnrelatedServerChanges(string laterValue)
    {
        var draft = CreateDraft();
        draft.Set("EnableChatter", "0");
        draft.Set("TpaCooldownInGameHours", "2.500");
        draft.CreateSaveRequest(10, "run-a");
        draft.Set("TpaCooldownInGameHours", laterValue);
        draft.Set("TEXT_ServerSaveAnnouncement", "My newer message");

        draft.MergeSaveResult(Result(10, "run-a", true,
            Value("EnableChatter", "0"), Value("TpaCooldownInGameHours", "2.5"),
            Value("TEXT_ServerSaveAnnouncement", "Saving..."), Value("Teleportation.HomeWarmupSeconds", "42")))
            .Should().BeTrue();

        draft.Get("EnableChatter").Should().Be("0");
        draft.Get("TpaCooldownInGameHours").Should().Be(laterValue);
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("2.5");
        draft.Get("TEXT_ServerSaveAnnouncement").Should().Be("My newer message");
        draft.GetOriginal("TEXT_ServerSaveAnnouncement").Should().Be("Saving...");
        draft.Get("Teleportation.HomeWarmupSeconds").Should().Be("42");
        draft.ChangedKeys.Should().BeEquivalentTo(new[] { "TpaCooldownInGameHours", "TEXT_ServerSaveAnnouncement" });
        var retry = draft.CreateSaveRequest(11, "run-a");
        retry.Values.Select(value => value.Key).Should().BeEquivalentTo(draft.ChangedKeys);
        retry.OriginalValues.Single(value => value.Key == "TpaCooldownInGameHours").Value.Should().Be("2.5");
        retry.Values.Should().NotContain(value => value.Key == "Teleportation.HomeWarmupSeconds");
    }

    [Fact]
    public void SuccessfulReply_ReplacesUnchangedSubmissionWithCanonicalValueAndClearsDirtyState()
    {
        var draft = CreateDraft();
        draft.Set("TpaCooldownInGameHours", "2.500");
        draft.CreateSaveRequest(10, "run-a");

        draft.MergeSaveResult(Result(10, "run-a", true, Value("TpaCooldownInGameHours", "2.5")))
            .Should().BeTrue();

        draft.Get("TpaCooldownInGameHours").Should().Be("2.5");
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("2.5");
        draft.IsDirty.Should().BeFalse();
        draft.ChangedKeys.Should().BeEmpty();
    }

    [Theory]
    [InlineData(9, "run-a", SetupWizardResultKind.SaveResult)]
    [InlineData(10, "run-other", SetupWizardResultKind.SaveResult)]
    [InlineData(10, "run-a", SetupWizardResultKind.Open)]
    public void MergeSaveResult_RejectsUncorrelatedRepliesWithoutConsumingThePendingRequest(
        long requestId, string runId, SetupWizardResultKind kind)
    {
        var draft = CreateDraft();
        draft.Set("EnableChatter", "0");
        draft.CreateSaveRequest(10, "run-a");
        var stale = Result(requestId, runId, true, Value("EnableChatter", "1"));
        stale.Kind = kind;

        draft.MergeSaveResult(stale).Should().BeFalse();

        draft.Get("EnableChatter").Should().Be("0");
        draft.GetOriginal("EnableChatter").Should().Be("1");
        draft.MergeSaveResult(Result(10, "run-a", true, Value("EnableChatter", "0"))).Should().BeTrue();
        draft.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void CancelRequest_PreservesDraftAllowsRetryAndRejectsLateReply()
    {
        var draft = CreateDraft();
        draft.Set("TpaCooldownInGameHours", "2");
        draft.CreateSaveRequest(10, "run-a");

        draft.CancelRequest();
        draft.Set("TpaCooldownInGameHours", "3");
        var retry = draft.CreateSaveRequest(11, "run-a");

        retry.Values.Should().ContainSingle().Which.Value.Should().Be("3");
        retry.OriginalValues.Should().ContainSingle().Which.Value.Should().Be("0.5");
        draft.MergeSaveResult(Result(10, "run-a", true, Value("TpaCooldownInGameHours", "2"))).Should().BeFalse();
        draft.Get("TpaCooldownInGameHours").Should().Be("3");
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("0.5");
        draft.MergeSaveResult(Result(11, "run-a", true, Value("TpaCooldownInGameHours", "3"))).Should().BeTrue();
        draft.IsDirty.Should().BeFalse();
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0.5")]
    public void FailedConflictRefresh_RetainsDraftUpdatesExpectedOriginalAndMergesUntouchedKeysForRetry(string laterValue)
    {
        var draft = CreateDraft();
        draft.Set("TpaCooldownInGameHours", "2");
        draft.CreateSaveRequest(10, "run-a");
        draft.Set("TpaCooldownInGameHours", laterValue);
        var failure = Result(10, "run-a", false,
            Value("TpaCooldownInGameHours", "4"), Value("Teleportation.HomeWarmupSeconds", "42"));
        failure.ConflictKeys = ["TpaCooldownInGameHours"];

        draft.MergeSaveResult(failure).Should().BeTrue();

        draft.Get("TpaCooldownInGameHours").Should().Be(laterValue);
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("4");
        draft.Get("Teleportation.HomeWarmupSeconds").Should().Be("42");
        draft.IsDirty.Should().BeTrue();
        var retry = draft.CreateSaveRequest(11, "run-a");
        retry.Values.Should().ContainSingle().Which.Should().BeEquivalentTo(Value("TpaCooldownInGameHours", laterValue));
        retry.OriginalValues.Should().ContainSingle().Which.Should().BeEquivalentTo(Value("TpaCooldownInGameHours", "4"));
    }

    [Fact]
    public void FailedSaveWithoutSnapshot_RetainsOriginalAndSubmittedDraftForRetry()
    {
        var draft = CreateDraft();
        draft.Set("EnableChatter", "0");
        draft.CreateSaveRequest(10, "run-a");

        draft.MergeSaveResult(Result(10, "run-a", false)).Should().BeTrue();

        draft.Get("EnableChatter").Should().Be("0");
        draft.GetOriginal("EnableChatter").Should().Be("1");
        draft.IsDirty.Should().BeTrue();
        draft.CreateSaveRequest(11, "run-a").OriginalValues.Should().ContainSingle().Which.Value.Should().Be("1");
    }

    [Fact]
    public void RefreshValues_PreservesDirtyValueAndExpectedOriginalWhileRefreshingCleanKeys()
    {
        var draft = CreateDraft();
        draft.Set("TpaCooldownInGameHours", "2");

        draft.RefreshValues([Value("TpaCooldownInGameHours", "4"), Value("EnableChatter", "0")]);

        draft.Get("TpaCooldownInGameHours").Should().Be("2");
        draft.GetOriginal("TpaCooldownInGameHours").Should().Be("0.5");
        draft.Get("EnableChatter").Should().Be("0");
        draft.GetOriginal("EnableChatter").Should().Be("0");
        var request = draft.CreateSaveRequest(12, "run-b");
        request.Values.Should().ContainSingle().Which.Should().BeEquivalentTo(Value("TpaCooldownInGameHours", "2"));
        request.OriginalValues.Should().ContainSingle().Which.Value.Should().Be("0.5");
    }

    private static SetupWizardDraft CreateDraft() => new(new[]
    {
        Value("EnableChatter", "1"), Value("TpaCooldownInGameHours", "0.5"),
        Value("TEXT_ServerSaveAnnouncement", "Saving..."), Value("Teleportation.HomeWarmupSeconds", "5")
    });

    private static ConfigAdminSettingValue Value(string key, string value) => new() { Key = key, Value = value };

    private static TheBasicsSetupWizardResultMessage Result(long requestId, string runId, bool success,
        params ConfigAdminSettingValue[] values) => new()
    {
        Kind = SetupWizardResultKind.SaveResult,
        RequestId = requestId,
        RunId = runId,
        Success = success,
        Values = values.ToList()
    };
}
