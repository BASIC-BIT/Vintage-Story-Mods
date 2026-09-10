# Scrollable Text Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the wrap-corrupts-text bug in every multiline text box in The BASICs and give the scene marker description box an always-present scrollbar, through one shared component.

**Architecture:** A new `thebasics.Gui` namespace holds `ScrollableTextArea`, a `GuiElementTextArea` subclass. Its `LoadValue` override keeps the caret's flat character index across a re-wrap, which fixes the vanilla 1.22.6 bug. With a scrollbar attached it also clips, sizes the scroll range from the line count, and keeps the caret in view. A composer extension, `AddScrollableTextArea`, adds the clip, text area and scrollbar in one call. The scene editor adopts the extension. The character sheet and player notes adopt the class for the caret fix only.

**Tech Stack:** C# (.NET 10), Vintage Story 1.22.6 client GUI API (`GuiComposer`, `GuiElementTextArea`, `GuiElementScrollbar`), xUnit + FluentAssertions.

**Spec:** `docs/research/2026-09-10-multiline-text-area.md` (root cause in section A, scrollbar pattern in B, existing code in C, component design in E). Read sections A and E before starting.

## Global Constraints

- Work in worktree `D:\bench\vs\work\s2-scene-markers` on branch `codex/scene-marker-locking`.
- Repo files use CRLF line endings. Never use `sed -i`. Use the Edit tool, or Python that reads and writes bytes and keeps `\r\n`.
- Build against the game API in `$VINTAGE_STORY` (`D:\Games\Vintagestory`). The decompiled reference is `D:\bench\vs\source\vintagestory\1.22.6\decompiled`.
- No new NuGet packages and no new project files.
- CI gates that must pass before every push:
  - `dotnet format whitespace .\mods-dll\thebasics.Tests\thebasics.Tests.csproj --verify-no-changes` exits 0.
  - `lizard -l csharp -C 25 -L 100 -a 8 -w --whitelist whitelizard.txt ./mods-dll/thebasics/src/` exits 0 (at most 25 branches, 100 lines and 8 parameters per function).
  - `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true` passes (823 tests before this plan).
- Commit messages end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- After each commit, push both refs: `git push origin codex/scene-marker-locking:codex/scene-markers codex/scene-marker-locking`.
- Player-facing copy follows the existing lang file style. This plan adds no lang keys.

## File Structure

| File | Responsibility |
|---|---|
| Create `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs` | The element, its pure scroll math, and the composer extension. One file, because the three change together. |
| Create `mods-dll/thebasics.Tests/Gui/TextAreaScrollTests.cs` | Tests for the pure scroll math. |
| Modify `mods-dll/thebasics/src/ModSystems/SceneDescriptions/SceneDescriptionDialog.cs:148` | Swap `AddTextArea` for `AddScrollableTextArea`. |
| Modify `mods-dll/thebasics/src/ModSystems/ChatUiSystem/CharacterSheetDialog.cs:912` | `ScrollClippedTextArea` derives from `ScrollableTextArea`. |
| Modify `mods-dll/thebasics/src/ModSystems/ChatUiSystem/PlayerNotesDialog.cs:328,343` | Construct `ScrollableTextArea` instead of `GuiElementTextArea`. |
| Modify `mods-dll/thebasics/docs/RELEASE_SMOKE_TEST.md` | Smoke-test steps for wrapping and scrolling. |

---

### Task 1: Pure scroll math

**Files:**
- Create: `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs`
- Test: `mods-dll/thebasics.Tests/Gui/TextAreaScrollTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static class TextAreaScroll` in namespace `thebasics.Gui` with:
  - `internal const double Padding = 4;`
  - `internal static double ContentHeight(int lineCount, double lineHeightPixels, double guiScale, double visible)` returns the scroll range in unscaled GUI units, never below `visible`.
  - `internal static double TargetFor(double caretTop, double caretBottom, double current, double visible, double total)` returns the scroll position that keeps the caret line in view, clamped to `[0, max(0, total - visible)]`.

