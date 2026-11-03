# Built-in asset packs

Bundled PNGs that ship with the app. The renderer picks them up at runtime via the **Built-in packs** buttons in the left panel of the editor (Drips / Word art), so end users get a useful starter library on first launch.

## Packs

| Pack | Count | Purpose |
|---|---|---|
| [`drips/`](drips/) | 156 PNGs | Blood and liquid drip overlays — front, back, sleeve, and pants variants at multiple colors and flipped orientations |
| [`word_art/`](word_art/) | 25 PNGs | Pre-rendered text treatments (based, bruh, bussin, conundrum vibes, etc.) using the in-app word art generator's output |

## How they're loaded

`src/RBX15.App/RBX15.App.csproj` copies `packs/**/*.png` byte for byte to `packs/<name>/` in the build output, so the same folder ships in the MSIX and the portable zip (this README stays out). At run time `PackSource` (`src/RBX15.App/Services/PackSource.cs`) reads `packs/<name>/` next to `RBX15.App.exe`.

## Adding a new pack

1. Drop your PNGs into `packs/your-pack-name/`. The csproj wildcard picks them up with no config change.
2. Add a constant for the folder name in `PackSource`, beside `Drips` and `WordArt`.
3. Add a command in `AssetLibraryViewModel` (beside `LoadDripsCommand`) that calls `LoadPack(PackSource.YourPack)`, and a button for it in `Views/AssetLibraryPanel.xaml` with an `AutomationProperties.Name`.
4. Add tests in `PackSourceTests` (the folder is found and lists its images) and `AssetLibraryViewModelTests` (the button loads the pack).

Keep packs under a few MB each. Large packs bloat the package and slow first-launch file enumeration.

## Licensing

Only bundle assets you created yourself or have explicit redistribution rights for. These ship inside a Microsoft Store submission and become part of a commercial product.
