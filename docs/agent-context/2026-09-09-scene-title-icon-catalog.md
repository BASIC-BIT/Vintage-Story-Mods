# Scene title icon catalog correction

User correction: title icons must include all available VTML icons, with a larger icon beside the title like the typing indicator, rather than only the six billboard symbols.

- Catalog includes the installed 1.22.7 built-in DrawIcon names, runtime CustomIcons, and loaded texture SVG assets from every domain. The built-in icon named none is a slashed-circle graphic, distinct from the None button that clears selection.
- Picker has 32 artwork tiles per page, name tooltips, a search field with Search button, pagination and None.
- Bubble reserves a left column for a 40-pixel icon beside 24-pixel text. Wrapping cannot move the icon above the title. Text uses left alignment within its column. Bubble scaling affects both together. Untitled bubbles have no title icon.
- Preview and world use the same Cairo surface compositor. Chat rendering keeps its existing default centering.
- TitleIconName uses new proto member 22 and sceneTitleIconName tree storage. Old member 21 is reserved and legacy six-symbol selections migrate by name. Save, pickup and appearance defaults preserve names. Names are not inserted into VTML.
- Parent cancellation, lock controls, and no-description defaults remain as before.

Validation: 799 tests passed before review correction adding the built-in none icon. Human visual acceptance pending.

QA: Search for wpHome, try a registered custom icon and an SVG icon, then page through the catalog. Confirm all tiles are clickable and hover names match. Compare short and wrapped titles with description on/off: icon stays left of title and does not overlap text. Try bubble sizes 40/100/200. Save/reopen/pick up/replace; choose None; cancel the parent editor; open a locked marker. Close/reopen the picker repeatedly.
Review corrections: included the built-in none/slashed-circle icon. SVG drawing accepts only textures/*.svg assets, and icon renderer exceptions are contained. A failed icon falls back to the readable text bubble. Picker pages survive malformed SVG or custom renderers.
Final validation: both review axes clear, 799 tests pass. Standard package built successfully; packaged DLL matches tested DLL and packaged JSON parses. QA server readback and both profile copies match SHA256 45ADA3EBF040DB9B91C606828B247005C5B6F5FBB1DC0C958E1F2E471F4171CC. Previous packages/logs retained in .tmp/scene-vtml-qa-2026-09-09. Human visual acceptance pending.