The test project already sees `internal` members through `[assembly: InternalsVisibleTo("VSTests")]` in `mods-dll/thebasics/Properties/AssemblyInfo.cs`.

- [ ] **Step 1: Write the failing tests**

Create `mods-dll/thebasics.Tests/Gui/TextAreaScrollTests.cs`:

```csharp
using FluentAssertions;
using thebasics.Gui;

namespace thebasics.Tests.Gui;

public class TextAreaScrollTests
{
    [Fact]
    public void ShortTextKeepsTheVisibleHeight()
    {
        TextAreaScroll.ContentHeight(3, 24, 1, 260).Should().Be(260);
    }

    [Fact]
    public void LongTextGrowsByLineHeightInUnscaledUnits()
    {
        TextAreaScroll.ContentHeight(20, 24, 1, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
        TextAreaScroll.ContentHeight(20, 48, 2, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
    }

    [Fact]
    public void ZeroGuiScaleIsTreatedAsOne()
    {
        TextAreaScroll.ContentHeight(20, 24, 0, 260).Should().Be(20 * 24 + TextAreaScroll.Padding);
    }

    [Theory]
    [InlineData(50, 74, 0, 100, 400, 0)]      // caret already visible: stay put
    [InlineData(120, 144, 100, 100, 400, 100)] // caret inside the window: stay put
    [InlineData(20, 44, 100, 100, 400, 20)]   // caret above the window: scroll up to it
    [InlineData(230, 254, 100, 100, 400, 154)] // caret below the window: scroll down to show it
    [InlineData(390, 414, 100, 100, 400, 300)] // never past the end
    [InlineData(-10, 14, 50, 100, 400, 0)]    // never above the start
    [InlineData(230, 254, 0, 100, 80, 0)]     // content shorter than the window: no scroll
    public void TargetKeepsTheCaretLineInView(double top, double bottom, double current, double visible, double total, double expected)
    {
        TextAreaScroll.TargetFor(top, bottom, current, visible, total).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true --filter FullyQualifiedName~TextAreaScrollTests`
Expected: build FAILS with `CS0246: The type or namespace name 'TextAreaScroll' could not be found` (or `CS0234` for `thebasics.Gui`).

- [ ] **Step 3: Write the minimal implementation**

Create `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs`:

```csharp
using System;

namespace thebasics.Gui;

/// <summary>Scroll arithmetic for <c>ScrollableTextArea</c>, in unscaled GUI units.</summary>
internal static class TextAreaScroll
{
    internal const double Padding = 4;

    internal static double ContentHeight(int lineCount, double lineHeightPixels, double guiScale, double visible) =>
        Math.Max(visible, lineCount * lineHeightPixels / (guiScale > 0 ? guiScale : 1) + Padding);

    internal static double TargetFor(double caretTop, double caretBottom, double current, double visible, double total)
    {
        var target = caretTop < current ? caretTop
            : caretBottom > current + visible ? caretBottom - visible
            : current;
        return Math.Clamp(target, 0, Math.Max(0, total - visible));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true --filter FullyQualifiedName~TextAreaScrollTests`
Expected: PASS, 10 tests (3 facts + 7 theory cases).

- [ ] **Step 5: Check formatting**

Run: `dotnet format whitespace .\mods-dll\thebasics.Tests\thebasics.Tests.csproj --verify-no-changes`
Expected: exit 0. If it fails, run it without `--verify-no-changes` and keep only the changes to the two new files.

- [ ] **Step 6: Commit**

