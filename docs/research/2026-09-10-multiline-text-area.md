# Multiline text area: wrap corruption, scrollbar, preview title

Date: 2026-09-10. Branch `codex/scene-marker-locking` at `cc73188`.
Primary source: decompiled 1.22.6 at `D:\bench\vs\source\vintagestory\1.22.6\decompiled` (paths below are relative to it; `API/` means `VintagestoryAPI/Vintagestory/API/Client/`). Mod paths are relative to `mods-dll/thebasics/src/`. No web sources used.

## Summary

- **A (confidence: high).** This is a vanilla bug in `GuiElementEditableTextBase.OnKeyPress`, and our code does not trigger it. When a keypress re-wraps the line, `LoadValue` swaps in the new line list but keeps the old (line, column) caret, clamped. The next statement then works out the flat caret index from that stale pair. Whenever `k` characters move to the next line, the caret ends up `k-1` characters short, and everything typed afterwards goes in at the wrong place. A line-for-line port of the decompiled code, fed widths from the game's own `libcairo-2.dll`, reproduces the owner's exact output (details under A).
- **B.** Vanilla's pattern is `BeginClip` → text area → `EndClip` → `AddVerticalScrollbar`. After `Compose()` you call `SetHeights(visible, total)`, update the total from the text-changed callback, move the text area's `Bounds.fixedY` in the scrollbar callback, and call `EnsureVisible` from `OnCursorMoved`. An always-visible scrollbar that does nothing until the text is long enough is how the stock scrollbar already behaves.
- **D.** Nothing in the code produces a stale title. The top hypothesis is that the preview hides the whole bubble in "On interaction" mode, and a focused mode dropdown switches modes when the mouse wheel is turned over it.
- **E.** Add one file, `Gui/ScrollableTextArea.cs`. It holds a `GuiElementTextArea` subclass that overrides `LoadValue` (fixes A) and an `AddScrollableTextArea` composer extension (fixes B).

## A. Root cause of the wrap corruption

### The code path (`API/GuiElementEditableTextBase.cs`)

1. `OnKeyPress` (895-928) inserts the character into the caret's line (905). If that line's width reaches the box width (906-908), it re-wraps the **whole text** with `Lineize` (910-915).
2. `LoadValue(linesStaging)` (922 → 348-361) sets `lines = newLines`. It only **clamps** `CaretPosLine`/`CaretPosInLine` against the new lines (358-359); it never translates them into the new layout. It then calls `TextChanged()` (360), which fires our `OnTextChanged` (401).
3. `CaretPosWithoutLineBreaks++` (923) runs the getter (140-150), which sums the new lines up to the stale caret line and adds the clamped column. The setter (151-172) then places the caret at getter+1.

The old caret sat at the end of the pre-wrap line (length `n`). After the wrap, that line is `n+1-k` long, so the column clamps to `n+1-k`, and the flat index comes out at `n+2-k` instead of `n+1`. Every later key inserts at the stale caret, which pushes the last `k-1` wrapped characters towards the end of the text.

### Reproducing the owner's exact string

The code measures in two different ways, and together they explain how a whole two-word fragment ended up wrapping at once:

- `Lineize` measures each word that has more text after it together with its trailing space (`text3 = " "`, `API/TextDrawUtil.cs` 215-216). It measures the last word of the text without that space.
- `OnKeyPress` measures the caret's line as it is (908). `TextExtents.Width` includes trailing spaces with the shipped Cairo: I measured `"out,"`=31, `"out, "`=33 and `"out,  "`=38 px ("sans-serif", 18 px, `D:\Games\Vintagestory\Lib\libcairo-2.dll`).

Trace for "…tuckered out, eepy boy. ":

