# Contributing to RBX15 Classic Shirt and Pants Maker

Builder-to-builder. Bring a patch, ship a patch. Here's what you need.

## Getting started

You need Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` pins the SDK feature band.

```bash
git clone https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.git
cd RBX15-Shirt-and-Pants
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release --no-build
dotnet run --project src/RBX15.App
```

Warnings are errors in every project (`src/Directory.Build.props`), and CI builds with `-warnaserror` too. The package build is `scripts/build-msix.ps1` and needs the Windows SDK for `makeappx`. Test counts are not written in the docs. The latest run of [.NET CI](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/actions/workflows/dotnet-ci.yml) has them.

## Architecture

One solution, `src/RBX15.slnx`, six projects. The engine knows nothing about WPF, and that is a hard rule.

| Project | Responsibility |
| --- | --- |
| `src/RBX15.Engine` | The document model and everything that turns it into pixels, on SkiaSharp and HarfBuzzSharp: template regions, layers, `LayerOps` and `Placement`, fills, adjustments, Word Art, clothing details, drawing tools and the eraser, history, the `.r15proj` reader and writer (`ProjectIO`), and the PNG export. It never references `System.Windows`, `PresentationCore` or `WindowsBase`. |
| `src/RBX15.App` | The WPF host. `EditorSession` owns the current `Document`, the selection and the undo history. View models in `ViewModels/`, views in `Views/`, the canvas in `Canvas/` and `Controls/`, services (file dialogs, settings, autosave, recovery, recent projects, the jump list, the clipboard) in `Services/`. `Composition.Build` is the one place services are constructed. |
| `src/RBX15.Parity` | A small tool and library that compares an engine render with a frozen golden image and renders scene files. The tests use it for the golden rule. |
| `src/RBX15.Engine.Tests`, `src/RBX15.App.Tests`, `src/RBX15.Parity.Tests` | xUnit. The App tests build the real window and view models over fakes. |

A few rules that keep the code honest:

- **Every document change is one undo step**, made through `EditorSession` (`Apply` or `ApplyWithSelection`). Documents are immutable records, so an edit returns a new `Document`.
- **New services go through `Composition.Build`** and get a fake in `src/RBX15.App.Tests/Fakes/` that `TestShell` wires in.
- **Tests never touch the machine they run on.** No test reads or writes `%LOCALAPPDATA%\RBX15`, the real clipboard or the real jump list, and none starts a process. Use `TestShell.Storage()` and the fakes. Anything that builds WPF objects runs through `StaThread.Run`.
- **No synthetic desktop input.** `grep -rnE "SendInput|mouse_event|keybd_event|SendKeys|SetCursorPos" src` must print exactly one line, in `BridgeExclusionTests.cs`. The test input bridge in `src/RBX15.App/Testing/` compiles only with `-p:EnableTestInputBridge=true`, and the package build refuses it.
- **Wall-clock assertions run only under `RBX15_PERF=1`.** Without it, performance tests report numbers and assert correctness.

## The ledger and the goldens

`docs/port/parity-ledger.json` lists every 4.1 behavior, who owns it in v5, how it is checked and whether it is done. [`docs/port/README.md`](docs/port/README.md) explains the format.

- Edit the JSON, never `parity-ledger.md` or `divergences.md`. Those are generated: `python tools/ledger/ledger.py render`.
- CI runs `python tools/ledger/ledger.py check`. A row closes only on evidence that exercises the behavior its `parityCheck` names.
- A deliberate change from 4.1 needs a `4.1: ... v5: ...` sentence in the row's `divergence` field.

The images under `tests/goldens/` are frozen 4.1 output. The engine must match them, and no pull request changes them. A golden failure is a bug in the engine, never a reason to loosen a tolerance. A v5 behavior that looks different on purpose gets a new golden next to the old one and a line in `divergences.md`. The rules for making goldens are in [`tools/parity-ref/README.md`](tools/parity-ref/README.md).

## Easy first contributions

### Add a clothing detail

1. In `src/RBX15.Engine/Details.cs`, add a value to `DetailType`, add a row to `Table` (the id, the type and the layer name), draw it in a new method that takes a `Pen`, like `Pocket(Pen p)`, and add its `case` to the switch in `Draw`. Regions resolve through `Template.Slot`, so the detail also works in Pants mode.
2. In `src/RBX15.App/ViewModels/DetailsViewModel.cs`, add a button to one of the four `Group(...)` lines in the constructor: `("My detail", "my-detail")`.
3. Add a test in `DetailsTests` (the id maps to the type and name; the render is template-sized and draws something) and a button fact in `DetailsTabTests`. There is no golden to match for something new, so assert on pixels you know.

### Add a Word Art style

1. In `src/RBX15.Engine/WordArt.cs`, add a value to `WordArtStyle` and a branch to `DrawLine` beside Shadow, Retro, Neon and Outline. If the style draws outside the text, give it a case in `Pad`.
2. In `src/RBX15.App/ViewModels/TextViewModel.cs`, add an option to `Styles`: `new(WordArtStyle.MyStyle, "My style")`.
3. Add tests in `WordArtTests` (it draws, and it differs from Solid) and `TextTabTests` (the button selects it).

### Add a placement preset

1. In `src/RBX15.Engine/Placement.cs`, add a static method that takes a snapshot or document and a layer id and returns the new one. Presets are pure functions.
2. In `src/RBX15.App/ViewModels/PlacementActions.cs`, add an action that runs it through `Run`. That makes it one undo step and a no-op with nothing selected.
3. In `PresetsViewModel.cs`, add a command and add a button in `Views/PresetsGroup.xaml` with an `AutomationProperties.Name`.
4. Add tests in `PlacementTests` (the geometry) and `PresetsGroupTests` (one step, nothing selected does nothing, Pants mode).

## Pull request guidelines

- **One feature per PR.** Easier to review, easier to revert.
- **Update CHANGELOG.md** under `## [Unreleased]`.
- **Add tests with the change.** Bug fixes get a failing test first.
- **Run the three checks before you push:** the `dotnet build` with `-warnaserror`, the `dotnet test`, and the synthetic-input grep above. If you touched the ledger, run `python tools/ledger/ledger.py check` too.
- **Use conventional commits** (`feat(app): ...`, `fix(engine): ...`, `docs: ...`).
- **Click through it.** This is a UI tool. Launch the app, drop an image, save a project, reopen it, export a PNG, and confirm it still works end to end.
- **Don't restructure beyond the scope of your change.** If something needs a bigger move, raise it in an issue first.