```bash
git add mods-dll/thebasics/src/Gui/ScrollableTextArea.cs mods-dll/thebasics.Tests/Gui/TextAreaScrollTests.cs
git commit -m "Add scroll arithmetic for a shared scrollable text area

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: The ScrollableTextArea element and composer extension

**Files:**
- Modify: `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs` (append to the file from Task 1)

**Interfaces:**
- Consumes: `TextAreaScroll.ContentHeight` and `TextAreaScroll.TargetFor` from Task 1.
- Produces, in namespace `thebasics.Gui`:
  - `internal class ScrollableTextArea : GuiElementTextArea`. It is not `sealed`, because the character sheet subclasses it in Task 4.
    - Constructor `ScrollableTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> onChanged, CairoFont font)`, the same parameter order as `GuiElementTextArea`.
    - `internal GuiElementScrollbar Scrollbar` (null means a plain text area that only carries the caret fix).
    - `internal void UpdateScroll()`
    - `internal void OnScroll(float value)`
  - `internal static class ScrollableTextAreaComposerExtensions` with:
    - `internal static GuiComposer AddScrollableTextArea(this GuiComposer composer, ElementBounds bounds, Action<string> onChanged, CairoFont font, string key)`
    - `internal static ScrollableTextArea GetScrollableTextArea(this GuiComposer composer, string key)`
  - The scrollbar's composer key is `key + "-scrollbar"`.

**Why no unit test here:** `SetCaretPos` measures text through `CairoFont`, which needs the client's native Cairo context. CI's dependency mirror ships only the managed `cairo-sharp.dll`, so constructing this element in a test would fail in CI. Task 5 verifies it in game instead. The arithmetic it depends on is covered by Task 1.

**Why the `LoadValue` override fixes the bug** (spec section A): in 1.22.6, `GuiElementEditableTextBase.OnKeyPress` (decompile lines 895-928) inserts a character, re-wraps with `Lineize`, calls `LoadValue(linesStaging)`, then runs `CaretPosWithoutLineBreaks++`. Vanilla `LoadValue` (348-361) only clamps the old line and column to the new lines. The flat index read by the `++` is therefore short by `k - 1` when `k` characters wrap. Reading the flat index before the swap and writing it back after puts the caret on the same character in the new layout. Backspace, delete, paste and `SetValue` all stay correct (spec section E lists why).

- [ ] **Step 1: Append the element and the extension**

Add these usings at the top of `mods-dll/thebasics/src/Gui/ScrollableTextArea.cs`, keeping `using System;`:

```csharp
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
```

Append below `TextAreaScroll`:

```csharp
/// <summary>
/// A multiline text area that keeps its caret on the right character when a keypress re-wraps the text,
/// and, with a <see cref="Scrollbar"/>, scrolls inside a fixed-height clip and follows the caret.
/// </summary>
internal class ScrollableTextArea : GuiElementTextArea
{
    /// <summary>Null for a plain text area that only carries the caret fix.</summary>
    internal GuiElementScrollbar Scrollbar;

    private readonly double _visible;

    internal ScrollableTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> onChanged, CairoFont font)
        : base(capi, bounds, null, font)
    {
        _visible = bounds.fixedHeight;
        // Vanilla autoheight counts hard line breaks twice and mixes scaled with unscaled units.
        Autoheight = false;
        OnTextChanged = text =>
        {
            UpdateScroll();
            onChanged?.Invoke(text);
        };
        OnCursorMoved = (_, caretY) => KeepCaretVisible(caretY);
    }

    // 1.22.6 OnKeyPress re-wraps, then advances the caret from its pre-wrap line and column, which lands
    // k-1 characters short when k characters wrap. Carrying the flat index across the swap fixes it.
    public override void LoadValue(List<string> newLines)
    {
        var caret = CaretPosWithoutLineBreaks;
        base.LoadValue(newLines);
        CaretPosWithoutLineBreaks = caret;
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (Scrollbar != null) Scrollbar.OnMouseWheel(api, args);
        else base.OnMouseWheel(api, args);
    }

    internal void UpdateScroll()
    {
        if (Scrollbar == null) return;
        Bounds.fixedHeight = TextAreaScroll.ContentHeight(lines.Count, Font.GetFontExtents().Height, RuntimeEnv.GUIScale, _visible);
        Bounds.CalcWorldBounds();
        Scrollbar.SetHeights((float)_visible, (float)Bounds.fixedHeight);
    }

    internal void OnScroll(float value)
    {
        Bounds.fixedY = -value;
        Bounds.CalcWorldBounds();
    }

    // caretY arrives in scaled pixels (GuiElementEditableTextBase.SetCaretPos). The vanilla EnsureVisible
    // compares it against a scaled height, so it is only right at GUI scale 1; this does its own math.
    private void KeepCaretVisible(double caretY)
    {
        if (Scrollbar == null) return;
        var scale = RuntimeEnv.GUIScale > 0 ? RuntimeEnv.GUIScale : 1;
        var top = caretY / scale;
        var bottom = top + Font.GetFontExtents().Height / scale + TextAreaScroll.Padding;
        var current = Scrollbar.CurrentYPosition;
        var target = TextAreaScroll.TargetFor(top, bottom, current, _visible, Bounds.fixedHeight);
        if (Math.Abs(target - current) <= 0.5) return;
        Scrollbar.CurrentYPosition = (float)target;
        Scrollbar.TriggerChanged();
    }
}

