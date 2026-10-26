# Windows release test plan

Run this checklist on a clean Windows 10 (1809 or later) or Windows 11 VM before every tagged release. It takes about 25 minutes. For the quick per-merge pass, use [`docs/SMOKE_TEST.md`](../docs/SMOKE_TEST.md). Steps that need a real crash, a real Explorer, a screen reader or the certification kit are MAN-10 to MAN-18 in [`docs/port/m5m6-manual.md`](../docs/port/m5m6-manual.md); this plan points to them where they apply.

## Prerequisites

- A Windows 10 1809+ or Windows 11 x64 VM with no prior RBX15 install and no .NET runtime installed (the app carries its own).
- The build under test, from `dist/` after `scripts/build-msix.ps1`: `RBX15-Maker-v<version>.msix` and `RBX15-Maker-v<version>-portable-win-x64.zip`. For a sideload test, the dev certificate trusted on the VM (see [`SIGNING.md`](SIGNING.md)).
- About 300 MB of free disk.
- A folder of PNG images for the drop tests, one with transparency. Any PNGs work.
- A network monitor (Resource Monitor, Fiddler or Wireshark) for section 14.

## 1. Install

Run 1a (Store or sideload MSIX) and 1b (zip) on separate clean VMs, or uninstall between them.

### 1a. MSIX (Store or sideload)

- [ ] Store: install "RBX15 Classic Shirt and Pants Maker" from the Store listing. Sideload: double-click the `.msix` and choose Install. The installer shows the app name and publisher "626Labs LLC".
- [ ] The install finishes without errors.
- [ ] Start shows the app as "RBX15 Classic Shirt and Pants Maker" with the RBX15 tile image. The splash screen shows on launch.
- [ ] Upgrade only: with 4.1 installed from the Store, quit it, install the new MSIX over it. The app is updated in place (same Start entry, no second copy), and Settings, Apps shows the new version.

### 1b. Portable zip

- [ ] Extract `RBX15-Maker-v<version>-portable-win-x64.zip` into a new folder.
- [ ] Run `RBX15.App.exe`. SmartScreen may warn because the zip is unsigned: choose More info, then Run anyway.
- [ ] The app starts with no "missing runtime" prompt.

## 2. First launch

- [ ] The window opens at 1200x800, centered.
- [ ] There is no menu bar, and the title bar reads "RBX15 Classic Shirt and Pants Maker".
- [ ] The header shows the 626Labs mark, "RBX15 Classic Shirt & Pants Maker" and "v5.0" in the subtitle.
- [ ] The ribbon tabs read Home, Background, Details and Text, and Home is active.
- [ ] The canvas shows the 585x559 template grid, with the 3D preview column to its right.
- [ ] The footer reads "DEVELOPED AT 626LABS".
- [ ] `%LOCALAPPDATA%\RBX15\logs\error.log` does not exist yet (zip), or is absent from the package's local data (MSIX; MAN-16 finds that folder).
- [ ] Drag the window to 640x480: it will not go smaller. Close it and relaunch: it returns to the same size and position.

## 3. Built-in asset packs

- [ ] Click **Drips**: the asset browser expands with 156 thumbnails.
- [ ] Click any thumbnail: a layer is added fit to the front torso slot.
- [ ] Drag a thumbnail onto the canvas: the layer lands where you drop it, named by its file.
- [ ] Click **Word art**: the grid shows 25 images. Click one: a layer is added with the right name.

## 4. Drop zone, canvas drop and paste

- [ ] Drag a PNG from File Explorer onto the drop zone: a layer is added centered in the front slot.
- [ ] Drag a PNG onto the canvas at several positions: the layer lands at the drop point.
- [ ] Copy an image with transparency in a browser and press Ctrl+V: a new layer is added on top, selected, with its transparency kept. Ctrl+Z removes it in one step. (MAN-13 covers Edge and the Snipping Tool.)
- [ ] Copy plain text and press Ctrl+V on the canvas: the status strip says "Nothing to paste. Copy an image first."

## 5. Layer manipulation

- [ ] Click a layer on the canvas: a cyan dashed box, corner handles and a magenta rotation handle appear.
- [ ] Drag a corner handle to scale. Drag the rotation handle to rotate, and the readout follows.
- [ ] The mouse wheel scales the selected layer.
- [ ] Arrow keys nudge 1 pixel, Shift+arrow 10. `[` and `]` rotate 5 degrees, with Shift 15. `R` resets rotation.
- [ ] Ctrl+D duplicates. Delete or Backspace removes. Ctrl+Z undoes and Ctrl+Y redoes.
- [ ] Drag a layer row to reorder it.

## 6. Image adjustments

- [ ] With a layer selected, the Image adjustments section shows brightness, contrast, saturation, hue and red, green and blue shift sliders.
- [ ] Each slider updates the canvas as you drag.
- [ ] **Reset adjustments** returns every slider to default and the layer to its original look.

## 7. Word Art

- [ ] Click the **Text** tab and type "RIZZ": the preview renders.
- [ ] Change the font, including Permanent Marker: the preview follows. All fonts render with no network connection.
- [ ] Step through the six styles (solid, gradient, neon, shadow, outline, retro): the preview follows each.
- [ ] **Add to canvas** adds a new layer.

## 8. Placement and presets

- [ ] With a layer selected, **Fit Front** fits the front torso. **F+B** places front and back. **Stretch** stretches across the torso. **Allover** tiles all regions. **Scatter** places random copies.
- [ ] On the Background tab, a preset (Meme Tee, Allover, Sleeve Pop, Mirror F+B, Scatter, Fullbleed) places the selected layer.

