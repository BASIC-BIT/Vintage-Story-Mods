using FluentAssertions;
using ProtoBuf;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardSheetSaveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DivergentReadbackRebasesPersistedNewFieldAndAllowsNextSave(bool laterPropertyEdit)
    {
        var current = new ModConfig();
        current.InitializeDefaultsIfNeeded();
        var state = new SetupWizardState(current);
        var draft = new SetupWizardDraft([], fields: CharacterSheetFieldConfigAdmin.BuildEntries(current));
        var added = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        added.Label = "Submitted new field";
        draft.SheetFields.Add(added);
        var request = draft.CreateSaveRequest(1, "run");
        state.TryValidatePatch(current, request, out var persisted, out _, out _, out _).Should().BeTrue();
        draft.MarkSaveTimedOut();
        persisted.CharacterSheetFields[0].Label = "Legacy changed another field";
        if (laterPropertyEdit) added.Label = "Later local label";
        draft.SheetFields.RemoveAt(1);
        draft.SheetFields.Reverse();
        var laterAdded = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        laterAdded.Label = "Added after submission";
        draft.SheetFields.Add(laterAdded);
        var localOrder = draft.SheetFields.Select(field => field.Id).ToArray();

        draft.MergeSaveReadback(new()
        {
            Kind = SetupWizardResultKind.Open,
            Success = true,
            SheetFields = CharacterSheetFieldConfigAdmin.BuildEntries(persisted)
        }).Should().BeTrue();

        added.OriginalId.Should().Be(added.Id);
        added.Label.Should().Be(laterPropertyEdit ? "Later local label" : "Submitted new field");
        draft.SheetFields.Select(field => field.Id).Should().Equal(localOrder);
        laterAdded.OriginalId.Should().BeEmpty();
        draft.HasUnconfirmedSave.Should().BeFalse();
        var nextRequest = draft.CreateSaveRequest(2, "run");
        state.TryValidatePatch(persisted, nextRequest, out var nextSaved, out _, out _, out var errors).Should().BeTrue();
        errors.Should().BeEmpty();
        nextSaved.CharacterSheetFields.Single(field => field.Id == added.Id).Label.Should().Be(added.Label);
        nextSaved.CharacterSheetFields.Select(field => field.Id).Should().Equal(localOrder);
    }

    [Theory]
    [InlineData("First,,Second")]
    [InlineData("First,first")]
    public void WizardRejectsMalformedChoiceRowsWithoutChangingLegacyNormalization(string options)
    {
        var current = new ModConfig(); current.InitializeDefaultsIfNeeded();
        var fields = CharacterSheetFieldConfigAdmin.BuildEntries(current);
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = 1,
            SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(fields),
            OriginalSheetFields = fields
        };
        request.SheetFields[0].Type = "option"; request.SheetFields[0].BindTo = ""; request.SheetFields[0].Options = options;
        new SetupWizardState(current).TryValidatePatch(current, request, out var saved, out _, out _, out var errors).Should().BeFalse();
        saved.Should().BeNull(); errors.Should().Contain(error => error.StartsWith("Choices must"));
        CharacterSheetFieldConfigAdmin.ValidateEntries(request.SheetFields, out _).Should().BeEmpty();
    }

    [Fact]
    public void FieldsRoundtripAndSaveTogetherWithScalars()
    {
        var current = new ModConfig();
        current.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft([new() { Key = "EnableChatter", Value = "1" }], fields: CharacterSheetFieldConfigAdmin.BuildEntries(current));
        draft.Set("EnableChatter", "0");
        draft.SheetFields[0].Label = "Changed";
        var request = Serializer.DeepClone(draft.CreateSaveRequest(1, "run"));
        request.SheetFields.Should().NotBeEmpty();
        new SetupWizardState(current).TryValidatePatch(current, request, out var saved, out var changed, out _, out var errors).Should().BeTrue();
        errors.Should().BeEmpty();
        changed.Should().Contain("CharacterSheetFields").And.Contain("EnableChatter");
        saved.CharacterSheetFields[0].Label.Should().Be("Changed");
        current.CharacterSheetFields[0].Label.Should().NotBe("Changed");
    }

    [Fact]
    public void AcknowledgementRebasesNewIdentityAndPreservesLaterEdit()
    {
        var entry = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        entry.Label = "First";
        var draft = new SetupWizardDraft([], fields: [entry]);
        draft.SheetFields.Add(CharacterSheetFieldConfigAdmin.CreateBuilderEntry());
        draft.SheetFields[1].Label = "New";
        var request = draft.CreateSaveRequest(1, "run");
        var saved = CharacterSheetFieldConfigAdmin.CloneEntries(request.SheetFields);
        foreach (var field in saved) field.OriginalId = field.Id;
        draft.SheetFields[1].Label = "Later";
        draft.MergeSaveResult(new() { Kind = SetupWizardResultKind.SaveResult, RequestId = 1, RunId = "run", Success = true, SheetFields = saved }).Should().BeTrue();
        draft.SheetFields[1].Label.Should().Be("Later");
        draft.SheetFields[1].OriginalId.Should().Be(draft.SheetFields[1].Id);
        draft.SheetFields[0].Should().BeEquivalentTo(saved[0]);
        draft.SheetFieldsChanged.Should().BeTrue();
    }

    [Fact]
    public void MissingSnapshotDisablesEditingButAllowsScalarSave()
    {
        var draft = new SetupWizardDraft([new() { Key = "EnableChatter", Value = "1" }]);
        draft.CanEditSheetFields.Should().BeFalse();
        draft.SheetFieldsUnavailableReason.Should().NotBeNullOrEmpty();
        draft.Set("EnableChatter", "0");
        draft.CreateSaveRequest(1, "run").SheetFields.Should().BeEmpty();
    }

    [Fact]
    public void StaleAndReboundFieldsAreRejected()
    {
        var current = new ModConfig();
        current.InitializeDefaultsIfNeeded();
        var original = CharacterSheetFieldConfigAdmin.BuildEntries(current);
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = 1,
            SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(original),
            OriginalSheetFields = original
        };
        request.SheetFields[0].OriginalId = "invented";
        request.SheetFields[0].Id = "invented";
        var state = new SetupWizardState(current);
        state.TryValidatePatch(current, request, out _, out _, out _, out _).Should().BeFalse();
        request.SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(original);
        current.CharacterSheetFields[0].Label = "Legacy edit";
        state.TryValidatePatch(current, request, out _, out _, out var conflicts, out _).Should().BeFalse();
        conflicts.Should().Contain("CharacterSheetFields");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimeoutReadbackPreservesLaterChangesAndRequiresFieldSnapshot(bool laterEdit)
    {
        var entry = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        entry.Label = "One";
        var draft = new SetupWizardDraft([], fields: [entry]);
        draft.SheetFields[0].Label = "Saved";
        var request = draft.CreateSaveRequest(5, "run");
        var saved = CharacterSheetFieldConfigAdmin.CloneEntries(request.SheetFields);
        saved[0].OriginalId = saved[0].Id;
        draft.MergeSaveResult(new() { Kind = SetupWizardResultKind.SaveResult, RequestId = 6, RunId = "run", Success = true, SheetFields = saved }).Should().BeFalse();
        draft.MarkSaveTimedOut();
        if (laterEdit) draft.SheetFields[0].Label = "Later";
        draft.MergeSaveReadback(new() { Kind = SetupWizardResultKind.Open, Success = false }).Should().BeFalse();
        draft.MergeSaveReadback(new() { Kind = SetupWizardResultKind.Open, Success = true }).Should().BeFalse();
        draft.HasUnconfirmedSave.Should().BeTrue();
        var reset = () => draft.ResetSheetFields();
        reset.Should().Throw<InvalidOperationException>();
        draft.MergeSaveReadback(new() { Kind = SetupWizardResultKind.Open, Success = true, SheetFields = saved }).Should().BeTrue();
        draft.SheetFields[0].Label.Should().Be(laterEdit ? "Later" : "Saved");
        draft.SheetFields[0].OriginalId.Should().Be(saved[0].Id);
        draft.SheetFieldsChanged.Should().Be(laterEdit);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("missing-original")]
    [InlineData("null-entry")]
    [InlineData("long")]
    [InlineData("many")]
    [InlineData("total")]
    [InlineData("delete-all")]
    [InlineData("duplicate-bind")]
    public void InvalidFieldPatchCannotApplyScalarEdits(string fault)
    {
        var current = new ModConfig();
        current.InitializeDefaultsIfNeeded();
        var original = CharacterSheetFieldConfigAdmin.BuildEntries(current);
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = 1,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }],
            SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(original),
            OriginalSheetFields = original
        };
        switch (fault)
        {
            case "null": request.SheetFields = null!; break;
            case "missing-original": request.OriginalSheetFields = []; break;
            case "null-entry": request.SheetFields[0] = null!; break;
            case "long": request.SheetFields[0].Options = new string('a', 8193); break;
            case "many": request.SheetFields = Enumerable.Range(0, 257).Select(_ => original[0] with { }).ToList(); break;
            case "total":
                request.SheetFields = Enumerable.Range(0, 128).Select(_ => original[0] with { Options = new string('a', 8192) }).ToList();
                break;
            case "delete-all": request.SheetFields = []; break;
            case "duplicate-bind": request.SheetFields[1].BindTo = request.SheetFields[0].BindTo = "thebasics.fullName"; break;
        }
        new SetupWizardState(current).TryValidatePatch(current, request, out var saved, out _, out _, out var errors).Should().BeFalse();
        saved.Should().BeNull();
        errors.Should().NotBeEmpty();
        current.EnableChatter.Should().BeTrue();
    }

    [Fact]
    public void OversizedSnapshotStaysViewableAndScalarSavesDoNotSendFields()
    {
        var fields = Enumerable.Range(0, 257).Select(_ => CharacterSheetFieldConfigAdmin.CreateBuilderEntry()).ToList();
        var draft = new SetupWizardDraft([new() { Key = "EnableChatter", Value = "1" }], fields: fields);
        draft.SheetFields.Should().HaveCount(257);
        draft.CanEditSheetFields.Should().BeFalse();
        draft.SheetFieldsUnavailableReason.Should().Contain("256");
        draft.Set("EnableChatter", "0");
        var request = draft.CreateSaveRequest(1, "run");
        request.SheetFields.Should().BeEmpty();
        request.OriginalSheetFields.Should().BeEmpty();
        request.Values.Should().ContainSingle();
    }

    [Fact]
    public void BoundsCountBothSnapshotsAndAllowExactIndividualLimit()
    {
        var entry = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        entry.Label = "One";
        entry.Options = new string('a', 8192);
        SetupWizardState.ValidateSheetSnapshots([entry], [entry]).Should().BeEmpty();
        var snapshot = Enumerable.Range(0, 64).Select(_ => entry with { }).ToList();
        SetupWizardState.ValidateSheetSnapshots(snapshot, snapshot).Should().Contain(error => error.Contains("1 MiB"));
        var fields = Enumerable.Range(0, 256).Select(_ => CharacterSheetFieldConfigAdmin.CreateBuilderEntry()).ToList();
        SetupWizardState.ValidateSheetSnapshots(fields, fields).Should().BeEmpty();
    }

    [Fact]
    public void LaterReorderRemovalAndReplacementPreserveStableIdentities()
    {
        var entries = Enumerable.Range(0, 3).Select(index => { var field = CharacterSheetFieldConfigAdmin.CreateBuilderEntry(); field.Label = $"Field {index}"; return field; }).ToList();
        var draft = new SetupWizardDraft([], fields: entries.Take(1));
        draft.SheetFields.AddRange(entries.Skip(1));
        var request = draft.CreateSaveRequest(2, "run");
        var saved = CharacterSheetFieldConfigAdmin.CloneEntries(request.SheetFields);
        foreach (var field in saved) field.OriginalId = field.Id;
        draft.SheetFields.RemoveAt(0);
        draft.SheetFields.Reverse();
        draft.SheetFields[0] = draft.SheetFields[0] with { Label = "Later replacement" };
        draft.SheetFields.Add(CharacterSheetFieldConfigAdmin.CreateBuilderEntry());
        var newId = draft.SheetFields[2].Id;
        draft.MergeSaveResult(new() { Kind = SetupWizardResultKind.SaveResult, RequestId = 2, RunId = "run", Success = true, SheetFields = saved }).Should().BeTrue();
        draft.SheetFields.Select(field => field.Id).Should().Equal(entries[2].Id, entries[1].Id, newId);
        draft.SheetFields[0].Label.Should().Be("Later replacement");
        draft.SheetFields[0].OriginalId.Should().Be(entries[2].Id);
        draft.SheetFields[1].Should().BeEquivalentTo(saved[1]);
        draft.SheetFields[2].OriginalId.Should().BeEmpty();
    }
}