internal static class ScrollableTextAreaComposerExtensions
{
    /// <summary>
    /// Adds a clipped text area with an always-present scrollbar. <paramref name="bounds"/> is the whole box;
    /// the text gets the box minus the bar, so its wrap width never changes when the bar becomes useful.
    /// </summary>
    internal static GuiComposer AddScrollableTextArea(this GuiComposer composer, ElementBounds bounds, Action<string> onChanged, CairoFont font, string key)
    {
        if (composer.Composed) return composer;
        var clip = ElementBounds.Fixed(bounds.fixedX, bounds.fixedY, bounds.fixedWidth - GuiElementScrollbar.DefaultScrollbarWidth - 3, bounds.fixedHeight);
        var area = new ScrollableTextArea(composer.Api, ElementBounds.Fixed(0, 0, clip.fixedWidth, clip.fixedHeight), onChanged, font);
        area.Scrollbar = new GuiElementScrollbar(composer.Api, area.OnScroll, ElementStdBounds.VerticalScrollbar(clip));
        composer.BeginClip(clip).AddInteractiveElement(area, key).EndClip().AddInteractiveElement(area.Scrollbar, key + "-scrollbar");
        // The scrollbar only has world bounds once the composer has composed.
        composer.OnComposed += area.UpdateScroll;
        return composer;
    }

    internal static ScrollableTextArea GetScrollableTextArea(this GuiComposer composer, string key) =>
        (ScrollableTextArea)composer.GetElement(key);
}
```

API facts this relies on, all from the 1.22.6 decompile under `VintagestoryAPI/Vintagestory/API/Client/`:
- `GuiElementEditableTextBase`: `protected List<string> lines` (114), `public Action<double, double> OnCursorMoved` (93), `public virtual void LoadValue(List<string>)` (348), `CaretPosWithoutLineBreaks` get and set (140-172).
- `GuiElementTextArea`: `public bool Autoheight` (14).
- `GuiElementScrollbar`: `DefaultScrollbarWidth` (9), constructor `(ICoreClientAPI, Action<float>, ElementBounds)` (70), `CurrentYPosition` (52), `SetHeights` (115), `OnMouseWheel` (141), `TriggerChanged` (203).
- `GuiComposer`: `public ICoreClientAPI Api` (56), `event Action OnComposed` (162, raised at 424), `AddInteractiveElement` (768).
- `ElementStdBounds.VerticalScrollbar(ElementBounds)` (153) places the bar at `clip.x + clip.width + 3`, so the bar ends on the box's right edge.
- The clip-plus-`Fixed(0, 0, ...)` child shape is the one `PlayerNotesDialog.AddFreeformEditor` already uses successfully (`PlayerNotesDialog.cs:338-357`).

- [ ] **Step 2: Build**

Run: `dotnet build .\mods-dll\thebasics\thebasics.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: `0 Error(s)`. If `lines` or `Font` is reported inaccessible, stop and report; the spec's evidence gaps flag member visibility as unverified by compilation.

