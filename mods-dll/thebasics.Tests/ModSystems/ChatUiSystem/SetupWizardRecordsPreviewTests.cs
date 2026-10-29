using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardRecordsPreviewTests
{
    [VisualTheory]
    [InlineData(1d, 270d)]
    [InlineData(1.25d, 310d)]
    public void CharacterScreenKeepsItsNativePanelAndOnlyAutoOpensTheAttachedSheet(double scale, double height)
    {
        using var host = CreateHost(scale);
        var draft = Draft();
        draft.Set("EnableCharacterSheets", "1");
        draft.Set("CharacterSheetAutoOpenOnCharacterScreen", "1");
        using var composer = Compose(host, draft, "characters.sheets", height);
        var preview = Preview(composer);
        var native = preview.Composer;
        Assert.IsType<GuiElementDialogBackground>(native["character-screen-background"]);
        Assert.Equal("Character (C)", DisplayedText(native.GetRichtext("character-screen-title")));
        Assert.Equal("Pip", DisplayedText(native.GetRichtext("sheet-name")));
        Assert.Equal("Cartographer", DisplayedText(native.GetRichtext("sheet-occupation")));
        Assert.IsType<SetupWizardActorLayer>(native["character-portrait"]);
        AssertFits(preview, native);

        draft.Set("CharacterSheetAutoOpenOnCharacterScreen", "0");
        SetupWizardRecordsPreview.Refresh(composer, "characters.sheets", draft, 0);
        Assert.Same(preview, Preview(composer));
        Assert.NotNull(preview.Composer["character-screen-background"]);
        Assert.Null(preview.Composer.GetElement("sheet-name"));
        Assert.Contains("manually", DisplayedText(preview.Composer.GetRichtext("records-status")));
        draft.Set("EnableCharacterSheets", "0");
        SetupWizardRecordsPreview.Refresh(composer, "characters.sheets", draft, 0);
        Assert.NotNull(preview.Composer["character-screen-background"]);
        Assert.Null(preview.Composer.GetElement("sheet-name"));
        Assert.Contains("off", DisplayedText(preview.Composer.GetRichtext("records-status")));
        AssertNoEffects(host);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void CharacterSlotsSwitchDistinctNativeSheetsAndKeepExistingCharactersWhenTheCapDrops(double scale)
    {
        using var host = CreateHost(scale);
        var draft = Draft();
        draft.Set("EnableCharacterSheets", "1");
        draft.Set("EnableRpCharacterSlots", "1");
        draft.Set("MaxRpCharacterSlots", "3");
        using var composer = Compose(host, draft, "characters.slots", 270);
        var preview = Preview(composer);
        Assert.Equal(new[] { "Pip", "Mara" }, preview.Composer.GetHorizontalTabs("character-tabs").tabs.Select(tab => tab.Name));
        Assert.Equal("Pip", DisplayedText(preview.Composer.GetRichtext("sheet-name")));
        Assert.True(preview.Composer.GetButton("new-character").Enabled);

        SetupWizardRecordsPreview.Refresh(composer, "characters.slots", draft, 4);
        Assert.Equal("Mara", DisplayedText(preview.Composer.GetRichtext("sheet-name")));
        Assert.Equal("Herbalist", DisplayedText(preview.Composer.GetRichtext("sheet-occupation")));
        Assert.Equal(1, preview.Composer.GetHorizontalTabs("character-tabs").activeElement);
        draft.Set("MaxRpCharacterSlots", "1");
        SetupWizardRecordsPreview.Refresh(composer, "characters.slots", draft, 4);
        Assert.Equal(2, preview.Composer.GetHorizontalTabs("character-tabs").tabs.Length);
        Assert.Equal("Mara", DisplayedText(preview.Composer.GetRichtext("sheet-name")));
        Assert.False(preview.Composer.GetButton("new-character").Enabled);
        Assert.Contains("kept", DisplayedText(preview.Composer.GetRichtext("records-status")));
        draft.Set("EnableRpCharacterSlots", "0");
        SetupWizardRecordsPreview.Refresh(composer, "characters.slots", draft, 4);
        Assert.Equal("Pip", Assert.Single(preview.Composer.GetHorizontalTabs("character-tabs").tabs).Name);
        Assert.False(preview.Composer.GetButton("new-character").Enabled);
        AssertNoEffects(host);
    }

    [VisualTheory]
    [InlineData("server.staffnotes", 1d)]
    [InlineData("server.personalnotes", 1.25d)]
    public void NotesUseNativeTimestampedEntriesAndASeparateFreeformDocument(string page, double scale)
    {
        using var host = CreateHost(scale);
        var draft = Draft();
        draft.Set("EnableAdminNotes", "1");
        draft.Set("EnableStructuredAdminNotes", "1");
        draft.Set("EnableAdminNoteLedger", "1");
        draft.Set("EnablePlayerNotes", "1");
        var original = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        using var composer = Compose(host, draft, page, 270);
        var preview = Preview(composer);
        var native = preview.Composer;
        Assert.Contains("10/15", native.GetButton("note-entry-0").Text);
        Assert.Contains("10/16", native.GetButton("note-entry-1").Text);
        Assert.True(native.GetButton("note-entry-0").Enabled);
        Assert.True(native.GetButton("note-entry-1").Enabled);
        Assert.Contains("Created", DisplayedText(native.GetRichtext("note-meta")));
        Assert.IsType<GuiElementTextInput>(native["note-title"]);
        Assert.IsAssignableFrom<GuiElementTextArea>(native["note-body"]);
        Assert.IsAssignableFrom<GuiElementTextArea>(native["notes-freeform"]);
        Assert.Contains("north gate", native.GetTextArea("note-body").GetText());
        Assert.Contains("Western trail", native.GetTextArea("notes-freeform").GetText());
        Assert.False(preview.Focusable);
        foreach (var key in new[] { "note-title", "note-body", "notes-freeform" })
        {
            var input = Assert.IsAssignableFrom<GuiElementEditableTextBase>(native[key]);
            Assert.True(input.Enabled);
            Assert.InRange(input.Font.Color[3], 0.9, 1);
            var before = input.GetText();
            var x = (int)(input.Bounds.renderX + input.Bounds.OuterWidth / 2);
            var y = (int)(input.Bounds.renderY + input.Bounds.OuterHeight / 2);
            preview.OnMouseDown(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            preview.OnMouseUp(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            preview.OnMouseMove(host.Api, new MouseEvent(x, y));
            preview.OnKeyPress(host.Api, new KeyEvent { KeyChar = 'x' });
            Assert.False(input.HasFocus);
            Assert.Equal(before, input.GetText());
            Assert.NotEqual("textselect", preview.MouseOverCursor);
        }
        AssertFits(preview, native);
        Assert.Equal(original, draft.Values.Select(value => (value.Key, value.Value)).ToArray());

        if (page == "server.staffnotes")
        {
            draft.Set("EnableStructuredAdminNotes", "0");
            SetupWizardRecordsPreview.Refresh(composer, page, draft, 0);
            Assert.Null(preview.Composer.GetElement("note-title"));
            Assert.NotNull(preview.Composer.GetElement("notes-freeform"));
            draft.Set("EnableAdminNoteLedger", "0");
            SetupWizardRecordsPreview.Refresh(composer, page, draft, 0);
            Assert.Null(preview.Composer.GetElement("notes-freeform"));
        }
        AssertNoEffects(host);
    }

    private static GuiComposer Compose(PreviewHost host, SetupWizardDraft draft, string page, double height)
    {
        var composer = host.Api.Gui.CreateCompo("records-preview-test", ElementBounds.Fixed(40, 40, 310, height));
        SetupWizardRecordsPreview.Add(composer, host.Api, ElementBounds.Fixed(0, 0, 310, height), page, draft, 0, host.CreateGuide);
        composer.Compose(focusFirstElement: false);
        composer.Render(0);
        return composer;
    }

    private static SetupWizardRecordsPreview.NativeRecordsElement Preview(GuiComposer composer) =>
        Assert.IsType<SetupWizardRecordsPreview.NativeRecordsElement>(composer["preview-records"]);

    private static void AssertFits(SetupWizardRecordsPreview.NativeRecordsElement preview, GuiComposer native)
    {
        Assert.InRange(native.Bounds.renderX, preview.Bounds.renderX - 1, preview.Bounds.renderX + 1);
        Assert.InRange(native.Bounds.renderY, preview.Bounds.renderY - 1, preview.Bounds.renderY + 1);
        Assert.InRange(native.Bounds.OuterWidth, 1, preview.Bounds.OuterWidth + 1);
        Assert.InRange(native.Bounds.OuterHeight, 1, preview.Bounds.OuterHeight + 1);
    }

    private static void AssertNoEffects(PreviewHost host)
    {
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("Send", StringComparison.Ordinal));
        Assert.Empty(host.Api.Gui.LoadedGuis);
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues());
    private static string DisplayedText(GuiElementRichtext text) =>
        string.Join(" ", text.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));
    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
}
