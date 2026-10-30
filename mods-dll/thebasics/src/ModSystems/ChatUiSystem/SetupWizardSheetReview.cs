using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class SetupWizardSheetReview
{
    internal const string RemovalWarning = "Remove this field from the template. Existing answers will remain stored but will no longer appear on sheets.";
    internal const string RulesWarning = "Existing answers are kept. Some may need updating to meet these rules.";
    internal const string BindingWarning = "This changes where the field gets its value. Existing stored answers will not be converted.";
    internal static IReadOnlyList<(string Title, string Detail)> Rows(IReadOnlyList<CharacterSheetFieldConfigEntryMessage> before,
        IReadOnlyList<CharacterSheetFieldConfigEntryMessage> after, bool requiredGate, bool nicknamesDisabled = false)
    {
        before = SetupWizardState.NormalizeSheetDraft(before);
        after = SetupWizardState.NormalizeSheetDraft(after);
        var rows = new List<(string Title, string Detail)>();
        if (CharacterSheetFieldConfigAdmin.EntriesEqual(before, after)) return rows;
        foreach (var old in before.Where(old => !after.Any(f => f.Id == old.Id))) rows.Add((SetupWizardSheetEditor.Label(old), RemovalWarning));
        foreach (var field in after)
        {
            var old = before.FirstOrDefault(f => f.Id == field.Id);
            if (old == null) { rows.Add((SetupWizardSheetEditor.Label(field), "Added: " + Type(field.Type) + ". " + SetupWizardSheetEditor.RequiredWarning(field, requiredGate, nicknamesDisabled))); continue; }
            var changes = FieldChanges(before, after, old, field, requiredGate, nicknamesDisabled);
            if (changes.Count > 0) rows.Add((SetupWizardSheetEditor.Label(field), string.Join("\n", changes)));
        }
        return rows;
    }
    private static List<string> FieldChanges(IReadOnlyList<CharacterSheetFieldConfigEntryMessage> before,
        IReadOnlyList<CharacterSheetFieldConfigEntryMessage> after, CharacterSheetFieldConfigEntryMessage old,
        CharacterSheetFieldConfigEntryMessage field, bool requiredGate, bool nicknamesDisabled)
    {
        var changes = new List<string>();
        Change("Label", old.Label, field.Label); Change("Description", old.Description, field.Description);
        Change("Answer type", Type(old.Type), Type(field.Type));
        Change("Required", old.Optional ? "Off" : "On", field.Optional ? "Off" : "On");
        Change("Who can see it", Visibility(old.Visibility), Visibility(field.Visibility));
        Change("Nearby inspection", old.ShowInLook ? "On" : "Off", field.ShowInLook ? "On" : "Off");
        Change("Choices", old.Options, field.Options); Change("Character limit", old.MaxLength, field.MaxLength);
        Change("Placement", Placement(old.LayoutSection), Placement(field.LayoutSection));
        Change("Width", old.Width == "half" ? "Half row" : "Full row", field.Width == "half" ? "Half row" : "Full row");
        Change("Text box height", old.EditorRows, field.EditorRows); Change("Use as", Binding(old.BindTo), Binding(field.BindTo));
        var oldOrder = before.Where(f => f.LayoutSection == field.LayoutSection && after.Any(a => a.Id == f.Id)).Select(f => f.Id).ToArray();
        var newOrder = after.Where(f => f.LayoutSection == field.LayoutSection && before.Any(b => b.Id == f.Id)).Select(f => f.Id).ToArray();
        if (Array.IndexOf(oldOrder, field.Id) != Array.IndexOf(newOrder, field.Id) && old.LayoutSection == field.LayoutSection)
            changes.Add("Order in section: " + (Array.IndexOf(oldOrder, field.Id) + 1) + " > " + (Array.IndexOf(newOrder, field.Id) + 1));
        if (changes.Count == 0) return changes;
        if (old.Type != field.Type || old.Options != field.Options || Tighter(old.MaxLength, field.MaxLength)) changes.Add(RulesWarning);
        if (old.BindTo != field.BindTo) changes.Add(BindingWarning);
        if ((old.Optional != field.Optional || old.Visibility != field.Visibility) && requiredGate)
        {
            var warning = SetupWizardSheetEditor.RequiredWarning(field, true, nicknamesDisabled);
            if (warning.Length > 0) changes.Add(warning);
        }
        return changes;
        void Change(string label, string original, string current) { if ((original ?? "") != (current ?? "")) changes.Add(label + ": " + (original ?? "") + " > " + (current ?? "")); }
    }

    private static bool Tighter(string before, string after) => int.TryParse(after, out var next) && next > 0 && (!int.TryParse(before, out var previous) || previous == 0 || next < previous);
    internal static string Type(string value) => value switch { "longstring" => "Long text", "number" => "Number", "option" => "Choice", _ => "Short text" };
    private static string Visibility(string value) => value switch { "nearby" => "Nearby inspection", "self" => "Player and staff", "admin" => "Staff only", _ => "Everyone" };
    private static string Binding(string value) => value switch { "thebasics.fullName" => "Character name", "thebasics.nickname" => "Nickname", _ => "Ordinary field" };
    private static string Placement(string value) => value == "header-side" ? "Beside portrait" : "Main sheet";
}
