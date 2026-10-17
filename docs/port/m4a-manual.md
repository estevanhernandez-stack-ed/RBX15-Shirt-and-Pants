# M4a manual checks

M4a checks the canvas, the layers panel, files, the asset library and the dialogs through a test-only input bridge that drives RBX15's own window from inside (`test-input-bridge` in the [parity ledger](parity-ledger.md)). Three checks are out of its reach and stay with a person: a drag from another app, a second monitor and a display above 100%. Run the Release build, `dotnet run --project src/RBX15.App/RBX15.App.csproj -c Release --no-build`, and answer each item pass, fail or not run.

| # | Do | Expect | Rows |
| --- | --- | --- | --- |
| MAN-1 | Drag a PNG from File Explorer over the ribbon and over the layers list, then onto the drop zone, then onto the canvas. Then load the Drips pack, start dragging a thumbnail, release it over the layers list, and drag a PNG from Explorer onto the canvas. | Over the ribbon and the list: the no-drop cursor, and nothing happens. The drop zone highlights and the layer lands centered in the front slot. On the canvas it lands centered on the drop point. After the cancelled thumbnail drag, the Explorer file is the one added (its name in the list), not the drip. | `shell-external-drop-guard`, `asset-drag-stale-reference`, `drop-canvas`, `panel-asset-library` |
| MAN-2 | Move the window to a second monitor, close it, relaunch. Look at the 1 px window border. | The window reopens on the second monitor. The border reads navy (#223A54). | Defect checks only: `shell-window-size` and `shell-window-frame` stay open for their other clauses. |
| MAN-3 | Optional: on a display at 125% or 150%, set zoom to 100% and press Download template. | Template edges are crisp, the layout matches the 100% display in DIPs, and the PNG is 585x559. Answer `not run` if no such display is available. | `canvas-device-pixel-render`, `cross-window-dpi-rendering` |

Two further limits rest on unit tests, not on a person: Alt+Tab during a row drag ends it as a lost capture (`LayersPanelDragTests`), and the cursor image Windows draws is never observable to automation, so the cursor rows are checked as the cursor the canvas sets (`CanvasCursorsTests`).