## Code style

- C# with nullable reference types on. Match the surrounding file's naming, comment density and idiom.
- Comments say why, not what. Where a behavior came from 4.1, the comment cites the old `editor.js` line and the ledger row.
- `PascalCase` for types and members, `_camelCase` for private fields, `camelCase` for locals.
- Keep view code thin. Behavior lives in view models and the engine, where it can be tested without a window.
- Prefer a small, named type over a long method with flags.

## What we're looking for

Check the [Known limitations in README.md](README.md#known-limitations). High-signal contributions:

- Additional Word Art styles and fonts (fonts need an open license shipped with the app).
- Accessibility: announcing canvas edits to screen readers, high-contrast support.
- Bug reports with repro steps from real Roblox upload failures.

## History

The 4.0 specs and plans under [docs/superpowers/](docs/superpowers/) are history, not the current design. The v5 port design is `docs/superpowers/specs/2026-10-11-native-v5-port-design.md`.

### Legacy 4.1 source

<!-- Legacy 4.1 -->
The Electron app (`main.js`, `editor.html`, `editor.js`, `styles.css`, `lib/`, `package.json`, `tests/*.test.js`) still sits in the repo root until the cutover removes it. v5 does not build from it. It is the reference the goldens in `tests/goldens/` were rendered from, through the Node tools in `tools/parity-ref/`. Don't extend it. Fixes to 4.1 behavior that v5 must match are rare, and they come with a note in the ledger.

## License

By contributing, you agree your code is released under the [MIT License](LICENSE). Built by [626Labs](https://626labs.dev).