| Key | What happens |
|---|---|
| `,` then space | The line is `…tuckered out, ` (66 chars). It can overflow, but `out,` is the last word, so `Lineize` measures it without the space and keeps it on line 0. |
| `e` | The line overflows again. `out,` is no longer the last word, so `Lineize` measures `…tuckered out, ` ≥ box and breaks before it. New lines: `["…tuckered " (61), "out, e" (6)]`, so k = 6. |
| clamp + `++` | The column clamps from 66 to 61. The getter returns 61 and the setter places the caret at 62, which is line 1, column 1 (between `o` and `ut, e`). The correct index is 67. |
| `epy boy. ` | These keys go in after `o`, producing `…tuckered oepy boy. ut, e`. |

I ported `OnKeyPress`, `LoadValue`, the caret getter/setter and `Lineize` line for line and drove them with real Cairo widths. The owner's exact output comes out at wrap widths of **512-513 px**. Other widths corrupt the text in other ways: 502 px gives `…tuckered o, eepy boy. ut` and 477 px gives `…all tut, eepy boy. uckered o`. The owner's string has 15 characters after `tuckered ` but only 14 were typed, so they presumably typed a space after "boy.". With monospace widths and wrap widths from 12 to 89, vanilla corrupted the sentence at 50-57 of the 78 widths.

### Candidates ruled out

- **(a) Our callback.** `RefreshPreview` (`SceneDescriptions/SceneDescriptionDialog.cs` 265-278) only redraws `SceneDrawingElement`s and toggles `Enabled` on number inputs and switches. Nothing in the dialog calls `SetValue`, `LoadValue` or `ReCompose` on the body per keystroke. The only `SetValue` is in `ApplyValues` (260), and that runs only at compose time. The callback does run synchronously inside `LoadValue`, before the faulty `++`, but it reads nothing from the caret.
- **(b) Autoheight or the fixed height.** `TextChanged` changes only `Bounds.fixedHeight` (`API/GuiElementTextArea.cs` 32-40). The caret math uses `lines` alone.
- **(c) Font or width mismatch.** Both measurements use the same `Font.SetupContext` (`API/CairoFont.cs` 206-245) and the same box width. `rightSpacing` stays at 0 for text areas (`GuiElementEditableTextBase` 67, 377, 906). Rendering draws the pre-split `lines` (434-444), so the drawn text and the caret always agree. The only mismatch is the trailing-space measurement described above, and all it does is change how large `k` is.
- **(d) Stale (line, column).** Confirmed; this is the cause.

### How vanilla's own editors use the element

- The sign editor (`VSSurvivalMod/.../GuiDialogTextInput.cs` 60-64), command block (`GuiDialogBlockEntityCommand.cs` 41-44), conditional block (41-46) and macro editor (`VintagestoryLib/.../GuiDialogMacroEditor.cs` 117-120) all use a plain `AddTextArea` inside a clip with a scrollbar.
- The editable book (`GuiDialogEditableBook.cs` 50, 62-66) uses a plain text area with `Autoheight=false` and page-based caret logic (113-138).
- None of them subclass the element or correct the caret. **In-game check:** a vanilla sign should show the same lag when a word is typed across the right edge.

### Two more vanilla quirks (not the reported bug)

- **Over-long words lose a character.** A word longer than a full line is hard-split by `Lineize` (`TextDrawUtil` 226-236). If that word was followed by a space, `caretPos` is rewound past a consumed space and `gotSpace` still appends one (258-262). The result is one character dropped and a space inserted (in the simulation, `Supercalifr gilistic…`). The fix below does not cover this.
- **`Autoheight` miscounts height.** It joins lines that already end in `\n` with `"\n"` (`GuiElementTextArea` 36), so every hard break counts twice. It also stores a GUI-scaled pixel height (`TextDrawUtil` 33-36, 113-116) in the unscaled `fixedHeight`, which gets scaled again (`API/ElementBounds.cs` 222). The box grows too tall whenever GUI scale ≠ 1.

## B. The vanilla scrollbar pattern

Sign editor (`GuiDialogTextInput.cs` 34-37, 60-64, 74, 98-113):