- [ ] **Step 3: Run the complexity gate**

Run: `lizard -l csharp -C 25 -L 100 -a 8 -w --whitelist whitelizard.txt ./mods-dll/thebasics/src/`
Expected: exit 0, no output.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: PASS, 833 tests (823 existing + 10 from Task 1).

- [ ] **Step 5: Commit**

```bash
git add mods-dll/thebasics/src/Gui/ScrollableTextArea.cs
git commit -m "Add a scrollable text area that survives line wraps

Vintage Story 1.22.6 advances the caret from its pre-wrap line and
column after a keypress re-wraps a text area, so typing across the
right edge inserts characters in the wrong place. The new element
carries the caret's flat index across the re-wrap, and can clip,
scroll and follow the caret with an always-present scrollbar.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Scene editor description box

**Files:**
- Modify: `mods-dll/thebasics/src/ModSystems/SceneDescriptions/SceneDescriptionDialog.cs:148` and its usings.

**Interfaces:**
- Consumes: `AddScrollableTextArea(ElementBounds, Action<string>, CairoFont, string)` from Task 2.
- Produces: nothing new. The `"body"` key and every `GetTextArea("body")` call (lines 253, 259, 260, 262, 294, 327, 505) keep working, because `GuiComposerHelpers.GetTextArea` casts to the base class.

- [ ] **Step 1: Swap the call**

In `SceneDescriptionDialog.cs`, replace:

```csharp
            .AddTextArea(textAreaBounds, _ => RefreshPreview(), CairoFont.TextInput(), "body")
```

with:

```csharp
            .AddScrollableTextArea(textAreaBounds, _ => RefreshPreview(), CairoFont.TextInput(), "body")
```

Add `using thebasics.Gui;` to the file's usings, in alphabetical position among the `thebasics.*` usings.

- [ ] **Step 2: Build and test**

Run: `dotnet build .\mods-dll\thebasics\thebasics.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: `0 Error(s)`.

Run: `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: PASS, 833 tests.

- [ ] **Step 3: Commit**

```bash
git add mods-dll/thebasics/src/ModSystems/SceneDescriptions/SceneDescriptionDialog.cs
git commit -m "Scroll the scene description box and keep typing in place

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Character sheet and player notes get the caret fix

**Files:**
- Modify: `mods-dll/thebasics/src/ModSystems/ChatUiSystem/CharacterSheetDialog.cs:912-916` and its usings.
- Modify: `mods-dll/thebasics/src/ModSystems/ChatUiSystem/PlayerNotesDialog.cs:328` and `:343` and its usings.

**Interfaces:**
- Consumes: `ScrollableTextArea(ICoreClientAPI, ElementBounds, Action<string>, CairoFont)` from Task 2, with `Scrollbar` left null.
- Produces: nothing new. Both dialogs keep their own scrolling: the character sheet scrolls the whole sheet in one container, and the notes freeform editor keeps its hand-built clip, scrollbar and `OnCaretPositionChanged` handler.

With `Scrollbar` null, `ScrollableTextArea` behaves like `GuiElementTextArea` except for the `LoadValue` fix and `Autoheight = false`. All three affected text areas already set `Autoheight = false` (`CharacterSheetDialog.cs:592-607`, `PlayerNotesDialog.cs:328-331`, `:343-346`). Neither dialog assigns `OnCursorMoved`, so the constructor's handler does not overwrite anything. Check that with the grep in Step 1.

- [ ] **Step 1: Confirm nothing else assigns the hooks the constructor sets**

