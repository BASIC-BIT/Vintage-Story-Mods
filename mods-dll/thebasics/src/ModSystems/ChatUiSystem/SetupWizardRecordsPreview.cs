using System;
using System.Globalization;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Local sample records, composed with the same native widgets as the record dialogs.</summary>
public static class SetupWizardRecordsPreview
{
    private const string Key = "preview-records";
    internal sealed record Frame(string Page, bool Enabled, bool AutoOpen, bool Slots, int Cap, int Character, bool Entries, bool Notebook);

    public static bool Supports(string pageId) => pageId is
        "characters.sheets" or "characters.slots" or "server.staffnotes" or "server.personalnotes";

    public static void Add(GuiComposer composer, ICoreClientAPI api, ElementBounds bounds, string pageId, SetupWizardDraft draft, double time,
        Func<ICoreClientAPI, string, ISetupGuidePreview> actorFactory = null, Func<bool> pinned = null)
    {
        if (Supports(pageId) && draft != null)
            composer.AddInteractiveElement(new NativeRecordsElement(api, bounds, Sample(pageId, draft, time), time, actorFactory, pinned), Key);
    }

    public static void Refresh(GuiComposer composer, string pageId, SetupWizardDraft draft, double time)
    {
        if (Supports(pageId) && draft != null && composer?.GetElement(Key) is NativeRecordsElement preview)
            preview.Update(Sample(pageId, draft, time), time);
    }

    public static bool IsReady(GuiComposer composer) => composer?.GetElement(Key) is not NativeRecordsElement preview || preview.IsReady;