```csharp
var area = ElementBounds.Fixed(0, 0, w, h);  var clip = area.ForkBoundingParent().WithFixedPosition(0, 30);
var bar = clip.CopyOffsetedSibling(area.fixedWidth + 3).WithFixedWidth(20);
.BeginClip(clip).AddTextArea(area, OnTextAreaChanged, font, "text").EndClip()
.AddVerticalScrollbar(OnNewScrollbarvalue, bar, "scrollbar")
// after Compose():            GetScrollbar("scrollbar").SetHeights(h, h);
// OnTextAreaChanged:          GetScrollbar("scrollbar").SetNewTotalHeight((float)textArea.Bounds.fixedHeight);
// OnNewScrollbarvalue(v):     textArea.Bounds.fixedY = 3 + textareaFixedY - v; textArea.Bounds.CalcWorldBounds();
```

- **Total height.** The sign editor relies on `Autoheight` (default `true`, `GuiElementTextArea` 14) and reads back `Bounds.fixedHeight`. The macro editor reads `Bounds.OuterHeight` instead (`GuiDialogMacroEditor.cs` 139-143), which is in scaled pixels while its visible height is unscaled (126). The command block never updates the total after `Compose` (`GuiDialogBlockEntityCommand.cs` 55, 91-93).
- **Keeping the caret visible.** This exists only in the macro and command editors. Both hook `OnCursorMoved` (`GuiDialogMacroEditor.cs` 125, 132-137; `GuiDialogBlockEntityCommand.cs` 53, 95-100) and call `EnsureVisible(x, y)` and `EnsureVisible(x, y + lineHeight + 5)`. `OnCursorMoved` fires from every `SetCaretPos` with `caretY` in scaled pixels (`GuiElementEditableTextBase` 293-306).
- **Always present, inactive when short.** `SetNewTotalHeight` clamps `visible/total` to 1, so the handle fills the track (`API/GuiElementScrollbar.cs` 125-133). The wheel and drag handlers do nothing while the handle fills the track (143, 155). No extra code is needed for this.
- **Units.** `SetHeights` and `CurrentYPosition` use whatever units the caller passes. `EnsureVisible` compares against `Bounds.InnerHeight`, which is scaled (229-230), so it is only exact at GUI scale 1. The `CurrentYPosition` setter is not clamped, and `ScrollConversionFactor` returns 1 when the handle fills the track (40-47, 58-61). Writing a Y value to a bar that cannot scroll therefore still moves the content, so the target must be clamped first.
- **Clipping and input.** `BeginClip` sets `InsideClipBounds` on everything added inside it (`API/GuiElementClipHelpler.cs` 10-19; `GuiComposer.cs` 790). `IsPositionInside` honours it (`API/GuiElement.cs` 758-768), so clicks on scrolled-away text miss it. The scissor is applied in `GuiElementClip.RenderInteractiveElements` (26-36).
- **Mouse wheel routing.** A wheel event goes first to the elements under the mouse, then to **every** element in order (`GuiComposer.cs` 539-564). In a dialog with several scrollbars, the first scrollbar picks up wheel events that nothing else handled.
- **The frame is drawn once.** Interactive elements are also added to the static element list (`GuiComposer.cs` 780-781, 393-396). The text area paints its frame there once, at the bounds it has at compose time and without clipping (`GuiElementTextArea` 42-47). Text must therefore be set **after** `Compose()`. All three of our dialogs already do this.

## C. Existing mod code