Run: `grep -n "OnCursorMoved\|\.OnTextChanged =" mods-dll/thebasics/src/ModSystems/ChatUiSystem/CharacterSheetDialog.cs mods-dll/thebasics/src/ModSystems/ChatUiSystem/PlayerNotesDialog.cs`
Expected: no output. If either file assigns `OnCursorMoved` or `OnTextChanged` after construction, stop and report, because that would replace the wrapper the constructor installs.

- [ ] **Step 2: Character sheet**

In `CharacterSheetDialog.cs`, replace:

```csharp
    private sealed class ScrollClippedTextArea : GuiElementTextArea
```

with:

```csharp
    private sealed class ScrollClippedTextArea : ScrollableTextArea
```

Add `using thebasics.Gui;` to the file's usings. The existing constructor already calls `base(capi, bounds, onTextChanged, font)` with the matching parameter order.

- [ ] **Step 3: Player notes**

In `PlayerNotesDialog.cs`, replace:

```csharp
        _bodyInput = new GuiElementTextArea(capi, ElementBounds.Fixed(x + 10, y + 110, width - 20, EntryBodyHeight), null, CairoFont.TextInput())
```

with:

```csharp
        _bodyInput = new ScrollableTextArea(capi, ElementBounds.Fixed(x + 10, y + 110, width - 20, EntryBodyHeight), null, CairoFont.TextInput())
```

and replace:

```csharp
        _freeformInput = new GuiElementTextArea(capi, ElementBounds.Fixed(0, 0, clipBounds.fixedWidth - 6, _freeformScrollViewportHeight), OnFreeformTextChanged, CairoFont.TextInput())
```

with:

```csharp
        _freeformInput = new ScrollableTextArea(capi, ElementBounds.Fixed(0, 0, clipBounds.fixedWidth - 6, _freeformScrollViewportHeight), OnFreeformTextChanged, CairoFont.TextInput())
```

Leave the `{ Autoheight = false }` initializers in place; they are now redundant but harmless. Leave the `_bodyInput` and `_freeformInput` field types as `GuiElementTextArea`. Add `using thebasics.Gui;`.

- [ ] **Step 4: Build, test, gates**

Run: `dotnet build .\mods-dll\thebasics\thebasics.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: `0 Error(s)`.

Run: `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true`
Expected: PASS, 833 tests.

Run: `lizard -l csharp -C 25 -L 100 -a 8 -w --whitelist whitelizard.txt ./mods-dll/thebasics/src/`
Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add mods-dll/thebasics/src/ModSystems/ChatUiSystem/CharacterSheetDialog.cs mods-dll/thebasics/src/ModSystems/ChatUiSystem/PlayerNotesDialog.cs
git commit -m "Keep typing in place in character sheet and note text boxes

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Smoke-test steps, deploy, and in-game verification

**Files:**
- Modify: `mods-dll/thebasics/docs/RELEASE_SMOKE_TEST.md`

**Interfaces:**
- Consumes: the built mod from Tasks 1-4.
- Produces: a QA package on the test server and both local profiles, and a QA card for the product owner.

- [ ] **Step 1: Add the smoke-test steps**

In `RELEASE_SMOKE_TEST.md`, the scene marker card numbers its items `1.` to `13.` with a three-space indent (item 6 is `6. **Read Versus Edit** (P0)` at about line 97). Insert this as the new item 7, directly after item 6, then renumber the old items 7 to 13 as 8 to 14:

```markdown
   7. **Long Description Typing And Scrolling** (P1)
      - Do: Shift-right-click a marker. In the description box, type "He is calmly snoozing, probably snoring. He got all tuckered out, eepy boy." so it wraps onto a second line, then keep typing until the text passes the bottom of the box. Press Backspace across a wrap, paste a long paragraph, press Up until the caret reaches the top, and scroll with the mouse wheel over the box. Repeat once at GUI scale 1.25 or 1.5.
      - Expect: The text reads exactly as typed at every wrap. The scrollbar is always shown beside the box and only moves once the text is taller than the box. The caret stays in view while typing, moving with arrow keys, and deleting. Clicking a scrolled line places the caret on it.
      - Watch for: characters landing out of order after a wrap, text drawn over the rows below the box, a caret that disappears below the box, or the box width changing when the scrollbar becomes useful.