## 9. Clothing details

- [ ] Click the **Details** tab. Change the color, the width and the stitch option.
- [ ] Click each generator (collars, cuffs, hem, seams, shoulder stripe, arm bands, yoke, pocket, placket, full set): a detail layer is added each time.

## 10. Shirt and Pants mode

- [ ] The Mode button switches Shirt to Pants: the region grid shows waist and legs, and the toggle gradient flips.
- [ ] Switch back to Shirt. Your layers stay, and the region fills clear on each switch.

## 11. Save, open, recents and file launch

- [ ] Type a template name, then click **Save work**: a Save dialog suggests `<name>.r15proj`. After saving, the title loses its `*`.
- [ ] Click **Clear**, then **Open** and pick the file: layers, positions, adjustments, region fills and mode return, and layer names are kept.
- [ ] The **Recent** button beside Open lists the file first, with the full path as a tooltip. Click an entry from a fresh launch: it opens.
- [ ] Rename the saved file in Explorer, then click its Recent entry: the status strip says the file was moved or deleted, and the entry leaves the list. **Clear recent list** empties the rest.
- [ ] Right-click the taskbar icon: a Recent category lists the same projects (MAN-11).
- [ ] With the app closed, double-click a `.r15proj` in Explorer: it opens in RBX15 (the Store build registers the type; for the zip, use Open with). With the app open, double-click a second project: a second window opens (MAN-12).
- [ ] Close a window with unsaved changes: a prompt offers Save, Don't save and Cancel.

## 12. Export PNG

- [ ] Click **Download template**: a 585x559 PNG named `<name>_roblox.png` is written.
- [ ] Open it in an image viewer: it matches the canvas.
- [ ] Image adjustments from section 6 are baked in on any visible layer.

## 13. Background and region colors

- [ ] On the Background tab, click each swatch: the canvas background changes. The Background image button accepts one PNG and replaces the solid color.
- [ ] Pick a Fill Style (solid, linear gradient, radial gradient, stripes, checker, dots, camo), then click a region tile: the region paints with that style. Nothing else opens on the click.
- [ ] With a layer selected, click a tile: the layer fits into that region instead.
- [ ] Right-click a tile: the fill clears.

## 14. Autosave and recovery

- [ ] Edit a document until the title shows `*`, then wait 70 seconds. A `.r15proj` and a `.json` appear in the `autosave` folder under the app's local data.
- [ ] Save: the pair is deleted.
- [ ] Edit again, wait 70 seconds, then end the task in Task Manager. Relaunch: the prompt "RBX15 didn't close properly. Restore your unsaved work..." appears. Restore opens the work with `*` in the title. Discard deletes the pair. Esc keeps it for the next launch. (MAN-10 is the full crash check.)
- [ ] A normal close of a clean or saved document leaves no autosave behind.

## 15. Privacy and network

- [ ] With the network monitor running, use the app through sections 3 to 14. Confirm the app makes **zero** network connections: no font requests, no update check, no telemetry, no DNS lookups for 626labs.dev or any other host.
- [ ] Block the app in Windows Firewall: nothing changes in how it behaves.
- [ ] Open `%LOCALAPPDATA%\RBX15` (or, for the Store build, the package's local data folder). It holds `settings.json`, `autosave` and `logs` only, and `error.log` is absent or empty of entries from this run.

## 16. Accessibility

- [ ] Tab moves through the ribbon, the left panel, the region tiles, the swatches, the layer rows and the drop zone, and each stop shows the focus ring.
- [ ] Start Narrator (Win+Ctrl+Enter) and Tab through the Home tab: each button, slider and tab reads a sensible name (MAN-14).
- [ ] Known limits, not failures: high-contrast themes are not supported, and edits made on the canvas are not announced.

## 17. Uninstall

- [ ] MSIX: Settings, Apps, "RBX15 Classic Shirt and Pants Maker", Uninstall. It completes, the Start entry and tile are gone, and the app's data under the package's local data is removed. Look in `%LOCALAPPDATA%\RBX15` afterwards and delete anything left.
- [ ] Zip: delete the extracted folder, then delete `%LOCALAPPDATA%\RBX15` by hand. The zip installs nothing else.
- [ ] No files you saved or exported are touched by either.

---

## MSIX submission checks

Only for Store submission builds. Run after `scripts/build-msix.ps1`, which asserts the identity values below when it packs.

- [ ] `makeappx pack` exits 0, with validation on (not `/nv`).
- [ ] Package Viewer shows Name `626LabsLLC.RBX15ClassicShirtandPantsMaker`, Publisher `CN=177BCE59-0966-4975-9962-10E36652141F`, Version `5.0.0.0`, DisplayName "RBX15 Classic Shirt and Pants Maker" and PublisherDisplayName "626Labs LLC".
- [ ] The only capability is `runFullTrust`.
- [ ] The tile images are present and correct: `Square44x44Logo`, `Square71x71Logo`, `Square150x150Logo`, `Wide310x150Logo`, `Square310x310Logo`, `StoreLogo` and `SplashScreen`.
- [ ] The target device family is Windows.Desktop with MinVersion 10.0.17763.0.
- [ ] The manifest registers the `.r15proj` file type with the "RBX15 project" display name.
- [ ] The self-signed MSIX installs on a test machine once the dev certificate is trusted, and launching from Start matches the zip build.
- [ ] Uninstalling from Settings, Apps removes the app cleanly.
- [ ] The Windows [App Certification Kit](https://developer.microsoft.com/windows/downloads/app-certification-kit/) passes every test category (MAN-17).