- **Scene editor.** A plain `AddTextArea`, 500×260, with the default `Autoheight=true` (`SceneDescriptions/SceneDescriptionDialog.cs` 126, 148). There is no clip. Once the text grows, its bounds and texture render over the rows below (`GuiElementTextArea` 36, 72); that is issue 2. There is no scrollbar.
- **Character sheet.** Long-string fields use `ScrollClippedTextArea : GuiElementTextArea` (`ChatUiSystem/CharacterSheetDialog.cs` 912-937), which scissors to its own bounds and the parent clip. They set `Autoheight=false` and `SetMaxLines(editorRows)` (592-607). The line cap stops overflow because `LoadValue` and `OnKeyPress` reject extra lines (`GuiElementEditableTextBase` 351, 916-918). The whole sheet scrolls as one clipped `GuiElementContainer` with a single scrollbar (172-189, 743-752), and fields live inside that container. **There is no caret fix.**
- **Player notes, entry body.** A plain `GuiElementTextArea` with `Autoheight=false`, `SetMaxLines(8)` and no scrollbar (`ChatUiSystem/PlayerNotesDialog.cs` 328-335).
- **Player notes, freeform editor.** This already has the full vanilla pattern (338-357, 391-501):
  - a clip plus `ElementStdBounds.VerticalScrollbar`;
  - a hand-computed height of `lines × lineHeight / GUIScale` (398-409);
  - `SetHeights` (418);
  - `OnCaretPositionChanged` → `EnsureVisible` (463-475);
  - `fixedY = 1 - scroll` (499);
  - scroll restore after recompose (421-451).

  **There is no caret fix.** Values are deferred until after `Compose` (232-239).