```

The smoke test has no character sheet or player notes card, so those dialogs are covered by the QA card in Step 5 only.

- [ ] **Step 2: Run every CI gate**

```bash
dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true
dotnet format whitespace .\mods-dll\thebasics.Tests\thebasics.Tests.csproj --verify-no-changes
lizard -l csharp -C 25 -L 100 -a 8 -w --whitelist whitelizard.txt ./mods-dll/thebasics/src/
.\scripts\check-agent-tooling.ps1
```

Expected: 833 tests pass, format exits 0, lizard exits 0, `Agent tooling check passed.`

- [ ] **Step 3: Commit and push**

```bash
git add mods-dll/thebasics/docs/RELEASE_SMOKE_TEST.md
git commit -m "Add smoke-test steps for long descriptions

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push origin codex/scene-marker-locking:codex/scene-markers codex/scene-marker-locking
```

- [ ] **Step 4: Package and deploy for QA**

Run under Windows PowerShell 5.1, not pwsh 7, because WinSCP only loads there:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\mods-dll\thebasics\scripts\build-and-package.ps1
```

Expected: `0 Error(s)`, `Successfully uploaded mod to SFTP server`, and copies to `D:\Games\VSProfiles\Profile2\Mods` and `Profile3\Mods`.

Restart the test server with the client token from the worktree `.env` (`PTERO_TOKEN`, the `ptlc_` key; the shell's own `PTERO_TOKEN` is an application key and returns 403). Never print the token:

```bash
TOKEN=$(grep -E '^PTERO_TOKEN=' .env | cut -d= -f2- | tr -d '\r"')
curl -s -o /dev/null -w "%{http_code}\n" -X POST "https://pt.basicbit.net/api/client/servers/8982de16/power" -H "Authorization: Bearer $TOKEN" -H "Accept: application/json" -H "Content-Type: application/json" -d '{"signal":"restart"}'
```

Expected: `204`. Poll `GET https://pt.basicbit.net/api/client/servers/8982de16/resources` every 5 seconds until the body contains `"current_state":"running"`.

Fetch logs and check the latest boot reached `RunGame` with no mod errors:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\mods-dll\thebasics\scripts\fetch-logs.ps1 -LogType all
```

Relaunch both clients:

```powershell
Get-Process Vintagestory -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile2" --fullscreen off -c 15.235.75.126:30000'
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile3" --fullscreen off'
```

- [ ] **Step 5: Record the package and hand over the QA card**

Append one line to `docs/agent-context/2026-09-10-scene-read-marks.md` with the commit, the package SHA256 prefix, the restart time, and "RunGame reached, no mod errors". Commit and push it.

The QA card for the product owner is the new smoke-test item from Step 1, plus these control and regression checks:
1. Type the same sentence on a vanilla sign. It should show the same corruption, which confirms the bug is vanilla and the fix is ours.
2. Character sheet: type across the right edge of a long field. The text reads as typed. The whole sheet still scrolls with the wheel.
3. Player notes: type across the right edge in an entry body and in the freeform editor. The text reads as typed. The freeform editor still scrolls and follows the caret.

---

## Out of Scope

- The rarer vanilla bug where a single word longer than the box loses one character when it is force-split. `Lineize(string)` is not virtual in 1.22.6 (spec section A, "Two more vanilla quirks"), so this plan cannot reach it.
- Moving the notes freeform editor onto `AddScrollableTextArea`. It already scrolls correctly; migrating it would delete about 60 lines but is a separate cleanup.
- The preview title glitch. Nothing in the code produces it (spec section D). If it recurs, check whether the Display dropdown shows "On interaction".
- The inspector description toggle, which is waiting on the product owner's choice.