    private static Frame Sample(string pageId, SetupWizardDraft draft, double time)
    {
        var slots = On(draft, "EnableRpCharacterSlots");
        var cap = int.TryParse(draft.Get("MaxRpCharacterSlots"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Max(1, value) : 3;
        var character = pageId == "characters.slots" && slots && double.IsFinite(time) && time >= 0 ? (int)(time % 8 / 4) : 0;
        var personal = pageId == "server.personalnotes";
        var enabled = pageId.StartsWith("characters.", StringComparison.Ordinal) ? On(draft, "EnableCharacterSheets", true)
            : On(draft, personal ? "EnablePlayerNotes" : "EnableAdminNotes");
        return new(pageId, enabled, On(draft, "CharacterSheetAutoOpenOnCharacterScreen", true), slots, cap, character,
            enabled && (personal || On(draft, "EnableStructuredAdminNotes")), enabled && (personal || On(draft, "EnableAdminNoteLedger")));
    }

    private static bool On(SetupWizardDraft draft, string key, bool fallback = false) =>
        ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out var enabled) ? enabled : fallback;

    // The unfocusable outer element forwards no input, so samples can keep native text contrast.
    internal sealed class NativeRecordsElement : GuiElement
    {
        private Frame _frame;
        private double _time;
        private readonly SetupWizardActorPool _actors;
        private readonly Func<bool> _pinned;
        internal GuiComposer Composer { get; private set; }
        internal bool IsReady => Composer?.GetElement("character-portrait") is not SetupWizardActorLayer layer || layer.IsReady;

        internal NativeRecordsElement(ICoreClientAPI api, ElementBounds bounds, Frame frame, double time,
            Func<ICoreClientAPI, string, ISetupGuidePreview> actorFactory, Func<bool> pinned) : base(api, bounds)
        {
            _frame = frame;
            _time = time;
            _actors = actorFactory == null ? null : new(api, actorFactory);
            _pinned = pinned ?? (() => true);
        }

        public override void ComposeElements(Context context, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            ComposeSample();
        }

        internal void Update(Frame frame, double time)
        {
            _time = time;
            if (frame == _frame) return;
            _frame = frame;
            if (Composer != null) ComposeSample();
        }

        private void ComposeSample()
        {
            Composer?.Dispose();
            Composer = null;
            var scale = Math.Max(RuntimeEnv.GUIScale, 0.1f);
            var composer = api.Gui.CreateCompo("thebasics-setup-records-sample",
                ElementBounds.Fixed(Bounds.renderX / scale, Bounds.renderY / scale, Bounds.fixedWidth, Bounds.fixedHeight));
            try
            {
                if (_frame.Page.StartsWith("characters.", StringComparison.Ordinal)) AddCharacter(composer);
                else AddNotes(composer);
                Composer = composer.Compose(focusFirstElement: false);
            }
            catch
            {
                composer.Dispose();
                throw;
            }
        }

        private void AddCharacter(GuiComposer composer)
        {
            var slotsPage = _frame.Page == "characters.slots";
            var showSheet = _frame.Enabled && (slotsPage || _frame.AutoOpen);
            var top = slotsPage ? 38d : 6;
            var bottom = Bounds.fixedHeight - 45;
            if (slotsPage)
            {
                var tabs = _frame.Slots ? new[] { new GuiTab { Name = "Pip" }, new GuiTab { Name = "Mara" } }
                    : new[] { new GuiTab { Name = "Pip" } };
                composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(5, 4, 300, 25), null,
                    Font(14), Font(14, true), "character-tabs");
                composer.GetHorizontalTabs("character-tabs").SetValue(_frame.Character, false);
            }
            else
            {
                AddAccountScreen(composer, top, bottom, showSheet);
            }

            if (showSheet)
            {
                var x = slotsPage ? 5d : 124;
                var width = slotsPage ? 300d : 181;
                Background(composer, "character-sheet-background", x, top, width, bottom - top);
                Text(composer, "Character sheet", "sheet-title", x + 10, top + 8, width - 20, 24, 15, true);
                var fieldY = top + 43;
                var fieldWidth = width - (slotsPage ? 78 : 20);
                if (slotsPage) Portrait(composer, x + width - 62, fieldY, 49, 88);
                Field(composer, "Name", _frame.Character == 0 ? "Pip" : "Mara", "sheet-name", x + 10, fieldY, fieldWidth);
                Field(composer, "Occupation", _frame.Character == 0 ? "Cartographer" : "Herbalist", "sheet-occupation", x + 10, fieldY + 49, fieldWidth);
                Field(composer, "About", _frame.Character == 0 ? "I know these roads." : "Every plant has a use.", "sheet-about", x + 10, fieldY + 98, width - 20);
            }

            if (slotsPage)
            {
                composer.AddSmallButton("New character", () => true, ElementBounds.Fixed(5, bottom + 5, 126, 27), EnumButtonStyle.Small, "new-character");
                composer.GetButton("new-character").Enabled = _frame.Enabled && _frame.Slots && _frame.Cap > 2;
            }
            var status = !_frame.Enabled ? "Character sheets are off." : slotsPage
                ? !_frame.Slots ? "One active character per account."
                    : _frame.Cap <= 2 ? "2 existing characters kept; new slots blocked." : "2 of " + _frame.Cap + " slots used by this account."
                : _frame.AutoOpen ? "Opens beside Character (C). Players can override this." : "Open the sheet manually. Players can override this.";
            Text(composer, status, "records-status", slotsPage ? 140 : 6, bottom + 6,
                slotsPage ? 164 : 298, 38, 11);
        }

        private void AddAccountScreen(GuiComposer composer, double top, double bottom, bool showSheet)
        {
            Background(composer, "character-screen-background", 5, top, showSheet ? 112 : 300, bottom - top);
            Text(composer, "Character (C)", "character-screen-title", 13, top + 9, showSheet ? 96 : 280, 22, 12, true);
            var portraitX = showSheet ? 37d : 130;
            composer.AddInset(ElementBounds.Fixed(portraitX, top + 42, 49, 92), 2);
            Portrait(composer, portraitX + 2, top + 44, 45, 88);
            Text(composer, "PipPlayer", "account-name", showSheet ? 17 : 100, top + 145, showSheet ? 90 : 100, 20, 12);
            Text(composer, "Commoner", "account-class", showSheet ? 17 : 100, top + 167, showSheet ? 90 : 100, 20, 12);
            // The character screen uses native inset slots; no live inventory or player is needed.
            for (var index = 0; index < 5; index++)
            {
                composer.AddInset(ElementBounds.Fixed(portraitX - 22, top + 41 + index * 20, 15, 15), 2);
                composer.AddInset(ElementBounds.Fixed(portraitX + 55, top + 41 + index * 20, 15, 15), 2);
            }
        }

        private void AddNotes(GuiComposer composer)
        {
            var personal = _frame.Page == "server.personalnotes";
            Background(composer, "notes-background", 5, 5, 300, Bounds.fixedHeight - 10);
            Text(composer, personal ? "Your notes about Mara" : "Staff notes about Mara", "notes-title", 14, 12, 280, 23, 15, true);
            if (!_frame.Entries && !_frame.Notebook)
            {
                Text(composer, _frame.Enabled ? "Entries and staff notebook are off." : personal ? "Personal notes are off." : "Staff notes are off.",
                    "records-status", 15, 57, 278, 70, 14);
                return;
            }

            var contentHeight = Bounds.fixedHeight - 54;
            var entryHeight = _frame.Notebook ? Math.Min(131, contentHeight * 0.62) : contentHeight;
            if (_frame.Entries)
            {
                composer.AddInset(ElementBounds.Fixed(13, 43, 102, entryHeight), 3)
                    .AddInset(ElementBounds.Fixed(122, 43, 175, entryHeight), 3);
                Text(composer, "Entries", "note-entries-label", 19, 49, 90, 18, 12, true);
                Button(composer, "10/15 North gate", "note-entry-0", 19, 73, 90, 25);
                Button(composer, "10/16 West trail", "note-entry-1", 19, 102, 90, 25);
                Text(composer, "Created 10/15 14:20", "note-meta", 129, 49, 161, 18, 10);
                var title = new GuiElementTextInput(api, ElementBounds.Fixed(129, 70, 161, 24), null, CairoFont.TextInput().WithFontSize(12));
                composer.AddInteractiveElement(title, "note-title");
                composer.OnComposed += () => title.SetValue("At the north gate");
                TextArea(composer, "Met Mara at the north gate.\nShe knows the northern roads.", "note-body", 129, 101, 161, entryHeight - 65);
            }
            if (_frame.Notebook)
            {
                var y = _frame.Entries ? 43 + entryHeight + 10 : 43;
                var height = Bounds.fixedHeight - y - 12;
                composer.AddInset(ElementBounds.Fixed(13, y, 284, height), 3);
                Text(composer, personal ? "Private Freeform Notes" : "Staff Notes", "notes-freeform-label", 20, y + 5, 270, 18, 12, true);
                TextArea(composer, "Western trail\nBring rope. Check the old bridge.\nAsk Mara about herbs.", "notes-freeform", 20, y + 26, 270, height - 34);
            }
        }

        private void Field(GuiComposer composer, string label, string value, string key, double x, double y, double width)
        {
            Text(composer, label, key + "-label", x, y, width, 19, 12, true);
            Text(composer, value, key, x, y + 19, width, 30, 13);
        }

        private void Portrait(GuiComposer composer, double x, double y, double width, double height)
        {
            if (_actors == null)
            {
                Text(composer, "Seraph omitted", "character-portrait-omitted", x, y, width, height, 10);
                return;
            }
            composer.AddInteractiveElement(new SetupWizardActorLayer(api, ElementBounds.Fixed(x, y, width, height), _actors,
                () => [new("record", _frame.Character == 0 ? "Pip" : "Mara", "idle", Math.Max(0, _time % 60), 0, 0, width, height)],
                _pinned), "character-portrait");
        }

        private void Background(GuiComposer composer, string key, double x, double y, double width, double height) =>
            composer.AddStaticElement(new GuiElementDialogBackground(api, ElementBounds.Fixed(x, y, width, height), false, 3, 0.75f), key);

        private static void Text(GuiComposer composer, string text, string key, double x, double y, double width, double height, double size, bool bold = false) =>
            composer.AddRichtext(VtmlUtils.EscapeVtml(text), Font(size, bold), ElementBounds.Fixed(x, y, width, height), key);

        private static CairoFont Font(double size, bool bold = false) => CairoFont.WhiteSmallText().WithFontSize((float)size)
            .WithWeight(bold ? FontWeight.Bold : FontWeight.Normal);

        private static void Button(GuiComposer composer, string text, string key, double x, double y, double width, double height)
        {
            var font = Font(10);
            composer.AddInteractiveElement(new GuiElementTextButton(composer.Api, text, font, font.Clone(), null,
                ElementBounds.Fixed(x, y, width, height), EnumButtonStyle.Small), key);
        }

        private void TextArea(GuiComposer composer, string value, string key, double x, double y, double width, double height)
        {
            var input = new GuiElementTextArea(api, ElementBounds.Fixed(x, y, width, Math.Max(25, height)), null, CairoFont.TextInput().WithFontSize(12))
            { Autoheight = false };
            composer.AddInteractiveElement(input, key);
            composer.OnComposed += () => input.SetValue(value, setCaretPosToEnd: false);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (Composer == null) return;
            Bounds.CalcWorldBounds();
            var scale = Math.Max(RuntimeEnv.GUIScale, 0.1f);
            var bounds = Composer.Bounds;
            var x = Bounds.renderX / scale;
            var y = Bounds.renderY / scale;
            if (bounds.fixedX != x || bounds.fixedY != y)
            {
                bounds.WithFixedPosition(x, y);
                bounds.MarkDirtyRecursive();
                bounds.CalcWorldBounds();
            }
            Composer.Render(deltaTime);
        }
        public override void Dispose()
        {
            Composer?.Dispose();
            Composer = null;
            _actors?.Dispose();
            base.Dispose();
        }
    }
}