- **Git history.** `git log -S"AddTextArea"` finds only `99ee9b0` (scene markers). `--grep="scroll|textarea|text area" -i` finds only `f22c50c` (#156, the bio layout). `ScrollClippedTextArea` arrived with `4d46adc` (#142) and the freeform scroll plumbing with `c4089ad` (#159). No commit mentions the caret or wrapping, so nothing has tried to fix A before.
- **Custom-element conventions** (`SceneDescriptions/SceneDrawingElement.cs` 42-51): an `internal` element class plus an `internal static` extension class. `Add…` returns the composer, does nothing if `composer.Composed`, and calls `AddInteractiveElement(element, key)`. `Get…` is a one-line cast of `GetElement(key)`.

## D. Preview title glitch, ranked hypotheses

1. **The bubble is hidden in "On interaction" mode, and the dropdown changes on the wheel.**
   - `DrawPreview` maps the `interaction` value to `OnInteraction` (`SceneDescriptionDialog.cs` 328-329). `ShouldShowDescription(true)` is false for that mode (`SceneDescriptionData.cs` 100-101), so the tile shows only the symbol.
   - A **focused** dropdown with the mouse over it changes value on the wheel and fires the callback (`API/GuiElementDropDown.cs` 314-323). The kind dropdown keeps focus after a pick, and people will use the wheel more once the body scrolls.
   - To tell it apart: was the whole bubble missing, and did the Display dropdown show "On interaction"? Repro: pick a mode, then wheel over the dropdown.
2. **The title shrinks to unreadable.**
   - The bubble is fitted into 16% of the tile height (`SceneDescriptionDialog.cs` 342-346). With "show body" on and a long body, the whole bubble, title included, scales down with every body line.
   - To tell it apart: the title was present but tiny, and "show body" was on. Repro: turn on "show body" and type a 10-line body.
3. **A render exception is swallowed.** `GenRichTextSurface` catches everything and returns `null` with no log (`ChatUiSystem/RichTextTextureUtils.cs` 166-169). The preview then draws no bubble. To tell it apart: add a warning log in that `catch`, and any hit shows up in `client-main.log`.
4. **A brief stale title during the mode-switch recompose.**
   - `Compose` redraws the preview before `Composed` becomes true (`GuiComposer.cs` 393-419). At that point `DrawPreview` falls back to `_appearance.Title`, which is never updated from the input (`SceneDescriptionDialog.cs` 323-327).
   - `ApplyValues` then redraws with an empty title (248, before 258), and again once `SetValue` fires the callback. All of this is synchronous, so it can only be seen if `ApplyValues` throws partway.
   - Separately, keys typed between the dropdown change and the deferred `Compose(pending)` are lost (304). That window is about one frame.
   - To tell it apart: log the title input text in `RefreshPreview`, then switch Targeted↔Nearby.
5. **Title text containing `&lt;`, `&gt;` or `&nbsp;`** renders as `<`, `>` or a space, because `EscapeVtml` does not escape `&` (`Utilities/VtmlUtils.cs` 36-45; `VintagestoryAPI/.../Common/VtmlParser.cs` 35, 136). Very unlikely.

**Ruled out.** The title callback does fire on paste and on programmatic `SetValue`: every edit goes through `LoadValue` → `TextChanged` → `OnTextChanged` (`GuiElementEditableTextBase` 334-361, 399-401, 826). There is also no surface cache to go stale; `DescriptionSurface` rebuilds on every redraw (`SceneMarkerVisuals.cs` 13-59).

## E. Recommended shared component

Add a new file, `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs` (namespace `thebasics.Gui`), as the first piece of the UI library. It follows the `SceneDrawingElement` conventions.

```csharp
internal class ScrollableTextArea : GuiElementTextArea
{
    internal GuiElementScrollbar Scrollbar;          // null = plain text area with the caret fix
    private readonly double _visible;                // fixed (unscaled) units

    internal ScrollableTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> onChanged, CairoFont font)
        : base(capi, bounds, null, font)
    {
        _visible = bounds.fixedHeight;
        Autoheight = false;                          // vanilla's height miscounts \n and GUI scale
        OnTextChanged = text => { UpdateScroll(); onChanged?.Invoke(text); };
        OnCursorMoved = (_, y) => KeepCaretVisible(y);
    }

    // 1.22.6 OnKeyPress re-wraps, then derives the flat caret from the pre-wrap (line, col): k-1 chars short.
    public override void LoadValue(List<string> newLines)
    {
        var caret = CaretPosWithoutLineBreaks;       // measured against the lines being replaced
        base.LoadValue(newLines);
        CaretPosWithoutLineBreaks = caret;           // re-derive (line, col) on the new wrap
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    { if (Scrollbar != null) Scrollbar.OnMouseWheel(api, args); else base.OnMouseWheel(api, args); }

    internal void UpdateScroll()
    {
        if (Scrollbar == null) return;
        Bounds.fixedHeight = Math.Max(_visible, lines.Count * Font.GetFontExtents().Height / RuntimeEnv.GUIScale + 4);
        Bounds.CalcWorldBounds();
        Scrollbar.SetHeights((float)_visible, (float)Bounds.fixedHeight);
    }

    private void KeepCaretVisible(double caretY)     // own math: EnsureVisible mixes scaled/unscaled units
    {
        if (Scrollbar == null) return;
        var top = caretY / RuntimeEnv.GUIScale; var bottom = top + Font.GetFontExtents().Height / RuntimeEnv.GUIScale + 4;
        var y = Scrollbar.CurrentYPosition;
        var target = Math.Clamp(top < y ? top : bottom > y + _visible ? bottom - _visible : y, 0, Math.Max(0, Bounds.fixedHeight - _visible));
        if (Math.Abs(target - y) > 0.5) { Scrollbar.CurrentYPosition = (float)target; Scrollbar.TriggerChanged(); }
    }

    internal void OnScroll(float value) { Bounds.fixedY = -value; Bounds.CalcWorldBounds(); }
}

internal static class ScrollableTextAreaComposerExtensions
{
    // bounds = the whole box; the text gets bounds minus the always-present bar, so wrap width never changes.
    internal static GuiComposer AddScrollableTextArea(this GuiComposer composer, ElementBounds bounds, Action<string> onChanged, CairoFont font, string key)
    {
        if (composer.Composed) return composer;
        var clip = ElementBounds.Fixed(bounds.fixedX, bounds.fixedY, bounds.fixedWidth - GuiElementScrollbar.DefaultScrollbarWidth - 3, bounds.fixedHeight);
        var area = new ScrollableTextArea(composer.Api, ElementBounds.Fixed(0, 0, clip.fixedWidth, clip.fixedHeight), onChanged, font);
        area.Scrollbar = new GuiElementScrollbar(composer.Api, area.OnScroll, ElementStdBounds.VerticalScrollbar(clip));
        composer.BeginClip(clip).AddInteractiveElement(area, key).EndClip().AddInteractiveElement(area.Scrollbar, key + "-scrollbar");
        composer.OnComposed += area.UpdateScroll;    // scrollbar bounds exist only after Compose (GuiComposer 419-424)
        return composer;
    }

    internal static ScrollableTextArea GetScrollableTextArea(this GuiComposer composer, string key) => (ScrollableTextArea)composer.GetElement(key);
}
```

`GetTextArea(key)` keeps working because it casts to the base class (`API/GuiComposerHelpers.cs` 923-926).

Why the `LoadValue` override is enough for A:
- **Typing.** `OnKeyPress` calls it before `++`, so the caret stays on the insertion point and then advances by one.
- **Backspace.** `OnKeyBackSpace` already restores the flat index itself (885-889).
- **Delete.** `OnKeyDelete` (873) now keeps the flat caret.
- **Paste.** `OnPaste` sets the flat index after `SetValue` (826-827).
- **Replacing a selection.** `DeleteSelectedText` sets `selection.Start` when the caret was at the end (476-481); otherwise the flat caret is kept.
- **`SetValue(…, true)`.** Still moves the caret to the end afterwards (336-341).

In the simulation, the override brought corruption of the owner's sentence from 50-57/78 widths down to 0/78.

What each dialog changes to adopt it:
- **`SceneDescriptions/SceneDescriptionDialog.cs:148`.** Change `.AddTextArea(` to `.AddScrollableTextArea(` with the same arguments. Nothing else changes. The wrap width becomes 477 and stays at 477. The scroll position resets on a mode recompose, which is acceptable.
- **`ChatUiSystem/CharacterSheetDialog.cs:912`.** Change `ScrollClippedTextArea : GuiElementTextArea` to `: ScrollableTextArea`. That fixes A; `Scrollbar` stays null, and the existing line cap still prevents overflow. A scrollbar per field would need a clip nested inside the `GuiElementContainer`, which I have not designed.
- **`ChatUiSystem/PlayerNotesDialog.cs:328` and `:343`.** Change `new GuiElementTextArea(` to `new ScrollableTextArea(`. That fixes A and keeps the working scroll plumbing. Optionally, the freeform block (352-356) could later move to `AddScrollableTextArea`, dropping 398-419, 463-475 and 487-501. The scroll restore (421-451) would then use `area.Scrollbar`.

Needs in-game verification (none of this can be proven from the decompile):
- the owner's sentence and a long paste at GUI scale 1.0, 1.25 and 1.5;
- the caret following the text when typing past the bottom and when pressing Up past the top;
- clicking to place the caret after scrolling (`SetCaretPos(x, y)` uses the scrolled `Bounds.absY`, 539);
- the selection highlight inside the clip (`RenderSelectionLine` 982-991);
- the wheel over the text versus over the mode dropdown;
- the vanilla sign lag, as the control case.

Unit tests are not practical: `SetCaretPos` needs `api.ElapsedMilliseconds`, and measuring needs `CairoFont.FontMeasuringContext`, which only the client sets up.

## Evidence gaps

- **The game was not run.** The reproduction is a port of the decompiled logic, driven by the shipped `libcairo-2.dll` with "sans-serif" at 18 px. In game, the face comes from `ClientSettings.DefaultFontName` (`VintagestoryLib/.../ClientProgram.cs` 161) and the owner's GUI scale is unknown, so the exact wrap threshold will differ.
- **One character is unaccounted for.** The owner presumably typed a space after "boy.", which would explain the extra character; this is not confirmed.
- **Only 1.22.6 was checked.** I did not look at 1.22.7+ to see whether `OnKeyPress` changed.
- **Decompiled, not compiled.** `CaretPosWithoutLineBreaks++` is read from the decompiled source as a get followed by a set. I did not check the IL.
- **The component is unbuilt.** It has not been compiled. The use of `lines`, `Font` and `OnCursorMoved` from a subclass rests on the member visibility in the decompile (`GuiElementEditableTextBase` 93, 114; `GuiElementTextBase` 19).
- **D was never reproduced.** Every item under D is a hypothesis.
- **The hard-split character loss is unfixed.** The over-long-word quirk (A, "Two more vanilla quirks") has no workaround here, because `Lineize(string)` is not virtual (363).
