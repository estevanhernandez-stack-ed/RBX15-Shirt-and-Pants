# RBX15 smoke test

A fast confidence pass for v5. Run it before every merge to the release branch and before every tagged release. It takes about 10 minutes. For the install, upgrade, network and uninstall sweep on a clean machine, use [`windows/TEST_PLAN.md`](../windows/TEST_PLAN.md). Checks that need a real install or a real crash (the packaged jump list, Explorer file launch, Narrator, the Windows App Certification Kit) are MAN-10 to MAN-18 in [`docs/port/m5m6-manual.md`](port/m5m6-manual.md), in the style of [`m4b-manual.md`](port/m4b-manual.md). Run those once per release, not per merge.

## How to run

1. Run the automated floor first and keep it green:

   ```bash
   dotnet build src/RBX15.slnx -c Release -warnaserror
   dotnet test src/RBX15.slnx -c Release --no-build
   ```

   The suite covers the engine, the render goldens, the view models, the shortcut router and a UI Automation pass over the real window. A step below that names an automated test is already proven there, so look at it again only when the suite is red or the step is the one you are changing.
2. Launch the Release build: `dotnet run --project src/RBX15.App -c Release --no-build`, or the installed build, or `RBX15.App.exe` from the portable zip.
3. The diagnostic is the error log, `%LOCALAPPDATA%\RBX15\logs\error.log`. A clean pass leaves that file absent or unchanged. Note its size and time before you start, and check it again at the end. In the Store build the folder is redirected into the package's local data (MAN-16 finds it).

Tick a box only when you saw the expected result.

---

## Part A: the four regressions from the 4.1 revival

These four broke or endangered 4.1.0 and 4.0.1. Test them first.

### A1. Asset click to add

Automated: `AssetLibraryViewModelTests`, `ImportServiceTests`.

- [ ] Home tab, Drips: the asset browser fills with thumbnails (156 images).
- [ ] Click a thumbnail: a layer appears in the front torso slot, fit to 85% of the slot.
- [ ] Drag a thumbnail onto the canvas: the layer lands where you drop it, named by its file.
- [ ] `error.log` has no new entry.

### A2. Eraser

Automated: `CanvasInteractionTests`, `LedgerScenarioTests`.

- [ ] Add an image layer and keep it selected.
- [ ] Press E (or click Eraser) and drag across the layer: a half-transparent magenta stroke previews while you drag.
- [ ] Release: the painted pixels are gone from that layer.
- [ ] Rotate the layer about 30 degrees and erase again: the hole lands under the cursor, not offset.
- [ ] Ctrl+Z puts the erased pixels back.
- [ ] With no layer selected, press E and drag: the status strip says to select a layer to erase from.

### A3. Layer-name injection

Automated: `LayerPropertiesViewModelTests`.

- [ ] Select a layer, then set its name in the Selected layer panel to `"><img src=x onerror=alert(1)>`.
- [ ] Press Tab or click away: nothing runs and no dialog opens.
- [ ] The name shows as literal text in the field and in the layers list.

### A4. Undo and redo

Automated: `EditorSessionTests`, `LedgerScenarioTests` (the undo matrix).

- [ ] On a new document, Undo and Redo are both disabled.
- [ ] Add a layer: Undo enables. Click Undo: the layer is gone and Redo enables. Click Redo: the layer returns.
- [ ] Background tab, click a background swatch, then Undo: the color reverts.
- [ ] Click a region tile with no layer selected: it fills. Undo clears it.
- [ ] Switch Mode from Shirt to Pants, then Undo: you are back on Shirt with the region fills intact.
- [ ] Ctrl+Y and Ctrl+Shift+Z both redo.
- [ ] Click Clear, then Ctrl+Z: the layers come back.

---

## Part B: core smoke

### B1. Launch and first paint

Automated: `ShellTests`, `ShellLookTests`.

- [ ] The window opens at 1200x800, centered, with the 626Labs mark, "RBX15 Classic Shirt & Pants Maker" and "v5.0" in the header, the ribbon, the 585x559 canvas and the 3D preview column.
- [ ] The window title is the product name with no `*`. The footer reads "DEVELOPED AT 626LABS".
- [ ] Drag the window narrower and shorter: it stops at 640x480.
- [ ] Close and relaunch: the window returns to its size and position.

### B2. Layers

Automated: `CanvasInteractionTests`, `ShortcutRouterTests`.

- [ ] Select a layer: a cyan box, corner handles and a magenta rotation handle appear.
- [ ] Drag to move, drag a corner to scale, drag the rotation handle to rotate.
- [ ] The wheel scales the layer. Arrow keys nudge 1 pixel (Shift: 10). `[` and `]` rotate 5 degrees (Shift: 15). `R` resets rotation. `Delete` removes the layer. Ctrl+D duplicates it.
- [ ] Drag a layer row to reorder; Alt+Up and Alt+Down do the same on a focused row.

### B3. Adjustments

Automated: `LayerPropertiesViewModelTests`, the render goldens.

- [ ] With a layer selected, move each slider (brightness, contrast, saturation, hue, red, green, blue): the canvas updates as you drag.
- [ ] Reset adjustments returns the layer to its original look.

### B4. Word Art

Automated: `TextTabTests`, the Word Art goldens.

- [ ] Text tab: type text and the preview renders. Step through all six styles (solid, gradient, neon, shadow, outline, retro).
- [ ] Change the font to Permanent Marker and to one other: the preview follows.
- [ ] Add to canvas drops a new layer.

### B5. Details, Placement and Presets

Automated: `DetailsTabTests`, `PlacementGroupTests`, `PresetsGroupTests`.

- [ ] Details tab: a collar (V-neck), a hem and a pocket each add a layer.
- [ ] Home tab, with a layer selected: Fit Front, F+B, Stretch, Allover and Scatter each reposition it.
- [ ] Background tab, with a layer selected: a preset (Meme Tee, then Mirror F+B) places the layer.

### B6. Region fills

Automated: `RegionsGroupTests`, `FillStyleTests`.

- [ ] Background tab: pick a Fill Style (for example stripes) and click a region tile. The region paints with that style, and nothing else opens.
- [ ] Right-click the tile: the fill clears.
- [ ] With a layer selected, click a tile: the layer fits into that region instead.

### B7. Save, Open and Export

Automated: `ProjectFileServiceTests`, `LedgerScenarioTests`, the export goldens.

- [ ] Set a template name, then Save work: a Save dialog opens with `<name>.r15proj`, and the title loses its `*` after saving.
- [ ] Clear, then Open the file: layers, positions, adjustments, region fills and mode all return, and layer names are kept.
- [ ] Download template: a 585x559 PNG named `<name>_roblox.png`. Open it in an image viewer; it matches the canvas, with adjustments baked in.

### B8. Mode

Automated: `ModeGroupTests`.

- [ ] The Mode button switches Shirt to Pants: the region grid changes to waist and legs and the toggle gradient flips.

---

## Part C: the v5 upgrades

### C1. Autosave and crash recovery

Automated: `AutosaveServiceTests`, `RecoveryServiceTests`, `StartupFlowTests` (a manual tick source, never a real 60 s timer). The real crash is MAN-10.

- [ ] Add a layer so the title shows `*`. Wait 70 seconds.
- [ ] Open `%LOCALAPPDATA%\RBX15\autosave`: a `.r15proj` and a `.json` with the same name exist.
- [ ] Save the project: both files are gone.
- [ ] Edit again, wait 70 seconds, then end the task in Task Manager (Details tab, End task).
- [ ] Relaunch: a prompt reads "RBX15 didn't close properly. Restore your unsaved work on <name>, autosaved <time>?" with Restore and Discard.
- [ ] Choose Restore: the work returns with `*` in the title. Ctrl+S saves to the original file when it still exists.
- [ ] Repeat the crash and choose Discard: the files are deleted. Repeat once more and press Esc: the files stay for the next launch.

### C2. Recent projects and the jump list

Automated: `RecentProjectsTests`, `FileGroupTests`. The packaged jump list is MAN-11.

- [ ] Save or open three different projects, then open the Recent button beside Open: the three appear newest first, with the full path as a tooltip.
- [ ] Click an entry: it opens, after the save prompt if the document has changes.
- [ ] Delete one of the files in Explorer, then click its entry: the status strip says the file was moved or deleted, and the entry leaves the list.
- [ ] Clear recent list empties the drop-down.
- [ ] Restoring from autosave (C1) does not add an entry.

### C3. Open from Explorer

Automated: `LaunchArgumentsTests`, `StartupLaunchFileTests`. Explorer behavior on the Store build is MAN-12.

- [ ] Double-click a `.r15proj` (Store build) or use Open with and choose `RBX15.App.exe` (zip), with the app closed: the file opens in a new window.
- [ ] Do it again while the app is open: a second window opens and the first is unchanged.
- [ ] Launch the exe from a terminal with a path that does not exist: the app opens empty and `error.log` gets no entry.

### C4. Paste

Automated: `PasteServiceTests` (synthetic clipboard data). Browser and Snipping Tool captures are MAN-13.

- [ ] Copy any image (right-click an image in a browser, Copy image). In RBX15, press Ctrl+V: a new layer appears on top, selected, centered in the front slot.
- [ ] Ctrl+Z removes it in one step.
- [ ] Copy a PNG file in Explorer and paste: a layer named by the file.
- [ ] Copy plain text and press Ctrl+V on the canvas: the status strip says "Nothing to paste. Copy an image first."
- [ ] Click into a text box (the template name) and press Ctrl+V with text on the clipboard: the text pastes into the box, not a layer.

### C5. Error log

Automated: `ErrorLogTests`.

- [ ] Before and after this pass, `error.log` has the same size and time (see How to run). A new entry from a clean pass is a failure: read it, then file it.
- [ ] If an entry exists, it names the time (UTC), the app and Windows versions, and the exception. It holds no layer names and no image data, and your user folder reads `%USERPROFILE%`.

---

## Result

- Date and build: ___________
- Tester: ___________
- Automated floor: ☐ build and tests green
- Part A (regressions): ___ / 4 groups pass
- Part B (core): ___ / 8 groups pass
- Part C (upgrades): ___ / 5 groups pass
- `error.log` unchanged by the pass: ☐ yes ☐ no
- Blocking issues: ___________

A release ships only when Part A is 4/4, Part B and Part C have no regressions, and the MAN list in `docs/port/m5m6-manual.md` has an answer for every item.
