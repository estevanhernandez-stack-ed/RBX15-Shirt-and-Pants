# RBX15 v5 M2a (Goldens v2, Engine Tools, Eraser and History) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**M2 is split in two.** This plan is M2a. M2b (Engine ProjectIO, Engine Details, Engine WordArt, and font use in the engine) gets its own plan. This plan freezes **all** of M2's goldens now, M2b's included (details, Word Art and project scenes, plus the `.r15proj` fixtures), while the real 4.1 app is still the reference. M2b only enables the scenes this plan skips by name.

**Goal:** Capture every M2 golden from the real 4.1 editor (goldens v2) with byte-pinned fonts, close the four M1 render gaps, and port the drawing tools, the eraser and undo history into the engine, so that the engine passes every shape and erase golden under the unchanged golden rule.

**Architecture:**

- **Goldens v2.** `tools/parity-ref/app-render.js` loads the real `editor.html` in a hidden Electron window on CPU raster, answers Google Fonts requests from `assets/fonts` (vendored at one pinned google/fonts commit), drives the page through its own globals, inputs and synthetic mouse events, and captures the real `exportPNG`. Its output in `tests/goldens/app` is frozen once, in Task 3. Four M1-gap scenes join the M0/M1 render goldens through M0's `render.js`.
- **Engine.** `RBX15.Engine` gains three static modules over the M1 records: `Tools` (stroke geometry, autocrop, drawing commit), `Eraser` (a port of `lib/erase.js`, stroking once) and `History` (capped undo and redo of immutable `EditorSnapshot`s). The eraser keeps an internal replay of 4.1's per-move stroking for parity only.
- **Parity.** `SceneLoader` v2 runs scene `actions` (`shape`, `erase`) through the engine and reports the ops it cannot run yet. `GoldenAppTests` holds every app scene to the shared `GoldenRule`, skipping M2b's ops by name.

**Tech Stack:** .NET 10 (`net10.0`), C# with nullable and warnings as errors, SkiaSharp `4.153.1`, xUnit 2.9.3, Electron 41.1.0 and `@napi-rs/canvas` (devDependencies, reference renderer only), Node `node:test`, Python 3 stdlib (ledger tool), Git Bash.

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 2, 5 and 6. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json) and [`docs/port/divergences.md`](../../port/divergences.md). Previous milestone: [M1 plan](2026-10-13-v5-m1-engine-render-core.md).

## Global Constraints

- **The engine never references WPF.** `RBX15.Engine` targets `net10.0` and depends only on SkiaSharp `4.153.1`.
- **No obsolete SkiaSharp APIs.** Warnings are errors. Verified against 4.153.1 on 2026-10-13:
  - Build paths with `SKPathBuilder` (`MoveTo`, `LineTo`, `ArcTo(SKRect, float, float, bool)`, `Close()`, `Detach()`).
  - Draw images with `DrawImage(SKImage, SKRect, SKSamplingOptions, SKPaint)` or `DrawImage(SKImage, float, float, SKSamplingOptions, SKPaint)`.
  - `SKColorFilter.CreateColorMatrix` translate values are in **0..1** units.
  - If any other call is obsolete, use the replacement the warning names. Never suppress the warning.
- **The render order is a contract** (spec section 2): clear, background (a stretched image, or the color, times opacity), region fills, then visible layers bottom to top. Export always applies the mask.
- **Sampling.** Layer and eraser-mask draws use `Renderer.LayerSampling` (`SKFilterMode.Linear`, `SKMipmapMode.None`), Canvas2D's default `imageSmoothingQuality = 'low'`. Any non scale-translate image draw goes through `Renderer.FloatPipeline`, and antialiasing follows `Renderer.DrawImageAntialiased` (Blink `ShouldDrawImageAntialiased`).
- **Strokes are drawn once** (approved divergence `draw-pen-incremental-stroke`). Production code never replays 4.1's per-move stroking. The only replay is the internal `EraseStroking.Replay41`, used by parity tests to meet the frozen eraser goldens.
- **The golden rule** (spec section 6): zero interior differences, and edge deltas of 3 or less. Scenes with an RGB-shifted layer use M1's rule (approved divergence `rgb-shift-compute`): the full rule on pixels whose golden alpha is 255, plus an alpha delta of at most 1 everywhere. Both golden gates apply it through the shared `GoldenRule`. When a scene fails, fix the engine. The test, the rule and the golden stay as they are.
- **Goldens are made only by the documented `tools/parity-ref` tools, run by a person.** CI never regenerates them. New goldens are captured **once**, in Task 3, and then frozen. The 30 M0/M1 render goldens stay byte-identical.
- **Golden scene copies are written with LF by a script, never by `cp`.** `core.autocrlf=true` on this machine can make a working-tree file CRLF, and `tests/goldens/** -text` stores bytes as-is.
- **Logs never land in the tree.** Test logs go to `.superpowers/logs/` (git-ignored); scratch renders go to `.superpowers/parity-check*/`. Heatmaps go to `diff-heatmaps/` (git-ignored, a CI artifact).
- **Region keys stay verbatim** (`torso_front`, `waist_front` and so on); saved projects store them.
- **The Electron app is untouched.** `main.js`, `editor.js`, `editor.html`, `styles.css`, `lib/`, `packs/`, `package.json`, `package-lock.json`, `tests/*.test.js` and `windows/` do not change. `tools/parity-ref` reads them as-is.
- **`ELECTRON_RUN_AS_NODE` is cleared** for every Electron run (`env -u ELECTRON_RUN_AS_NODE`), or `electron.exe` starts as plain Node.
- **Conventional commits.** Each commit ends with the `Co-Authored-By` trailer of the model that made it; a subagent commit carries its own model's trailer, not the controller's.
- **Dashboard:** log forks with project `6KpHItSNg9NYHkJg9thn` and origin `Dunder · Claude Code`.

## Review Focus

The five input classes most likely to bite that the drafted tests did not exercise. Each now has a pinning test in its owning task.

- **A shape dragged entirely outside the 585x559 template** (a drag that starts and ends past the canvas edge, which the canvas reports in template coordinates). Expected: nothing reaches the stroke canvas, so no layer is committed, as 4.1's alpha scan finds nothing (`editor.js:1257-1262`). Pinned in Task 5 (`A_shape_entirely_off_the_template_commits_no_layer`).
- **A scene action with no `op`** (a hand-written or truncated scene). Expected: `UnsupportedOps` reports it as `""` and `Parse` refuses the scene with `NotSupportedException`, so it can never be compared half-built. Pinned in Task 5 (`An_action_without_an_op_refuses_to_parse`).
- **Erasing a layer whose scale is 0** (reachable only from a hand-edited project; the UI clamps at 0.01). Expected: `lib/erase.js:58`'s `scale(1/0, 1/0)` is ignored, as Canvas2D ignores a non-finite transform, and the center collapses to `(x, y)`, so no exception and a deterministic hole. Pinned in Task 6 (`Scale_zero_skips_the_non_finite_inverse_scale_like_canvas`).
- **An eraser size of 0, negative, NaN or infinite** (state restored from a bad project or a future API caller). Expected: Canvas2D ignores such a `lineWidth`, so the fresh stroke canvas keeps its default width 1. Pinned in Task 6 (`Invalid_sizes_keep_the_canvas_default_line_width`).
- **A history of capacity 1.** Expected: one step back, one step forward, the older record evicted, and an empty undo parks nothing. Pinned in Task 7 (`Capacity_one_keeps_only_the_latest_step`).

---

## File structure

```text
assets/fonts/                  18 family dirs: 19 TTF + 18 license files, README.md manifest (Task 1)
tools/fonts/fetch-google-fonts.sh   pinned google/fonts download with blob checks (Task 1)
tools/parity-ref/
  fonts.js                     css2 request -> vendored face map (Task 2)
  app-harness.js               page script that drives the real editor.html (Task 2)
  app-render.js                Electron main: render scenes v2 / save projects (Task 2)
  app-render.test.js           9 node:test cases (Task 2)
  scenes.json                  + 4 M1-gap scenes (Task 3)
  app-scenes.json              82 v2 scenes (Task 3)
  project-scenes.json          3 project fixture sources (Task 3)
  README.md                    goldens v2 documented (Task 3)
tests/goldens/
  render/                      + 4 gap-*.png, merged index.json, LF scenes.json (Task 3)
  app/                         82 PNGs, index.json, scenes.json (Task 3)
  projects/                    3 .r15proj, index.json, scenes.json (Task 3)
  fixtures/bg-large-2160.png   large background fixture (Task 3)
src/RBX15.Engine/
  Tools.cs                     ShapeTool, Stroke, RenderStroke, Autocrop, CommitDrawing, DrawingName (Task 5)
  Eraser.cs                    Eraser.Apply, EraseStroke, internal EraseStroking replay (Task 6)
  History.cs                   EditorSnapshot, History (Task 7)
  Renderer.cs                  index doc rows (Tasks 4, 6); three members internal (Task 6)
  RBX15.Engine.csproj          InternalsVisibleTo RBX15.Parity (Task 6)
src/RBX15.Engine.Tests/
  VendoredFontsTests.cs  ToolsTests.cs  EraserTests.cs  HistoryTests.cs
  GoldenRule.cs                the shared golden rule (Task 5)
  GoldenRenderTests.cs         rewritten to call GoldenRule (Task 5)
  GoldenAppTests.cs            the app golden gate (Tasks 5, 6)
src/RBX15.Parity/
  SceneLoader.cs               scene format v2: actions, UnsupportedOps, erase (Tasks 5, 6)
  RBX15.Parity.csproj          InternalsVisibleTo RBX15.Engine.Tests (Task 6)
src/RBX15.Parity.Tests/
  SceneLoaderTests.cs          34 scenes (Task 4), v2 facts (Task 5)
  SceneLoaderEraseTests.cs     erase op (Task 6)
docs/port/                     ledger rows closed, eraser-apply divergence (Task 8)
```

**Test counts.** The M1 baseline is Engine.Tests 92 passing and Parity.Tests 13 passing. Each run step below states the cumulative count after its task.

---

### Task 1: Vendor the fonts

**Files:**

- Create: `tools/fonts/fetch-google-fonts.sh`, `assets/fonts/README.md`, `assets/fonts/<family>/*` (19 TTF files and 18 license files, listed in the manifest in Step 6), `src/RBX15.Engine.Tests/VendoredFontsTests.cs`
- Modify: `.gitignore` (re-include `/assets/`), `.gitattributes` (store fonts byte-for-byte)

**Interfaces:**

- Consumes: google/fonts at commit `66928797f271fed5ee428544e7229685e2d90127` over `https://raw.githubusercontent.com`; `RepoRoot.Find` (M1, `src/RBX15.Parity/RepoRoot.cs`).
- Produces:
  - `assets/fonts/<dir>/<file>`: every face 4.1 requests from Google Fonts, as TTF, plus each family's license. The Word Art faces come from `editor.html:11` and the UI faces from `styles.css:6`.
  - `assets/fonts/README.md`: the pinned commit as the literal `google/fonts@<sha>`, plus a manifest table. Each row has seven columns: the file path in backticks, family, weights, source path, kind, git blob in backticks, and the bare SHA-256 last. Task 2's `tools/parity-ref/fonts.js` and M2b's bundling read these paths.
  - `VendoredFontsTests` (CI): every byte under `assets/fonts` matches the manifest.

**What 4.1 requests, and the file that serves each request.** Word Art always draws with `900 <size>px "<family>", sans-serif` (`editor.js:2131`), and the `<select id="waFont">` lists exactly 15 families (`editor.html:230-246`). The Google Fonts URL (`editor.html:11`) asks for these faces:

| Family | Requested weights | Vendored file (google/fonts path) | Kind | What a 900 request draws |
| --- | --- | --- | --- | --- |
| Anton | 400 (default) | `ofl/anton/Anton-Regular.ttf` | static 400 | Chromium synthetic bold |
| Bebas Neue | 400 | `ofl/bebasneue/BebasNeue-Regular.ttf` | static 400 | synthetic bold |
| Dancing Script | 700 | `ofl/dancingscript/DancingScript[wght].ttf` | variable, wght 400-700 | the 700 face, axis at 700 |
| Great Vibes | 400 | `ofl/greatvibes/GreatVibes-Regular.ttf` | static 400 | synthetic bold |
| Pacifico | 400 | `ofl/pacifico/Pacifico-Regular.ttf` | static 400 | synthetic bold |
| Creepster | 400 | `ofl/creepster/Creepster-Regular.ttf` | static 400 | synthetic bold |
| Poppins | 700, 900 | `ofl/poppins/Poppins-Bold.ttf`, `ofl/poppins/Poppins-Black.ttf` | static 700, static 900 | true 900 |
| Lato | 900 | `ofl/lato/Lato-Black.ttf` | static 900 | true 900 |
| Bangers | 400 | `ofl/bangers/Bangers-Regular.ttf` | static 400 | synthetic bold |
| Permanent Marker | 400 | `apache/permanentmarker/PermanentMarker-Regular.ttf` | static 400 (Apache-2.0) | synthetic bold |
| Bungee Shade | 400 | `ofl/bungeeshade/BungeeShade-Regular.ttf` | static 400 | synthetic bold |
| Rubik Mono One | 400 | `ofl/rubikmonoone/RubikMonoOne-Regular.ttf` | static 400 | synthetic bold |
| Press Start 2P | 400 | `ofl/pressstart2p/PressStart2P-Regular.ttf` | static 400 | synthetic bold |
| Righteous | 400 | `ofl/righteous/Righteous-Regular.ttf` | static 400 | synthetic bold |
| Orbitron | 700, 900 | `ofl/orbitron/Orbitron[wght].ttf` | variable, wght 400-900 | the 900 face, axis at 900 |

The UI faces come from `styles.css:6`, which asks for Space Grotesk 400/500/600/700, JetBrains Mono 400/500/600 and Inter 400/500/600/700. They are served from `ofl/spacegrotesk/SpaceGrotesk[wght].ttf` (wght 300-700), `ofl/jetbrainsmono/JetBrainsMono[wght].ttf` (wght 100-800) and `ofl/inter/Inter[opsz,wght].ttf` (opsz 14-32, wght 100-900). google/fonts has no static files for those three families at this commit. The weight classes and axis ranges above were read from each file's `OS/2` and `fvar` tables.

**The pin.** The commit is the newest google/fonts `main` when this plan was written. It was picked with `git ls-remote https://github.com/google/fonts refs/heads/main`. The implementer does not re-pick it. If any download in Step 4 fails or mismatches, stop and report it. Re-pinning means:

1. Run the same `ls-remote`.
2. Replace `COMMIT` and every blob id in the script with the GitHub tree listing at the new commit (`https://api.github.com/repos/google/fonts/contents/<dir>?ref=<sha>`).
3. Regenerate the manifest.
4. Re-freeze every Word Art golden.

That is a controller decision, not an implementer's.

**Risk notes that act in this task:**

- **`.gitignore` already ignores `assets/`.** Line 10 is `Assets/`, and `core.ignorecase=true` on this machine, so `git check-ignore -v --no-index assets/fonts/README.md` prints `.gitignore:10:Assets/`. Step 3 adds `!/assets/` right after that line. Without it, `git add assets/fonts` silently adds nothing.
- **Permanent Marker is Apache-2.0, not OFL.** google/fonts keeps it at `apache/permanentmarker/` with `LICENSE.txt`, which this task vendors. Every other family is SIL OFL 1.1.
- **Five upstream `OFL.txt` files are CRLF** (Anton, Bangers, Bungee Shade, Rubik Mono One, Space Grotesk). Step 3 sets `assets/fonts/** -text` before the first `git add`, and the fetch script verifies blobs with `git hash-object --no-filters`. With `core.autocrlf=true`, a plain `git hash-object` reports a different id for those files.

- [ ] **Step 1: Branch, then write the failing manifest test**

M2a builds on the M1 branch tip. Run: `git checkout feat/v5-m1-engine-render-core && git pull --ff-only && git checkout -b feat/v5-m2a-goldens-tools-eraser-history` (skip if executing in a worktree already on this branch). Tasks 4 and 8 compare against `git merge-base HEAD feat/v5-m1-engine-render-core`, so the check survives the M1 branch moving or merging.

Create `src/RBX15.Engine.Tests/VendoredFontsTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// assets/fonts holds the only copy of the Word Art and UI fonts. Goldens v2 are captured with these
/// exact files injected as @font-face, and the v5 bundle ships the same files, so the manifest in
/// assets/fonts/README.md pins every byte (SHA-256) and the google/fonts commit they came from.
/// Google Fonts' served files drift over time; these may not.
/// </summary>
public partial class VendoredFontsTests
{
    const string Commit = "66928797f271fed5ee428544e7229685e2d90127";
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "assets", "fonts");

    [GeneratedRegex(@"^\| `(?<file>[^`]+)` \|.*\| (?<sha>[0-9a-f]{64}) \|$", RegexOptions.Multiline)]
    private static partial Regex ManifestRow();

    static Dictionary<string, string> Manifest()
    {
        var text = File.ReadAllText(Path.Combine(Dir, "README.md")).Replace("\r\n", "\n");
        return ManifestRow().Matches(text).ToDictionary(m => m.Groups["file"].Value, m => m.Groups["sha"].Value);
    }

    static IEnumerable<string> FilesOnDisk() =>
        Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Dir, f).Replace('\\', '/'))
            .Where(f => f != "README.md");

    [Fact]
    public void Manifest_names_the_pinned_google_fonts_commit() =>
        Assert.Contains($"google/fonts@{Commit}", File.ReadAllText(Path.Combine(Dir, "README.md")));

    [Fact]
    public void Manifest_lists_19_font_files_and_18_licenses()
    {
        var files = Manifest().Keys.ToList();
        Assert.Equal(19, files.Count(f => f.EndsWith(".ttf", StringComparison.Ordinal)));
        Assert.Equal(18, files.Count(f => f.EndsWith(".txt", StringComparison.Ordinal)));
    }

    [Fact]
    public void Every_vendored_file_matches_its_manifest_hash()
    {
        var mismatches = new List<string>();
        foreach (var (file, sha) in Manifest())
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Dir, file))));
            if (actual != sha) mismatches.Add($"{file}: manifest {sha}, file {actual}");
        }
        Assert.Empty(mismatches);
    }

    [Fact]
    public void Nothing_unlisted_sits_in_assets_fonts() =>
        Assert.Equal(Manifest().Keys.Order(StringComparer.Ordinal), FilesOnDisk().Order(StringComparer.Ordinal));

    [Fact]
    public void Every_family_directory_carries_its_license()
    {
        var dirs = Directory.GetDirectories(Dir);
        Assert.Equal(18, dirs.Length);
        foreach (var dir in dirs)
            Assert.True(File.Exists(Path.Combine(dir, "OFL.txt")) || File.Exists(Path.Combine(dir, "LICENSE.txt")), dir);
    }
}
```

- [ ] **Step 2: Run it and see it fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~VendoredFontsTests"`
Expected: FAIL. All 5 tests fail with `System.IO.DirectoryNotFoundException: Could not find a part of the path ...assets\fonts...`.

- [ ] **Step 3: Let git see and keep the font bytes**

In `.gitignore`, replace the line `Assets/` with these two lines. `core.ignorecase=true` makes `Assets/` match `assets/`, and git cannot re-include a file whose parent directory is excluded, so the re-include has to name the directory:

```gitignore
Assets/
!/assets/
```

Append to `.gitattributes`:

```gitattributes
*.ttf binary
assets/fonts/** -text
```

Run:

```bash
git check-ignore -v --no-index assets/fonts/README.md; echo "exit $?"
git check-ignore -v --no-index windows/msix/Assets/x.png
git check-attr -a assets/fonts/anton/OFL.txt
```

Expected:

- The first command prints nothing, then `exit 1`: the path is no longer ignored. This was checked with the same two rules and `core.ignorecase=true` in a scratch repo.
- The second prints `.gitignore:10:Assets/` followed by a tab and `windows/msix/Assets/x.png`, so nested `Assets` directories stay ignored.
- The third includes `text: unset`.

- [ ] **Step 4: Fetch the fonts from the pinned commit**

Create `tools/fonts/fetch-google-fonts.sh`:

```bash
#!/usr/bin/env bash
# Vendors the Word Art fonts (editor.html:11) and UI fonts (styles.css:6) into assets/fonts from
# ONE pinned google/fonts commit. Each file is checked against the git blob id GitHub lists for it
# at that commit (--no-filters: five upstream OFL.txt files are CRLF), so a download that is not
# byte-identical to the pinned tree fails loudly. Re-pinning is a controller decision: see
# assets/fonts/README.md.
#   bash tools/fonts/fetch-google-fonts.sh
set -euo pipefail
COMMIT=66928797f271fed5ee428544e7229685e2d90127
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DEST="$ROOT/assets/fonts"
# <path in google/fonts> <path under assets/fonts> <git blob id at COMMIT>
FILES=(
  "ofl/anton/Anton-Regular.ttf anton/Anton-Regular.ttf 4d65707db9d8663ccfa99cd28cd3a4e0025be178"
  "ofl/anton/OFL.txt anton/OFL.txt 93feddee37145696e123ea65a6772cab09663f9e"
  "ofl/bebasneue/BebasNeue-Regular.ttf bebasneue/BebasNeue-Regular.ttf c328c6e08b20a20a1de47d823e007ee73812a438"
  "ofl/bebasneue/OFL.txt bebasneue/OFL.txt da9571488f44176ef90d7f10c0f402c8be74db67"
  "ofl/dancingscript/DancingScript[wght].ttf dancingscript/DancingScript[wght].ttf 9f521e7fb357ec9ea9820ebd36a9d7b1f72f2935"
  "ofl/dancingscript/OFL.txt dancingscript/OFL.txt 07ff8df965f089a2b89071b3d46cfd15777634a6"
  "ofl/greatvibes/GreatVibes-Regular.ttf greatvibes/GreatVibes-Regular.ttf a1327ff3b862246f1f253639040edcc473e5f3a9"
  "ofl/greatvibes/OFL.txt greatvibes/OFL.txt 70025926f5684ebed9d119424b178faa4e0a5cc6"
  "ofl/pacifico/OFL.txt pacifico/OFL.txt 33864cc8de4dd3ea8f3daab1154be28577918bae"
  "ofl/pacifico/Pacifico-Regular.ttf pacifico/Pacifico-Regular.ttf 27765d02ac13f241b1e207dbda7b2dd1b68280da"
  "ofl/creepster/Creepster-Regular.ttf creepster/Creepster-Regular.ttf 668c59cf43b545294b1fd778ee0cd0f4e18cd02c"
  "ofl/creepster/OFL.txt creepster/OFL.txt 92bc9bee87bafcb1a3ccc6854ba98683411ae7e0"
  "ofl/poppins/OFL.txt poppins/OFL.txt 76df3b565672e3248a5715339d092d4cb6c75019"
  "ofl/poppins/Poppins-Black.ttf poppins/Poppins-Black.ttf 6612e5228fcdeabd840f4ca17de889ca447e22f4"
  "ofl/poppins/Poppins-Bold.ttf poppins/Poppins-Bold.ttf 1982f38ab21303459aa1155265052ca599fa58d1"
  "ofl/lato/Lato-Black.ttf lato/Lato-Black.ttf 53a31ac8f56e9e3f67d35844f2b41ef9ade0e156"
  "ofl/lato/OFL.txt lato/OFL.txt 98383e3d835a5c5aea988a74b569193a30dcbcec"
  "ofl/bangers/Bangers-Regular.ttf bangers/Bangers-Regular.ttf 9b0f8c1f1b45e7bb4971b7d6a8604f9aacacdd32"
  "ofl/bangers/OFL.txt bangers/OFL.txt bf717d4be410cf7616b9bd345b40ab4817a26e6f"
  "apache/permanentmarker/LICENSE.txt permanentmarker/LICENSE.txt d645695673349e3947e8e5ae42332d0ac3164cd7"
  "apache/permanentmarker/PermanentMarker-Regular.ttf permanentmarker/PermanentMarker-Regular.ttf 3218fc5b3d395c3a3fc207fdf17a27bb32af3242"
  "ofl/bungeeshade/BungeeShade-Regular.ttf bungeeshade/BungeeShade-Regular.ttf 80ada80a0404713e7cd31a6b7982ff7a549acdfe"
  "ofl/bungeeshade/OFL.txt bungeeshade/OFL.txt 6f47072f01b8698301896b247b7087c023a4da2d"
  "ofl/rubikmonoone/OFL.txt rubikmonoone/OFL.txt 7e7e709034ebad08173011538f60ccbd9ce3ce07"
  "ofl/rubikmonoone/RubikMonoOne-Regular.ttf rubikmonoone/RubikMonoOne-Regular.ttf 1923947b912685017b6f8ddcca5766f322d89c29"
  "ofl/pressstart2p/OFL.txt pressstart2p/OFL.txt 22796dfe6e3784d69d255b0bf253e2b16f3281de"
  "ofl/pressstart2p/PressStart2P-Regular.ttf pressstart2p/PressStart2P-Regular.ttf 39adf42efa597906e53be689474ac82214112124"
  "ofl/righteous/OFL.txt righteous/OFL.txt 22f3d202eb01975fbcd48a25f3ad82ab624986cb"
  "ofl/righteous/Righteous-Regular.ttf righteous/Righteous-Regular.ttf 07fc0b45132b1d7039c202e6d09c91b1a137ec26"
  "ofl/orbitron/OFL.txt orbitron/OFL.txt 91ede459e18c22d3f2ba7c27d925b49cc945abe4"
  "ofl/orbitron/Orbitron[wght].ttf orbitron/Orbitron[wght].ttf 1fc54ce7116a6ec549f37358466bcc92f9a72c33"
  "ofl/spacegrotesk/OFL.txt spacegrotesk/OFL.txt cb512b9af44ff61e75e1aad387b7424cdfab36a3"
  "ofl/spacegrotesk/SpaceGrotesk[wght].ttf spacegrotesk/SpaceGrotesk[wght].ttf a1b2e6c26093066510a31147e7aec9abdc8d6c5e"
  "ofl/inter/Inter[opsz,wght].ttf inter/Inter[opsz,wght].ttf 047c92f6e2212473dc436020afed689527076d44"
  "ofl/inter/OFL.txt inter/OFL.txt 21f6aff961064c2e429f570995e446bcdd555422"
  "ofl/jetbrainsmono/JetBrainsMono[wght].ttf jetbrainsmono/JetBrainsMono[wght].ttf aa310be8b717fe3774f9444dd89d5f4101cc6d10"
  "ofl/jetbrainsmono/OFL.txt jetbrainsmono/OFL.txt 821a3dac22aff15a1f1c9689a1d79c45bb58ca39"
)
for row in "${FILES[@]}"; do
  read -r src dst blob <<< "$row"
  mkdir -p "$DEST/$(dirname "$dst")"
  # Brackets and commas are percent-encoded: curl treats [] as a glob.
  enc="$(printf '%s' "$src" | sed 's/\[/%5B/g; s/\]/%5D/g; s/,/%2C/g')"
  curl -sSf -o "$DEST/$dst" "https://raw.githubusercontent.com/google/fonts/$COMMIT/$enc"
  got="$(git hash-object --no-filters "$DEST/$dst")"
  if [ "$got" != "$blob" ]; then
    echo "MISMATCH $dst: got $got, want $blob" >&2
    exit 1
  fi
  echo "ok $dst"
done
echo "fetched ${#FILES[@]} files at google/fonts@$COMMIT"
```

Run: `bash tools/fonts/fetch-google-fonts.sh`
Expected: 37 lines starting with `ok`, then `fetched 37 files at google/fonts@66928797f271fed5ee428544e7229685e2d90127`, and exit code 0. On a `MISMATCH` or a curl error, stop and report. Do not edit blob ids.

- [ ] **Step 5: Confirm the bytes against the manifest values before writing the README**

Run:

```bash
cd assets/fonts && sha256sum anton/OFL.txt dancingscript/'DancingScript[wght].ttf' permanentmarker/LICENSE.txt && cd ../..
```

Expected:

```text
ee67e6ee22790b7929f1a3769ca2801d565c64b5a9096942c1adf5596de9c9e4 *anton/OFL.txt
21808625578fe8d8cd10cb684be546dca077b27cd03a53a2f1ec11dc743c924c *dancingscript/DancingScript[wght].ttf
cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30 *permanentmarker/LICENSE.txt
```

The separator may print as two spaces instead of a space and an asterisk; only the hashes matter. A different `anton/OFL.txt` hash means line endings were converted: re-check Step 3.

- [ ] **Step 6: Write the manifest**

Create `assets/fonts/README.md`:

````markdown
# Vendored fonts

The 15 Word Art families 4.1 loads from Google Fonts (`editor.html:11`) and the 3 UI families from `styles.css:6`, vendored as TTF from one pinned commit: **google/fonts@66928797f271fed5ee428544e7229685e2d90127** (main, 2026-10-12).

Goldens v2 (`tests/goldens/app`) are captured with exactly these files injected as `@font-face` by `tools/parity-ref/fonts.js`, and the v5 bundle ships the same files. Google Fonts' served files drift over time; these do not.

## Rules

- Files change only on purpose. That means re-pinning the commit in `tools/fonts/fetch-google-fonts.sh`, re-running it, regenerating every row below, and re-freezing every Word Art golden. `VendoredFontsTests` fails on any byte that does not match this table.
- Files are stored byte-for-byte (`.gitattributes`: `assets/fonts/** -text`). Five upstream `OFL.txt` files use CRLF line endings.
- 4.1 always draws Word Art at weight 900 (`editor.js:2131`). Chromium synthesizes bold for faces below 600. Dancing Script's 700 face and the 900 faces of Poppins, Lato and Orbitron are real weights. A variable file serves each requested weight with its `wght` axis set to that weight.
- Permanent Marker is licensed Apache-2.0 (google/fonts keeps it under `apache/`), so its directory carries `LICENSE.txt`. Every other family is SIL OFL 1.1, with `OFL.txt`.

## Manifest

| File | Family | 4.1 requests | Source at the pinned commit | Kind | Git blob | SHA-256 |
| --- | --- | --- | --- | --- | --- | --- |
| `anton/Anton-Regular.ttf` | Anton | 400 | `ofl/anton/Anton-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `4d65707db9d8663ccfa99cd28cd3a4e0025be178` | a4ba3a92350ebb031da0cb47630ac49eb265082ca1bc0450442f4a83ab947cab |
| `anton/OFL.txt` | Anton | license | `ofl/anton/OFL.txt` | SIL OFL 1.1 | `93feddee37145696e123ea65a6772cab09663f9e` | ee67e6ee22790b7929f1a3769ca2801d565c64b5a9096942c1adf5596de9c9e4 |
| `bebasneue/BebasNeue-Regular.ttf` | Bebas Neue | 400 | `ofl/bebasneue/BebasNeue-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `c328c6e08b20a20a1de47d823e007ee73812a438` | 08e4623805102d819f58601e46e345648846075e363b2ceb23313c2d1c83ec73 |
| `bebasneue/OFL.txt` | Bebas Neue | license | `ofl/bebasneue/OFL.txt` | SIL OFL 1.1 | `da9571488f44176ef90d7f10c0f402c8be74db67` | 72082f6cb4d04be2ecf7cc7d9e1e7d73787f0af8a5a278a47cade70c16b78341 |
| `dancingscript/DancingScript[wght].ttf` | Dancing Script | 700 | `ofl/dancingscript/DancingScript[wght].ttf` | variable, wght 400-700; served as the 700 face | `9f521e7fb357ec9ea9820ebd36a9d7b1f72f2935` | 21808625578fe8d8cd10cb684be546dca077b27cd03a53a2f1ec11dc743c924c |
| `dancingscript/OFL.txt` | Dancing Script | license | `ofl/dancingscript/OFL.txt` | SIL OFL 1.1 | `07ff8df965f089a2b89071b3d46cfd15777634a6` | 6f090277c00af96651ce6dbcc38ff1591047a3bffef486e80b6a32e8276a8201 |
| `greatvibes/GreatVibes-Regular.ttf` | Great Vibes | 400 | `ofl/greatvibes/GreatVibes-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `a1327ff3b862246f1f253639040edcc473e5f3a9` | 8d509802186f1b51572531ecf313e8098f9a5bfdfaca93f0c9b34467f9982d15 |
| `greatvibes/OFL.txt` | Great Vibes | license | `ofl/greatvibes/OFL.txt` | SIL OFL 1.1 | `70025926f5684ebed9d119424b178faa4e0a5cc6` | 61093a21f5e63dedf54222b3c09997e54c0fe43e3851d21386e02ddcbc246d49 |
| `pacifico/OFL.txt` | Pacifico | license | `ofl/pacifico/OFL.txt` | SIL OFL 1.1 | `33864cc8de4dd3ea8f3daab1154be28577918bae` | a47e5daeda73568969395c656823102678f2eefb0d7d7ecb47aac4cc17e42204 |
| `pacifico/Pacifico-Regular.ttf` | Pacifico | 400 | `ofl/pacifico/Pacifico-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `27765d02ac13f241b1e207dbda7b2dd1b68280da` | 5b6c0d5334a7bf77dea52b975c5a0c408878c0f7115ed5b6fb151f634b7bf701 |
| `creepster/Creepster-Regular.ttf` | Creepster | 400 | `ofl/creepster/Creepster-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `668c59cf43b545294b1fd778ee0cd0f4e18cd02c` | 402aeb734586c74aecd3dbdc454589b1fb12e2e1c71f782fd019ae68066d9f44 |
| `creepster/OFL.txt` | Creepster | license | `ofl/creepster/OFL.txt` | SIL OFL 1.1 | `92bc9bee87bafcb1a3ccc6854ba98683411ae7e0` | c09e3b81d9cfeec46bc29a3374000e8ad013d694bdf24a288088491dd4cd5c69 |
| `poppins/OFL.txt` | Poppins | license | `ofl/poppins/OFL.txt` | SIL OFL 1.1 | `76df3b565672e3248a5715339d092d4cb6c75019` | 6be04893d770899a015649c7aa3b582f871b272f8747a92b78b17c3e5c8b2573 |
| `poppins/Poppins-Black.ttf` | Poppins | 900 | `ofl/poppins/Poppins-Black.ttf` | static, wght 900 | `6612e5228fcdeabd840f4ca17de889ca447e22f4` | d82aaaf98a9283f9a8edd24e51173337d8eaf09e25cd3d98831f8ec8461748a1 |
| `poppins/Poppins-Bold.ttf` | Poppins | 700 | `ofl/poppins/Poppins-Bold.ttf` | static, wght 700 | `1982f38ab21303459aa1155265052ca599fa58d1` | 983676516167748b74de6f4771fb384c664fd913acb8b471122ecacf5da5ea6c |
| `lato/Lato-Black.ttf` | Lato | 900 | `ofl/lato/Lato-Black.ttf` | static, wght 900 | `53a31ac8f56e9e3f67d35844f2b41ef9ade0e156` | 808c62839c62dbce7de689af7603666fc7f8b81e0df537d8a5212c87580d4337 |
| `lato/OFL.txt` | Lato | license | `ofl/lato/OFL.txt` | SIL OFL 1.1 | `98383e3d835a5c5aea988a74b569193a30dcbcec` | 74ba064d03f1f1c4a952da936c3eb71866c34404916734de3cae73b34357e59e |
| `bangers/Bangers-Regular.ttf` | Bangers | 400 | `ofl/bangers/Bangers-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `9b0f8c1f1b45e7bb4971b7d6a8604f9aacacdd32` | 4160a7311de9342674cce9160cde9fcbb30f48190397d86ff1b70b455af65824 |
| `bangers/OFL.txt` | Bangers | license | `ofl/bangers/OFL.txt` | SIL OFL 1.1 | `bf717d4be410cf7616b9bd345b40ab4817a26e6f` | 630dd5a307c0657b094d324e069b390edf90182fe5003639e06c02a0e5769af7 |
| `permanentmarker/LICENSE.txt` | Permanent Marker | license | `apache/permanentmarker/LICENSE.txt` | Apache-2.0 | `d645695673349e3947e8e5ae42332d0ac3164cd7` | cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30 |
| `permanentmarker/PermanentMarker-Regular.ttf` | Permanent Marker | 400 | `apache/permanentmarker/PermanentMarker-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `3218fc5b3d395c3a3fc207fdf17a27bb32af3242` | 28f82c8a7943cb8e9d599f8554da1d4fc75dbcf69b9885ad6c0611d20c6946c5 |
| `bungeeshade/BungeeShade-Regular.ttf` | Bungee Shade | 400 | `ofl/bungeeshade/BungeeShade-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `80ada80a0404713e7cd31a6b7982ff7a549acdfe` | 665c626082682b462f4e696308eafa7071c114da8f8ff3fb73a289c7ff7df482 |
| `bungeeshade/OFL.txt` | Bungee Shade | license | `ofl/bungeeshade/OFL.txt` | SIL OFL 1.1 | `6f47072f01b8698301896b247b7087c023a4da2d` | d5787a50dde5be6c6daecab9ed459939b1bc37cff0d0e00257eaf1ec2cf4c16c |
| `rubikmonoone/OFL.txt` | Rubik Mono One | license | `ofl/rubikmonoone/OFL.txt` | SIL OFL 1.1 | `7e7e709034ebad08173011538f60ccbd9ce3ce07` | 43d530580461a574f6dfed9e15af6a74e95f7c04d9bfa1174a63ff036e8eee07 |
| `rubikmonoone/RubikMonoOne-Regular.ttf` | Rubik Mono One | 400 | `ofl/rubikmonoone/RubikMonoOne-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `1923947b912685017b6f8ddcca5766f322d89c29` | b22624b6a36e543e942106edfccd198c50b57b65b924bfe02a6f0a5699429e6b |
| `pressstart2p/OFL.txt` | Press Start 2P | license | `ofl/pressstart2p/OFL.txt` | SIL OFL 1.1 | `22796dfe6e3784d69d255b0bf253e2b16f3281de` | 705960c3281a5765ecc0b59bd4ed7ca59eed165748076bc2fc3e8fdbfeb944b0 |
| `pressstart2p/PressStart2P-Regular.ttf` | Press Start 2P | 400 | `ofl/pressstart2p/PressStart2P-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `39adf42efa597906e53be689474ac82214112124` | 034c77f1f05ec89421e4a63f0e3a4ca1ecf852cc6d2bf611f126f275728e017d |
| `righteous/OFL.txt` | Righteous | license | `ofl/righteous/OFL.txt` | SIL OFL 1.1 | `22f3d202eb01975fbcd48a25f3ad82ab624986cb` | 1c5bc055869d5e9151ec7774082727db720ec4293c65be67f91a4bdcaeb02998 |
| `righteous/Righteous-Regular.ttf` | Righteous | 400 | `ofl/righteous/Righteous-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `07fc0b45132b1d7039c202e6d09c91b1a137ec26` | 2ffb3fe5c27d7e6571210b800448c4e234e651b46c6b4426c1bb567e5341348a |
| `orbitron/OFL.txt` | Orbitron | license | `ofl/orbitron/OFL.txt` | SIL OFL 1.1 | `91ede459e18c22d3f2ba7c27d925b49cc945abe4` | ab609b0e110d622435ff337cdf233288556e011bbf9bd0550be98846c0630819 |
| `orbitron/Orbitron[wght].ttf` | Orbitron | 700, 900 | `ofl/orbitron/Orbitron[wght].ttf` | variable, wght 400-900; served as the 700 and 900 faces | `1fc54ce7116a6ec549f37358466bcc92f9a72c33` | f42db2dd16e642258e35782916eceb1dcdbea06fb958d77ad71dc5963587e8fd |
| `spacegrotesk/OFL.txt` | Space Grotesk | license | `ofl/spacegrotesk/OFL.txt` | SIL OFL 1.1 | `cb512b9af44ff61e75e1aad387b7424cdfab36a3` | 564ce565c371c5e5bbf286006565a7c9aa55a9f56e7ca58d56e05d649dd61a72 |
| `spacegrotesk/SpaceGrotesk[wght].ttf` | Space Grotesk | 400, 500, 600, 700 | `ofl/spacegrotesk/SpaceGrotesk[wght].ttf` | variable, wght 300-700 (UI) | `a1b2e6c26093066510a31147e7aec9abdc8d6c5e` | acad6de1fc93436f5c0f1f4137751ef04f1aea3063e7036535970ffcfbd79f72 |
| `inter/Inter[opsz,wght].ttf` | Inter | 400, 500, 600, 700 | `ofl/inter/Inter[opsz,wght].ttf` | variable, opsz 14-32, wght 100-900 (UI) | `047c92f6e2212473dc436020afed689527076d44` | 29160a80ff49ddcab2c97711247e08b1fab27a484a329ce8b813d820dc559031 |
| `inter/OFL.txt` | Inter | license | `ofl/inter/OFL.txt` | SIL OFL 1.1 | `21f6aff961064c2e429f570995e446bcdd555422` | 5b9321a4298cfeb6b34354164a1c3afc3db114569984c502b9b35d988fd58c57 |
| `jetbrainsmono/JetBrainsMono[wght].ttf` | JetBrains Mono | 400, 500, 600 | `ofl/jetbrainsmono/JetBrainsMono[wght].ttf` | variable, wght 100-800 (UI) | `aa310be8b717fe3774f9444dd89d5f4101cc6d10` | 48715a42ec242c21e9f02692891e147d022299a52e48d5e413e1a942193ffeda |
| `jetbrainsmono/OFL.txt` | JetBrains Mono | license | `ofl/jetbrainsmono/OFL.txt` | SIL OFL 1.1 | `821a3dac22aff15a1f1c9689a1d79c45bb58ca39` | b2fe5e8987594e9ffd1d2ca52a2f5d73eb8335243893c5d6254b5ad69269591d |
````

- [ ] **Step 7: Run the test and see it pass**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~VendoredFontsTests"`
Expected: `Passed: 5, Failed: 0`.

Then run the whole suite: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 97, Failed: 0, Skipped: 0` (M1's 92 plus 5), including all 30 `GoldenRenderTests`. Parity.Tests `Passed: 13, Failed: 0`.

- [ ] **Step 8: Commit**

End the message with your own model's `Co-Authored-By` trailer (Global Constraints); every commit in this plan does.

```bash
git add .gitignore .gitattributes tools/fonts/fetch-google-fonts.sh assets/fonts src/RBX15.Engine.Tests/VendoredFontsTests.cs
git status --porcelain -- assets/fonts | wc -l
git commit -m "chore(fonts): vendor the 15 Word Art and 3 UI font families at google/fonts@6692879"
```

Expected: the `wc -l` prints `38` (37 font and license files plus the README, all staged as `A`).

- [ ] **Step 9: Prove git stored the bytes unchanged**

Run:

```bash
git cat-file -p HEAD:assets/fonts/anton/OFL.txt | sha256sum
git cat-file -p HEAD:assets/fonts/dancingscript/'DancingScript[wght].ttf' | sha256sum
```

Expected: `ee67e6ee22790b7929f1a3769ca2801d565c64b5a9096942c1adf5596de9c9e4  -` and `21808625578fe8d8cd10cb684be546dca077b27cd03a53a2f1ec11dc743c924c  -`.

- [ ] **Step 10: Log the pin**

Log with `manage_decisions log`: project `6KpHItSNg9NYHkJg9thn`, origin `Dunder · Claude Code`, category `architecture`. If the implementer has no dashboard tool, hand this text to the controller instead. The record should say:

- the Word Art and UI fonts are vendored from google/fonts@66928797f271fed5ee428544e7229685e2d90127 into `assets/fonts`, and goldens v2 and the v5 bundle both use these files
- Permanent Marker is Apache-2.0
- variable files serve Dancing Script 700 and Orbitron 700/900
- `.gitignore` now re-includes `/assets/`

---

### Task 2: App-driven reference renderer

**Files:**

- Create: `tools/parity-ref/fonts.js`, `tools/parity-ref/app-harness.js`, `tools/parity-ref/app-render.js`, `tools/parity-ref/app-render.test.js`

**Interfaces:**

- Consumes:
  - The real `editor.html`, `editor.js`, `styles.css` and `lib/*.js`, read-only and loaded as-is.
  - `assets/fonts` and its manifest (Task 1).
  - `node_modules/electron` 41.1.0 and `@napi-rs/canvas` (devDependencies).
  - `tests/goldens/render/scenes.json` and the M0 PNGs, used as a cross-check.
- Produces:
  - `fonts.js` (plain Node, unit-testable):
    - `FACES: Record<family, Record<weight, assetPath>>`
    - `WORDART_FAMILIES: string[]` (the 15 `waFont` options in order)
    - `facesFor(url) -> {family, weight, file}[]`, which throws on any face that has no vendored file
    - `cssFor(url) -> string`
    - `urlFor(file) -> string`
    - `fileFor(url) -> string | null`
    - `respond(url) -> {body, headers} | null`
    - `wordArtFaces() -> {family, weight}[]`
    - `FONTS_DIR`
  - `app-harness.js`: a page script that defines `window.__pref` with:
    - `render(scene, assets) -> Promise<dataUrl>`
    - `save(scene, assets) -> Promise<jsonText>`
    - `selfCheck(faces, dancingUrl) -> Promise<number>`
  - `app-render.js`, run as Electron main:
    - `electron tools/parity-ref/app-render.js --scenes <file> --out <dir> [--only a,b]` writes `<dir>/<name>.png` plus a merged `<dir>/index.json`.
    - `electron tools/parity-ref/app-render.js --save-projects <file> --out-dir <dir> [--only a,b]` writes `<dir>/<name>.r15proj` (the text 4.1's `saveProject` produced) plus a merged `<dir>/index.json`.
    - Both print `fonts ok: 17 word art faces` first, then `rendered <name>` or `saved <name>` once per scene. Any error exits 1.
  - **Scene format v2** (contract): the M1 fields, applied first, plus an optional `actions` array. The ops are `shape`, `erase`, `detail`, `wordart` and `project`. The renderer refuses any other op, including a pen stroke (pen strokes are not frozen; approved divergence).

**How each op drives 4.1 (the cites this code relies on):**

- **Base fields** are applied as if the scene were the app's own state:
  - `switchMode(mode)` (`editor.js:1695-1717`), only when the mode differs from Shirt.
  - `bgColor`, `bgOpacity` and `bgImage` globals (`editor.js:78-80`).
  - `regionColors`, with the region buttons repainted the way `loadProject` does (`editor.js:2615-2618`).
  - Layers via `makeLayer(img, 'L'+n, x, y, scale, overrides)` (`editor.js:134-142`), on a fresh page where `nextId` starts at 1 (`editor.js:499`). Layer ids are therefore 1..n, matching `SceneLoader` (`Id = i`).
- **Mouse.** All three handlers read `rect = canvas.getBoundingClientRect(); mx = (clientX - rect.left) / zoom` (`editor.js:1059-1061`, `1150-1152`). The harness pins `#mainCanvas` at the viewport origin, checks that `rect.left` and `rect.top` are integers, and sends `clientX = rect.left + x * zoom` with `zoom = 1` (`editor.js:81`). It also checks the constructed `MouseEvent` kept the exact coordinate. Template points must be integers.
- **`shape`:**
  - Sets the tool with `setTool` (`editor.js:473-478`), using the tool ids from `editor.js:464-471`: `line` maps to `line`, `rect` to `rect`, and `ellipse` to `circle`.
  - Sets `#drawColor`, `#drawSize` and `#drawFill` through their own listeners (`editor.js:492-494`). `#drawSize` is a range input, 1..50 (`editor.html:57`).
  - Sends `mousedown` at `from`, one `mousemove` at `to`, and `mouseup`. The mousemove handler clears the canvas and redraws the shape from the start point to the pointer (`editor.js:1204-1233`); mouseup commits the shape through `addDrawingLayer` (`editor.js:1285`, `528-540`), which is asynchronous, so the harness waits for the new layer.
- **`erase`:**
  - Selects the base layer by id with `selectLayer` (`editor.js:575-580`), then `setTool('eraser')`, then sets `#drawSize`.
  - Sends `mousedown` at `points[0]` and one `mousemove` per remaining point. Each mousemove runs `lineTo` and strokes the whole path again (`editor.js:1200-1203`); the eraser gets no extra stroke at mouseup (`editor.js:1253-1255` strokes only for the pen).
  - The erase itself runs through `applyEraseStroke`, and the harness waits for the asynchronous swap of `sel.img` (`editor.js:1265-1283`, `lib/erase.js:43-73`).
  - At least two points are required: a lone click strokes nothing.
- **`detail`:** sets `#detailColor`, `#detailWidth` (range 1..8) and `#detailDash` (`editor.html:179-182`), then clicks the real `.detail-btn[data-detail=<type>]`. That button calls `createDetailLayer(names[type], gen)` (`editor.js:2077-2103`, `1834-1865`). Shirt mode only: every generator throws in Pants mode (ledger `details-pants-mode-behavior`).
- **`wordart`:**
  - Sets the fields present on the op through their real inputs: `#waText`, `#waFont`, `#waColor` (fill), `#waStroke`, `#waColor2` (grad), `#waSize`, `#waStrokeW` (range 0..20) and `#waAngle` (range 0..360) (`editor.html:229-262`).
  - Clicks the real `.wordart-style-btn` (`editor.js:2111-2118`).
  - Awaits `document.fonts.load('900 <size>px "<family>"')` and asserts it loaded vendored faces. The canvas fetches fonts lazily, so without this step 4.1 can draw the fallback font (ledger `wordart-font-loading-race`).
  - Clicks `#btnAddWordArt` (`editor.js:2257-2291`). A field left off the op keeps its `editor.html` default.
- **`project`:** puts a real `File` on `#projectFileInput` and fires `change` (`editor.js:2684-2687`), which runs `loadProject` (`editor.js:2593-2680`). The harness then waits for the new layer array and the background image.
- **Export:** stubs `window.confirm` to return true (`editor.js:1504-1506`), intercepts `HTMLAnchorElement.prototype.click`, and calls the real `exportPNG()`. It returns `link.href` (`editor.js:1535-1539`).
- **Save:** intercepts `URL.createObjectURL` to keep the `Blob` and the anchor click, then calls the real `saveProject()` (`editor.js:2550-2591`). The blob is read before `revokeObjectURL` matters.
- **Fonts:** `protocol.handle('https')` answers `fonts.googleapis.com/css2` with `@font-face` rules for exactly the requested faces, pointing at `https://fonts.gstatic.com/rbx15-vendored/<file>`. It answers those URLs with the vendored bytes and `Access-Control-Allow-Origin: *` (the page is `file://`, and font fetches are CORS). Any other http(s) request is answered 404 and fails the run. `window.alert` is recorded, and any alert fails the scene, because a blocking dialog would hang the hidden window.

**Environment gotcha:** this machine's sessions may set `ELECTRON_RUN_AS_NODE`. Every spawn deletes it from the child environment, as the tests do.

- [ ] **Step 1: Write the failing tests**

Create `tools/parity-ref/app-render.test.js`:

```js
// node --test tools/parity-ref/app-render.test.js
// Guards the app-driven reference renderer: every font 4.1 asks Google for resolves to a vendored
// file, the real app agrees with the M0 goldens, the ops drive the real handlers, and a project
// round-trips through 4.1's own saveProject and loadProject.
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { loadImage, createCanvas } = require('@napi-rs/canvas');
const fonts = require('./fonts.js');

const ROOT = path.resolve(__dirname, '..', '..');
const ELECTRON = require('electron'); // in plain Node this is the path to electron.exe

function appRender(args) {
  const env = { ...process.env };
  delete env.ELECTRON_RUN_AS_NODE;
  return spawnSync(ELECTRON, [path.join(__dirname, 'app-render.js'), ...args], { env, encoding: 'utf8', timeout: 300000 });
}

function renderOk(scenes, dir, extra = []) {
  const file = path.join(dir, 'in-scenes.json');
  fs.writeFileSync(file, JSON.stringify(scenes));
  const r = appRender(['--scenes', file, '--out', dir, ...extra]);
  assert.strictEqual(r.status, 0, `app-render failed:\n${r.stdout}\n${r.stderr}`);
  return r.stdout;
}

async function pixels(file) {
  const img = await loadImage(fs.readFileSync(file));
  const c = createCanvas(img.width, img.height);
  const ctx = c.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return Buffer.from(ctx.getImageData(0, 0, img.width, img.height).data);
}

async function pixel(file, x, y) {
  const img = await loadImage(fs.readFileSync(file));
  const c = createCanvas(img.width, img.height);
  const ctx = c.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return Array.from(ctx.getImageData(x, y, 1, 1).data);
}

const tmp = (prefix) => fs.mkdtempSync(path.join(os.tmpdir(), prefix));
const label = (faces) => faces.map((f) => `${f.family} ${f.weight}`);

test('fonts: every face editor.html and styles.css request maps to a vendored file', () => {
  const html = fs.readFileSync(path.join(ROOT, 'editor.html'), 'utf8');
  const css = fs.readFileSync(path.join(ROOT, 'styles.css'), 'utf8');
  const wordArtUrl = /href="(https:\/\/fonts\.googleapis\.com\/css2\?[^"]+)"/.exec(html)[1].replace(/&amp;/g, '&');
  const uiUrl = /@import url\('(https:\/\/fonts\.googleapis\.com\/css2\?[^']+)'\)/.exec(css)[1];
  assert.deepStrictEqual(label(fonts.facesFor(wordArtUrl)), [
    'Anton 400', 'Bebas Neue 400', 'Dancing Script 700', 'Great Vibes 400', 'Pacifico 400', 'Creepster 400',
    'Poppins 700', 'Poppins 900', 'Lato 900', 'Bangers 400', 'Permanent Marker 400', 'Bungee Shade 400',
    'Rubik Mono One 400', 'Press Start 2P 400', 'Righteous 400', 'Orbitron 700', 'Orbitron 900',
  ]);
  assert.deepStrictEqual(label(fonts.facesFor(uiUrl)), [
    'Space Grotesk 400', 'Space Grotesk 500', 'Space Grotesk 600', 'Space Grotesk 700',
    'JetBrains Mono 400', 'JetBrains Mono 500', 'JetBrains Mono 600',
    'Inter 400', 'Inter 500', 'Inter 600', 'Inter 700',
  ]);
  for (const f of [...fonts.facesFor(wordArtUrl), ...fonts.facesFor(uiUrl)]) {
    assert.ok(fs.existsSync(path.join(fonts.FONTS_DIR, ...f.file.split('/'))), f.file);
  }
  const select = /<select id="waFont"[\s\S]*?<\/select>/.exec(html)[0];
  assert.deepStrictEqual(fonts.WORDART_FAMILIES, [...select.matchAll(/value="([^"]+)"/g)].map((m) => m[1]));
  assert.strictEqual(fonts.wordArtFaces().length, 17);
});

test('fonts: a face with no vendored file is refused, never served from the network', () => {
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Comic+Sans'), /no vendored face for Comic Sans 400/);
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Anton:wght@700'), /no vendored face for Anton 700/);
  assert.throws(() => fonts.facesFor('https://fonts.googleapis.com/css2?family=Anton:ital,wght@1,400'), /unsupported axis spec/);
  assert.throws(() => fonts.fileFor('https://fonts.gstatic.com/rbx15-vendored/anton/evil.ttf'), /not a vendored font/);
  assert.strictEqual(fonts.respond('https://example.com/x'), null);
});

test('fonts: cssFor emits one @font-face per requested face, with percent-encoded file urls', () => {
  const css = fonts.cssFor('https://fonts.googleapis.com/css2?family=Dancing+Script:wght@700&display=swap');
  assert.match(css, /font-family: 'Dancing Script';/);
  assert.match(css, /font-weight: 700;/);
  assert.match(css, /src: url\(https:\/\/fonts\.gstatic\.com\/rbx15-vendored\/dancingscript\/DancingScript%5Bwght%5D\.ttf\) format\('truetype'\);/);
  assert.strictEqual((css.match(/@font-face/g) || []).length, 1);
  const url = fonts.urlFor('dancingscript/DancingScript[wght].ttf');
  assert.strictEqual(fonts.fileFor(url), path.join(fonts.FONTS_DIR, 'dancingscript', 'DancingScript[wght].ttf'));
});

test('fonts: respond serves css and font bytes with CORS for the file:// page', () => {
  const css = fonts.respond('https://fonts.googleapis.com/css2?family=Anton&display=swap');
  assert.strictEqual(css.headers['access-control-allow-origin'], '*');
  assert.match(css.headers['content-type'], /^text\/css/);
  const ttf = fonts.respond(fonts.urlFor('anton/Anton-Regular.ttf'));
  assert.strictEqual(ttf.headers['content-type'], 'font/ttf');
  assert.ok(ttf.body.equals(fs.readFileSync(path.join(fonts.FONTS_DIR, 'anton', 'Anton-Regular.ttf'))));
});

test('the real app agrees with the frozen M0 goldens on base scenes', async () => {
  const all = JSON.parse(fs.readFileSync(path.join(ROOT, 'tests', 'goldens', 'render', 'scenes.json'), 'utf8'));
  const names = ['fill-linear-0-torso', 'layer-rotate-scale-fliph-opacity', 'rgb-shift', 'full-scene'];
  const dir = tmp('pref-app-m0-');
  const out = renderOk(all.filter((s) => names.includes(s.name)), dir);
  assert.match(out, /fonts ok: 17 word art faces/);
  for (const n of names) {
    const same = (await pixels(path.join(dir, n + '.png'))).equals(await pixels(path.join(ROOT, 'tests', 'goldens', 'render', n + '.png')));
    assert.ok(same, `${n}: app-driven export differs from the M0 golden`);
  }
});

test('shape and erase ops drive the real canvas handlers, deterministically', async () => {
  const scene = {
    name: 'probe-ops', bgColor: '#ff00ff',
    layers: [{ image: 'tests/goldens/render/bg-default.png', x: 0, y: 0 }],
    actions: [
      { op: 'erase', layer: 0, points: [[260, 138], [330, 138]], size: 20 },
      { op: 'shape', tool: 'line', from: [240, 100], to: [350, 100], color: '#ff0000', width: 9, fill: false },
    ],
  };
  const a = tmp('pref-ops-a-');
  const b = tmp('pref-ops-b-');
  renderOk([scene], a);
  renderOk([scene], b);
  const file = path.join(a, 'probe-ops.png');
  assert.deepStrictEqual(fs.readFileSync(file), fs.readFileSync(path.join(b, 'probe-ops.png')));
  // The 9px line covers rows 95.5-104.5, so row 100 is fully covered: opaque drawColor
  // (editor.js:1207-1216), committed at scale 1 on integer coordinates by autocrop (editor.js:533).
  assert.deepStrictEqual(await pixel(file, 295, 100), [255, 0, 0, 255]);
  // The 20px erase stroke covers rows 128-148 at identity (layer at 0,0, scale 1, lib/erase.js:43-73),
  // so the layer is gone there and the #ff00ff background shows.
  assert.deepStrictEqual(await pixel(file, 295, 138), [255, 0, 255, 255]);
  // Untouched layer pixels keep bg-default.png's navy, #192e44 (editor.js:78).
  assert.deepStrictEqual(await pixel(file, 295, 180), [25, 46, 68, 255]);
});

test('detail and word art ops build layers through the real buttons, deterministically', () => {
  const scene = {
    name: 'probe-detail-wordart',
    actions: [
      { op: 'detail', type: 'collar-v', color: '#ffffff', width: 4, stitch: true },
      { op: 'wordart', text: 'RIZZ', font: 'Dancing Script', style: 'neon', size: 64, fill: '#17d4fa', stroke: '#000000', grad: '#f22f89', strokeWidth: 3, angle: 0 },
    ],
  };
  const a = tmp('pref-dw-a-');
  const b = tmp('pref-dw-b-');
  assert.match(renderOk([scene], a), /rendered probe-detail-wordart/);
  renderOk([scene], b);
  assert.deepStrictEqual(fs.readFileSync(path.join(a, 'probe-detail-wordart.png')), fs.readFileSync(path.join(b, 'probe-detail-wordart.png')));
});

test('a project round-trips through the real saveProject and loadProject', async () => {
  const source = {
    name: 'probe-roundtrip', bgColor: '#0f1f31',
    regionColors: { torso_front: { type: 'linear', c1: '#ff0000', c2: '#0000ff', angle: 30 }, torso_back: '#ff8800' },
    layers: [{ image: 'packs/word_art/word_626_anton.png', x: 240, y: 100, scale: 0.6, rotation: 12, brightness: 130, rShift: 20 }],
  };
  const projDir = tmp('pref-proj-');
  const scenesFile = path.join(projDir, 'in-projects.json');
  fs.writeFileSync(scenesFile, JSON.stringify([source]));
  const saved = appRender(['--save-projects', scenesFile, '--out-dir', projDir]);
  assert.strictEqual(saved.status, 0, `save failed:\n${saved.stdout}\n${saved.stderr}`);
  assert.match(saved.stdout, /saved probe-roundtrip/);
  const projFile = path.join(projDir, 'probe-roundtrip.r15proj');
  const project = JSON.parse(fs.readFileSync(projFile, 'utf8'));
  assert.strictEqual(project.version, '4.0'); // editor.js:2552
  assert.strictEqual(project.mode, 'Shirt');
  assert.strictEqual(project.regionColors.torso_back, '#ff8800'); // legacy string kept verbatim, editor.js:2559
  assert.strictEqual(project.layers.length, 1);
  assert.strictEqual(project.layers[0].rShift, 20);
  assert.match(project.layers[0].imgSrc, /^data:image\/png;base64,/);

  const dir = tmp('pref-reload-');
  renderOk([source, { name: 'probe-reloaded', actions: [{ op: 'project', file: projFile }] }], dir);
  assert.ok((await pixels(path.join(dir, 'probe-roundtrip.png'))).equals(await pixels(path.join(dir, 'probe-reloaded.png'))),
    'the reloaded project must export the same pixels as the scene that saved it');
});

test('a failing action stops the run with the scene, the action index and the op', () => {
  for (const [scene, pattern] of [
    [{ name: 'probe-bad-erase', actions: [{ op: 'erase', layer: 0, points: [[1, 1], [2, 2]], size: 5 }] }, /probe-bad-erase action 0 \(erase\): scene has no layer 0/],
    [{ name: 'probe-pen', actions: [{ op: 'pen', points: [[1, 1], [2, 2]] }] }, /probe-pen action 0 \(pen\): unknown op "pen"/],
    [{ name: 'probe-clamped', actions: [{ op: 'shape', tool: 'line', from: [1, 1], to: [9, 9], color: '#ffffff', width: 60, fill: false }] }, /#drawSize refused 60/],
  ]) {
    const dir = tmp('pref-bad-');
    const file = path.join(dir, 'in-scenes.json');
    fs.writeFileSync(file, JSON.stringify([scene]));
    const r = appRender(['--scenes', file, '--out', dir]);
    assert.strictEqual(r.status, 1, `${scene.name} should fail:\n${r.stdout}\n${r.stderr}`);
    assert.match(r.stderr, pattern);
  }
});
```

- [ ] **Step 2: Run them and see them fail**

Run: `node --test tools/parity-ref/app-render.test.js`
Expected: FAIL. The file aborts with `Error: Cannot find module './fonts.js'`.

- [ ] **Step 3: Implement the font map**

Create `tools/parity-ref/fonts.js`:

```js
// Maps the Google Fonts css2 requests 4.1 makes (editor.html:11 for Word Art, styles.css:6 for the
// UI) to the vendored files in assets/fonts (pinned by assets/fonts/README.md). app-render.js
// answers fonts.googleapis.com and fonts.gstatic.com from here, so goldens are drawn with exactly
// these bytes and never with whatever Google serves today. A face 4.1 requests that has no vendored
// file is an error, not a network fetch.
const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..', '..');
const FONTS_DIR = path.join(ROOT, 'assets', 'fonts');
const HOST = 'https://fonts.gstatic.com/rbx15-vendored/';

// family -> requested weight -> path under assets/fonts. The weights are exactly the ones 4.1
// requests; a variable file serves several weights and Chromium sets its wght axis to each.
const FACES = {
  'Anton': { 400: 'anton/Anton-Regular.ttf' },
  'Bebas Neue': { 400: 'bebasneue/BebasNeue-Regular.ttf' },
  'Dancing Script': { 700: 'dancingscript/DancingScript[wght].ttf' },
  'Great Vibes': { 400: 'greatvibes/GreatVibes-Regular.ttf' },
  'Pacifico': { 400: 'pacifico/Pacifico-Regular.ttf' },
  'Creepster': { 400: 'creepster/Creepster-Regular.ttf' },
  'Poppins': { 700: 'poppins/Poppins-Bold.ttf', 900: 'poppins/Poppins-Black.ttf' },
  'Lato': { 900: 'lato/Lato-Black.ttf' },
  'Bangers': { 400: 'bangers/Bangers-Regular.ttf' },
  'Permanent Marker': { 400: 'permanentmarker/PermanentMarker-Regular.ttf' },
  'Bungee Shade': { 400: 'bungeeshade/BungeeShade-Regular.ttf' },
  'Rubik Mono One': { 400: 'rubikmonoone/RubikMonoOne-Regular.ttf' },
  'Press Start 2P': { 400: 'pressstart2p/PressStart2P-Regular.ttf' },
  'Righteous': { 400: 'righteous/Righteous-Regular.ttf' },
  'Orbitron': { 700: 'orbitron/Orbitron[wght].ttf', 900: 'orbitron/Orbitron[wght].ttf' },
  'Space Grotesk': {
    400: 'spacegrotesk/SpaceGrotesk[wght].ttf', 500: 'spacegrotesk/SpaceGrotesk[wght].ttf',
    600: 'spacegrotesk/SpaceGrotesk[wght].ttf', 700: 'spacegrotesk/SpaceGrotesk[wght].ttf',
  },
  'JetBrains Mono': {
    400: 'jetbrainsmono/JetBrainsMono[wght].ttf', 500: 'jetbrainsmono/JetBrainsMono[wght].ttf',
    600: 'jetbrainsmono/JetBrainsMono[wght].ttf',
  },
  'Inter': {
    400: 'inter/Inter[opsz,wght].ttf', 500: 'inter/Inter[opsz,wght].ttf',
    600: 'inter/Inter[opsz,wght].ttf', 700: 'inter/Inter[opsz,wght].ttf',
  },
};

// The <select id="waFont"> options, in order (editor.html:231-245).
const WORDART_FAMILIES = ['Anton', 'Bebas Neue', 'Dancing Script', 'Great Vibes', 'Pacifico', 'Creepster',
  'Poppins', 'Lato', 'Bangers', 'Permanent Marker', 'Bungee Shade', 'Rubik Mono One', 'Press Start 2P',
  'Righteous', 'Orbitron'];

const VENDORED = new Set(Object.values(FACES).flatMap((w) => Object.values(w)));

function facesFor(url) {
  const u = new URL(url);
  if (u.origin !== 'https://fonts.googleapis.com' || u.pathname !== '/css2') throw new Error(`not a Google Fonts css2 url: ${url}`);
  const out = [];
  for (const spec of u.searchParams.getAll('family')) {
    const [family, axes] = spec.split(':');
    let weights = [400];
    if (axes !== undefined) {
      const m = /^wght@(\d+(?:;\d+)*)$/.exec(axes);
      if (!m) throw new Error(`unsupported axis spec "${axes}" for ${family}`);
      weights = m[1].split(';').map(Number);
    }
    for (const weight of weights) {
      const file = FACES[family] && FACES[family][weight];
      if (!file) throw new Error(`no vendored face for ${family} ${weight}`);
      out.push({ family, weight, file });
    }
  }
  return out;
}

const urlFor = (file) => HOST + file.split('/').map(encodeURIComponent).join('/');

function cssFor(url) {
  return facesFor(url).map((f) => [
    '@font-face {',
    `  font-family: '${f.family}';`,
    '  font-style: normal;',
    `  font-weight: ${f.weight};`,
    '  font-display: swap;',
    `  src: url(${urlFor(f.file)}) format('truetype');`,
    '}',
    '',
  ].join('\n')).join('');
}

function fileFor(url) {
  if (!url.startsWith(HOST)) return null;
  const rel = new URL(url).pathname.slice('/rbx15-vendored/'.length).split('/').map(decodeURIComponent).join('/');
  if (!VENDORED.has(rel)) throw new Error(`not a vendored font: ${rel}`);
  return path.join(FONTS_DIR, ...rel.split('/'));
}

function respond(url) {
  const u = new URL(url);
  if (u.hostname === 'fonts.googleapis.com') {
    return { body: cssFor(url), headers: { 'content-type': 'text/css; charset=utf-8', 'access-control-allow-origin': '*' } };
  }
  const file = fileFor(url);
  if (file) return { body: fs.readFileSync(file), headers: { 'content-type': 'font/ttf', 'access-control-allow-origin': '*' } };
  return null;
}

const wordArtFaces = () => WORDART_FAMILIES.flatMap((family) => Object.keys(FACES[family]).map((w) => ({ family, weight: Number(w) })));

module.exports = { FACES, WORDART_FAMILIES, FONTS_DIR, HOST, facesFor, cssFor, urlFor, fileFor, respond, wordArtFaces };
```

- [ ] **Step 4: Run the font tests**

Run: `node --test --test-name-pattern="^fonts:" tools/parity-ref/app-render.test.js`
Expected: `# pass 4`, `# fail 0`: the 4 `fonts:` tests pass, and the other 5 are filtered out by the name pattern.

- [ ] **Step 5: Implement the page harness**

Create `tools/parity-ref/app-harness.js`:

```js
// Injected by app-render.js into the REAL editor.html after it loads. It drives 4.1 the way a
// person does: the page's own globals (switchMode, makeLayer, setTool, selectLayer, exportPNG,
// saveProject), its real inputs and buttons, and synthetic mouse events on #mainCanvas. Nothing in
// editor.js is replaced. window.alert and window.confirm are stubbed and anchor clicks intercepted,
// so a hidden window never blocks and downloads are captured instead of written.
(function () {
  'use strict';
  if (window.__pref) return;

  const alerts = [];
  window.alert = (message) => { alerts.push(String(message)); };
  window.confirm = () => true; // exportPNG's empty-template guard, editor.js:1504-1506

  // Pin the canvas at the viewport origin so synthesized client coordinates are integers. The
  // handlers map them back with (clientX - rect.left) / zoom (editor.js:1059-1061, 1150-1152).
  const pin = document.createElement('style');
  pin.textContent = '#mainCanvas { position: fixed !important; left: 0 !important; top: 0 !important; margin: 0 !important; }';
  document.head.appendChild(pin);

  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  async function waitFor(done, what, ms = 15000) {
    const start = performance.now();
    while (!done()) {
      if (alerts.length) throw new Error(`4.1 raised an alert while waiting for ${what}: ${alerts.join(' | ')}`);
      if (performance.now() - start > ms) throw new Error(`timed out waiting for ${what}`);
      await sleep(5);
    }
  }

  function loadImg(src) {
    return new Promise((resolve, reject) => {
      if (!src) { reject(new Error('missing image data')); return; }
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error('image decode failed'));
      img.src = src;
    });
  }

  // Set a real control and fire the event its 4.1 listener uses. The value must stick: range and
  // color inputs sanitize, and a scene that silently clamps would freeze the wrong golden.
  function setInput(id, value, eventName = 'input') {
    const el = byId(id);
    el.value = String(value);
    if (el.value !== String(value)) throw new Error(`#${id} refused ${value} (holds ${JSON.stringify(el.value)})`);
    el.dispatchEvent(new Event(eventName, { bubbles: true }));
  }

  function setCheck(id, on) {
    const el = byId(id);
    el.checked = on;
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function mouse(type, point) {
    const [x, y] = point;
    if (!Number.isInteger(x) || !Number.isInteger(y)) throw new Error(`template point ${JSON.stringify(point)} must be integers`);
    const c = byId('mainCanvas');
    const rect = c.getBoundingClientRect();
    if (!Number.isInteger(rect.left) || !Number.isInteger(rect.top)) throw new Error(`canvas not at integer client coordinates (${rect.left}, ${rect.top})`);
    const clientX = rect.left + x * zoom; // zoom: editor.js:81, 1 unless changed
    const clientY = rect.top + y * zoom;
    const ev = new MouseEvent(type, { clientX, clientY, button: 0, buttons: type === 'mouseup' ? 0 : 1, bubbles: true, cancelable: true, view: window });
    if (ev.clientX !== clientX || ev.clientY !== clientY) throw new Error(`MouseEvent rounded (${clientX}, ${clientY})`);
    c.dispatchEvent(ev);
  }

  // Base scene fields, applied as the app's own state. Defaults: editor.js:76-107 and makeLayer
  // (editor.js:134-142). A fresh page has nextId = 1 (editor.js:499), so layer ids are 1..n.
  async function applyBase(scene, images) {
    const mode = scene.mode || 'Shirt';
    if (mode !== currentMode) switchMode(mode); // editor.js:1695-1717
    bgColor = scene.bgColor ?? '#192e44';
    bgOpacity = scene.bgOpacity ?? 1;
    bgImage = scene.bgImage ? await loadImg(images[scene.bgImage]) : null;
    regionColors = JSON.parse(JSON.stringify(scene.regionColors || {}));
    document.querySelectorAll('.region-btn').forEach((b) => paintRegionButton(b, regionColors[b.dataset.r] || null)); // as editor.js:2615-2618
    layers = [];
    const ids = [];
    for (const [i, d] of (scene.layers || []).entries()) {
      const layer = makeLayer(await loadImg(images[d.image]), 'L' + (i + 1), d.x ?? 0, d.y ?? 0, d.scale ?? 1, {
        visible: d.visible ?? true, opacity: d.opacity ?? 1, rotation: d.rotation ?? 0,
        flipH: !!d.flipH, flipV: !!d.flipV,
        brightness: d.brightness ?? 100, contrast: d.contrast ?? 100, saturate: d.saturate ?? 100,
        hueRotate: d.hueRotate ?? 0, rShift: d.rShift ?? 0, gShift: d.gShift ?? 0, bShift: d.bShift ?? 0,
      });
      layers.push(layer);
      ids.push(layer.id);
    }
    selectedLayerId = null;
    updateUI();
    render();
    return ids;
  }

  const SHAPE_TOOLS = { line: 'line', rect: 'rect', ellipse: 'circle' }; // tool ids, editor.js:464-471

  async function shape(op) {
    const tool = SHAPE_TOOLS[op.tool];
    if (!tool) throw new Error(`unknown shape tool "${op.tool}"`);
    setTool(tool); // editor.js:473-478
    setInput('drawColor', op.color); // editor.js:492
    setInput('drawSize', op.width); // editor.js:493; range 1..50 (editor.html:57)
    setCheck('drawFill', !!op.fill); // editor.js:494
    const before = layers.length;
    mouse('mousedown', op.from); // editor.js:1121-1146
    mouse('mousemove', op.to); // clear and redraw the shape, editor.js:1204-1233
    mouse('mouseup', op.to); // commit via addDrawingLayer, editor.js:1249-1293
    await waitFor(() => layers.length === before + 1, `the ${op.tool} drawing layer (editor.js:528-540)`);
  }

  async function erase(op, baseIds) {
    const id = baseIds[op.layer];
    if (id === undefined) throw new Error(`scene has no layer ${op.layer}`);
    const layer = layers.find((l) => l.id === id);
    if (!layer) throw new Error(`layer ${op.layer} is no longer in the document`);
    if (!Array.isArray(op.points) || op.points.length < 2) {
      throw new Error('erase needs at least 2 points: a lone click strokes nothing (editor.js:1253-1255 strokes at mouseup only for the pen)');
    }
    selectLayer(id); // editor.js:575-580
    setTool('eraser');
    setInput('drawSize', op.size);
    const img = layer.img;
    mouse('mousedown', op.points[0]);
    for (const p of op.points.slice(1)) mouse('mousemove', p); // lineTo + stroke per move, editor.js:1200-1203
    mouse('mouseup', op.points[op.points.length - 1]);
    await waitFor(() => layer.img !== img, `the erased bitmap of layer ${op.layer} (editor.js:1265-1283)`);
  }

  async function detail(op) {
    if (currentMode !== 'Shirt') throw new Error('detail ops run in Shirt mode only: every 4.1 generator throws in Pants mode');
    if (!/^[a-z-]+$/.test(op.type)) throw new Error(`bad detail type "${op.type}"`);
    const btn = document.querySelector(`.detail-btn[data-detail="${op.type}"]`);
    if (!btn) throw new Error(`no detail button for "${op.type}" (editor.html:187-222)`);
    setInput('detailColor', op.color); // read by createDetailLayer, editor.js:1840
    setInput('detailWidth', op.width); // range 1..8, editor.html:181
    setCheck('detailDash', !!op.stitch); // editor.js:1842, 1849
    const before = layers.length;
    btn.click(); // editor.js:2077-2103 -> createDetailLayer, editor.js:1834-1865
    await waitFor(() => layers.length === before + 1, `the ${op.type} detail layer`);
  }

  const WORDART_INPUTS = [['text', 'waText'], ['fill', 'waColor'], ['stroke', 'waStroke'], ['grad', 'waColor2'],
    ['size', 'waSize'], ['strokeWidth', 'waStrokeW'], ['angle', 'waAngle']]; // editor.html:229-262

  async function wordart(op) {
    for (const [key, id] of WORDART_INPUTS) if (op[key] !== undefined) setInput(id, op[key]);
    if (op.font !== undefined) setInput('waFont', op.font, 'change');
    if (op.style !== undefined) {
      const btn = document.querySelector(`.wordart-style-btn[data-style="${op.style}"]`);
      if (!/^[a-z]+$/.test(op.style) || !btn) throw new Error(`unknown word art style "${op.style}"`);
      btn.click(); // sets waStyle, editor.js:2111-2118
    }
    // The canvas fetches a web font lazily, on first use, so 4.1 can draw the fallback font
    // (ledger wordart-font-loading-race). Load the exact face renderWordArt asks for first
    // (editor.js:2125, 2131).
    const family = byId('waFont').value;
    const spec = `900 ${+byId('waSize').value || 64}px "${family}"`;
    const faces = await document.fonts.load(spec);
    await document.fonts.ready;
    if (!faces.length || faces.some((f) => f.status !== 'loaded') || !document.fonts.check(spec)) {
      throw new Error(`vendored font did not load for ${spec}`);
    }
    const before = layers.length;
    byId('btnAddWordArt').click(); // editor.js:2257-2291
    await waitFor(() => layers.length === before + 1, 'the word art layer');
  }

  async function project(op, projects) {
    const p = projects[op.file];
    if (!p) throw new Error(`project file ${op.file} was not provided`);
    const data = JSON.parse(p.text);
    const want = (data.layers || []).filter((d) => d.imgSrc).length;
    if (!want) throw new Error('a project fixture needs at least one image layer, or its load cannot be observed');
    const before = layers;
    const input = byId('projectFileInput');
    const dt = new DataTransfer();
    dt.items.add(new File([p.text], p.name, { type: 'application/json' }));
    input.files = dt.files;
    input.dispatchEvent(new Event('change', { bubbles: true })); // editor.js:2684-2687 -> loadProject, 2593-2680
    await waitFor(() => layers !== before && layers.length === want &&
      (!data.bgImageSrc || (bgImage !== null && bgImage.src === data.bgImageSrc)), `project ${op.file} to finish loading`);
  }

  async function run(scene, assets) {
    alerts.length = 0;
    await document.fonts.ready;
    const ids = await applyBase(scene, assets.images);
    for (const [i, op] of (scene.actions || []).entries()) {
      try {
        if (op.op === 'shape') await shape(op);
        else if (op.op === 'erase') await erase(op, ids);
        else if (op.op === 'detail') await detail(op);
        else if (op.op === 'wordart') await wordart(op);
        else if (op.op === 'project') await project(op, assets.projects);
        else throw new Error(`unknown op "${op.op}"`);
        if (alerts.length) throw new Error(`4.1 raised an alert: ${alerts.join(' | ')}`);
      } catch (e) {
        throw new Error(`${scene.name} action ${i} (${op.op}): ${e.message}`);
      }
    }
  }

  // Run fn and return what the anchor it clicks would have downloaded.
  function captureDownload(fn) {
    const realClick = HTMLAnchorElement.prototype.click;
    let got = null;
    HTMLAnchorElement.prototype.click = function () { got = { href: this.href, download: this.download }; };
    try { fn(); } finally { HTMLAnchorElement.prototype.click = realClick; }
    if (!got) throw new Error('no download link was clicked');
    return got;
  }

  window.__pref = {
    async render(scene, assets) {
      await run(scene, assets);
      const { href } = captureDownload(() => exportPNG()); // editor.js:1500-1540
      if (!href.startsWith('data:image/png;base64,')) throw new Error('exportPNG did not produce a PNG data url');
      return href;
    },

    async save(scene, assets) {
      await run(scene, assets);
      const realCreate = URL.createObjectURL;
      let blob = null;
      URL.createObjectURL = (b) => { blob = b; return realCreate.call(URL, b); };
      try { captureDownload(() => saveProject()); } finally { URL.createObjectURL = realCreate; } // editor.js:2550-2591
      if (!blob) throw new Error('saveProject made no blob');
      return blob.text();
    },

    // Every Word Art face loads from the vendored files, and Dancing Script's variable file draws
    // at wght 700 (its @font-face weight) rather than its 400 default.
    async selfCheck(faces, dancingUrl) {
      for (const f of faces) {
        const spec = `${f.weight} 64px "${f.family}"`;
        const got = await document.fonts.load(spec);
        if (!got.length || got.some((x) => x.status !== 'loaded')) throw new Error(`vendored face did not load: ${spec}`);
      }
      const probe = new FontFace('PrefDancing400', `url("${dancingUrl}")`, { weight: '400' });
      await probe.load();
      document.fonts.add(probe);
      const ctx = document.createElement('canvas').getContext('2d');
      ctx.font = '900 64px "Dancing Script"';
      const asServed = ctx.measureText('RIZZ').width;
      ctx.font = '400 64px "PrefDancing400"';
      const atDefault = ctx.measureText('RIZZ').width;
      document.fonts.delete(probe);
      if (asServed === atDefault) throw new Error('Dancing Script drew at wght 400: the @font-face weight 700 did not reach the variable axis');
      return faces.length;
    },
  };
}());
```

- [ ] **Step 6: Implement the renderer**

Create `tools/parity-ref/app-render.js`:

```js
// Electron main. Renders v2 parity scenes by driving the REAL 4.1 app (editor.html loaded as-is,
// with the window settings main.js:5-13 uses) instead of lifted functions, so shapes, the eraser,
// details, Word Art and project loading are captured from the code 4.1 ships. GPU raster is off,
// as in render.js. Google Fonts requests are answered from assets/fonts (fonts.js); any other
// network request fails the run.
//   electron tools/parity-ref/app-render.js --scenes tools/parity-ref/app-scenes.json --out tests/goldens/app [--only a,b]
//   electron tools/parity-ref/app-render.js --save-projects tools/parity-ref/project-scenes.json --out-dir tests/goldens/projects [--only a,b]
const { app, BrowserWindow, protocol } = require('electron');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const fonts = require('./fonts.js');

app.commandLine.appendSwitch('disable-gpu');
app.commandLine.appendSwitch('disable-accelerated-2d-canvas');

const ROOT = path.resolve(__dirname, '..', '..');
const HARNESS = fs.readFileSync(path.join(__dirname, 'app-harness.js'), 'utf8');
const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : undefined; };
const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const blob = (rel) => execFileSync('git', ['hash-object', rel], { cwd: ROOT, encoding: 'utf8' }).trim();

const violations = [];
function checkNetwork(where) {
  if (violations.length) throw new Error(`${where}: ${violations.join('; ')}`);
}

function dataUrl(rel) {
  const file = path.resolve(ROOT, rel);
  if (!fs.existsSync(file)) throw new Error(`scene image not found: ${rel}`);
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg' }[path.extname(file).toLowerCase()];
  if (!mime) throw new Error(`unsupported scene image type: ${rel}`);
  return `data:${mime};base64,` + fs.readFileSync(file).toString('base64');
}

function assetsFor(scene) {
  const images = {};
  const projects = {};
  for (const rel of [scene.bgImage, ...(scene.layers || []).map((l) => l.image)].filter(Boolean)) images[rel] = dataUrl(rel);
  for (const op of scene.actions || []) {
    if (op.op !== 'project') continue;
    const file = path.resolve(ROOT, op.file);
    if (!fs.existsSync(file)) throw new Error(`project file not found: ${op.file}`);
    projects[op.file] = { name: path.basename(file), text: fs.readFileSync(file, 'utf8') };
  }
  return { images, projects };
}

function loadScenes(file) {
  let scenes = JSON.parse(fs.readFileSync(path.resolve(file), 'utf8'));
  const only = arg('--only');
  if (only) {
    const want = only.split(',');
    for (const n of want) if (!scenes.some((s) => s.name === n)) throw new Error(`--only names an unknown scene: ${n}`);
    scenes = scenes.filter((s) => want.includes(s.name));
  }
  const names = scenes.map((s) => s.name);
  if (new Set(names).size !== names.length) throw new Error('duplicate scene names');
  for (const n of names) if (!/^[a-z0-9][a-z0-9-]*$/.test(n || '')) throw new Error(`bad scene name: ${n}`);
  return scenes;
}

// Run an async page expression; a rejection comes back with its message instead of Electron's
// generic "Script failed to execute".
async function callPage(win, expr) {
  const r = await win.webContents.executeJavaScript(
    `(${expr}).then((value) => ({ ok: true, value }), (e) => ({ ok: false, error: String((e && e.message) || e) }))`);
  if (!r.ok) throw new Error(r.error);
  return r.value;
}

async function freshPage(win) {
  await win.loadFile(path.join(ROOT, 'editor.html'));
  await win.webContents.executeJavaScript(HARNESS);
}

app.whenReady().then(async () => {
  try {
    for (const scheme of ['https', 'http']) {
      protocol.handle(scheme, (request) => {
        try {
          const r = fonts.respond(request.url);
          if (r) return new Response(r.body, { status: 200, headers: r.headers });
        } catch (e) {
          violations.push(e.message);
          return new Response(e.message, { status: 500 });
        }
        violations.push(`blocked network request ${request.url}`);
        return new Response('blocked by tools/parity-ref', { status: 404 });
      });
    }

    const scenesFile = arg('--scenes');
    const saveFile = arg('--save-projects');
    const out = scenesFile ? arg('--out') : arg('--out-dir');
    if (!!scenesFile === !!saveFile || !out) {
      throw new Error('usage: --scenes <file> --out <dir> | --save-projects <file> --out-dir <dir> [--only a,b]');
    }
    const scenes = loadScenes(scenesFile || saveFile);
    const outDir = path.resolve(out);
    fs.mkdirSync(outDir, { recursive: true });

    const win = new BrowserWindow({
      show: false, width: 1200, height: 800,
      webPreferences: { nodeIntegration: true, contextIsolation: false, backgroundThrottling: false },
    });
    await freshPage(win);
    const n = await callPage(win, `__pref.selfCheck(${JSON.stringify(fonts.wordArtFaces())}, ${JSON.stringify(fonts.urlFor(fonts.FACES['Dancing Script'][700]))})`);
    checkNetwork('font self-check');
    console.log(`fonts ok: ${n} word art faces`);

    // Provenance is per scene and merged like render.js: a run with --only keeps every other
    // entry. Top-level fields describe the most recent run.
    const run = {
      electron: process.versions.electron, chrome: process.versions.chrome,
      editorJsBlob: blob('editor.js'), editorHtmlBlob: blob('editor.html'), fillsJsBlob: blob('lib/fills.js'),
      eraseJsBlob: blob('lib/erase.js'), historyJsBlob: blob('lib/history.js'),
      fontsManifestSha256: sha256(fs.readFileSync(path.join(fonts.FONTS_DIR, 'README.md'))),
    };
    const indexFile = path.join(outDir, 'index.json');
    const prior = fs.existsSync(indexFile) ? JSON.parse(fs.readFileSync(indexFile, 'utf8')).scenes || [] : [];
    const index = { ...run, scenes: prior.slice() };

    for (const scene of scenes) {
      await freshPage(win);
      const call = scenesFile ? 'render' : 'save';
      const result = await callPage(win, `__pref.${call}(${JSON.stringify(scene)}, ${JSON.stringify(assetsFor(scene))})`);
      checkNetwork(scene.name);
      const bytes = scenesFile ? Buffer.from(result.split(',')[1], 'base64') : Buffer.from(result, 'utf8');
      fs.writeFileSync(path.join(outDir, scene.name + (scenesFile ? '.png' : '.r15proj')), bytes);
      const entry = { name: scene.name, sha256: sha256(bytes), ...run };
      const at = index.scenes.findIndex((s) => s.name === scene.name);
      if (at >= 0) index.scenes[at] = entry; else index.scenes.push(entry);
      console.log(scenesFile ? 'rendered' : 'saved', scene.name);
    }
    fs.writeFileSync(indexFile, JSON.stringify(index, null, 2) + '\n');
    app.exit(0);
  } catch (e) {
    console.error(e.stack || String(e));
    app.exit(1);
  }
});
```

- [ ] **Step 7: Run all the tests**

Run: `node --test tools/parity-ref/app-render.test.js`
Expected: `# tests 9`, `# pass 9`, `# fail 0`. If something fails, read the cause first:

- **`fonts ok` never printed, or `blocked network request`.** The `https` handler is not answering. Check that `protocol.handle` runs inside `app.whenReady` before the first `loadFile`.
- **`Dancing Script drew at wght 400`.** Chromium did not apply the `@font-face` weight to the variable axis. Stop and report: the font contract needs a controller decision. Do not add `font-variation-settings` on your own.
- **`app-driven export differs from the M0 golden`.** The real page and the lifted functions disagree. Stop and report the scene names. Never regenerate an M0 golden.
- **Determinism failures.** The renderer is not on CPU raster. Do not loosen the assertion.

Then run every tool test:

Run: `node --test "tools/parity-ref/*.test.js"`
Expected: `# tests 13`, `# pass 13`, `# fail 0`: the 4 in `render.test.js` and the 9 in `app-render.test.js`.

- [ ] **Step 8: Commit**

```bash
git add tools/parity-ref/fonts.js tools/parity-ref/app-harness.js tools/parity-ref/app-render.js tools/parity-ref/app-render.test.js
git commit -m "feat(parity-ref): app-driven reference renderer over the real 4.1 editor with vendored fonts"
```

---

### Task 3: Freeze goldens v2 and the M1-gap scenes

**Files:**

- Create:
  - `tests/goldens/fixtures/bg-large-2160.png` (a copy of `docs/store-assets/store-box-2160x2160.png`)
  - `tools/parity-ref/project-scenes.json`
  - `tests/goldens/projects/*.r15proj`, `tests/goldens/projects/index.json`, `tests/goldens/projects/scenes.json`
  - `tools/parity-ref/app-scenes.json`
  - `tests/goldens/app/*.png`, `tests/goldens/app/index.json`, `tests/goldens/app/scenes.json`
  - `tests/goldens/render/gap-*.png` (4 files)
- Modify:
  - `tools/parity-ref/scenes.json` (append 4 scenes)
  - `tests/goldens/render/index.json` and `tests/goldens/render/scenes.json`
  - `tools/parity-ref/README.md`

**Interfaces:**

- Consumes: `render.js` (M0), `app-render.js` and its `--save-projects` verb (Task 2), `assets/fonts` (Task 1).
- Produces:
  - 34 render goldens: the 30 M0 files byte-identical, plus 4 gap scenes (`gap-layer-rotate-90`, `gap-bg-image-prescale`, `gap-layer-mip2`, `gap-opacity-with-adjust`) rendered by M0's `render.js`. `GoldenRenderTests` reads `tests/goldens/render/scenes.json`, so the gap scenes join it at once; Task 4 makes them green.
  - 82 app goldens at `tests/goldens/app/<name>.png`, from `tests/goldens/app/scenes.json`. Later tasks consume them:
    - `GoldenAppTests` (Task 5) runs the `shape` scenes; Task 6 adds the `erase` scenes. `detail`, `wordart` and `project` scenes are skipped with a reason naming the op.
    - M2b enables the skipped scenes.
  - 3 project fixtures at `tests/goldens/projects/<name>.r15proj`, saved by 4.1's own `saveProject`. M2b's ProjectIO tests read them.
- Scene count: 12 shape, 8 erase, 37 detail, 22 Word Art and 3 project scenes, 82 in all.

**Why each scene exists (cites).**

- **Shapes** follow `editor.js:1204-1233`. The 8 shirt-torso and arm-front regions are filled first, so strokes cross the gutters and the export mask (`editor.js:1531-1533`).
  - Rects and ellipses are dragged both down-right and up-left, because 4.1 normalizes the drag with `min` and `abs` (`editor.js:1218-1221`, `1225-1228`).
  - Fill draws a fill and no outline (`editor.js:1222`, `1231`).
  - Line ignores Fill and uses round caps (`editor.js:1210-1216`).
  - Width 1 tests half-pixel alignment.
  - The drawn layer is the autocrop with pad 10 (`editor.js:144-171`), placed at the crop origin with scale 1 (`editor.js:533`).
  - Three degenerate and clipping cases, which the ledger rows `draw-shape-ellipse`, `draw-shape-rect` and `draw-canvas-template-clip` name in their parity checks. All three are stroked, not filled: a filled zero-area shape paints nothing, so 4.1 commits no layer and the harness would time out waiting for one.
    - `shape-ellipse-degenerate`: a zero-width drag, so `rx = 0` (`editor.js:1225-1228`) and Blink draws the degenerate ellipse as line segments.
    - `shape-rect-zero-height`: `h = 0`, so Blink's `strokeRect` strokes the closed path from `(x, y)` to `(right, bottom)`.
    - `shape-line-off-canvas`: a line that crosses `x = 585`. The stroke canvas is 585x559 (`editor.js:1128-1131`), so the part past the edge is lost before autocrop. The harness dispatches straight to `#mainCanvas`, so `x = 600` is a valid event coordinate.
- **Erase** follows `lib/erase.js:43-73` through `editor.js:1265-1283`:
  - Plain, rotated and scaled, flipped and maximum-size cases.
  - An RGB-shifted layer: the single-slot `_rgbCache` is invalidated at `editor.js:1279`.
  - The bottom layer of two: erase applies to the selected layer only.
  - Three two-point drags: `erase-plain-one-segment`, `erase-max-size` and `erase-two-point-transformed` (scaled, rotated 30 degrees and flipped). With two points 4.1 strokes exactly once (one mousemove), so production's stroke-once eraser must meet these goldens directly. The other five erase scenes have three or more points, and 4.1 re-strokes the whole path on every move (`editor.js:1200-1203`). Task 6 meets those through an internal replay of 4.1's stroking, and measures how far stroke-once lands from them.
- **Details:** all 16 generators (`editor.js:1867-2075`). Each is captured at the default settings, `#333333` at width 2 (`editor.html:179-182`), and again with Stitch (`setLineDash([w*2, w*2])`, `editor.js:1849`). Five extra variants cover:
  - Width 8, where Henley's button radius is the line width (`editor.js:1916`).
  - Width 1, where the placket button radius is `max(1.5, w*0.8)` (`editor.js:2023`).
  - A width 1 stitch.
  - The full set.
- **Word Art:** 6 styles (`editor.js:2166-2235`) in Anton, Poppins and Dancing Script. Anton gets synthetic bold, Poppins true 900, and Dancing Script the variable 700 face. Four extra scenes:
  - Multiline in Orbitron, a variable 900 face (`editor.js:2135-2142`, `2163-2167`).
  - Stroke width 12, which grows the pad to 24 (`editor.js:2145`).
  - A gradient at angle 135 (`editor.js:2171-2177`).
  - Outline with stroke width 0, which draws at 3 (`editor.js:2217`).

  Every field is explicit, at the `editor.html:229-262` defaults unless varied. Note: the layer is placed at `min(128/w, 128/h) * 0.85` (`editor.js:2272-2273`), so glyph detail is checked at reduced scale.
- **Projects:** `loadProject` (`editor.js:2593-2680`) reads three fixtures that 4.1's own `saveProject` wrote (`editor.js:2550-2591`):
  - `proj-shirt-rich`: descriptor fills, legacy string fills (`#ff8800`, short `#333`), adjustments, a flip, an RGB shift and a hidden layer.
  - `proj-pants-legacy`: Pants mode, legacy strings only, and a background image at 60%.
  - `proj-shirt-drawn`: drawn, erased, detail and Word Art layers, all carried as embedded PNGs.
- **M1-gap scenes** (contract):
  - Rotation exactly 90: Chromium's `SkMatrix::setRotate` snaps sin and cos to exact 0 and 1, which makes the draw scale-translate (`Renderer.DrawImageAntialiased`).
  - A 2160x2160 background drawn at 585x559: the decode cache picks mip level 1, since 1080 is at least 585 and 559 and 540 is below 559 (`Renderer.DecodeCacheSize`).
  - Anton (198x125) at scale 0.2: the target is 40x25 and the mip level is 2, from 99x63 to 50x32, stopping at 25x16.
  - Opacity 0.5 with brightness and saturate: `globalAlpha` combined with `ctx.filter` (`editor.js:212`, `222-224`).

- [ ] **Step 1: Add the large background fixture**

```bash
mkdir -p tests/goldens/fixtures
cp docs/store-assets/store-box-2160x2160.png tests/goldens/fixtures/bg-large-2160.png
node -e "const b=require('fs').readFileSync('tests/goldens/fixtures/bg-large-2160.png');console.log(b.readUInt32BE(16)+'x'+b.readUInt32BE(20))"
```

Expected: `2160x2160`. The copy keeps the gap golden independent of future store-art edits.

- [ ] **Step 2: Append the four M1-gap scenes**

In `tools/parity-ref/scenes.json`, the last scene is `full-scene`. Change the end of that line from `}] }` to `}] },`, then insert these four lines before the closing `]`:

```json
  { "name": "gap-layer-rotate-90", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 241, "y": 64, "rotation": 90 }] },
  { "name": "gap-bg-image-prescale", "bgImage": "tests/goldens/fixtures/bg-large-2160.png" },
  { "name": "gap-layer-mip2", "layers": [{ "image": "packs/word_art/word_626_anton.png", "x": 270, "y": 120, "scale": 0.2 }] },
  { "name": "gap-opacity-with-adjust", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74, "opacity": 0.5, "brightness": 140, "saturate": 40 }] }
```

Run:

```bash
node - <<'EOF'
const { execFileSync } = require('child_process');
const old = JSON.parse(execFileSync('git', ['show', 'HEAD:tools/parity-ref/scenes.json'], { encoding: 'utf8' }));
const now = JSON.parse(require('fs').readFileSync('tools/parity-ref/scenes.json', 'utf8'));
const kept = old.every((s, i) => JSON.stringify(s) === JSON.stringify(now[i]));
console.log(kept && now.length === 34 ? 'scenes: 30 kept, 4 appended' : 'SCENE DRIFT');
process.exit(kept && now.length === 34 ? 0 : 1);
EOF
```

Expected: `scenes: 30 kept, 4 appended`.

- [ ] **Step 3: Render only the gap scenes, and prove the 30 stayed byte-identical**

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/render.js --scenes tools/parity-ref/scenes.json --out tests/goldens/render --only gap-layer-rotate-90,gap-bg-image-prescale,gap-layer-mip2,gap-opacity-with-adjust
node -e "const fs=require('fs');fs.writeFileSync(process.argv[2],fs.readFileSync(process.argv[1],'utf8').replace(/\r\n/g,'\n'))" tools/parity-ref/scenes.json tests/goldens/render/scenes.json
grep -c $'\r' tests/goldens/render/scenes.json
git diff --stat HEAD -- tests/goldens/render/scenes.json
git diff --exit-code --stat HEAD -- 'tests/goldens/render/*.png' && echo "30 render goldens byte-identical"
node - <<'EOF'
const { execFileSync } = require('child_process');
const old = JSON.parse(execFileSync('git', ['show', 'HEAD:tests/goldens/render/index.json'], { encoding: 'utf8' }));
const now = JSON.parse(require('fs').readFileSync('tests/goldens/render/index.json', 'utf8'));
const kept = old.scenes.every((s, i) => JSON.stringify(s) === JSON.stringify(now.scenes[i]));
console.log(kept && now.scenes.length === 34 ? 'index: 30 kept, 4 added' : 'INDEX DRIFT');
process.exit(kept && now.scenes.length === 34 ? 0 : 1);
EOF
git status --porcelain -- tests/goldens/render
```

Expected:

- Four `rendered gap-...` lines.
- `0`: the copy has no CR bytes. `tests/goldens/** -text` stores bytes as-is and HEAD's copy is LF, so a `cp` of a CRLF working-tree source would rewrite all 30 existing lines.
- `1 file changed, 5 insertions(+), 1 deletion(-)`: the `full-scene` line gains its comma, and four lines are added.
- `30 render goldens byte-identical`.
- `index: 30 kept, 4 added`.
- Exactly these status lines:

```text
 M tests/goldens/render/index.json
 M tests/goldens/render/scenes.json
?? tests/goldens/render/gap-bg-image-prescale.png
?? tests/goldens/render/gap-layer-mip2.png
?? tests/goldens/render/gap-layer-rotate-90.png
?? tests/goldens/render/gap-opacity-with-adjust.png
```

- [ ] **Step 4: Prove the gap goldens are reproducible**

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/render.js --scenes tools/parity-ref/scenes.json --out .superpowers/parity-check --only gap-layer-rotate-90,gap-bg-image-prescale,gap-layer-mip2,gap-opacity-with-adjust
node -e "const a=require('./tests/goldens/render/index.json').scenes,b=require('./.superpowers/parity-check/index.json').scenes;const bad=b.filter(s=>a.find(x=>x.name===s.name).sha256!==s.sha256);console.log(bad.length?'MISMATCH '+bad.map(s=>s.name):'reproducible: '+b.length);process.exit(bad.length?1:0)"
rm -rf .superpowers/parity-check
```

Expected: `reproducible: 4`.

- [ ] **Step 5: Run the render gate against the gap scenes, and record the result**

The four `gap-*` scenes exist to expose M1 gaps, so this step records their result and changes nothing else: no engine code, no golden, no rule. Task 4, which runs next, owns any fix. The script writes the Step 6 commit message, so the recorded result goes into history verbatim.

```bash
mkdir -p .superpowers/logs
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests" > .superpowers/logs/gap-gate.log 2>&1; tail -5 .superpowers/logs/gap-gate.log
python - <<'EOF'
log = open(".superpowers/logs/gap-gate.log", encoding="utf-8", errors="replace").read().splitlines()
out = []
for n in ["gap-layer-rotate-90", "gap-bg-image-prescale", "gap-layer-mip2", "gap-opacity-with-adjust"]:
    failed = any(l.lstrip().startswith("Failed ") and f'"{n}"' in l for l in log)
    detail = next((l.strip() for l in log if f"{n}: DiffResult" in l), "")
    out.append(f"{n}: " + (f"fail, {detail}" if failed else "pass"))
subject = "test(goldens): freeze four M1-gap render goldens (rot 90, bg prescale, mip 2, opacity+adjust)"
body = "GoldenRenderTests on the gap scenes (Task 4 owns any fix):\n" + "\n".join(out)
open(".superpowers/logs/gap-commit-msg.txt", "w", encoding="utf-8", newline="\n").write(subject + "\n\n" + body + "\n")
print("\n".join(out))
EOF
```

Expected:

- The summary reports `Total: 34` for `RBX15.Engine.Tests.dll`. The 30 pre-existing scenes pass: `Passed:` is 30 plus the number of `pass` lines below, and `Failed:` is the number of `fail` lines.
- Four lines, one per gap scene, each `pass`, or `fail,` followed by the scene's `DiffResult` from the assert message.

Report the four lines to the controller. A failing gap scene keeps the branch red until Task 4 lands, which is why Task 4 runs next.

- [ ] **Step 6: Commit the gap goldens**

Append your model's `Co-Authored-By` trailer as the last line of `.superpowers/logs/gap-commit-msg.txt` (after a blank line), then:

```bash
git add tests/goldens/fixtures/bg-large-2160.png tools/parity-ref/scenes.json tests/goldens/render
git commit -F .superpowers/logs/gap-commit-msg.txt
git log -1 --format=%s
```

Expected: `test(goldens): freeze four M1-gap render goldens (rot 90, bg prescale, mip 2, opacity+adjust)`.

- [ ] **Step 7: Write the project source scenes**

Create `tools/parity-ref/project-scenes.json`:

```json
[
  { "name": "proj-shirt-rich", "bgColor": "#0f1f31", "bgOpacity": 0.85, "regionColors": { "torso_front": { "type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45 }, "torso_back": { "type": "linear", "c1": "#17d4fa", "c2": "#f22f89", "angle": 45 }, "torso_left": "#ff8800", "rarm_front": "#333", "larm_front": { "type": "dots", "c1": "#050c18", "c2": "#17d4fa", "scale": 12 }, "torso_up": { "type": "camo", "c1": "#556b2f", "c2": "#c2b280", "scale": 16 } }, "layers": [{ "image": "packs/drips/drip_white_sleeve_v1.png", "x": 217, "y": 355, "opacity": 0.8 }, { "image": "packs/word_art/word_626_anton.png", "x": 245, "y": 110, "scale": 0.5, "rotation": -8, "flipH": true, "saturate": 140 }, { "image": "packs/drips/drip_red_wide_v1.png", "x": 100, "y": 380, "scale": 0.6, "hueRotate": 200, "rShift": 30 }, { "image": "packs/drips/drip_cyan_front_v1.png", "x": 427, "y": 74, "visible": false }] },
  { "name": "proj-pants-legacy", "mode": "Pants", "bgImage": "packs/drips/drip_red_wide_v1.png", "bgOpacity": 0.6, "regionColors": { "waist_front": "#ff8800", "waist_back": "#0088ff", "rleg_front": "#333", "lleg_front": "#f22f89" }, "layers": [{ "image": "packs/drips/drip_cyan_pants_leg_v1.png", "x": 217, "y": 355 }] },
  { "name": "proj-shirt-drawn", "regionColors": { "torso_front": { "type": "solid", "c1": "#2d2d2d" } }, "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74 }], "actions": [{ "op": "shape", "tool": "rect", "from": [250, 90], "to": [300, 140], "color": "#ffcc00", "width": 4, "fill": true }, { "op": "erase", "layer": 0, "points": [[240, 150], [350, 150]], "size": 18 }, { "op": "detail", "type": "collar-crew", "color": "#ffffff", "width": 3, "stitch": false }, { "op": "wordart", "text": "W", "font": "Anton", "style": "neon", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] }
]
```

- [ ] **Step 8: Save the fixtures through 4.1's own saveProject, and check them**

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js --save-projects tools/parity-ref/project-scenes.json --out-dir tests/goldens/projects
node -e "const fs=require('fs');fs.writeFileSync(process.argv[2],fs.readFileSync(process.argv[1],'utf8').replace(/\r\n/g,'\n'))" tools/parity-ref/project-scenes.json tests/goldens/projects/scenes.json
node - <<'EOF'
const fs = require('fs');
const read = (n) => JSON.parse(fs.readFileSync(`tests/goldens/projects/${n}.r15proj`, 'utf8'));
const want = { 'proj-shirt-rich': ['Shirt', 4], 'proj-pants-legacy': ['Pants', 1], 'proj-shirt-drawn': ['Shirt', 4] };
for (const [n, [mode, count]] of Object.entries(want)) {
  const p = read(n);
  const ok = p.version === '4.0' && p.app === 'RBX15 Classic Shirt and Pants Maker' && p.mode === mode &&
    p.layers.length === count && p.layers.every((l) => /^data:image\/png;base64,/.test(l.imgSrc));
  if (!ok) throw new Error(`${n}: unexpected shape`);
  console.log('ok', n, mode, count);
}
const rich = read('proj-shirt-rich');
if (rich.regionColors.torso_left !== '#ff8800' || rich.regionColors.rarm_front !== '#333' || rich.regionColors.torso_front.type !== 'linear') throw new Error('rich fills');
if (rich.layers[3].visible !== false || rich.layers[2].rShift !== 30 || rich.layers[1].flipH !== true) throw new Error('rich layers');
const pants = read('proj-pants-legacy');
if (!/^data:image\/png;base64,/.test(pants.bgImageSrc) || pants.bgOpacity !== 0.6 || pants.regionColors.waist_front !== '#ff8800') throw new Error('pants');
const drawn = read('proj-shirt-drawn');
const names = drawn.layers.map((l) => l.name);
if (JSON.stringify(names) !== JSON.stringify(['L1', 'Rect drawing', 'Crew Neck', '✨ W [Anton]'])) throw new Error('drawn names ' + JSON.stringify(names));
console.log('fixtures ok');
EOF
```

Expected:

- `fonts ok: 17 word art faces`, then 3 `saved proj-...` lines.
- `ok proj-shirt-rich Shirt 4`, `ok proj-pants-legacy Pants 1` and `ok proj-shirt-drawn Shirt 4`.
- `fixtures ok`.

The layer names come straight from 4.1:

- `Rect drawing` is `currentTool` capitalized plus `' drawing'` (`editor.js:1285`).
- `Crew Neck` comes from the names map (`editor.js:2085`).
- `✨ W [Anton]` is `'\u{2728} ' + text + ' [' + font + ']'` (`editor.js:2276`).

The fixtures carry `savedAt` (`editor.js:2554`), so re-saving never reproduces their bytes. They are frozen from this run, and the save verb is never re-run against `tests/goldens/projects`.

- [ ] **Step 9: Commit the fixtures**

```bash
git add tools/parity-ref/project-scenes.json tests/goldens/projects
git commit -m "test(goldens): three .r15proj fixtures saved through 4.1 saveProject"
```

- [ ] **Step 10: Write the app scene set**

Create `tools/parity-ref/app-scenes.json`:

```json
[
  { "name": "shape-line", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "line", "from": [180, 90], "to": [540, 190], "color": "#17d4fa", "width": 4, "fill": false }] },
  { "name": "shape-line-thick", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "line", "from": [200, 180], "to": [520, 80], "color": "#f22f89", "width": 30, "fill": false }] },
  { "name": "shape-line-w1", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "line", "from": [240, 120], "to": [350, 131], "color": "#ffffff", "width": 1, "fill": false }] },
  { "name": "shape-rect", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "rect", "from": [250, 90], "to": [340, 180], "color": "#ffcc00", "width": 6, "fill": false }] },
  { "name": "shape-rect-reverse-w1", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "rect", "from": [400, 190], "to": [300, 100], "color": "#ffffff", "width": 1, "fill": false }] },
  { "name": "shape-rect-fill", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "rect", "from": [180, 100], "to": [520, 170], "color": "#00ff88", "width": 4, "fill": true }] },
  { "name": "shape-ellipse", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "ellipse", "from": [245, 85], "to": [345, 190], "color": "#ff5472", "width": 5, "fill": false }] },
  { "name": "shape-ellipse-fill", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "ellipse", "from": [430, 80], "to": [550, 195], "color": "#6600cc", "width": 4, "fill": true }] },
  { "name": "shape-ellipse-reverse-thick", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "ellipse", "from": [350, 190], "to": [240, 90], "color": "#17d4fa", "width": 20, "fill": false }] },
  { "name": "shape-ellipse-degenerate", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "ellipse", "from": [300, 85], "to": [300, 195], "color": "#ffcc00", "width": 6, "fill": false }] },
  { "name": "shape-rect-zero-height", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "rect", "from": [250, 140], "to": [350, 140], "color": "#00ff88", "width": 6, "fill": false }] },
  { "name": "shape-line-off-canvas", "regionColors": { "torso_up": { "type": "solid", "c1": "#2d2d2d" }, "torso_right": { "type": "solid", "c1": "#2d2d2d" }, "torso_front": { "type": "solid", "c1": "#2d2d2d" }, "torso_left": { "type": "solid", "c1": "#2d2d2d" }, "torso_back": { "type": "solid", "c1": "#2d2d2d" }, "torso_down": { "type": "solid", "c1": "#2d2d2d" }, "rarm_front": { "type": "solid", "c1": "#2d2d2d" }, "larm_front": { "type": "solid", "c1": "#2d2d2d" } }, "actions": [{ "op": "shape", "tool": "line", "from": [470, 120], "to": [600, 60], "color": "#f22f89", "width": 10, "fill": false }] },
  { "name": "erase-plain", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74 }], "actions": [{ "op": "erase", "layer": 0, "points": [[240, 90], [270, 130], [300, 110], [330, 160], [350, 140]], "size": 20 }] },
  { "name": "erase-plain-one-segment", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74 }], "actions": [{ "op": "erase", "layer": 0, "points": [[236, 140], [354, 140]], "size": 16 }] },
  { "name": "erase-rotated-scaled", "layers": [{ "image": "packs/word_art/word_626_anton.png", "x": 200, "y": 90, "scale": 0.75, "rotation": 30 }], "actions": [{ "op": "erase", "layer": 0, "points": [[250, 110], [290, 150], [330, 130], [360, 170]], "size": 14 }] },
  { "name": "erase-flipped", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74, "flipH": true, "flipV": true }], "actions": [{ "op": "erase", "layer": 0, "points": [[235, 80], [300, 200], [355, 90]], "size": 24 }] },
  { "name": "erase-rgb-shift", "layers": [{ "image": "packs/word_art/word_626_anton.png", "x": 200, "y": 90, "rShift": 60, "gShift": -40, "bShift": 20 }], "actions": [{ "op": "erase", "layer": 0, "points": [[210, 120], [300, 150], [390, 120]], "size": 30 }] },
  { "name": "erase-max-size", "layers": [{ "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74 }], "actions": [{ "op": "erase", "layer": 0, "points": [[260, 100], [330, 180]], "size": 50 }] },
  { "name": "erase-bottom-of-two", "layers": [{ "image": "packs/drips/drip_red_wide_v1.png", "x": 165, "y": 40 }, { "image": "packs/drips/drip_cyan_front_v1.png", "x": 231, "y": 74 }], "actions": [{ "op": "erase", "layer": 0, "points": [[180, 100], [300, 110], [420, 100]], "size": 24 }] },
  { "name": "erase-two-point-transformed", "layers": [{ "image": "packs/word_art/word_626_anton.png", "x": 200, "y": 90, "scale": 0.75, "rotation": 30, "flipH": true }], "actions": [{ "op": "erase", "layer": 0, "points": [[230, 120], [360, 160]], "size": 12 }] },
  { "name": "detail-collar-v", "actions": [{ "op": "detail", "type": "collar-v", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-collar-v-stitch", "actions": [{ "op": "detail", "type": "collar-v", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-collar-crew", "actions": [{ "op": "detail", "type": "collar-crew", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-collar-crew-stitch", "actions": [{ "op": "detail", "type": "collar-crew", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-collar-scoop", "actions": [{ "op": "detail", "type": "collar-scoop", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-collar-scoop-stitch", "actions": [{ "op": "detail", "type": "collar-scoop", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-collar-henley", "actions": [{ "op": "detail", "type": "collar-henley", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-collar-henley-stitch", "actions": [{ "op": "detail", "type": "collar-henley", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-cuff-left", "actions": [{ "op": "detail", "type": "cuff-left", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-cuff-left-stitch", "actions": [{ "op": "detail", "type": "cuff-left", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-cuff-right", "actions": [{ "op": "detail", "type": "cuff-right", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-cuff-right-stitch", "actions": [{ "op": "detail", "type": "cuff-right", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-cuffs-both", "actions": [{ "op": "detail", "type": "cuffs-both", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-cuffs-both-stitch", "actions": [{ "op": "detail", "type": "cuffs-both", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-hem", "actions": [{ "op": "detail", "type": "hem", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-hem-stitch", "actions": [{ "op": "detail", "type": "hem", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-seam-center", "actions": [{ "op": "detail", "type": "seam-center", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-seam-center-stitch", "actions": [{ "op": "detail", "type": "seam-center", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-seam-sides", "actions": [{ "op": "detail", "type": "seam-sides", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-seam-sides-stitch", "actions": [{ "op": "detail", "type": "seam-sides", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-shoulder-stripe", "actions": [{ "op": "detail", "type": "shoulder-stripe", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-shoulder-stripe-stitch", "actions": [{ "op": "detail", "type": "shoulder-stripe", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-arm-bands", "actions": [{ "op": "detail", "type": "arm-bands", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-arm-bands-stitch", "actions": [{ "op": "detail", "type": "arm-bands", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-yoke", "actions": [{ "op": "detail", "type": "yoke", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-yoke-stitch", "actions": [{ "op": "detail", "type": "yoke", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-pocket", "actions": [{ "op": "detail", "type": "pocket", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-pocket-stitch", "actions": [{ "op": "detail", "type": "pocket", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-placket", "actions": [{ "op": "detail", "type": "placket", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-placket-stitch", "actions": [{ "op": "detail", "type": "placket", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-full-detail", "actions": [{ "op": "detail", "type": "full-detail", "color": "#333333", "width": 2, "stitch": false }] },
  { "name": "detail-full-detail-stitch", "actions": [{ "op": "detail", "type": "full-detail", "color": "#333333", "width": 2, "stitch": true }] },
  { "name": "detail-collar-henley-w8", "actions": [{ "op": "detail", "type": "collar-henley", "color": "#ffffff", "width": 8, "stitch": false }] },
  { "name": "detail-placket-w1", "actions": [{ "op": "detail", "type": "placket", "color": "#ffffff", "width": 1, "stitch": false }] },
  { "name": "detail-placket-w8-stitch", "actions": [{ "op": "detail", "type": "placket", "color": "#f22f89", "width": 8, "stitch": true }] },
  { "name": "detail-seam-sides-w1-stitch", "actions": [{ "op": "detail", "type": "seam-sides", "color": "#ffffff", "width": 1, "stitch": true }] },
  { "name": "detail-full-detail-w8-stitch", "actions": [{ "op": "detail", "type": "full-detail", "color": "#17d4fa", "width": 8, "stitch": true }] },
  { "name": "wordart-solid-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "solid", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-gradient-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "gradient", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-neon-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "neon", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-shadow-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "shadow", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-outline-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "outline", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-retro-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "retro", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-solid-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "solid", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-gradient-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "gradient", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-neon-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "neon", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-shadow-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "shadow", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-outline-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "outline", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-retro-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "retro", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-solid-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "solid", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-gradient-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "gradient", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-neon-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "neon", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-shadow-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "shadow", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-outline-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "outline", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-retro-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "retro", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-multiline-orbitron", "actions": [{ "op": "wordart", "text": "NO\nCAP", "font": "Orbitron", "style": "solid", "size": 72, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 3, "angle": 0 }] },
  { "name": "wordart-stroke-12-anton", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Anton", "style": "solid", "size": 64, "fill": "#17d4fa", "stroke": "#ffffff", "grad": "#f22f89", "strokeWidth": 12, "angle": 0 }] },
  { "name": "wordart-gradient-135-poppins", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Poppins", "style": "gradient", "size": 64, "fill": "#ffcc00", "stroke": "#000000", "grad": "#6600cc", "strokeWidth": 3, "angle": 135 }] },
  { "name": "wordart-outline-stroke-0-dancing", "actions": [{ "op": "wordart", "text": "RIZZ", "font": "Dancing Script", "style": "outline", "size": 64, "fill": "#17d4fa", "stroke": "#000000", "grad": "#f22f89", "strokeWidth": 0, "angle": 0 }] },
  { "name": "project-shirt-rich", "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-shirt-rich.r15proj" }] },
  { "name": "project-pants-legacy", "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-pants-legacy.r15proj" }] },
  { "name": "project-shirt-drawn", "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-shirt-drawn.r15proj" }] }
]
```

Run:

```bash
node -e "const s=require('./tools/parity-ref/app-scenes.json');const c={};for(const x of s){const k=(x.actions||[])[0].op;c[k]=(c[k]||0)+1}console.log(s.length,JSON.stringify(c))"
```

Expected: `82 {"shape":12,"erase":8,"detail":37,"wordart":22,"project":3}`.

The set is frozen in Step 16. A scene added after that cannot get a 4.1 golden, so check now that the degenerate and clipping shapes (`shape-ellipse-degenerate`, `shape-rect-zero-height`, `shape-line-off-canvas`) and the four two-point and multi-point erase cases are present; Task 8's coverage gate reads them.

- [ ] **Step 11: Render the app goldens**

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js --scenes tools/parity-ref/app-scenes.json --out tests/goldens/app
node -e "const fs=require('fs');fs.writeFileSync(process.argv[2],fs.readFileSync(process.argv[1],'utf8').replace(/\r\n/g,'\n'))" tools/parity-ref/app-scenes.json tests/goldens/app/scenes.json
grep -c $'\r' tests/goldens/app/scenes.json tests/goldens/projects/scenes.json
ls tests/goldens/app/*.png | wc -l
```

Expected: `fonts ok: 17 word art faces`, then 82 `rendered <name>` lines; `tests/goldens/app/scenes.json:0` and `tests/goldens/projects/scenes.json:0` (no CR bytes); and the count prints `82`. `tests/goldens/app/` holds 82 PNGs, `index.json` and `scenes.json`.

If a run stops with `timed out waiting for the ellipse drawing layer` or `the rect drawing layer` on `shape-ellipse-degenerate` or `shape-rect-zero-height`, 4.1 drew nothing for that degenerate shape. Stop and report it: Task 5's `Ellipse_with_one_zero_radius_strokes_a_line` and `Rect_stroke_with_zero_height_draws_a_line` assume Blink strokes a line there, and the controller must decide before anything is frozen.

- [ ] **Step 12: Prove the app goldens are reproducible**

Render again into a scratch folder under the git-ignored `.superpowers/`. Git Bash's `$TEMP` and Windows paths disagree, so stay inside the repo. Then compare hashes against the new index and delete the folder:

```bash
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js --scenes tools/parity-ref/app-scenes.json --out .superpowers/parity-check-app
node -e "const a=require('./tests/goldens/app/index.json').scenes,b=require('./.superpowers/parity-check-app/index.json').scenes;const bad=a.filter((s,i)=>s.sha256!==b[i].sha256);console.log(bad.length?'MISMATCH '+bad.map(s=>s.name):'reproducible: '+a.length);process.exit(bad.length?1:0)"
rm -rf .superpowers/parity-check-app
```

Expected: `reproducible: 82`. A Word Art mismatch means fonts are not deterministic: check that `fonts ok` printed and that no request escaped. Never accept a nondeterministic golden.

- [ ] **Step 13: Eyeball eight goldens**

Open these files in `tests/goldens/app/`. In every image, gutters and the area outside the template are transparent.

- **`shape-rect-fill.png`:** a green bar crosses torso Right, Front and Left on the gray fill, cut by transparent gutters.
- **`shape-ellipse-reverse-thick.png`:** a thick cyan ellipse on the front torso, the same shape a down-right drag would give.
- **`erase-rotated-scaled.png`:** the rotated and scaled "626" word art is missing pixels along a diagonal stroke through its right half.
- **`erase-bottom-of-two.png`:** the red drip has a gap, and the cyan drip on top is intact.
- **`detail-full-detail-stitch.png`:** a faint dashed crew collar, hem, both cuffs and side seams. The default `#333333` is low-contrast on the navy background, so zoom in.
- **`wordart-neon-anton.png`:** glowing cyan RIZZ in heavy block capitals, centered on the front torso. It must not be a sans-serif fallback.
- **`wordart-solid-dancing.png`:** a connected script RIZZ, not block letters.
- **`project-pants-legacy.png`:** the pants layout, with the drip background at reduced opacity, orange, blue, gray and magenta panels, and a cyan drip on the right leg front.

If any looks wrong, stop: a scene is mis-specified, or the harness drove the wrong control.

- [ ] **Step 14: Document goldens v2**

Replace `tools/parity-ref/README.md` with:

````markdown
# tools/parity-ref

Renders parity scenes through shipped 4.1 code in Electron with GPU raster off. The output is the frozen golden set the native v5 engine is held to (spec section 6). There are two renderers:

- `render.js` (M0) lifts the render functions verbatim out of `editor.js` (`extract.js`) and renders the render-core scenes into `tests/goldens/render`.
- `app-render.js` (goldens v2) loads the real `editor.html` and drives it the way a person does: real globals, real inputs and buttons, and synthetic mouse events on the canvas. It renders the v2 scenes into `tests/goldens/app`.

## Rules

- Goldens in `tests/goldens/` are made **only** by these tools, run by a person. CI never regenerates them.
- They are frozen from 4.1. Regenerating them after `editor.js` changes is wrong unless the change is a 4.x release the native port must also match.
- A deliberate v5 divergence with a visual effect gets a **new** golden next to the 4.1 one, plus a note in `docs/port/divergences.md`. The 4.1 golden is never overwritten.
- Word Art goldens are drawn with the vendored fonts in `assets/fonts` (see its README). `app-render.js` answers every Google Fonts request from those files and fails on any other network request.
- Project fixtures in `tests/goldens/projects` were written once by 4.1's own `saveProject`. They embed `savedAt`, so re-saving never reproduces them. Never re-run `--save-projects` into that folder.
- This directory is deleted at cutover. The goldens and `assets/fonts` stay.

## Use

```bash
node --test "tools/parity-ref/*.test.js"   # the tools' own tests
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/render.js \
  --scenes tools/parity-ref/scenes.json --out tests/goldens/render [--only name1,name2]
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js \
  --scenes tools/parity-ref/app-scenes.json --out tests/goldens/app [--only name1,name2]
env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/app-render.js \
  --save-projects tools/parity-ref/project-scenes.json --out-dir <dir> [--only name1,name2]
```

Each `index.json` records, per golden, its SHA-256, the Electron and Chrome versions, and the git blob hashes of the shipped files that produced it. `app-render.js` also records `editor.html`, `lib/erase.js` and `lib/history.js`, plus the SHA-256 of the font manifest. A run with `--only` merges into the existing index and never drops other goldens' records.

## Scene format

`{ name, mode?, bgColor?, bgOpacity?, bgImage?, regionColors?, layers?: [{ image, x?, y?, scale?, rotation?, flipH?, flipV?, opacity?, visible?, brightness?, contrast?, saturate?, hueRotate?, rShift?, gShift?, bShift? }], actions? }`

- Image paths are repo-relative. Defaults match `makeLayer` (`editor.js:134-142`) and the editor's initial state. The output is always the export composition, so the mask is always applied.
- `actions` (v2, `app-render.js` only) run in order after the base fields:
  - `{ op: "shape", tool: "line"|"rect"|"ellipse", from: [x,y], to: [x,y], color: "#rrggbb", width, fill }` sends mousedown, one mousemove and mouseup through the real canvas handlers.
  - `{ op: "erase", layer, points: [[x,y], ...], size }` selects base layer `layer` (0-based), switches to the eraser and drags through `points`. At least two points are required.
  - `{ op: "detail", type, color, width, stitch }` sets the Details settings and clicks the real detail button. Shirt mode only.
  - `{ op: "wordart", text?, font?, style?, size?, fill?, stroke?, grad?, strokeWidth?, angle? }` sets the Text-tab controls, waits for the vendored font, and clicks "Add to canvas". Omitted fields keep the `editor.html` defaults.
  - `{ op: "project", file }` loads a `.r15proj` through the real Open path.
- Template points are integers, and every value must fit its control's range: brush 1-50, detail width 1-8, stroke width 0-20, angle 0-360. Pen strokes are not frozen (approved divergence); the renderer refuses any op it does not know.
````

- [ ] **Step 15: Run every check once more**

Run: `node --test "tools/parity-ref/*.test.js"`
Expected: `# tests 13`, `# pass 13`, `# fail 0`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected:

- Engine.Tests `Total: 101` (Task 1's 97 plus the 4 gap rows). The 97 pre-existing tests pass, and the four gap rows match what Step 5 recorded: `Passed:` is 97 plus the gap `pass` count, `Failed:` the gap `fail` count.
- Parity.Tests `Total: 13`, `Passed: 12`, `Failed: 1`. The failure is `SceneLoaderTests.All_frozen_scenes_load` (`Expected: 30`, `Actual: 34`), because `tests/goldens/render/scenes.json` now holds 34 scenes. Task 4 Step 2 updates that assert; this task changes no test code.
- Nothing else changes state.

- [ ] **Step 16: Commit the app goldens**

```bash
git add tools/parity-ref/app-scenes.json tools/parity-ref/README.md tests/goldens/app
git commit -m "test(goldens): freeze 82 app-driven 4.1 goldens (shapes, eraser, details, word art, projects)"
```

From this commit on, nothing under `tests/goldens/app`, `tests/goldens/projects` or `tests/goldens/render` changes for the rest of M2a (Task 8 Step 3 checks it).

---

### Task 4: M1-gap goldens through the engine

**Files:**

- Modify: `src/RBX15.Parity.Tests/SceneLoaderTests.cs` (`All_frozen_scenes_load` counts 34 scenes)
- Modify: `src/RBX15.Engine/Renderer.cs` (the Chromium-compat index doc comment, plus engine fixes only if Step 3 finds a failing scene)
- Test: `src/RBX15.Engine.Tests/GoldenRenderTests.cs` (M1, unchanged in this task: it is a theory over `tests/goldens/render/scenes.json`, so the four gap scenes joined it in Task 3)

**Interfaces:**

- Consumes:
  - The four gap scenes appended to `tools/parity-ref/scenes.json`, their PNGs in `tests/goldens/render/`, the LF copy at `tests/goldens/render/scenes.json` and the merged `index.json` (Task 3). They cover layer rotation exactly 90, a large background image drawn smaller than its size, a layer at scale below 0.25 (mip level 2), and layer opacity below 1 combined with adjustments.
  - `GoldenRenderTests.Engine_export_matches_the_frozen_4_1_golden(string name)`, `SceneLoaderTests.All_frozen_scenes_load`, `Renderer.DrawImageAntialiased`, `Renderer.IsScaleTranslate`, `Renderer.DecodeCacheSize`, `Renderer.DrawLayer`, `Images.DecodeCacheScaled` (M1).
- Produces:
  - The M1 golden gate at 34 scenes, all green, before any M2a engine module lands.
  - The Renderer Chromium-compat index naming the gap goldens that now exercise each row (the background pre-scale path is no longer "unit test only").

**Base commit.** M2a branched from the M1 tip (Task 1 Step 1). The checks below compare against `BASE=$(git merge-base HEAD feat/v5-m1-engine-render-core)`, which is `f4b147e` (`docs(engine): ownership contracts, canvas precondition, Chromium-compat index`) at the time of writing and survives the M1 branch moving or merging.

**The fix-the-engine rule** (restated from M1 Task 6, binding here):

- The golden rule (spec section 6): zero interior differences, and edge deltas of 3 or less. None of the four gap scenes has an RGB shift.
- When a scene fails, use superpowers:systematic-debugging, starting from the heatmap the test writes to `diff-heatmaps/<scene>.png`.
- Fix the engine. Never edit a golden, a scene, `GoldenRenderTests.cs`, or `ImageDiff`'s tolerance. Never regenerate a golden. Only a person runs `tools/parity-ref`.
- If a difference turns out to be an unbridgeable Chromium-versus-Skia behavior, stop and report it with the heatmap and the measured numbers (`DiffResult` text). That calls for a spec-level decision (a new golden plus a divergence row), not an implementer's call.
- Every engine fix keeps all 30 M1 scenes green. Re-run the whole gate after each fix, not only the scene you fixed.
- The gap golden is the regression test for a fix. This task adds no test methods, so the counts below stay exact; a diagnostic assertion used while debugging is not committed.

- [ ] **Step 1: Prove that the gap goldens are appended, and that the 30 M1 goldens are byte-identical**

Run from the repo root in Git Bash:

```bash
BASE=$(git merge-base HEAD feat/v5-m1-engine-render-core); git log -1 --format='base %h %s' "$BASE"
BASE="$BASE" python - <<'EOF'
import hashlib, json, os, subprocess
BASE = os.environ["BASE"]
def show(path):
    return json.loads(subprocess.check_output(["git", "show", f"{BASE}:{path}"]))
old_scenes = show("tests/goldens/render/scenes.json")
old_index = show("tests/goldens/render/index.json")["scenes"]
scenes = json.load(open("tests/goldens/render/scenes.json", encoding="utf-8"))
source = json.load(open("tools/parity-ref/scenes.json", encoding="utf-8"))
index = json.load(open("tests/goldens/render/index.json", encoding="utf-8"))["scenes"]
assert scenes == source, "tests/goldens/render/scenes.json is not a copy of tools/parity-ref/scenes.json"
assert len(old_scenes) == 30 and scenes[:30] == old_scenes, "an M1 scene changed"
assert len(scenes) == 34, f"expected 34 scenes, found {len(scenes)}"
assert index[:30] == old_index, "an M1 index record changed"
assert [s["name"] for s in index[30:]] == [s["name"] for s in scenes[30:]], "index and scenes disagree on the gap scenes"
for rec in index:
    sha = hashlib.sha256(open(f"tests/goldens/render/{rec['name']}.png", "rb").read()).hexdigest()
    assert sha == rec["sha256"], f"{rec['name']}.png does not match its index sha256"
adjusted = lambda l: l.get("brightness", 100) != 100 or l.get("contrast", 100) != 100 or l.get("saturate", 100) != 100 or l.get("hueRotate", 0) != 0
kinds = {
    "ROT90": lambda s: any(l.get("rotation") == 90 for l in s.get("layers", [])),
    "BGPRESCALE": lambda s: "bgImage" in s,
    "MIP2": lambda s: any(l.get("scale", 1) < 0.25 for l in s.get("layers", [])),
    "OPACITYADJUST": lambda s: any(l.get("opacity", 1) < 1 and adjusted(l) for l in s.get("layers", [])),
}
for kind, test in kinds.items():
    names = [s["name"] for s in scenes[30:] if test(s)]
    assert names, f"no gap scene covers {kind}"
    print(kind, names[0])
print("34 scenes; 30 M1 goldens unchanged")
EOF
git diff --name-status "$BASE" -- "tests/goldens/render/*.png"
```

Expected:

- `base f4b147e docs(engine): ownership contracts, canvas precondition, Chromium-compat index` (or the later M1 tip M2a branched from, if the controller rebased).
- `ROT90 gap-layer-rotate-90`, `BGPRESCALE gap-bg-image-prescale`, `MIP2 gap-layer-mip2`, `OPACITYADJUST gap-opacity-with-adjust`.
- `34 scenes; 30 M1 goldens unchanged`.
- The `git diff` prints exactly four lines, each starting with `A` and naming one of the four gap PNGs. An `M` or `D` line means an M1 golden was touched. Stop and report it.

- [ ] **Step 2: Count the 34 frozen scenes in the loader test**

The failing test already exists: Task 3 left `All_frozen_scenes_load` red. Run it:

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderTests.All_frozen_scenes_load"`
Expected: `Failed: 1` with `Assert.Equal() Failure: Values differ`, `Expected: 30`, `Actual: 34`.

In `src/RBX15.Parity.Tests/SceneLoaderTests.cs`, in `All_frozen_scenes_load`, replace:

```csharp
        Assert.Equal(30, scenes.Count);
```

with:

```csharp
        Assert.Equal(34, scenes.Count); // 30 M0/M1 scenes + 4 M1-gap scenes (M2a Task 3)
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderTests"`
Expected: `Passed: 4, Failed: 0`.

```bash
git add src/RBX15.Parity.Tests/SceneLoaderTests.cs
git commit -m "test(parity): the render scene set holds 34 frozen scenes"
```

- [ ] **Step 3: Run the 34-scene gate**

The gap rows are the failing tests of this step, if Task 3 Step 5 recorded any `fail`. Run:

```bash
mkdir -p .superpowers/logs
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests" > .superpowers/logs/golden.log 2>&1; tail -30 .superpowers/logs/golden.log
```

Expected after this step: `Passed: 34`, `Failed: 0`. The Parity.Tests project reports that no test matches the filter. That is fine.

If any scene fails, apply the fix-the-engine rule above. The Chromium-compat index at the top of `src/RBX15.Engine/Renderer.cs` maps each gap scene to the code most likely at fault:

| Gap scene | Index row | Where to look first |
| --- | --- | --- |
| `gap-layer-rotate-90` | Blink ShouldDrawImageAntialiased; FloatPipeline | `Renderer.DrawImageAntialiased` and `IsScaleTranslate`. A 90 degree rotation keeps rects as rects, so Blink draws without antialiasing. A rotation is never scale-translate, so the draw takes the float pipeline. **Risk, an unverified hypothesis to test first:** `canvas.RotateRadians((float)(90 * Math.PI / 180))` may leave a float residue of about 1e-8 in `ScaleX`/`ScaleY` instead of an exact 0, where Chromium's `SkMatrix::setRotate` snaps sin and cos. That would make `rectStaysRect` false and turn antialiasing on. Check `canvas.TotalMatrix` with a throwaway assertion before changing anything. |
| `gap-bg-image-prescale` | cc decode cache | `DrawBackground` (it calls the decode cache with `585 / w` and `559 / h`), `Renderer.DecodeCacheSize` and `Images.DecodeCacheScaled`. Until now only a unit test exercised this path, with no golden. |
| `gap-layer-mip2` | cc decode cache | `Renderer.DecodeCacheSize`: the level loop, and ceil-halving of odd sizes at level 2 (Anton, 198x125, at 0.2: 99x63, then 50x32). |
| `gap-opacity-with-adjust` | AutoLayerForImageFilter | `Renderer.DrawLayer`. Opacity is on the inner paint, inside `SaveLayer(dst, filter)`. Which paint carries the alpha (the inner draw or the layer restore) is the suspect. The 4.1 behavior is `editor.js:176-234` (`globalAlpha` then `ctx.filter`). |

Each fix is its own commit, after the full gate and `RendererTests` pass:

```bash
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests|FullyQualifiedName~RendererTests"
git add src/RBX15.Engine
git commit -m "fix(engine): exact 90-degree rotation draws without antialiasing"
```

Expected before each such commit: `Passed: 53, Failed: 0` (34 golden rows plus M1's 19 `RendererTests`). The subject above is the form for the ROT90 case; for any other fix, write a `fix(engine):` subject that names the Chromium mechanism the heatmap proved, the same way. Also add the scene that proved the fix to the matching index row in Step 4. If no scene failed, there is nothing to commit in this step.

- [ ] **Step 4: Record the gap goldens in the Chromium-compat index**

The index says the background pre-scale path has no golden. That line is now false. This script edits only the doc comment. It reads the scene names from the same classification Step 1 printed, and each replacement must match exactly once:

```bash
python - <<'EOF'
import json
scenes = json.load(open("tests/goldens/render/scenes.json", encoding="utf-8"))[30:]
adjusted = lambda l: l.get("brightness", 100) != 100 or l.get("contrast", 100) != 100 or l.get("saturate", 100) != 100 or l.get("hueRotate", 0) != 0
def first(test):
    return next(s["name"] for s in scenes if test(s))
rot90 = first(lambda s: any(l.get("rotation") == 90 for l in s.get("layers", [])))
bg = first(lambda s: "bgImage" in s)
mip2 = first(lambda s: any(l.get("scale", 1) < 0.25 for l in s.get("layers", [])))
opadj = first(lambda s: any(l.get("opacity", 1) < 1 and adjusted(l) for l in s.get("layers", [])))
path = "src/RBX15.Engine/Renderer.cs"
text = open(path, encoding="utf-8", newline="").read()
edits = [
    ("layer-rotate-neg-flipv-upscale, rgb-shift-with-adjust-rotated</description>",
     f"layer-rotate-neg-flipv-upscale, rgb-shift-with-adjust-rotated, {rot90} (exactly 90: rect stays rect, no antialiasing)</description>"),
    ("rgb-shift-with-adjust-rotated, full-scene (unit test Rotated_layer_composites_in_float_not_lowp)",
     f"rgb-shift-with-adjust-rotated, full-scene, {rot90} (unit test Rotated_layer_composites_in_float_not_lowp)"),
    ("adjust-extremes, rgb-shift-with-adjust-rotated, full-scene</description>",
     f"adjust-extremes, rgb-shift-with-adjust-rotated, full-scene, {opadj} (opacity under a filter)</description>"),
    ("full-scene only (scaled-down word art); the background path is pinned by a unit test, no golden",
     f"full-scene (scaled-down word art), {mip2} (mip level 2), {bg} (background pre-scale)"),
]
for old, new in edits:
    assert text.count(old) == 1, f"expected exactly one match for: {old}"
    text = text.replace(old, new)
open(path, "w", encoding="utf-8", newline="").write(text)
print("index updated:", rot90, bg, mip2, opadj)
EOF
git diff --stat -- src/RBX15.Engine/Renderer.cs
dotnet build src/RBX15.slnx -c Release -warnaserror
```

Expected: `index updated: gap-layer-rotate-90 gap-bg-image-prescale gap-layer-mip2 gap-opacity-with-adjust`, a diff of `1 file changed, 4 insertions(+), 4 deletions(-)` in `Renderer.cs`, and a build with `0 Warning(s)` and `0 Error(s)`. All four replacement strings match exactly once at `f4b147e`. If Step 3 needed engine fixes, add each proving scene by hand to the row its fix touched; the diff then also shows those lines.

```bash
git add src/RBX15.Engine/Renderer.cs
git commit -m "docs(engine): index the four M1-gap goldens in the Chromium-compat table"
```

- [ ] **Step 5: Run everything**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: `0 Warning(s)`. Engine.Tests `Passed: 101, Failed: 0, Skipped: 0` (97 plus the 4 gap rows). Parity.Tests `Passed: 13, Failed: 0`. The branch is green again, and every later task starts from `Failed: 0`.

---

### Task 5: Engine Tools, SceneLoader v2 and the app golden gate

**Files:**

- Create: `src/RBX15.Engine/Tools.cs`, `src/RBX15.Engine.Tests/ToolsTests.cs`, `src/RBX15.Engine.Tests/GoldenRule.cs`, `src/RBX15.Engine.Tests/GoldenAppTests.cs`
- Modify: `src/RBX15.Parity/SceneLoader.cs` (scene format v2: `actions`, `UnsupportedOps`), `src/RBX15.Parity.Tests/SceneLoaderTests.cs` (6 new facts), `src/RBX15.Engine.Tests/GoldenRenderTests.cs` (calls the shared `GoldenRule`; behavior unchanged)

**Interfaces:**

- Consumes:
  - `Layer`, `Document` (`src/RBX15.Engine/Document.cs`), `TemplateSize`, `CssColor.ToSk`, `Renderer.Export` (M1)
  - `ImageDiff.Load/FromImage/Compare/Heatmap`, `RepoRoot.Find`, `SceneLoader` (M1); test helpers `Px.At`, `Px.Render` (M1)
  - `tests/goldens/app/scenes.json` and `tests/goldens/app/<name>.png` (Task 3; frozen, never edited here)
  - The 34-scene `GoldenRenderTests` gate, green (Task 4)
- Produces:
  - `public enum ShapeTool { Pen, Line, Rect, Ellipse }`
  - `public sealed record Stroke(ShapeTool Tool, IReadOnlyList<SKPoint> Points, SKColor Color, float Width, bool Fill)`
  - `public static SKImage Tools.RenderStroke(Stroke stroke)`
  - `public static (SKImage Image, int X, int Y)? Tools.Autocrop(SKImage canvas, int pad = 10)`
  - `public static Layer? Tools.CommitDrawing(Stroke stroke, int id, string name)`
  - `public static string Tools.DrawingName(ShapeTool tool)`: the name 4.1 passes to `addDrawingLayer` (helper; the contract's `name` parameter is filled from it)
  - `public static IReadOnlyList<string> SceneLoader.UnsupportedOps(JsonElement scene)`
  - `SceneLoader.Parse(JsonElement e, string repoRoot)` runs `shape` actions; a scene holding any other op throws `NotSupportedException`
  - `static class GoldenRule` (Engine.Tests) with `public static void AssertMatches(string name, string goldenPath, Document document, string heatmapPath)`
  - `GoldenAppTests` with `public static TheoryData<string> ScenesBlockedBy(string op)` and the theory `Engine_export_matches_the_frozen_4_1_app_golden(string name)`, run in CI on every PR

**What 4.1 does, and what this task ports** (all lines in `editor.js` unless named):

- **Brush state.** `drawColor` `#17d4fa`, `drawSize` 4, `drawFill` false (`:90-92`). Colors are opaque; the color input has no alpha (ledger `draw-brush-controls`).
- **The stroke canvas.** Every gesture draws into a fresh transparent 585x559 canvas (`:1128-1131`), so anything outside the template is lost (ledger `draw-canvas-template-clip`).
- **Pen.** `lineWidth = drawSize`, `lineCap` and `lineJoin` round, `beginPath`, `moveTo(start)` (`:1133-1143`); every move does `lineTo` + `stroke()` on the same open path (`:1199-1203`), and mouseup strokes once more (`:1253-1255`). v5 builds the same path from all points and strokes it **once** (approved divergence, ledger `draw-pen-incremental-stroke`). 4.1 paints nothing for a press with no movement (a lone `moveTo`); v5 draws a filled dot of diameter `Width` (approved divergence, ledger `draw-pen-single-click`, spec section 2 Tools row).
- **Shapes** are drawn only on mousemove, into a cleared canvas (`:1205-1234`); mouseup never redraws, so the shape is the one for the last move point. `strokeStyle = fillStyle = drawColor`, `lineWidth = drawSize`, `lineCap = 'round'`, and `lineJoin` is never set, so it is canvas-default miter with `miterLimit` 10 (`:1207-1210`). A press with no move never reaches this code, so nothing is drawn.
  - **Line** (`:1212-1216`): `moveTo(start)`, `lineTo(mouse)`, `stroke()`. `drawFill` is ignored.
  - **Rect** (`:1217-1223`): `x = min`, `y = min`, `w = |dx|`, `h = |dy|`; `fillRect` when Fill, else `strokeRect` (centered on the edge, miter corners).
  - **Ellipse** (tool id `circle`, `:1224-1233`): center = midpoint, `rx = |dx|/2`, `ry = |dy|/2`, `ellipse(cx, cy, rx, ry, 0, 0, 2π)` after `beginPath`; `fill()` (nonzero) when Fill, else `stroke()`.
- **How Chromium turns those calls into Skia paths** (ported so SkiaSharp sees the same geometry; these are Chromium source behaviors, not 4.1 JS, and no golden pins the degenerate cases):
  - A full `ellipse()` is two 180-degree `arcTo`s on the oval from angle 0, not `addOval` (Blink `Path::AddEllipse`). `Fills.cs` already uses this form for 4.1's `arc` dots and passes its goldens.
  - `ellipse()` with a zero radius is `DegenerateEllipse`: line segments through the points at 0, π/2, π, 3π/2 and 2π. Both radii zero leaves a lone `moveTo`, which paints nothing.
  - `strokeRect` with exactly one zero side strokes the closed path `moveTo(x, y)`, `lineTo(right, bottom)`, `close()` (Blink `StrokeRectOnCanvas`); otherwise Skia `drawRect`.
- **Commit** (`:1249-1293`): if any pixel has alpha > 0, `addDrawingLayer(drawCanvas, Tool + ' drawing')` (`:1285`), with the tool id capitalized: `Pen drawing`, `Line drawing`, `Rect drawing`, `Circle drawing`. Otherwise no layer (ledger `draw-empty-no-layer`).
- **addDrawingLayer** (`:528-540`): `autocrop`, then `crop.canvas.toDataURL()` loaded into an `Image`, then `makeLayer(img, name, crop.x, crop.y, 1)` pushed on top. So the layer is at the crop origin, scale 1, every other field a `makeLayer` default (`:134-142`). The image went through a PNG round trip: un-premultiplied by `toDataURL`, re-premultiplied when the `img` decodes. v5 keeps the layer image un-premultiplied (`Rgba8888`/`Unpremul`, the same form `Images.Decode` gives), and drawing re-premultiplies it through `Images.ForDrawing`, exactly as every other layer image.
- **autocrop** (`:144-170`): `pad = pad || 10` (`:145`, so 0 means 10); scan every pixel for alpha > 0 (`getImageData`, un-premultiplied); none means null (`:159`); pad and clamp to `[0, size-1]` (`:160-163`); copy the box at identity into a new canvas (`:164-169`); return `{canvas, x, y}`.

**Contract resolutions made in this task** (flagged for the controller):

- `CommitDrawing(stroke, id, name)` keeps the binding signature. The 4.1 name comes from `Tools.DrawingName(stroke.Tool)`, which SceneLoader v2 (and later the app) passes as `name`.
- Shapes with fewer than two points draw nothing (4.1 never draws a shape without a move). The dot-on-press divergence is the pen's only.
- Shape actions in a scene must carry `tool`, `from`, `to`, `color` and `width`; `fill` defaults to false. A missing field is a malformed scene and throws, rather than silently taking a default the reference renderer may not have used.
- In this task SceneLoader v2 runs `shape` only. `erase` scenes are skipped by name until Task 6 adds `"erase"` to `SupportedOps` and replaces `GoldenAppTests` without its `erase` row. The `UnsupportedOps` loader test uses `detail`, an op that stays unsupported through M2a, so Task 6 does not break it.

**When an app scene fails** (the M1 Task 6 rule, unchanged):

- Use superpowers:systematic-debugging, starting from the heatmap the test writes to `diff-heatmaps/app/<scene>.png`.
- Fix the engine. Never edit a golden, `scenes.json` or the rule.
- If a difference turns out to be an unbridgeable Chromium-versus-Skia behavior, stop and report it with the heatmap and the measured numbers (`MaxDelta`, `InteriorDifferences`, `EdgeOverTolerance`). That calls for a spec-level decision (a new golden plus a divergence row), not an implementer's call.

- [ ] **Step 1: Write the failing Tools tests**

Every expected value below follows from the geometry (integer-aligned edges, or pixels well inside or well outside a stroke), not from a 4.1 run. Pixel `(x, y)` covers `[x, x+1] x [y, y+1]`.

Create `src/RBX15.Engine.Tests/ToolsTests.cs`:

```csharp
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class ToolsTests
{
    static readonly SKColor Cyan = new(0x17, 0xd4, 0xfa);

    static Stroke Shape(ShapeTool tool, float x0, float y0, float x1, float y1, float width = 4, bool fill = false) =>
        new(tool, new[] { new SKPoint(x0, y0), new SKPoint(x1, y1) }, Cyan, width, fill);

    static int Differing(SKImage a, SKImage b)
    {
        using var x = ImageDiff.FromImage(a);
        using var y = ImageDiff.FromImage(b);
        return ImageDiff.Compare(x, y).DifferingPixels;
    }

    static SKImage Block(int x, int y, int w, int h) => Px.Render(TemplateSize.Width, TemplateSize.Height, c =>
    {
        using var p = new SKPaint { Color = SKColors.Red };
        c.DrawRect(SKRect.Create(x, y, w, h), p);
    });

    // ── RenderStroke ─────────────────────────────────────────────

    [Fact]
    public void Stroke_canvas_is_template_sized_kN32_and_transparent()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100));
        Assert.Equal((TemplateSize.Width, TemplateSize.Height), (img.Width, img.Height));
        Assert.Equal(SKImageInfo.PlatformColorType, img.ColorType);
        Assert.Equal(0, Px.At(img, 10, 10).Alpha);
    }

    [Fact]
    public void Pen_strokes_the_whole_path_once()
    {
        // Half-alpha color: a path stroked once covers its own crossing a single time, so the
        // crossing has the same alpha as a plain segment. 4.1's per-move re-stroke would composite
        // the crossing over itself (editor.js:1199-1203, ledger draw-pen-incremental-stroke).
        var half = new SKColor(255, 0, 0, 128);
        var pen = new Stroke(ShapeTool.Pen,
            new[] { new SKPoint(100, 100), new SKPoint(200, 200), new SKPoint(200, 100), new SKPoint(100, 200) }, half, 10, false);
        using var img = Tools.RenderStroke(pen);
        Assert.Equal(128, Px.At(img, 150, 150).Alpha); // the self-crossing
        Assert.Equal(128, Px.At(img, 200, 150).Alpha); // middle of the vertical segment
    }

    [Fact]
    public void Pen_press_without_movement_draws_a_dot_of_the_brush_size()
    {
        // Approved divergence draw-pen-single-click: 4.1 paints nothing for a lone moveTo.
        using var img = Tools.RenderStroke(new Stroke(ShapeTool.Pen, new[] { new SKPoint(100.5f, 100.5f) }, Cyan, 10, false));
        Assert.Equal(255, Px.At(img, 100, 100).Alpha);
        Assert.Equal(255, Px.At(img, 103, 100).Alpha); // farthest corner 3.54 from center, radius 5
        Assert.Equal(0, Px.At(img, 106, 100).Alpha);   // nearest point 5.5 from center
        Assert.Equal(0, Px.At(img, 94, 100).Alpha);

        // Ledger autocrop parity check: layer bounds = dot bbox (pixels 95..105) padded by 10.
        var crop = Tools.Autocrop(img);
        Assert.NotNull(crop);
        using var cropped = crop.Value.Image;
        Assert.Equal((85, 85, 31, 31), (crop.Value.X, crop.Value.Y, cropped.Width, cropped.Height));
    }

    [Fact]
    public void Line_has_round_caps_and_ignores_fill()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100, width: 10));
        Assert.Equal(255, Px.At(img, 150, 100).Alpha);
        Assert.Equal(255, Px.At(img, 97, 100).Alpha);  // inside the round cap; a butt cap stops at x=100
        Assert.Equal(0, Px.At(img, 206, 100).Alpha);   // past the cap radius
        using var filled = Tools.RenderStroke(Shape(ShapeTool.Line, 100, 100, 200, 100, width: 10, fill: true));
        Assert.Equal(0, Differing(img, filled));       // editor.js:1212-1216 never reads drawFill
    }

    [Fact]
    public void Rect_fill_covers_the_drag_rect_in_any_direction()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 120, fill: true));
        Assert.Equal(255, Px.At(img, 100, 100).Alpha);
        Assert.Equal(255, Px.At(img, 149, 119).Alpha);
        Assert.Equal(0, Px.At(img, 99, 110).Alpha);
        Assert.Equal(0, Px.At(img, 150, 110).Alpha);
        Assert.Equal(0, Px.At(img, 125, 99).Alpha);
        Assert.Equal(0, Px.At(img, 125, 120).Alpha);
        using var reversed = Tools.RenderStroke(Shape(ShapeTool.Rect, 150, 120, 100, 100, fill: true));
        Assert.Equal(0, Differing(img, reversed));     // min/abs normalization, editor.js:1218-1221
    }

    [Fact]
    public void Rect_stroke_is_centered_on_the_edge_with_miter_corners()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 120, width: 4));
        Assert.Equal(255, Px.At(img, 98, 110).Alpha);  // band x 98..102 on the left edge
        Assert.Equal(255, Px.At(img, 101, 110).Alpha);
        Assert.Equal(0, Px.At(img, 97, 110).Alpha);
        Assert.Equal(0, Px.At(img, 102, 110).Alpha);
        Assert.Equal(0, Px.At(img, 125, 110).Alpha);   // hollow
        Assert.Equal(255, Px.At(img, 98, 98).Alpha);   // square outer corner: a round join would cut it
    }

    [Fact]
    public void Rect_stroke_with_zero_height_draws_a_line()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Rect, 100, 100, 150, 100, width: 4));
        Assert.Equal(255, Px.At(img, 125, 100).Alpha);
        Assert.Equal(255, Px.At(img, 125, 98).Alpha);
        Assert.Equal(0, Px.At(img, 125, 97).Alpha);
    }

    [Fact]
    public void Ellipse_fill_uses_the_drag_box_center_and_radii()
    {
        // (100,100)-(160,140): center (130,120), rx 30, ry 20 (editor.js:1225-1228).
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 160, 140, fill: true));
        Assert.Equal(255, Px.At(img, 130, 120).Alpha);
        Assert.Equal(0, Px.At(img, 99, 120).Alpha);
        Assert.Equal(0, Px.At(img, 160, 120).Alpha);
        Assert.Equal(0, Px.At(img, 130, 99).Alpha);
        Assert.Equal(0, Px.At(img, 130, 140).Alpha);
        Assert.Equal(0, Px.At(img, 101, 101).Alpha);   // the box corner is outside the ellipse
        using var reversed = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 160, 140, 100, 100, fill: true));
        Assert.Equal(0, Differing(img, reversed));
    }

    [Fact]
    public void Ellipse_stroke_is_hollow()
    {
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 160, 140, width: 4));
        Assert.Equal(0, Px.At(img, 130, 120).Alpha);
        Assert.True(Px.At(img, 130, 100).Alpha > 0);
        Assert.True(Px.At(img, 100, 120).Alpha > 0);
    }

    [Fact]
    public void Ellipse_with_one_zero_radius_strokes_a_line()
    {
        // Blink DegenerateEllipse: segments through the points at 0, pi/2, pi, 3pi/2, 2pi.
        using var img = Tools.RenderStroke(Shape(ShapeTool.Ellipse, 100, 100, 100, 140, width: 4));
        Assert.Equal(255, Px.At(img, 100, 120).Alpha);
        Assert.Equal(255, Px.At(img, 98, 120).Alpha);
        Assert.Equal(0, Px.At(img, 97, 120).Alpha);
    }

    // ── Autocrop ─────────────────────────────────────────────────

    [Fact]
    public void Autocrop_pads_the_alpha_bounds_by_10_and_copies_pixels()
    {
        using var img = Block(50, 60, 4, 3);
        var crop = Tools.Autocrop(img);
        Assert.NotNull(crop);
        using var cropped = crop.Value.Image;
        Assert.Equal((40, 50, 24, 23), (crop.Value.X, crop.Value.Y, cropped.Width, cropped.Height));
        Assert.Equal(SKColors.Red, Px.At(cropped, 10, 10));
        Assert.Equal(SKColors.Red, Px.At(cropped, 13, 12));
        Assert.Equal(0, Px.At(cropped, 9, 10).Alpha);
        Assert.Equal(0, Px.At(cropped, 14, 12).Alpha);
    }

    [Fact]
    public void Autocrop_clips_the_padding_to_the_canvas()
    {
        using var topLeft = Block(0, 0, 2, 2);
        var a = Tools.Autocrop(topLeft)!.Value;
        using (a.Image) Assert.Equal((0, 0, 12, 12), (a.X, a.Y, a.Image.Width, a.Image.Height));

        using var bottomRight = Block(583, 557, 2, 2);
        var b = Tools.Autocrop(bottomRight)!.Value;
        using (b.Image) Assert.Equal((573, 547, 12, 12), (b.X, b.Y, b.Image.Width, b.Image.Height));
    }

    [Fact]
    public void Autocrop_of_an_empty_canvas_is_null()
    {
        using var empty = Px.Render(TemplateSize.Width, TemplateSize.Height, _ => { });
        Assert.Null(Tools.Autocrop(empty));
    }

    [Fact]
    public void Autocrop_counts_alpha_1_and_pad_0_means_10()
    {
        using var bmp = new SKBitmap(new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bmp.Erase(SKColors.Transparent);
        bmp.SetPixel(300, 200, new SKColor(255, 0, 0, 1));
        using var img = SKImage.FromBitmap(bmp);

        var crop = Tools.Autocrop(img)!.Value;
        using (crop.Image) Assert.Equal((290, 190, 21, 21), (crop.X, crop.Y, crop.Image.Width, crop.Image.Height));

        var zero = Tools.Autocrop(img, 0)!.Value; // pad || 10, editor.js:145
        using (zero.Image) Assert.Equal((290, 190, 21, 21), (zero.X, zero.Y, zero.Image.Width, zero.Image.Height));
    }

    // ── CommitDrawing ────────────────────────────────────────────

    [Fact]
    public void Commit_places_the_cropped_layer_at_the_crop_origin()
    {
        var layer = Tools.CommitDrawing(Shape(ShapeTool.Rect, 100, 100, 150, 120, fill: true), 7, "Rect drawing");
        Assert.NotNull(layer);
        Assert.Equal((7, "Rect drawing"), (layer.Id, layer.Name));
        Assert.Equal((90.0, 90.0, 1.0, 0.0, 1.0, true), (layer.X, layer.Y, layer.Scale, layer.Rotation, layer.Opacity, layer.Visible));
        Assert.Equal((false, false, 100.0, 100.0, 100.0, 0.0), (layer.FlipH, layer.FlipV, layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate));
        Assert.Equal((70, 40), (layer.Image.Width, layer.Image.Height));
        // The toDataURL round trip: stored un-premultiplied, like Images.Decode.
        Assert.Equal((SKColorType.Rgba8888, SKAlphaType.Unpremul), (layer.Image.ColorType, layer.Image.AlphaType));
        Assert.Equal(new SKColor(0x17, 0xd4, 0xfa, 255), Px.At(layer.Image, 10, 10));
        Assert.Equal(0, Px.At(layer.Image, 9, 10).Alpha);
    }

    [Fact]
    public void Nothing_drawn_commits_no_layer()
    {
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Rect, 100, 100, 100, 100, fill: true), 1, "Rect drawing"));
        Assert.Null(Tools.CommitDrawing(new Stroke(ShapeTool.Line, new[] { new SKPoint(100, 100) }, Cyan, 4, false), 1, "Line drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 100, 100, 100), 1, "Circle drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 100, 100, 140, fill: true), 1, "Circle drawing"));
        Assert.Null(Tools.CommitDrawing(new Stroke(ShapeTool.Pen, Array.Empty<SKPoint>(), Cyan, 4, false), 1, "Pen drawing"));
    }

    [Fact]
    public void A_shape_entirely_off_the_template_commits_no_layer()
    {
        // Review Focus: the stroke canvas is 585x559 (editor.js:1128-1131), so a drag wholly outside
        // it leaves no pixel and 4.1's alpha scan commits nothing (editor.js:1257-1262).
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Line, -60, -60, -10, -10), 1, "Line drawing")); // round cap reaches x = -8
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Rect, 600, 100, 700, 200, fill: true), 1, "Rect drawing"));
        Assert.Null(Tools.CommitDrawing(Shape(ShapeTool.Ellipse, 100, 570, 200, 640), 1, "Circle drawing")); // top edge y = 568
    }

    [Fact]
    public void Commit_keeps_only_the_part_inside_the_template()
    {
        var layer = Tools.CommitDrawing(Shape(ShapeTool.Line, -50, 100, 50, 100), 1, "Line drawing");
        Assert.NotNull(layer);
        Assert.Equal((0.0, 88.0), (layer.X, layer.Y)); // left end clipped at x=0; band y 98..102, minus pad
    }

    [Theory]
    [InlineData(ShapeTool.Pen, "Pen drawing")]
    [InlineData(ShapeTool.Line, "Line drawing")]
    [InlineData(ShapeTool.Rect, "Rect drawing")]
    [InlineData(ShapeTool.Ellipse, "Circle drawing")]
    public void Drawing_layers_are_named_after_the_4_1_tool_id(ShapeTool tool, string name) =>
        Assert.Equal(name, Tools.DrawingName(tool));
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~ToolsTests"`
Expected: build failure, `error CS0246: The type or namespace name 'Stroke' could not be found` (and the same for `ShapeTool`), plus `The name 'Tools' does not exist in the current context`.

- [ ] **Step 3: Implement Tools**

Create `src/RBX15.Engine/Tools.cs`:

```csharp
using System.Runtime.InteropServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>The drawing tools that make layers. 4.1's tool id for Ellipse is 'circle'.</summary>
public enum ShapeTool { Pen, Line, Rect, Ellipse }

/// <summary>
/// One finished gesture in template coordinates. Pen uses every point; Line, Rect and Ellipse use
/// the first and the last (4.1 draws shapes from the press point to the last move point).
/// </summary>
public sealed record Stroke(ShapeTool Tool, IReadOnlyList<SKPoint> Points, SKColor Color, float Width, bool Fill);

/// <summary>
/// Pen, line, rect and ellipse geometry, autocrop and the drawing commit (editor.js:1120-1293,
/// :528-540, :144-170). Strokes render on the same kN32 premultiplied basis as <see cref="Renderer"/>,
/// which is the surface Chromium's software canvas draws into.
/// </summary>
public static class Tools
{
    /// <summary>The name 4.1 gives a drawing layer: the tool id capitalized plus ' drawing' (editor.js:1285).</summary>
    public static string DrawingName(ShapeTool tool) => tool switch
    {
        ShapeTool.Pen => "Pen drawing",
        ShapeTool.Line => "Line drawing",
        ShapeTool.Rect => "Rect drawing",
        ShapeTool.Ellipse => "Circle drawing",
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

    /// <summary>
    /// A 585x559 transparent canvas with the stroke drawn as 4.1 draws it. Pen: one path stroked
    /// once with round caps and joins (approved divergence: 4.1 re-strokes per move); a single point
    /// is a filled dot of diameter Width (approved divergence: 4.1 paints nothing). Shapes need two
    /// points, as 4.1 only draws them on mousemove (editor.js:1205-1234).
    /// </summary>
    public static SKImage RenderStroke(Stroke stroke)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var pts = stroke.Points;
        if (stroke.Tool == ShapeTool.Pen)
        {
            if (pts.Count > 0) DrawPen(canvas, stroke);
        }
        else if (pts.Count >= 2)
        {
            DrawShape(canvas, stroke, pts[0], pts[pts.Count - 1]);
        }
        return surface.Snapshot();
    }

    // Canvas state for a stroke: lineWidth = drawSize, lineCap round on every tool (editor.js:1140,
    // :1210); lineJoin round for the pen (:1141) and canvas-default miter, miterLimit 10, for shapes.
    static SKPaint StrokePaint(Stroke s, SKStrokeJoin join) => new()
    {
        Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = s.Width,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = join, StrokeMiter = 10,
    };

    static SKPaint FillPaint(Stroke s) => new() { Color = s.Color, IsAntialias = true, Style = SKPaintStyle.Fill };

    static void DrawPen(SKCanvas canvas, Stroke s)
    {
        var pts = s.Points;
        if (pts.Count == 1)
        {
            using var dot = FillPaint(s);
            canvas.DrawCircle(pts[0], s.Width / 2, dot);
            return;
        }
        using var pb = new SKPathBuilder();
        pb.MoveTo(pts[0].X, pts[0].Y);
        for (int i = 1; i < pts.Count; i++) pb.LineTo(pts[i].X, pts[i].Y);
        using var path = pb.Detach();
        using var paint = StrokePaint(s, SKStrokeJoin.Round);
        canvas.DrawPath(path, paint);
    }

    static void DrawShape(SKCanvas canvas, Stroke s, SKPoint a, SKPoint b)
    {
        switch (s.Tool)
        {
            case ShapeTool.Line:
            {
                // editor.js:1212-1216. drawFill is never read for lines.
                using var pb = new SKPathBuilder();
                pb.MoveTo(a.X, a.Y);
                pb.LineTo(b.X, b.Y);
                using var path = pb.Detach();
                using var paint = StrokePaint(s, SKStrokeJoin.Miter);
                canvas.DrawPath(path, paint);
                break;
            }
            case ShapeTool.Rect:
            {
                // editor.js:1218-1223: normalized box, fillRect or strokeRect.
                var rect = SKRect.Create(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
                if (s.Fill)
                {
                    using var fill = FillPaint(s);
                    canvas.DrawRect(rect, fill);
                }
                else
                {
                    StrokeRect(canvas, rect, s);
                }
                break;
            }
            case ShapeTool.Ellipse:
            {
                // editor.js:1225-1233, in JS doubles, handed to canvas as floats.
                float cx = (float)(((double)a.X + b.X) / 2), cy = (float)(((double)a.Y + b.Y) / 2);
                float rx = (float)(Math.Abs((double)b.X - a.X) / 2), ry = (float)(Math.Abs((double)b.Y - a.Y) / 2);
                using var path = EllipsePath(cx, cy, rx, ry);
                if (path is null) break;
                using var paint = s.Fill ? FillPaint(s) : StrokePaint(s, SKStrokeJoin.Miter);
                canvas.DrawPath(path, paint);
                break;
            }
        }
    }

    /// <summary>
    /// Blink StrokeRectOnCanvas: a rect with exactly one zero side is stroked as the closed path
    /// (x, y) to (right, bottom), so the zero-length sides add nothing; otherwise drawRect.
    /// </summary>
    static void StrokeRect(SKCanvas canvas, SKRect r, Stroke s)
    {
        using var paint = StrokePaint(s, SKStrokeJoin.Miter);
        if ((r.Width > 0) != (r.Height > 0))
        {
            using var pb = new SKPathBuilder();
            pb.MoveTo(r.Left, r.Top);
            pb.LineTo(r.Right, r.Bottom);
            pb.Close();
            using var line = pb.Detach();
            canvas.DrawPath(line, paint);
            return;
        }
        canvas.DrawRect(r, paint);
    }

    /// <summary>
    /// ctx.ellipse(cx, cy, rx, ry, 0, 0, 2pi) after beginPath, as Blink builds it. Full ellipse:
    /// two 180-degree arcs from angle 0 (Path::AddEllipse). A zero radius: DegenerateEllipse, line
    /// segments through the points at 0, pi/2, pi, 3pi/2 and 2pi; both zero leaves a lone moveTo,
    /// which paints nothing, so null.
    /// </summary>
    static SKPath? EllipsePath(float cx, float cy, float rx, float ry)
    {
        if (rx == 0 && ry == 0) return null;
        using var pb = new SKPathBuilder();
        if (rx == 0 || ry == 0)
        {
            const float halfPi = MathF.PI / 2, twoPi = MathF.PI * 2;
            pb.MoveTo(cx + rx, cy);
            for (float angle = halfPi; angle < twoPi; angle += halfPi)
                pb.LineTo(cx + rx * MathF.Cos(angle), cy + ry * MathF.Sin(angle));
            pb.LineTo(cx + rx * MathF.Cos(twoPi), cy + ry * MathF.Sin(twoPi));
            return pb.Detach();
        }
        var oval = new SKRect(cx - rx, cy - ry, cx + rx, cy + ry);
        pb.MoveTo(cx + rx, cy);
        pb.ArcTo(oval, 0, 180, false);
        pb.ArcTo(oval, 180, 180, false);
        return pb.Detach();
    }

    /// <summary>
    /// autocrop (editor.js:144-170): the alpha &gt; 0 bounding box, padded (pad || 10, so 0 means 10),
    /// clamped to the canvas, copied at identity into a kN32 premultiplied image. Null when nothing
    /// has alpha. Caller owns the returned image.
    /// </summary>
    public static (SKImage Image, int X, int Y)? Autocrop(SKImage canvas, int pad = 10)
    {
        if (pad == 0) pad = 10;
        if (pad < 0) throw new ArgumentOutOfRangeException(nameof(pad), "4.1 callers never pass a negative pad");
        int sw = canvas.Width, sh = canvas.Height;
        var alpha = ReadAlpha(canvas);
        int minX = sw, minY = sh, maxX = 0, maxY = 0;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                if (alpha[y * sw + x] > 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < minX) return null;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(sw - 1, maxX + pad);
        maxY = Math.Min(sh - 1, maxY + pad);
        int cropW = maxX - minX + 1, cropH = maxY - minY + 1;

        var info = new SKImageInfo(cropW, cropH, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create crop surface");
        surface.Canvas.Clear(SKColors.Transparent);
        // drawImage(source, minX, minY, w, h, 0, 0, w, h): an integer translate at identity scale,
        // so every pixel copies exactly.
        using var copy = new SKPaint();
        surface.Canvas.DrawImage(canvas, -minX, -minY, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), copy);
        return (surface.Snapshot(), minX, minY);
    }

    static byte[] ReadAlpha(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        var alpha = new byte[image.Width * image.Height];
        var handle = GCHandle.Alloc(alpha, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), image.Width, 0, 0))
                throw new InvalidOperationException("cannot read stroke alpha");
        }
        finally
        {
            handle.Free();
        }
        return alpha;
    }

    /// <summary>
    /// The layer 4.1's mouseup + addDrawingLayer create (editor.js:1257-1285, :528-540): the stroke
    /// autocropped, at the crop origin, scale 1, every other field a makeLayer default (:134-142).
    /// The image is stored un-premultiplied, the form 4.1's toDataURL + img decode round trip
    /// leaves it in and the form Images.Decode gives; drawing re-premultiplies it. Null when the
    /// stroke left no pixel with alpha &gt; 0 (no layer, ledger draw-empty-no-layer).
    /// </summary>
    public static Layer? CommitDrawing(Stroke stroke, int id, string name)
    {
        using var drawn = RenderStroke(stroke);
        if (Autocrop(drawn) is not { } crop) return null;
        using var premul = crop.Image;
        var info = new SKImageInfo(premul.Width, premul.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!premul.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read cropped drawing");
        return new Layer { Id = id, Name = name, Image = SKImage.FromPixelCopy(bmp.PeekPixels()), X = crop.X, Y = crop.Y, Scale = 1 };
    }
}
```

- [ ] **Step 4: Run the Tools tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~ToolsTests"`
Expected: `Passed: 22` (18 facts + 4 naming cases), `Failed: 0`, 0 warnings.

If a geometry assertion fails, read the pixel before touching anything: every expected value above is a full-coverage or zero-coverage pixel by construction, so a failure means the path or paint differs from the 4.1 call it ports. Fix `Tools.cs`, not the test.

- [ ] **Step 5: Commit**

```bash
git add src/RBX15.Engine/Tools.cs src/RBX15.Engine.Tests/ToolsTests.cs
git commit -m "feat(engine): drawing tools, autocrop and the drawing-layer commit"
```

Add your model's `Co-Authored-By` trailer to the message (Global Constraints).

- [ ] **Step 6: Write the failing SceneLoader v2 tests**

In `src/RBX15.Parity.Tests/SceneLoaderTests.cs`, append these methods inside the `SceneLoaderTests` class, after `All_frozen_scenes_load`:

```csharp
    [Fact]
    public void Shape_actions_append_drawing_layers_after_the_scene_layers()
    {
        var s = Load("""
            { "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ],
              "actions": [
                { "op": "shape", "tool": "rect", "from": [100, 100], "to": [150, 120], "color": "#17d4fa", "width": 4, "fill": true },
                { "op": "shape", "tool": "ellipse", "from": [200, 200], "to": [260, 240], "color": "#f22f89", "width": 6 } ] }
            """);
        Assert.Equal(new[] { 1, 2, 3 }, s.Document.Layers.Select(l => l.Id));
        Assert.Equal(new[] { "L1", "Rect drawing", "Circle drawing" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal((90.0, 90.0), (s.Document.Layers[1].X, s.Document.Layers[1].Y));
    }

    [Fact]
    public void A_shape_that_draws_nothing_adds_no_layer()
    {
        var s = Load("""
            { "name": "x", "actions": [ { "op": "shape", "tool": "rect", "from": [100, 100], "to": [100, 100], "color": "#17d4fa", "width": 4, "fill": true } ] }
            """);
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Unsupported_ops_are_listed_once_in_order_and_refuse_to_parse()
    {
        // detail stays unsupported through M2a (Engine Details is M2b), so Task 6 adding erase leaves this intact.
        var e = JsonDocument.Parse("""
            { "name": "x", "actions": [
                { "op": "shape", "tool": "line", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 },
                { "op": "wordart", "text": "HI" }, { "op": "detail", "type": "hem" }, { "op": "wordart", "text": "YO" } ] }
            """).RootElement;
        Assert.Equal(new[] { "wordart", "detail" }, SceneLoader.UnsupportedOps(e));
        var ex = Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
        Assert.Contains("wordart, detail", ex.Message);
    }

    [Fact]
    public void An_action_without_an_op_refuses_to_parse()
    {
        // Review Focus: a hand-written or truncated action is reported as op "" and never half-applied.
        var e = JsonDocument.Parse("""{ "name": "x", "actions": [ { "tool": "line" } ] }""").RootElement;
        Assert.Equal(new[] { "" }, SceneLoader.UnsupportedOps(e));
        Assert.Throws<NotSupportedException>(() => SceneLoader.Parse(e, Root));
    }

    [Fact]
    public void Scenes_without_actions_have_no_unsupported_ops()
    {
        Assert.Empty(SceneLoader.UnsupportedOps(JsonDocument.Parse("""{ "name": "x" }""").RootElement));
    }

    [Fact]
    public void Unknown_shape_tool_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Load("""
            { "name": "x", "actions": [ { "op": "shape", "tool": "star", "from": [0, 0], "to": [10, 10], "color": "#000000", "width": 2 } ] }
            """));
    }
```

- [ ] **Step 7: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderTests"`
Expected: build failure, `error CS0117: 'SceneLoader' does not contain a definition for 'UnsupportedOps'`.

- [ ] **Step 8: Implement SceneLoader v2**

Replace the whole of `src/RBX15.Parity/SceneLoader.cs` with:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }
}
```

- [ ] **Step 9: Run the loader tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderTests"`
Expected: `Passed: 10, Failed: 0` (the 4 M1 facts plus the 6 new ones).

Then confirm the render scenes still load and pass unchanged: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests"`
Expected: `Passed: 34, Failed: 0`.

- [ ] **Step 10: Commit**

```bash
git add src/RBX15.Parity/SceneLoader.cs src/RBX15.Parity.Tests/SceneLoaderTests.cs
git commit -m "feat(parity): scene format v2 runs shape actions through Engine Tools"
```

Add your model's `Co-Authored-By` trailer.

- [ ] **Step 11: Share the golden rule between the two gates**

Create `src/RBX15.Engine.Tests/GoldenRule.cs` (the M1 rule moved verbatim out of `GoldenRenderTests`):

```csharp
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// The golden rule (spec section 6) as both golden gates apply it: zero interior differences and
/// edge deltas of 3 or less. Scenes with an RGB shift (approved divergence rgb-shift-compute) are
/// held to the full rule on pixels whose golden alpha is 255, plus an alpha delta of at most 1
/// everywhere. A failing scene writes a heatmap (a CI artifact) before the assert.
/// </summary>
static class GoldenRule
{
    static SKBitmap OpaqueOnly(SKBitmap source, SKBitmap golden)
    {
        var copy = source.Copy();
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
                if (golden.GetPixel(x, y).Alpha < 255) copy.SetPixel(x, y, SKColors.Transparent);
        return copy;
    }

    public static void AssertMatches(string name, string goldenPath, Document document, string heatmapPath)
    {
        using var golden = ImageDiff.Load(goldenPath);
        using var rendered = Renderer.Export(document);
        using var candidate = ImageDiff.FromImage(rendered);

        bool rgbShift = document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
        DiffResult result;
        if (rgbShift)
        {
            using var g = OpaqueOnly(golden, golden);
            using var c = OpaqueOnly(candidate, golden);
            result = ImageDiff.Compare(g, c);
            int worstAlpha = 0;
            for (int y = 0; y < golden.Height; y++)
                for (int x = 0; x < golden.Width; x++)
                    worstAlpha = Math.Max(worstAlpha, Math.Abs(golden.GetPixel(x, y).Alpha - candidate.GetPixel(x, y).Alpha));
            Assert.True(worstAlpha <= 1, $"{name}: RGB shift must never change alpha (worst alpha delta {worstAlpha})");
        }
        else
        {
            result = ImageDiff.Compare(golden, candidate);
        }

        if (!result.Pass)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(heatmapPath, data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
}
```

Replace the whole of `src/RBX15.Engine.Tests/GoldenRenderTests.cs` with:

```csharp
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M1 gate: the engine's export of every frozen 4.1 scene passes the golden rule (spec section 6).
/// Goldens are never edited here. A failing scene writes a heatmap to diff-heatmaps/ (a CI artifact).
/// </summary>
public class GoldenRenderTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "render");
    static readonly Lazy<IReadOnlyList<SceneLoader.Scene>> Scenes = new(() => SceneLoader.LoadAll(Path.Combine(Dir, "scenes.json"), Root));

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenes.Value) data.Add(s.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_export_matches_the_frozen_4_1_golden(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", name + ".png"));
    }
}
```

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests"`
Expected: `Passed: 34, Failed: 0`, as in Step 9. The refactor moves code and changes no behavior.

- [ ] **Step 12: Write the app golden gate**

Create `src/RBX15.Engine.Tests/GoldenAppTests.cs`:

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2a Task 6 for erase, M2b for detail, wordart and project).
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string EraseSkip = "op 'erase' is not run by the engine yet (Engine Eraser, M2a)";
    const string DetailSkip = "op 'detail' is not run by the engine yet (Engine Details, M2b)";
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    const string ProjectSkip = "op 'project' is not run by the engine yet (Engine ProjectIO, M2b)";
    static readonly string[] PendingOps = { "erase", "detail", "wordart", "project" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "erase", Skip = EraseSkip)]
    [MemberData(nameof(ScenesBlockedBy), "detail", Skip = DetailSkip)]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    [MemberData(nameof(ScenesBlockedBy), "project", Skip = ProjectSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Raw.Value.Single(e => NameOf(e) == name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
```

- [ ] **Step 13: Run the app golden gate**

First count what the gate should report:

```bash
python -c "import json; s=json.load(open('tests/goldens/app/scenes.json', encoding='utf-8')); run=[x['name'] for x in s if all(a.get('op')=='shape' for a in x.get('actions',[]))]; print('runnable', len(run), 'skipped', len(s)-len(run))"
```

Expected: `runnable 12 skipped 70`. Runnable is every scene with shape actions only: the 12 shape scenes of Task 3. Skipped is 82 - 12 = 70: 8 erase, 37 detail, 22 Word Art and 3 project scenes.

Run: `mkdir -p .superpowers/logs; dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" > .superpowers/logs/app-golden.log 2>&1; tail -30 .superpowers/logs/app-golden.log`
Expected: `Failed: 0, Passed: 14, Skipped: 70` (12 theory rows plus the 2 facts). Each skipped row's reason names its op.

Follow the "When an app scene fails" rule above for anything else. These are the first engine-versus-Chromium comparisons of stroked and filled paths, so failures are findings, not test bugs. In order of likelihood:

- **Antialiased edges of strokes and ovals.** Electron's Chromium and SkiaSharp `4.153.1` ship different Skia milestones, and path coverage (analytic AA and the conic-to-quad tolerance) can drift between them. The heatmap shows a one-pixel ring on the shape's outline. Deltas of 3 or less pass; anything larger is the stop-and-report case.
- **A whole shape offset by a fraction of a pixel.** The reference renderer maps template points to client coordinates (`(clientX - rect.left) / zoom`, `editor.js:1151-1152`). Compare the scene's `from` and `to` with the golden's provenance in `tests/goldens/app/index.json` and report. Never edit either.
- **Low-alpha edge colors on the committed layer.** Those come from the `toDataURL` round trip. Check that `CommitDrawing` still reads the crop back as `Rgba8888`/`Unpremul` and that drawing goes through `Images.ForDrawing`.
- **Ellipse outline only.** Check `EllipsePath` against Blink's two 180-degree `arcTo`s from angle 0.

- [ ] **Step 14: Run everything and commit**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: `0 Warning(s)`. Engine.Tests `Passed: 137, Failed: 0, Skipped: 70` (Task 4's 101, plus 22 `ToolsTests`, plus 14 `GoldenAppTests`). Parity.Tests `Passed: 19, Failed: 0` (13 plus 6).

```bash
git add src/RBX15.Engine.Tests/GoldenRule.cs src/RBX15.Engine.Tests/GoldenRenderTests.cs src/RBX15.Engine.Tests/GoldenAppTests.cs
git commit -m "test(engine): app golden gate over goldens v2, skipping unimplemented ops by name"
```

Add your model's `Co-Authored-By` trailer.

**Ledger evidence this task produces** (rows close in Task 8 only, never here): `autocrop` (`Autocrop_*` and the dot-crop check), `draw-shape-line`, `draw-shape-rect`, `draw-shape-ellipse` and `draw-canvas-template-clip` (the 12 shape goldens, including the three degenerate and clipping scenes). Partial evidence for rows that stay open because an App or caller half remains: `draw-commit-layer`, `draw-empty-no-layer`, `draw-pen-single-click` (divergence implemented in the engine) and `draw-pen-incremental-stroke` (the pen half, no golden). Also open after this task:

- `draw-begin` and `draw-tool-start`: undo-at-mousedown, which is caller semantics (M3/M4, see History).
- `canvas-draw-tools`: needs the eraser and the UI.
- The `select-*` rows: hit-testing and drags, not in the M2a contract.

---

### Task 6: Engine Eraser

**Files:**

- Create: `src/RBX15.Engine/Eraser.cs`, `src/RBX15.Engine.Tests/EraserTests.cs`, `src/RBX15.Parity.Tests/SceneLoaderEraseTests.cs`
- Modify:
  - `src/RBX15.Engine/Renderer.cs` (three members become `internal`, one Chromium-compat index row)
  - `src/RBX15.Engine/RBX15.Engine.csproj` (`InternalsVisibleTo` `RBX15.Parity`)
  - `src/RBX15.Parity/RBX15.Parity.csproj` (`InternalsVisibleTo` `RBX15.Engine.Tests`)
  - `src/RBX15.Parity/SceneLoader.cs` (full replacement of Task 5's file: `erase` joins `SupportedOps`)
  - `src/RBX15.Engine.Tests/GoldenAppTests.cs` (full replacement of Task 5's file: no `erase` skip row, erase through the replay, a production theory)
- Test: `src/RBX15.Engine.Tests/EraserTests.cs`, `src/RBX15.Parity.Tests/SceneLoaderEraseTests.cs`, `src/RBX15.Engine.Tests/GoldenAppTests.cs`

**Interfaces:**

- Consumes:
  - `Layer`, `Document` (`src/RBX15.Engine/Document.cs`), `TemplateSize.Width/Height`, `Images.ForDrawing(SKImage) -> SKImage` (cache-owned, never disposed by callers), `Images.Decode` (M1)
  - `Renderer.LayerSampling` (public), and `Renderer.FloatPipeline`, `Renderer.IsScaleTranslate(SKMatrix)`, `Renderer.DrawImageAntialiased(SKMatrix, float, float)` (private in M1; this task makes them `internal`), `Renderer.Export(Document)`, `Renderer.DrawLayer(SKCanvas, Layer)` (M1)
  - From Task 5: `SceneLoader.Parse(JsonElement e, string repoRoot)`, `SceneLoader.UnsupportedOps(JsonElement scene)`, `SceneLoader.LoadAll`, the private `ApplyShape` and `SupportedOps`, `GoldenRule.AssertMatches(string, string, Document, string)`, `GoldenAppTests.ScenesBlockedBy(string op)`
  - `Px.At`, `Px.Render`, `Px.Solid` (M1 test helpers)
  - The 8 frozen erase scenes in `tests/goldens/app` (Task 3)
- Produces:
  - `public static Layer Eraser.Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size)` (the contract signature; strokes once)
  - `public static SKImage Eraser.EraseStroke(Layer layer, SKImage strokeCanvas)` (the port of `applyEraseStroke`)
  - `public static readonly SKColor Eraser.StrokeColor`
  - Internal, for parity only (`InternalsVisibleTo` `RBX15.Engine.Tests` and `RBX15.Parity`):
    - `internal enum EraseStroking { Once, Replay41 }`
    - `internal static Layer Eraser.Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size, EraseStroking stroking)`
    - `internal static SKImage Eraser.StrokeCanvas(IReadOnlyList<SKPoint> points, float size, EraseStroking stroking = EraseStroking.Once)`
    - `internal static bool Eraser.HasContent(SKImage image)`
  - `public static Document SceneLoader.ApplyErase(Document document, JsonElement action)` (strokes once), and `internal static Document SceneLoader.ApplyErase(Document document, JsonElement action, EraseStroking stroking)`
  - `internal static SceneLoader.Scene SceneLoader.Parse(JsonElement e, string repoRoot, EraseStroking eraseStroking)`; the public two-argument `Parse` and `LoadAll` stroke once
  - `GoldenAppTests.TwoPointEraseScenes()` and the theory `Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)`

**What 4.1 does (port source):**

- Mousedown with the eraser (`editor.js:1120-1146`): `saveUndo()`, a fresh transparent 585x559 `drawCanvas`, `strokeStyle = '#f22f89'` (`editor.js:1138`), `lineWidth = drawSize`, round caps and joins, `beginPath()`, `moveTo(start)`. Nothing is stroked yet.
- Every mousemove (`editor.js:1200-1203`): `lineTo(mx, my)` then `stroke()` on the same open path. The whole path so far is stroked again each time.
- Mouseup (`editor.js:1248-1294`): only the pen strokes once more (`editor.js:1253-1255`); the eraser does not. If the stroke canvas has no pixel with alpha above 0 (`editor.js:1257-1262`), nothing happens. Otherwise `applyEraseStroke(sel.img, sel, drawCanvas, createCanvas)` (`editor.js:1272-1275`), and the result goes through `toDataURL()` into a new `Image` that replaces only `sel.img` (`editor.js:1276-1282`), so layers sharing the old image keep it.
- `lib/erase.js:43-73`: `w = imgW*scale`, `cx = x + w/2` (`:47-50`), `rad = (rotation || 0)*PI/180` (`:51`). Mask canvas at image size, CTM in this order: `translate(imgW/2, imgH/2)`, `scale(1/s, 1/s)`, `scale(flipH?-1:1, flipV?-1:1)`, `rotate(-rad)`, `translate(-cx, -cy)`, then `drawImage(strokeCanvas, 0, 0)` with plain source-over (`:56-63`). Then an image-sized `out` canvas: `drawImage(source, 0, 0)`, `destination-out`, `drawImage(mask, 0, 0)` at identity (`:66-71`). Erasing is proportional to mask alpha (`:34-35`).
- A click without movement never strokes (moveTo only), so the alpha scan finds nothing and nothing is erased.
- The eraser's 4.1 tests: `tests/erase.test.js:34-71` (five cases, ported below case for case with the same coordinates and expected alphas).

**What the erased edge depends on (the stroke-once question):**

Segment `j` of an `n`-move drag is composited `n - j + 1` times in 4.1, because each `stroke()` repaints the whole open path. A pixel the stroke covers fully is alpha 255 after the first pass and stays 255 (src-over never lowers alpha), so it is erased to 0 either way. A partially covered pixel on the antialiased fringe with coverage `a` accumulates towards `1 - (1 - a)^k`, and the erased alpha there is `layerAlpha * (1 - maskAlpha)`. Under a scaled or rotated layer the mask is resampled bilinearly, which spreads that fringe by up to a pixel, so those resampled pixels inherit the same dependence. So:

1. Fully erased pixels are identical in 4.1 and v5.
2. The fringe depends on how many mousemove events the drag produced and on where each segment sits in the drag. That is the approved stroke-once divergence `draw-pen-incremental-stroke` ("Pen and eraser re-stroke the whole path"), applied to the eraser.
3. A two-point drag (one mousemove) is a single stroke in 4.1 too, so it matches v5 exactly.

**How the goldens are met without loosening anything:**

- Production `Eraser.Apply` strokes once. `EraserTests` pins all three facts above on the stroke canvas and on the erased layer, and `GoldenAppTests.Production_stroke_once_matches_the_golden_on_two_point_erase_scenes` holds production to the frozen goldens on the three two-point scenes.
- The internal `EraseStroking.Replay41` reproduces 4.1 exactly. It mirrors the event sequence `app-render.js` sent: mousedown at `points[0]` (moveTo, no stroke), one mousemove per later point (lineTo, then stroke the whole path), and mouseup with no stroke. `GoldenAppTests` applies every erase action through it, so **all 8 erase scenes** are compared under the unchanged golden rule (through `GoldenRule`, which carries the RGB-shift rule for `erase-rgb-shift`). No erase scene is skipped.
- Step 13 measures how far production lands from the five multi-point goldens and records it in the commit body: the documented finding the contract asks for. The ledger records the divergence once, in Task 8.
- If the replay fails a scene, the port is wrong. That is never the divergence: debug it (Step 12). If production fails a two-point scene, the port is wrong too, because once and replay are identical there.

- [ ] **Step 1: Write the failing eraser unit tests**

Create `src/RBX15.Engine.Tests/EraserTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// tests/erase.test.js ported case for case (same layers, strokes, pixels and expected alphas),
/// plus Apply's commit rules (editor.js:1257-1283) and the stroke-once versus 4.1-replay question.
/// </summary>
public class EraserTests
{
    const int TW = TemplateSize.Width, TH = TemplateSize.Height;

    static readonly SKColor Red = new(255, 0, 0);

    /// <summary>erase.test.js:12-18 solidLayer.</summary>
    static SKImage SolidLayer(int w, int h) => Px.Solid(w, h, Red);

    /// <summary>erase.test.js:20-26 strokeRect: a template-sized canvas with one #000 rect.</summary>
    static SKImage StrokeRect(int x, int y, int w, int h, byte alpha = 255) =>
        Px.Render(TW, TH, c =>
        {
            using var p = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
            c.DrawRect(SKRect.Create(x, y, w, h), p);
        });

    /// <summary>erase.test.js:32 IDENTITY, as a Layer.</summary>
    static Layer At(SKImage image, double x = 0, double y = 0, double scale = 1, double rotation = 0,
                    bool flipH = false, bool flipV = false, int id = 1) =>
        new() { Id = id, Image = image, X = x, Y = y, Scale = scale, Rotation = rotation, FlipH = flipH, FlipV = flipV };

    static int AlphaAt(SKImage image, int x, int y) => Px.At(image, x, y).Alpha;

    static byte[] Alphas(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        var px = bmp.GetPixelSpan();
        var a = new byte[image.Width * image.Height];
        for (int i = 0; i < a.Length; i++) a[i] = px[i * 4 + 3];
        return a;
    }

    // ---- erase.test.js, case for case ----

    [Fact]
    public void Erases_stroked_pixels_at_identity_and_leaves_the_rest()
    {
        // erase.test.js:34-39
        using var stroke = StrokeRect(5, 5, 6, 6);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20)), stroke);
        Assert.Equal(0, AlphaAt(erased, 7, 7));
        Assert.Equal(255, AlphaAt(erased, 15, 15));
    }

    [Fact]
    public void Maps_the_template_stroke_through_offset_and_scale()
    {
        // erase.test.js:41-49: 10x10 layer at (20,20) scaled 2x covers template 20..40.
        using var stroke = StrokeRect(20, 20, 10, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2), stroke);
        Assert.Equal(0, AlphaAt(erased, 2, 2));
        Assert.Equal(255, AlphaAt(erased, 8, 8));
    }

    [Fact]
    public void Rotation_180_maps_a_top_left_stroke_to_bottom_right_pixels()
    {
        // erase.test.js:51-57
        using var stroke = StrokeRect(20, 20, 10, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, rotation: 180), stroke);
        Assert.Equal(0, AlphaAt(erased, 8, 8));
        Assert.Equal(255, AlphaAt(erased, 2, 2));
    }

    [Fact]
    public void FlipH_maps_a_left_side_stroke_to_right_side_pixels()
    {
        // erase.test.js:59-65
        using var stroke = StrokeRect(20, 20, 10, 20);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, flipH: true), stroke);
        Assert.Equal(0, AlphaAt(erased, 8, 5));
        Assert.Equal(255, AlphaAt(erased, 2, 5));
    }

    [Fact]
    public void Does_not_mutate_the_source_layer_image()
    {
        // erase.test.js:67-71, plus the 4.0 regression guard (ledger tests-lib-erase): the result
        // really is erased, the source really is not.
        using var source = SolidLayer(20, 20);
        using var stroke = StrokeRect(0, 0, 20, 20);
        using var erased = Eraser.EraseStroke(At(source), stroke);
        Assert.Equal(255, AlphaAt(source, 10, 10));
        Assert.Equal(0, AlphaAt(erased, 10, 10));
    }

    // ---- ledger parity checks beyond erase.test.js ----

    [Fact]
    public void FlipV_maps_a_top_stroke_to_bottom_pixels()
    {
        // erase.js:60 flips both axes; the flipH case (erase.test.js:59-65) transposed.
        using var stroke = StrokeRect(20, 20, 20, 10);
        using var erased = Eraser.EraseStroke(At(SolidLayer(10, 10), x: 20, y: 20, scale: 2, flipV: true), stroke);
        Assert.Equal(0, AlphaAt(erased, 5, 8));
        Assert.Equal(255, AlphaAt(erased, 5, 2));
    }

    [Fact]
    public void Rotated_30_degrees_the_hole_lands_under_the_cursor()
    {
        // Ledger tests-lib-erase (SMOKE_TEST A2). A 40x40 layer at (100,100), center (120,120),
        // rotated 30 degrees. The stroke centered at template (129,125) is offset (9,5) from the
        // center; un-rotating by 30 degrees gives image offset (10.29,-0.17), image pixel (30,19).
        // With the rotation sign wrong it would land at image (25.29,28.83), pixel (25,28).
        using var stroke = StrokeRect(127, 123, 4, 4);
        using var erased = Eraser.EraseStroke(At(SolidLayer(40, 40), x: 100, y: 100, rotation: 30), stroke);
        Assert.Equal(0, AlphaAt(erased, 30, 19));
        Assert.Equal(255, AlphaAt(erased, 25, 28));
        Assert.Equal(255, AlphaAt(erased, 10, 20));
    }

    [Fact]
    public void Erase_amount_is_proportional_to_stroke_alpha()
    {
        // erase.js:34-35 and :69-70: destination-out keeps dest alpha * (1 - maskA).
        // 255 * (1 - 128/255) = 127.
        using var stroke = StrokeRect(0, 0, 20, 20, alpha: 128);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20)), stroke);
        Assert.Equal(127, AlphaAt(erased, 10, 10));
        Assert.Equal(255, Px.At(erased, 10, 10).Red);
    }

    [Fact]
    public void Scale_zero_skips_the_non_finite_inverse_scale_like_canvas()
    {
        // Review Focus. A hand-edited project can carry scale 0 (the UI clamps at 0.01). erase.js:58
        // then calls scale(1/0, 1/0), which canvas ignores, and w = h = 0 puts the center at (x, y)
        // (erase.js:47-50). The mask CTM is translate(imgW/2, imgH/2) then translate(-x, -y), so
        // template pixels 0..4 land on image pixels 10..14.
        using var stroke = StrokeRect(0, 0, 5, 5);
        using var erased = Eraser.EraseStroke(At(SolidLayer(20, 20), scale: 0), stroke);
        Assert.Equal(0, AlphaAt(erased, 12, 12));
        Assert.Equal(255, AlphaAt(erased, 2, 2));
        Assert.Equal(255, AlphaAt(erased, 17, 17));
    }

    [Fact]
    public void Result_is_unpremultiplied_rgba_at_the_layer_image_size()
    {
        // editor.js:1276-1282: the result goes through toDataURL and a new Image, the same
        // representation Images.Decode gives every layer image.
        using var stroke = StrokeRect(0, 0, 5, 5);
        using var erased = Eraser.EraseStroke(At(SolidLayer(30, 12), x: 3, y: 4, scale: 0.5), stroke);
        Assert.Equal((30, 12), (erased.Width, erased.Height));
        Assert.Equal((SKColorType.Rgba8888, SKAlphaType.Unpremul), (erased.ColorType, erased.AlphaType));
    }

    // ---- Apply: the commit rules ----

    [Fact]
    public void A_click_without_movement_erases_nothing()
    {
        // editor.js:1143-1144 only moveTo on mousedown; with no mousemove nothing is stroked, and the
        // alpha scan (1257-1264) skips the erase.
        var layer = At(SolidLayer(20, 20));
        Assert.Same(layer, Eraser.Apply(layer, [new SKPoint(10, 10)], 8));
        Assert.Same(layer, Eraser.Apply(layer, [], 8));
    }

    [Fact]
    public void A_drag_entirely_off_the_template_erases_nothing()
    {
        // The stroke canvas is 585x559 (editor.js:1129-1131); a stroke outside it leaves no pixel.
        var layer = At(SolidLayer(20, 20));
        Assert.Same(layer, Eraser.Apply(layer, [new SKPoint(-100, -100), new SKPoint(-50, -50)], 4));
    }

    [Fact]
    public void Apply_erases_along_the_drag_and_keeps_the_layer_identity_and_transform()
    {
        var source = SolidLayer(100, 100);
        var layer = At(source, x: 0, y: 0) with { Name = "Kept", Opacity = 0.5 };
        var erased = Eraser.Apply(layer, [new SKPoint(10, 50), new SKPoint(90, 50)], 10);
        Assert.NotSame(source, erased.Image);
        Assert.Equal(0, AlphaAt(erased.Image, 50, 50));
        Assert.Equal(255, AlphaAt(erased.Image, 50, 10));
        Assert.Equal(layer with { Image = erased.Image }, erased);
        Assert.Equal(255, AlphaAt(source, 50, 50));
    }

    [Fact]
    public void Layers_sharing_an_image_keep_their_pixels()
    {
        // Ledger layer-sharing-img: clones share one image; only the erased layer gets a new one.
        var shared = SolidLayer(100, 100);
        var a = At(shared, id: 1);
        var b = At(shared, id: 2);
        var erased = Eraser.Apply(a, [new SKPoint(10, 50), new SKPoint(90, 50)], 10);
        Assert.Equal(0, AlphaAt(erased.Image, 50, 50));
        Assert.Same(shared, b.Image);
        Assert.Equal(255, AlphaAt(b.Image, 50, 50));
    }

    [Fact]
    public void An_rgb_shifted_layer_renders_the_erased_pixels()
    {
        // editor.js:1279 invalidates 4.1's single-slot shift cache after an erase. v5's shift cache
        // is keyed by image (Adjust.ShiftRgb), so the new image cannot hit the old entry.
        var layer = At(SolidLayer(20, 20), x: 100, y: 100) with { RShift = -40 };
        using var before = Px.Render(TW, TH, c => Renderer.DrawLayer(c, layer));
        var erased = Eraser.Apply(layer, [new SKPoint(103, 110), new SKPoint(117, 110)], 6);
        using var after = Px.Render(TW, TH, c => Renderer.DrawLayer(c, erased));
        Assert.Equal(255, AlphaAt(before, 110, 110));
        Assert.Equal(0, AlphaAt(after, 110, 110));
    }

    // ---- stroke once (v5) versus 4.1's per-move replay ----

    static readonly SKPoint[] Zigzag = [new(100, 100), new(160, 130), new(220, 100), new(280, 140)];

    [Fact]
    public void A_two_point_drag_strokes_identically_once_and_replayed()
    {
        // One mousemove = one stroke() in 4.1 (editor.js:1200-1203), and no stroke at mouseup for the
        // eraser (1253-1255). So a two-point erase is pixel-identical in v5 and 4.1.
        SKPoint[] two = [new(100, 100), new(260, 140)];
        using var once = Eraser.StrokeCanvas(two, 20, EraseStroking.Once);
        using var replay = Eraser.StrokeCanvas(two, 20, EraseStroking.Replay41);
        Assert.Equal(Alphas(replay), Alphas(once));
    }

    [Fact]
    public void Invalid_sizes_keep_the_canvas_default_line_width()
    {
        // Review Focus. Canvas2D ignores a lineWidth that is 0, negative, NaN or infinite, and the
        // stroke canvas is fresh per gesture (editor.js:1129-1131), so its default width 1 stays.
        // A width-1 line on y = 100.5 covers row 100 fully.
        SKPoint[] drag = [new(100, 100.5f), new(200, 100.5f)];
        using var one = Eraser.StrokeCanvas(drag, 1);
        Assert.Equal(255, AlphaAt(one, 150, 100));
        foreach (var size in new[] { 0f, -3f, float.NaN, float.PositiveInfinity })
        {
            using var got = Eraser.StrokeCanvas(drag, size);
            Assert.Equal(Alphas(one), Alphas(got));
        }
    }

    [Fact]
    public void Replay_only_raises_partially_covered_pixels()
    {
        // 4.1 composites segment j of n (n - j + 1) times. Fully covered pixels are 255 either way,
        // and src-over never lowers alpha, so the replay can differ only where the single stroke is
        // below 255: the antialiased fringe. On a zigzag of three segments some fringe pixel rises.
        using var once = Eraser.StrokeCanvas(Zigzag, 20, EraseStroking.Once);
        using var replay = Eraser.StrokeCanvas(Zigzag, 20, EraseStroking.Replay41);
        var (o, r) = (Alphas(once), Alphas(replay));
        int raised = 0;
        for (int i = 0; i < o.Length; i++)
        {
            if (o[i] == 255) Assert.Equal(255, r[i]);
            Assert.True(r[i] >= o[i], $"pixel {i % TW},{i / TW}: replay {r[i]} below once {o[i]}");
            if (r[i] > o[i]) raised++;
        }
        Assert.True(raised > 0, "a three-segment drag must raise some fringe pixel when replayed");
    }

    [Fact]
    public void Opaque_erasure_is_identical_and_only_the_fringe_keeps_more_alpha_once()
    {
        // At identity the mask is the stroke canvas, so the erased layer mirrors the test above:
        // wherever the single stroke erases fully, the replay does too; elsewhere stroke-once leaves
        // at least as much alpha as 4.1 did. This is the whole stroke-once divergence.
        var layer = At(SolidLayer(400, 200));
        var once = Eraser.Apply(layer, Zigzag, 20, EraseStroking.Once);
        var replay = Eraser.Apply(layer, Zigzag, 20, EraseStroking.Replay41);
        var (o, r) = (Alphas(once.Image), Alphas(replay.Image));
        int fullyErased = 0;
        for (int i = 0; i < o.Length; i++)
        {
            if (o[i] == 0) { Assert.Equal(0, r[i]); fullyErased++; }
            Assert.True(o[i] >= r[i], $"pixel {i % 400},{i / 400}: once {o[i]} below replay {r[i]}");
        }
        Assert.True(fullyErased > 0);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~EraserTests"`
Expected: the build fails with `error CS0103: The name 'Eraser' does not exist in the current context` and `error CS0103: The name 'EraseStroking' does not exist in the current context` in `EraserTests.cs`.

- [ ] **Step 3: Open the Chromium-compat helpers to the eraser and to Parity**

The mask draw in `lib/erase.js:63` is a Canvas2D `drawImage` through a transform, so it follows the same Blink and Skia rules the layer draw does: antialias per `ShouldDrawImageAntialiased`, and the float pipeline for any non scale-translate matrix. Reuse them; don't copy them.

In `src/RBX15.Engine/Renderer.cs`, add `internal` to exactly these three declarations (indentation unchanged):

- `static readonly SKColorFilter FloatPipeline = SKColorFilter.CreateColorMatrix(new float[]` becomes `internal static readonly SKColorFilter FloatPipeline = SKColorFilter.CreateColorMatrix(new float[]`
- `static bool IsScaleTranslate(SKMatrix m) => m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0;` becomes `internal static bool IsScaleTranslate(SKMatrix m) => m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0;`
- `static bool DrawImageAntialiased(SKMatrix m, float w, float h)` becomes `internal static bool DrawImageAntialiased(SKMatrix m, float w, float h)`

In the class summary's Chromium compatibility index, insert these two lines directly before the line that starts `/// <item><term>FEComponentTransfer`:

```csharp
/// <item><term>Eraser mask draw (lib/erase.js:63): same antialias and float-pipeline rules as a layer draw</term>
///   <description><c>Eraser.EraseStroke</c> (reuses <c>DrawImageAntialiased</c>, <c>FloatPipeline</c>); the 8 erase scenes in tests/goldens/app via GoldenAppTests</description></item>
```

In `src/RBX15.Engine/RBX15.Engine.csproj`, replace:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.Engine.Tests" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.Engine.Tests" />
    <InternalsVisibleTo Include="RBX15.Parity" />
  </ItemGroup>
```

`RBX15.Parity` needs `EraseStroking` so `SceneLoader` can replay 4.1's stroking for the goldens. Nothing outside the engine, its tests and the parity harness sees it.

- [ ] **Step 4: Implement the eraser**

Create `src/RBX15.Engine/Eraser.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// How the eraser's template-space stroke is rasterized before it is punched out. Internal: the
/// editor always strokes once; the replay exists only so parity tests can meet the frozen goldens.
/// </summary>
internal enum EraseStroking
{
    /// <summary>
    /// v5: the whole polyline stroked once. The approved divergence draw-pen-incremental-stroke
    /// ("Pen and eraser re-stroke the whole path"), recorded for the eraser on ledger row eraser-apply.
    /// Fully covered pixels erase exactly as in 4.1; only the antialiased fringe of a drag with three
    /// or more points keeps slightly more alpha. A two-point drag is identical (measured in M2a Task 6).
    /// </summary>
    Once,

    /// <summary>
    /// 4.1 (editor.js:1133-1144, 1200-1203): one open path, re-stroked after every mousemove, and
    /// not stroked again at mouseup for the eraser (editor.js:1253-1255 is pen only). This mirrors the
    /// event sequence tools/parity-ref/app-render.js sends: mousedown at points[0] (moveTo only), one
    /// mousemove per later point (lineTo, then stroke the whole path), mouseup with no stroke.
    /// Segment j of n is therefore composited n - j + 1 times, which only raises partially covered
    /// (antialiased) pixels. Parity only: it reproduces the frozen eraser goldens.
    /// </summary>
    Replay41,
}

/// <summary>
/// The eraser (lib/erase.js, editor.js:1265-1283). The stroke is drawn in template space, mapped
/// into the selected layer's own pixels through the inverse of its render transform, and punched
/// out with destination-out at identity, in two steps exactly as lib/erase.js does it.
/// </summary>
public static class Eraser
{
    /// <summary>editor.js:1138: eraser strokes are opaque magenta. Only their alpha matters.</summary>
    public static readonly SKColor StrokeColor = new(0xF2, 0x2F, 0x89);

    /// <summary>
    /// The layer with <paramref name="templatePoints"/> (a drag, in template coordinates) erased
    /// from its image, stroked once with round caps and joins at <paramref name="size"/>.
    /// Returns <paramref name="layer"/> itself when the stroke leaves no pixel on the 585x559
    /// stroke canvas: fewer than two points (a click without movement only calls moveTo,
    /// editor.js:1143-1144) or a drag entirely off the template (editor.js:1257-1264).
    /// Otherwise a new Layer with a new image; the old image, and any layer sharing it, is untouched.
    /// </summary>
    public static Layer Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size) =>
        Apply(layer, templatePoints, size, EraseStroking.Once);

    /// <summary><see cref="Apply(Layer, IReadOnlyList{SKPoint}, float)"/> with an explicit stroking mode (parity only).</summary>
    internal static Layer Apply(Layer layer, IReadOnlyList<SKPoint> templatePoints, float size, EraseStroking stroking)
    {
        using var stroke = StrokeCanvas(templatePoints, size, stroking);
        if (!HasContent(stroke)) return layer;
        return layer with { Image = EraseStroke(layer, stroke) };
    }

    /// <summary>
    /// The 585x559 transparent stroke canvas 4.1 builds on mousedown (editor.js:1129-1144):
    /// kN32 premultiplied, StrokeColor, lineWidth = size, round caps and joins, antialiased.
    /// Fewer than two points draw nothing. A width that canvas would reject (not finite, or 0 or
    /// less) keeps canvas's default lineWidth of 1.
    /// </summary>
    internal static SKImage StrokeCanvas(IReadOnlyList<SKPoint> points, float size, EraseStroking stroking = EraseStroking.Once)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create stroke surface");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (points.Count >= 2)
        {
            using var paint = new SKPaint
            {
                Color = StrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = float.IsFinite(size) && size > 0 ? size : 1,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
            };
            // Once: only the full path. Replay41: the path through points[0..end] after every move.
            int firstEnd = stroking == EraseStroking.Replay41 ? 1 : points.Count - 1;
            for (int end = firstEnd; end < points.Count; end++)
            {
                using var builder = new SKPathBuilder();
                builder.MoveTo(points[0].X, points[0].Y);
                for (int k = 1; k <= end; k++) builder.LineTo(points[k].X, points[k].Y);
                using var path = builder.Detach();
                canvas.DrawPath(path, paint);
            }
        }
        return surface.Snapshot();
    }

    /// <summary>
    /// lib/erase.js applyEraseStroke (lines 43-73). <paramref name="strokeCanvas"/> is template
    /// sized; any pixel with alpha above 0 erases, proportionally to its alpha. Returns a new
    /// image the size of <paramref name="layer"/>'s image, un-premultiplied RGBA like
    /// <see cref="Images.Decode"/>, because 4.1 round-trips the result through toDataURL and a new
    /// Image (editor.js:1276-1282). The source image is never modified.
    /// </summary>
    public static SKImage EraseStroke(Layer layer, SKImage strokeCanvas)
    {
        int imgW = layer.Image.Width, imgH = layer.Image.Height;               // erase.js:44-45
        double w = imgW * layer.Scale, h = imgH * layer.Scale;                 // erase.js:47-48
        double cx = layer.X + w / 2, cy = layer.Y + h / 2;                     // erase.js:49-50
        double rotation = double.IsNaN(layer.Rotation) ? 0 : layer.Rotation;   // erase.js:51, (t.rotation || 0)
        double rad = rotation * Math.PI / 180;
        var info = new SKImageInfo(imgW, imgH, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        // Step 1 (erase.js:56-63): the stroke into layer-image space through the inverse transform.
        // Canvas ignores a transform call with a non-finite argument; so does this (scale 0 gives 1/0).
        using var maskSurface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create mask surface");
        var m = maskSurface.Canvas;
        m.Clear(SKColors.Transparent);
        m.Translate((float)(imgW / 2.0), (float)(imgH / 2.0));
        double inv = 1 / layer.Scale;
        if (double.IsFinite(inv)) m.Scale((float)inv, (float)inv);
        m.Scale(layer.FlipH ? -1 : 1, layer.FlipV ? -1 : 1);
        if (double.IsFinite(rad)) m.RotateRadians((float)-rad);
        if (double.IsFinite(cx) && double.IsFinite(cy)) m.Translate((float)-cx, (float)-cy);
        var stroke = Images.ForDrawing(strokeCanvas);
        var ctm = m.TotalMatrix;
        using (var paint = new SKPaint
        {
            IsAntialias = Renderer.DrawImageAntialiased(ctm, stroke.Width, stroke.Height),
            ColorFilter = Renderer.IsScaleTranslate(ctm) ? null : Renderer.FloatPipeline,
        })
            m.DrawImage(stroke, SKRect.Create(0, 0, stroke.Width, stroke.Height), Renderer.LayerSampling, paint);
        using var mask = maskSurface.Snapshot();

        // Step 2 (erase.js:66-71): copy the layer image, then destination-out the mask at identity.
        using var outSurface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create erase surface");
        var o = outSurface.Canvas;
        o.Clear(SKColors.Transparent);
        using (var copy = new SKPaint())
            o.DrawImage(Images.ForDrawing(layer.Image), 0, 0, Renderer.LayerSampling, copy);
        using (var dstOut = new SKPaint { BlendMode = SKBlendMode.DstOut })
            o.DrawImage(mask, 0, 0, Renderer.LayerSampling, dstOut);
        using var erased = outSurface.Snapshot();

        var unpremul = new SKImageInfo(imgW, imgH, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(unpremul);
        if (!erased.ReadPixels(unpremul, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read erased pixels");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    /// <summary>editor.js:1257-1262: any pixel with alpha above 0.</summary>
    internal static bool HasContent(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("cannot read stroke pixels");
        var px = bmp.GetPixelSpan();
        for (int i = 3; i < px.Length; i += 4)
            if (px[i] != 0) return true;
        return false;
    }
}
```

Notes for the reviewer, each one deliberate:

- `Images.ForDrawing` on both sources puts them on kN32 premultiplied pixels, the representation Chromium's canvas and decoded images have. The legacy blitter rule in the Renderer index depends on it.
- The result is read back un-premultiplied (`Rgba8888`, `Unpremul`), the same as `Images.Decode`, because 4.1 round-trips the erased canvas through `toDataURL()` and a new `Image` (`editor.js:1276-1282`). Every later draw premultiplies it again through `ForDrawing`, as Chromium's decode does.
- Transform calls with a non-finite argument are skipped, because Canvas2D ignores them. With scale 0 (only reachable from a hand-edited project; the UI clamps at 0.01), `1/0` is skipped exactly as in 4.1. `Scale_zero_skips_the_non_finite_inverse_scale_like_canvas` pins it.
- `Apply` returns the same `Layer` instance when the stroke canvas is empty, so a caller can tell an erase that did nothing (no new history step is the caller's business, per the History contract).
- The eraser does not call `Tools.RenderStroke`. The pen's single-point case draws a dot in v5 (divergence `draw-pen-single-click`), while a single-point eraser press erases nothing, as in 4.1.
- `EraseStroking`, the four-argument `Apply`, `StrokeCanvas` and `HasContent` are `internal`. The editor has one eraser, and it strokes once.

- [ ] **Step 5: Run the unit tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~EraserTests"`
Expected: `Passed!  - Failed:     0, Passed:    19, Skipped:     0, Total:    19`.

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `0 Warning(s)`, `0 Error(s)`.

If `Erase_amount_is_proportional_to_stroke_alpha` reads 126 or 128 instead of 127, the destination-out draw is being composited in a different 8-bit pipeline than expected. Do not change the expected value: it is `255 * (1 - 128/255) = 127`, exactly, in every pipeline. Check that the destination-out paint carries no color filter and that the mask is the kN32 snapshot.

- [ ] **Step 6: Commit the engine eraser**

```bash
git add src/RBX15.Engine/Eraser.cs src/RBX15.Engine/Renderer.cs src/RBX15.Engine/RBX15.Engine.csproj src/RBX15.Engine.Tests/EraserTests.cs
git commit -m "feat(engine): eraser, a port of lib/erase.js that strokes once, with an internal 4.1 replay for parity"
```

- [ ] **Step 7: Write the failing SceneLoader erase tests**

Create `src/RBX15.Parity.Tests/SceneLoaderEraseTests.cs` (a separate file, so Task 5's `SceneLoaderTests.cs` is untouched). It uses only the public API, which strokes once:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Parity.Tests;

public class SceneLoaderEraseTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static SKImage Opaque(int w, int h)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(new SKColor(255, 0, 0));
        return surface.Snapshot();
    }

    static int AlphaAt(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        Assert.True(image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0));
        return bmp.GetPixel(x, y).Alpha;
    }

    [Fact]
    public void Erase_replaces_only_the_indexed_layer()
    {
        var bottom = new Layer { Id = 1, Image = Opaque(100, 100) };
        var top = new Layer { Id = 2, Image = Opaque(100, 100) };
        var doc = new Document { Layers = ImmutableList.Create(bottom, top) };
        var action = Json("""{ "op": "erase", "layer": 1, "points": [[10, 50], [90, 50]], "size": 10 }""");

        var result = SceneLoader.ApplyErase(doc, action);

        Assert.Same(bottom, result.Layers[0]);
        Assert.Equal(2, result.Layers[1].Id);
        Assert.Equal(0, AlphaAt(result.Layers[1].Image, 50, 50));
        Assert.Equal(255, AlphaAt(result.Layers[1].Image, 50, 10));
    }

    [Fact]
    public void Erase_naming_a_missing_layer_is_a_scene_error()
    {
        var doc = new Document { Layers = ImmutableList.Create(new Layer { Id = 1, Image = Opaque(10, 10) }) };
        var action = Json("""{ "op": "erase", "layer": 1, "points": [[0, 0], [5, 5]], "size": 4 }""");
        Assert.Throws<InvalidDataException>(() => SceneLoader.ApplyErase(doc, action));
    }

    [Fact]
    public void Parse_runs_an_erase_after_a_shape_against_the_scene_layer()
    {
        // app-render.js indexes erase layers into the scene's own layers; a drawing appends on top
        // (editor.js:534), so index 0 is still the scene layer after the shape.
        var scene = Json("""{ "name": "e", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png" } ], "actions": [ { "op": "shape", "tool": "line", "from": [10, 10], "to": [60, 10], "color": "#ffffff", "width": 4 }, { "op": "erase", "layer": 0, "points": [[0, 0], [64, 75], [127, 0]], "size": 30 } ] }""");
        Assert.Empty(SceneLoader.UnsupportedOps(scene));
        var s = SceneLoader.Parse(scene, Root);
        Assert.Equal(new[] { "L1", "Line drawing" }, s.Document.Layers.Select(l => l.Name));
        Assert.Equal((128, 150), (s.Document.Layers[0].Image.Width, s.Document.Layers[0].Image.Height));
    }

    [Fact]
    public void A_single_point_erase_leaves_the_layer_as_it_was()
    {
        // editor.js:1143-1144: a press without movement only calls moveTo, so nothing is erased.
        var layer = new Layer { Id = 1, Image = Opaque(20, 20) };
        var doc = new Document { Layers = ImmutableList.Create(layer) };
        var result = SceneLoader.ApplyErase(doc, Json("""{ "op": "erase", "layer": 0, "points": [[5, 5]], "size": 8 }"""));
        Assert.Same(layer, result.Layers[0]);
    }
}
```

- [ ] **Step 8: Run them and watch them fail**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderEraseTests"`
Expected: the build fails with `error CS0117: 'SceneLoader' does not contain a definition for 'ApplyErase'`.

- [ ] **Step 9: Extend SceneLoader v2 for the erase op**

Replace the whole of `src/RBX15.Parity/SceneLoader.cs` (Task 5's file) with the version below. Compared with Task 5 it changes exactly these things: `SupportedOps` gains `"erase"`; the class summary gains a paragraph on stroking; `Parse` gains an internal overload that threads the stroking mode, with the public two-argument `Parse` delegating to it with `EraseStroking.Once`; the action switch expression gains the `"erase"` arm before the throwing arm; and the two `ApplyErase` overloads are new. `LoadAll` and `UnsupportedOps` and `ApplyShape` are unchanged.

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// <para>
/// Scene format v2 adds an ordered <c>actions</c> array, run after the scene's own layers exist.
/// The loader runs only the ops the engine implements (SupportedOps). A scene holding any other op
/// refuses to parse, so no test ever compares a half-built document against a golden.
/// </para>
/// <para>
/// The public API strokes the eraser once, as the editor does. The internal overloads take an
/// <see cref="EraseStroking"/> so the golden gate can replay 4.1's per-move stroking.
/// </para>
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    /// <summary>Ops the engine runs. Each engine task that implements an op adds it here.</summary>
    static readonly HashSet<string> SupportedOps = new(StringComparer.Ordinal) { "shape", "erase" };

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    /// <summary>The scene's action ops the engine does not run yet, each once, in first-use order.</summary>
    public static IReadOnlyList<string> UnsupportedOps(JsonElement scene)
    {
        var ops = new List<string>();
        if (scene.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
            {
                var op = action.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                if (!SupportedOps.Contains(op) && !ops.Contains(op)) ops.Add(op);
            }
        return ops;
    }

    public static Scene Parse(JsonElement e, string repoRoot) => Parse(e, repoRoot, EraseStroking.Once);

    internal static Scene Parse(JsonElement e, string repoRoot, EraseStroking eraseStroking)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

        var name = Str(e, "name") ?? throw new InvalidDataException("scene without a name");
        var unsupported = UnsupportedOps(e);
        if (unsupported.Count > 0)
            throw new NotSupportedException($"scene {name}: ops the engine does not run yet: {string.Join(", ", unsupported)}");

        var fills = ImmutableList.CreateBuilder<(string, Fill)>();
        if (e.TryGetProperty("regionColors", out var rc))
            foreach (var p in rc.EnumerateObject())
                fills.Add((p.Name, p.Value.ValueKind == JsonValueKind.String
                    ? Fill.Solid(p.Value.GetString()!)
                    : new Fill(Str(p.Value, "type") ?? "solid", Str(p.Value, "c1"), Str(p.Value, "c2"), Num(p.Value, "angle"), Num(p.Value, "scale"))));

        var layers = ImmutableList.CreateBuilder<Layer>();
        if (e.TryGetProperty("layers", out var ls))
        {
            int i = 0;
            foreach (var l in ls.EnumerateArray())
            {
                i++;
                layers.Add(new Layer
                {
                    Id = i, Name = "L" + i, Image = Image(Str(l, "image")!),
                    Visible = Bool(l, "visible") ?? true, Opacity = Num(l, "opacity") ?? 1,
                    X = Num(l, "x") ?? 0, Y = Num(l, "y") ?? 0, Scale = Num(l, "scale") ?? 1, Rotation = Num(l, "rotation") ?? 0,
                    FlipH = Bool(l, "flipH") ?? false, FlipV = Bool(l, "flipV") ?? false,
                    Brightness = Num(l, "brightness") ?? 100, Contrast = Num(l, "contrast") ?? 100,
                    Saturate = Num(l, "saturate") ?? 100, HueRotate = Num(l, "hueRotate") ?? 0,
                    RShift = (int)(Num(l, "rShift") ?? 0), GShift = (int)(Num(l, "gShift") ?? 0), BShift = (int)(Num(l, "bShift") ?? 0),
                });
            }
        }

        var bgImage = Str(e, "bgImage");
        var document = new Document
        {
            Mode = Str(e, "mode") == "Pants" ? TemplateMode.Pants : TemplateMode.Shirt,
            BgColor = Str(e, "bgColor") ?? "#192e44",
            BgOpacity = Num(e, "bgOpacity") ?? 1,
            BgImage = bgImage is null ? null : Image(bgImage),
            RegionFills = fills.ToImmutable(),
            Layers = layers.ToImmutable(),
        };

        if (e.TryGetProperty("actions", out var actions))
            foreach (var action in actions.EnumerateArray())
                document = action.GetProperty("op").GetString() switch
                {
                    "shape" => ApplyShape(document, action),
                    "erase" => ApplyErase(document, action, eraseStroking),
                    var op => throw new InvalidDataException($"scene {name}: op '{op}' has no handler"),
                };

        return new Scene(name, document);
    }

    /// <summary>
    /// A shape gesture from <c>from</c> to <c>to</c>, committed as 4.1 commits it: a new drawing layer
    /// on top, with the next id after every layer already there (editor.js:1285, :528-540).
    /// tool, from, to, color and width are required; fill defaults to false (drawFill, editor.js:92).
    /// </summary>
    static Document ApplyShape(Document doc, JsonElement a)
    {
        var tool = a.GetProperty("tool").GetString() switch
        {
            "line" => ShapeTool.Line,
            "rect" => ShapeTool.Rect,
            "ellipse" => ShapeTool.Ellipse,
            var t => throw new InvalidDataException($"unknown shape tool '{t}'"),
        };
        SKPoint Pt(string key)
        {
            var p = a.GetProperty(key);
            return new SKPoint(p[0].GetSingle(), p[1].GetSingle());
        }
        bool fill = a.TryGetProperty("fill", out var f) && f.GetBoolean();
        var stroke = new Stroke(tool, new[] { Pt("from"), Pt("to") },
            CssColor.ToSk(a.GetProperty("color").GetString()!), a.GetProperty("width").GetSingle(), fill);
        int id = doc.Layers.IsEmpty ? 1 : doc.Layers.Max(l => l.Id) + 1;
        var layer = Tools.CommitDrawing(stroke, id, Tools.DrawingName(tool));
        return layer is null ? doc : doc with { Layers = doc.Layers.Add(layer) };
    }

    /// <summary>
    /// Scene v2 erase op: <c>{ "op": "erase", "layer": i, "points": [[x,y],...], "size": n }</c>, stroked
    /// once like the editor. 4.1 (app-render.js) selects layer i, picks the eraser, presses at
    /// points[0], moves through the rest and releases (editor.js:1120-1146, 1196-1203, 1248-1283).
    /// Drawings append above the scene's layers (editor.js:534 layers.push), so i indexes the
    /// document's layers directly.
    /// </summary>
    public static Document ApplyErase(Document document, JsonElement action) => ApplyErase(document, action, EraseStroking.Once);

    internal static Document ApplyErase(Document document, JsonElement action, EraseStroking stroking)
    {
        int index = action.GetProperty("layer").GetInt32();
        if (index < 0 || index >= document.Layers.Count)
            throw new InvalidDataException($"erase: the scene has no layer {index}");
        var points = action.GetProperty("points").EnumerateArray()
            .Select(p => new SKPoint((float)p[0].GetDouble(), (float)p[1].GetDouble()))
            .ToList();
        float size = (float)action.GetProperty("size").GetDouble();
        var erased = Eraser.Apply(document.Layers[index], points, size, stroking);
        return document with { Layers = document.Layers.SetItem(index, erased) };
    }
}
```

In `src/RBX15.Parity/RBX15.Parity.csproj`, insert this item group directly before the closing `</Project>` line, so `GoldenAppTests` can call the internal `Parse` overload:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="RBX15.Engine.Tests" />
  </ItemGroup>

```

- [ ] **Step 10: Run the loader tests**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderEraseTests"`
Expected: `Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~SceneLoaderTests"`
Expected: `Passed: 10, Failed: 0`. Task 5's `Unsupported_ops_are_listed_once_in_order_and_refuse_to_parse` still passes, because it uses `detail`.

- [ ] **Step 11: Hold every erase scene to the golden rule**

Replace the whole of `src/RBX15.Engine.Tests/GoldenAppTests.cs` (Task 5's file) with:

```csharp
using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The M2 gate: every frozen goldens-v2 scene (tests/goldens/app, captured from the real 4.1
/// editor.html) whose actions the engine runs passes the golden rule. A scene holding an op the
/// engine does not run yet is skipped with a reason that names the op; the task that implements
/// the op adds it to SceneLoader's SupportedOps and deletes that op's MemberData line here
/// (M2b for detail, wordart and project).
/// <para>
/// Erase actions run through the internal EraseStroking.Replay41, which re-strokes the eraser path
/// after every mousemove exactly as app-render.js drove 4.1 (mousedown at points[0], one mousemove
/// per later point, mouseup with no stroke), so every erase scene meets the unchanged rule.
/// Production strokes once (approved divergence draw-pen-incremental-stroke); it is held to the
/// goldens directly on the two-point drags, where 4.1 strokes once too.
/// </para>
/// Goldens are never edited. Heatmaps go to diff-heatmaps/app/.
/// </summary>
public class GoldenAppTests
{
    const string DetailSkip = "op 'detail' is not run by the engine yet (Engine Details, M2b)";
    const string WordArtSkip = "op 'wordart' is not run by the engine yet (Engine WordArt, M2b)";
    const string ProjectSkip = "op 'project' is not run by the engine yet (Engine ProjectIO, M2b)";
    static readonly string[] PendingOps = { "detail", "wordart", "project" };

    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    static JsonElement Scene(string name) => Raw.Value.Single(e => NameOf(e) == name);

    /// <summary>Scenes whose first unsupported op is <paramref name="op"/>; "" selects the scenes the engine runs in full.</summary>
    public static TheoryData<string> ScenesBlockedBy(string op)
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            var unsupported = SceneLoader.UnsupportedOps(e);
            if ((unsupported.Count == 0 ? "" : unsupported[0]) == op) data.Add(NameOf(e));
        }
        return data;
    }

    /// <summary>Runnable scenes with at least one erase, every erase a two-point drag (one stroke in 4.1).</summary>
    public static TheoryData<string> TwoPointEraseScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value)
        {
            if (SceneLoader.UnsupportedOps(e).Count != 0 || !e.TryGetProperty("actions", out var actions)) continue;
            var erases = actions.EnumerateArray().Where(a => a.GetProperty("op").GetString() == "erase").ToList();
            if (erases.Count > 0 && erases.All(a => a.GetProperty("points").GetArrayLength() == 2)) data.Add(NameOf(e));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenesBlockedBy), "")]
    [MemberData(nameof(ScenesBlockedBy), "detail", Skip = DetailSkip)]
    [MemberData(nameof(ScenesBlockedBy), "wordart", Skip = WordArtSkip)]
    [MemberData(nameof(ScenesBlockedBy), "project", Skip = ProjectSkip)]
    public void Engine_export_matches_the_frozen_4_1_app_golden(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root, EraseStroking.Replay41);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".png"));
    }

    [Theory]
    [MemberData(nameof(TwoPointEraseScenes))]
    public void Production_stroke_once_matches_the_golden_on_two_point_erase_scenes(string name)
    {
        var scene = SceneLoader.Parse(Scene(name), Root);
        GoldenRule.AssertMatches(name, Path.Combine(Dir, name + ".png"), scene.Document, Path.Combine(Root, "diff-heatmaps", "app", name + ".once.png"));
    }

    [Fact]
    public void Every_app_scene_runs_or_is_skipped_by_a_named_op()
    {
        var unclassified = Raw.Value
            .SelectMany(e => SceneLoader.UnsupportedOps(e).Where(op => !PendingOps.Contains(op)).Select(op => $"{NameOf(e)}: '{op}'"))
            .ToList();
        Assert.Empty(unclassified);
    }

    [Fact]
    public void Every_app_scene_has_a_golden()
    {
        var missing = Raw.Value.Select(NameOf).Where(n => !File.Exists(Path.Combine(Dir, n + ".png"))).ToList();
        Assert.Empty(missing);
    }
}
```

- [ ] **Step 12: Run the app golden gate on the erase scenes**

First count what the gate should report:

```bash
python - <<'EOF'
import json
s = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
runs = lambda x: all(a["op"] in ("shape", "erase") for a in x.get("actions", []))
run = [x for x in s if runs(x)]
two = [x["name"] for x in run if any(a["op"] == "erase" for a in x["actions"]) and all(len(a["points"]) == 2 for a in x["actions"] if a["op"] == "erase")]
print("runnable", len(run), "two-point", len(two), "skipped", len(s) - len(run), two)
EOF
```

Expected: `runnable 20 two-point 3 skipped 62 ['erase-plain-one-segment', 'erase-max-size', 'erase-two-point-transformed']`. Runnable is the 12 shape and 8 erase scenes; skipped is 37 + 22 + 3 = 62. A two-point count of 0 means Task 3 froze no two-point erase scene: stop and report it.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" > .superpowers/logs/app-golden.log 2>&1; tail -30 .superpowers/logs/app-golden.log`
Expected: `Failed: 0, Passed: 25, Skipped: 62` (20 replayed scenes, 3 production two-point scenes, 2 facts). Each skipped row's reason names `detail`, `wordart` or `project`.

If `Engine_export_matches_the_frozen_4_1_app_golden` fails for an erase scene, the port is wrong. Use superpowers:systematic-debugging, starting from `diff-heatmaps/app/<scene>.png`. In order of likelihood:

- **Off-center or mirrored hole.** The inverse transform order or the rotation sign. Compare `EraseStroke` line by line with `lib/erase.js:58-62`.
- **Hole in the right place, soft or hard edges wrong.** The mask draw's sampling or antialias. It must be `LayerSampling` with `DrawImageAntialiased(ctm, 585, 559)` and `FloatPipeline` off scale-translate.
- **Fringe off by 1 to 2 levels everywhere, interior exact.** The destination-out draw's 8-bit pipeline. Try `ColorFilter = Renderer.FloatPipeline` on the `dstOut` paint (the same Skia lowp-versus-float split the Renderer index documents). Keep it only if the replay then passes every erase scene, and add a Chromium-compat index row for it.
- **Every pixel of the erased layer off by one level on semi-transparent pixels.** The premultiply round trip. Both sources must go through `Images.ForDrawing`, and the result must be read back un-premultiplied.
- **Hole shifted by a fraction of a pixel along the drag.** The golden's event coordinates. Read the scene's provenance in `tests/goldens/app/index.json`. If app-render.js mapped template points to client pixels with rounding, report it rather than compensating in the engine.
- **A wrong replay.** `StrokeCanvas` with `Replay41` must stroke the path `points[0..1]`, then `points[0..2]`, up to the full path, and nothing at mouseup. A replay that strokes at mousedown or once more at the end is not what app-render.js sent.

If `Production_stroke_once_matches_the_golden_on_two_point_erase_scenes` fails while the replay passes the same scene, `Once` and `Replay41` disagree on a two-point drag, which `A_two_point_drag_strokes_identically_once_and_replayed` rules out: check that the two modes build the same path. `erase-rgb-shift` goes through `GoldenRule`'s RGB-shift rule like every shifted scene; a failure there is judged under that rule, not plain `Compare`. Never edit a golden and never loosen `ImageDiff` or `GoldenRule`. If none of these explains it, stop and report the scene, the heatmap and the `DiffResult`.

- [ ] **Step 13: Measure stroke-once against the multi-point goldens (the documented finding)**

The contract asks what stroking once does to erase parity. Render all 8 erase scenes through the public path (stroke once) with the Parity CLI, diff each against its golden, and keep the numbers for the commit body:

```bash
mkdir -p .superpowers/m2a-measure .superpowers/logs
python - <<'EOF'
import json
s = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
e = [x for x in s if any(a["op"] == "erase" for a in x.get("actions", [])) and all(a["op"] in ("shape", "erase") for a in x["actions"])]
open(".superpowers/m2a-measure/erase-scenes.json", "w", encoding="utf-8", newline="\n").write(json.dumps(e))
print(len(e), "erase scenes")
EOF
dotnet run --project src/RBX15.Parity -c Release -- render .superpowers/m2a-measure/erase-scenes.json .superpowers/m2a-measure/once
for n in erase-plain erase-plain-one-segment erase-rotated-scaled erase-flipped erase-rgb-shift erase-max-size erase-bottom-of-two erase-two-point-transformed; do
  printf '%s ' "$n"; dotnet run --project src/RBX15.Parity -c Release --no-build -- diff "tests/goldens/app/$n.png" ".superpowers/m2a-measure/once/$n.png"
done | tee .superpowers/logs/erase-finding.txt
```

Expected:

- `8 erase scenes`, then 8 `rendered erase-...` lines.
- 8 diff lines, one per scene, each `<scene> same-size=True max-delta=... differing=... interior=... edge-over-tolerance=... pass=...`.
- The three two-point scenes (`erase-plain-one-segment`, `erase-max-size`, `erase-two-point-transformed`) print `pass=True`.
- The multi-point scenes `erase-plain`, `erase-rotated-scaled`, `erase-flipped` and `erase-bottom-of-two` print `interior=0`; their `pass` value and `edge-over-tolerance` are the finding, whatever they are.
- `erase-rgb-shift` is recorded only. A plain `diff` has no RGB-shift rule, so its line also counts the approved `rgb-shift-compute` difference and cannot isolate the stroking.

Any of those four multi-point lines with `interior` above 0 contradicts the fringe-only analysis above. Stop and report it with that scene's heatmap (`--heatmap .superpowers/m2a-measure/<scene>.heat.png`): stroking once would change more than the approved divergence covers. The measured file feeds Step 14's commit body; it is not committed itself.

- [ ] **Step 14: Commit the loader and the erase gate, with the finding**

Write the commit message from Step 13's output, then commit:

```bash
python - <<'EOF'
lines = open(".superpowers/logs/erase-finding.txt", encoding="utf-8").read().strip().splitlines()
msg = "\n".join([
    "test(engine): every erase golden met through the 4.1 replay; stroke-once pinned on two-point drags",
    "",
    "Production Eraser.Apply strokes once (approved divergence draw-pen-incremental-stroke).",
    "GoldenAppTests applies erase actions through the internal EraseStroking.Replay41, which",
    "mirrors app-render.js's mousedown / mousemove-per-point / mouseup sequence, so all 8 erase",
    "scenes meet the unchanged golden rule. Production meets the 3 two-point goldens directly.",
    "",
    "Measured stroke-once against each erase golden (RBX15.Parity diff):",
    *lines,
])
open(".superpowers/logs/erase-commit-msg.txt", "w", encoding="utf-8", newline="\n").write(msg + "\n")
print(msg)
EOF
```

Append your model's `Co-Authored-By` trailer as the last line of `.superpowers/logs/erase-commit-msg.txt` (after a blank line), then:

```bash
git add src/RBX15.Parity/SceneLoader.cs src/RBX15.Parity/RBX15.Parity.csproj src/RBX15.Parity.Tests/SceneLoaderEraseTests.cs src/RBX15.Engine.Tests/GoldenAppTests.cs
git commit -F .superpowers/logs/erase-commit-msg.txt
```

Expected: the printed message ends with the 8 measured lines, and `git log -1 --format=%s` prints `test(engine): every erase golden met through the 4.1 replay; stroke-once pinned on two-point drags`.

- [ ] **Step 15: Run everything**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: `0 Warning(s)`. Engine.Tests `Passed: 167, Failed: 0, Skipped: 62` (Task 5's 137, plus 19 `EraserTests`, plus 11 more `GoldenAppTests`: 8 erase rows, 3 production rows). Parity.Tests `Passed: 23, Failed: 0` (19 plus 4). `GoldenRenderTests` still passes all 34 scenes, which shows the `internal` change in `Renderer.cs` moved no pixels.

**Ledger evidence this task hands to Task 8** (no `ledger.py done` here; Task 8 closes rows in one batch and writes the eraser divergence sentence):

- `eraser-math-inverse` (golden): `EraseStroke` follows `lib/erase.js:43-73`, and every erase scene passes `GoldenAppTests` under the replay, including scale (`erase-rotated-scaled`, `erase-two-point-transformed`), rotation 30, `flipH` and `flipV` (`erase-flipped`).
- `tests-lib-erase` (unit): the five `erase.test.js` cases with the same pixels and values, plus the 30-degree "hole lands under the cursor" case the row asks for.
- `layer-sharing-img` (unit, owner Engine.Document): `Layers_sharing_an_image_keep_their_pixels` is its parity check (copy-on-write erase), and Task 7's `Snapshots_share_documents_and_bitmaps_by_reference` pins bitmap sharing by reference. The per-image RGB-shift cache it also asks for shipped in M1 (`fd2b8c2`).
- `eraser-apply` stays open: the "no selection" alert and the hidden-layer and selection rules are App.Canvas behavior. Task 8 records the stroke-once divergence on it.
- `eraser-cursor`, `cursor-eraser` and `eraser-selection-semantics` stay open for the App.

---

### Task 7: Engine History

**Files:**

- Create: `src/RBX15.Engine/History.cs`
- Test: `src/RBX15.Engine.Tests/HistoryTests.cs`

**Interfaces:**

- Consumes: `Document`, `Layer` (`src/RBX15.Engine/Document.cs`, M1), `Fill.Solid` (`src/RBX15.Engine/Fills.cs`, M1), `TemplateMode` (`src/RBX15.Engine/Template.cs`, M1), test helper `Px.Solid(int, int, SKColor)` (`src/RBX15.Engine.Tests/Px.cs`, M1).
- Produces:
  - `public sealed record EditorSnapshot(Document Document, int? SelectedLayerId)`
  - `public sealed class History`
    - `History(int capacity = 30)`: throws `ArgumentOutOfRangeException` when `capacity < 1`
    - `int Capacity { get; }`
    - `void Record(EditorSnapshot before)`: push onto undo (evicting the oldest past `Capacity`), clear redo
    - `bool CanUndo { get; }`, `bool CanRedo { get; }`
    - `int UndoCount { get; }`, `int RedoCount { get; }`: the port of `sizes()` (`lib/history.js:57`); additive to the contract (approved), needed so the cap test ports with identical expectations
    - `EditorSnapshot? Undo(EditorSnapshot current)`: null when nothing to undo (and `current` is not parked)
    - `EditorSnapshot? Redo(EditorSnapshot current)`: mirror of `Undo`
  - Selection rule: the returned snapshot carries `current.SelectedLayerId` when a layer with that id exists in the returned `Document`, otherwise `null`.

**What 4.1 does (cited):**

- Stack discipline, `lib/history.js:25-58`. The default limit is 30 (`:26`), and the editor passes 30 (`editor.js:86`). `push` appends, evicts the oldest with `shift` when over the limit, and clears redo (`:33-36`). `undo(current)` returns `null` on an empty stack **before** parking anything (`:43`), otherwise parks `current` on redo and pops undo (`:44-45`). `redo` is the mirror (`:49-53`). `canUndo`, `canRedo` and `sizes` are at `:55-57`.
- The editor snapshots before every mutation (`saveUndo`, `editor.js:1420`). The buttons call `history.undo(snapshotState())` / `history.redo(snapshotState())` and restore the result, ignoring `null` (`editor.js:1477-1486`).
- A snapshot is JSON with every layer's image re-encoded as a data URL (`editor.js:1404-1415`). Restore (`restoreState`, `editor.js:1422-1468`) is asynchronous, reloading every image, and always deselects (`selectedLayerId = null`, `editor.js:1451`). `nextId` only grows after a restore (`editor.js:1465`).
- `switchMode` clears the region fills (`editor.js:1711`), so undo across a mode switch must bring the fills back from the snapshot (`editor.js:1427`, `editor.js:1444`).

**Approved divergences implemented here** (`docs/port/divergences.md`):

- `history-redo-stack-uncapped` / `history-snapshot-memory`: redo is capped at `Capacity`, undo depth stays 30, and snapshots share immutable bitmaps.
- `undo-rapid-restore-race` / `undo-restore`: restore is synchronous. A returned snapshot is the stored `Document` reference, so there is nothing to load and nothing to race.
- `undo-restore-ui-side-effects`: the selection survives undo and redo when the layer still exists (spec §5, "Undo").

**Finding for the reviewer (not a behavior change):** in both 4.1 and v5, `UndoCount + RedoCount` never exceeds the cap. `Record` leaves redo empty and undo at most the cap, and `Undo`/`Redo` move exactly one entry from one stack to the other. So the "undo depth can briefly exceed 30" quirk in `history-redo-stack-uncapped` cannot actually happen; the v5 caps on both stacks are defensive. `Neither_stack_ever_exceeds_capacity` pins the invariant.

**Selection rule, resolved:** the contract says the selection survives "when the layer id exists in the restored document; otherwise null". The id that survives is the **live** selection (`current.SelectedLayerId`), because spec §5 says "selection survives undo when the layer still exists". The `SelectedLayerId` stored inside a recorded snapshot does not override the live one; it is overwritten in the returned record. `The_live_selection_is_what_survives_not_the_recorded_one` pins this.

- [ ] **Step 1: Write the failing tests**

Create `src/RBX15.Engine.Tests/HistoryTests.cs`:

```csharp
using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>
/// History is a port of lib/history.js. The first seven facts are tests/history.test.js case for case;
/// the 4.1 strings become snapshots labeled through BgColor, which History never inspects.
/// </summary>
public class HistoryTests
{
    static EditorSnapshot Snap(string label, int? selected = null) => new(new Document { BgColor = label }, selected);

    static string? Label(EditorSnapshot? s) => s?.Document.BgColor;

    static Layer L(int id) => new() { Id = id, Image = Px.Solid(2, 2, SKColors.Red), Name = $"Layer {id}" };

    // ---- tests/history.test.js, case for case ----

    [Fact] // tests/history.test.js:10-14
    public void Undo_returns_the_recorded_snapshot()
    {
        var h = new History();
        h.Record(Snap("A"));
        Assert.Equal("A", Label(h.Undo(Snap("B"))));
    }

    [Fact] // tests/history.test.js:16-19
    public void Undo_on_an_empty_stack_returns_null()
    {
        var h = new History();
        Assert.Null(h.Undo(Snap("current")));
    }

    [Fact] // tests/history.test.js:21-27
    public void Redo_brings_back_the_state_undo_parked()
    {
        var h = new History();
        h.Record(Snap("A"));
        Assert.Equal("A", Label(h.Undo(Snap("B"))));
        Assert.Equal("B", Label(h.Redo(Snap("A"))));
    }

    [Fact] // tests/history.test.js:29-37
    public void A_new_record_clears_the_redo_stack()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Undo(Snap("B"));
        Assert.True(h.CanRedo);
        h.Record(Snap("A2"));
        Assert.False(h.CanRedo);
        Assert.Null(h.Redo(Snap("X")));
    }

    [Fact] // tests/history.test.js:39-47
    public void Multi_step_undo_and_redo_preserve_order()
    {
        var h = new History();
        h.Record(Snap("s0"));
        h.Record(Snap("s1"));
        h.Record(Snap("s2"));
        Assert.Equal("s2", Label(h.Undo(Snap("s3"))));
        Assert.Equal("s1", Label(h.Undo(Snap("s2"))));
        Assert.Equal("s2", Label(h.Redo(Snap("s1"))));
        Assert.Equal("s3", Label(h.Redo(Snap("s2"))));
        Assert.False(h.CanRedo);
    }

    [Fact] // tests/history.test.js:49-57
    public void Respects_the_cap_evicting_the_oldest()
    {
        var h = new History(3);
        foreach (var s in new[] { "a", "b", "c", "d", "e" }) h.Record(Snap(s));
        Assert.Equal((3, 0), (h.UndoCount, h.RedoCount));
        Assert.Equal("e", Label(h.Undo(Snap("live"))));
        Assert.Equal("d", Label(h.Undo(Snap("e"))));
        Assert.Equal("c", Label(h.Undo(Snap("d"))));
        Assert.Null(h.Undo(Snap("c")));
    }

    [Fact] // tests/history.test.js:59-68
    public void CanUndo_and_CanRedo_reflect_stack_state()
    {
        var h = new History();
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        h.Record(Snap("A"));
        Assert.True(h.CanUndo);
        h.Undo(Snap("B"));
        Assert.False(h.CanUndo);
        Assert.True(h.CanRedo);
    }

    // ---- ledger checks and approved divergences ----

    [Fact] // undo-model, history-snapshot-memory: editor.js:86 passes 30; lib/history.js:35 evicts the oldest
    public void Default_depth_is_30_and_the_31st_record_evicts_the_first()
    {
        var h = new History();
        Assert.Equal(30, h.Capacity);
        for (int i = 0; i < 35; i++) h.Record(Snap($"s{i}"));
        Assert.Equal(30, h.UndoCount);
        string? last = null;
        for (int i = 0; i < 30; i++) last = Label(h.Undo(Snap("live")));
        Assert.Equal("s5", last);
        Assert.Null(h.Undo(Snap("live")));
    }

    [Fact] // history-redo-stack-uncapped: the ledger's check sequence (30 records, 5 undos, 5 redos, 1 record)
    public void Redo_is_capped_and_undo_depth_stays_30()
    {
        var h = new History();
        for (int i = 0; i < 30; i++) h.Record(Snap($"s{i}"));
        for (int i = 0; i < 5; i++) h.Undo(Snap("live"));
        Assert.Equal((25, 5), (h.UndoCount, h.RedoCount));
        for (int i = 0; i < 5; i++) h.Redo(Snap("live"));
        Assert.Equal((30, 0), (h.UndoCount, h.RedoCount));
        h.Record(Snap("s30"));
        Assert.Equal((30, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact] // the invariant behind the redo cap: one entry moves per step, Record leaves redo empty
    public void Neither_stack_ever_exceeds_capacity()
    {
        var h = new History(3);
        int n = 0;
        foreach (char op in "rrrruuuuuyyyyyruyryuuuyyyy")
        {
            var live = Snap($"x{n++}");
            if (op == 'r') h.Record(live);
            else if (op == 'u') h.Undo(live);
            else h.Redo(live);
            Assert.InRange(h.UndoCount + h.RedoCount, 0, 3);
        }
    }

    [Fact] // contract: Record pushes undo and clears redo (lib/history.js:33-36); empty redo parks nothing (:50)
    public void Record_after_undo_clears_redo_and_an_empty_redo_parks_nothing()
    {
        var h = new History();
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        h.Undo(Snap("C"));
        h.Undo(Snap("B"));
        Assert.Equal((0, 2), (h.UndoCount, h.RedoCount));
        h.Record(Snap("A"));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
        Assert.Null(h.Redo(Snap("A2")));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact] // lib/history.js:43: an empty undo returns before parking current
    public void Undo_on_empty_parks_nothing()
    {
        var h = new History();
        Assert.Null(h.Undo(Snap("live")));
        Assert.Equal((0, 0), (h.UndoCount, h.RedoCount));
        Assert.False(h.CanRedo);
    }

    [Fact] // undo-restore-ui-side-effects: 4.1 deselects on every restore (editor.js:1451); v5 keeps the selection
    public void Selection_survives_when_the_layer_exists_in_the_restored_document()
    {
        var doc = new Document { Layers = ImmutableList.Create(L(1), L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(doc, 2));
        var edited = doc with { BgColor = "#ff0000" };
        var undone = h.Undo(new EditorSnapshot(edited, 2))!;
        Assert.Same(doc, undone.Document);
        Assert.Equal(2, undone.SelectedLayerId);
        var redone = h.Redo(undone)!;
        Assert.Same(edited, redone.Document);
        Assert.Equal(2, redone.SelectedLayerId);
    }

    [Fact] // spec §5: the selection survives only "when the layer still exists"
    public void Selection_becomes_null_when_the_layer_is_gone()
    {
        var before = new Document { Layers = ImmutableList.Create(L(1)) };
        var after = before with { Layers = before.Layers.Add(L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(before, 1));
        var undone = h.Undo(new EditorSnapshot(after, 2))!;
        Assert.Same(before, undone.Document);
        Assert.Null(undone.SelectedLayerId);
        var redone = h.Redo(undone)!;
        Assert.Same(after, redone.Document);
        Assert.Null(redone.SelectedLayerId);
    }

    [Fact] // resolved contract ambiguity: the live selection survives; the recorded one does not override it
    public void The_live_selection_is_what_survives_not_the_recorded_one()
    {
        var doc = new Document { Layers = ImmutableList.Create(L(1), L(2)) };
        var h = new History();
        h.Record(new EditorSnapshot(doc, 1));
        h.Record(new EditorSnapshot(doc with { BgColor = "#000001" }, 1));
        Assert.Equal(2, h.Undo(new EditorSnapshot(doc with { BgColor = "#000002" }, 2))!.SelectedLayerId);
        Assert.Null(h.Undo(new EditorSnapshot(doc with { BgColor = "#000001" }, null))!.SelectedLayerId);
    }

    [Fact] // history-snapshot-memory: no re-encoding (4.1 stores a data URL per layer per snapshot, editor.js:1413)
    public void Snapshots_share_documents_and_bitmaps_by_reference()
    {
        var l1 = L(1);
        var l2 = L(2);
        var before = new Document { Layers = ImmutableList.Create(l1) };
        var after = before with { Layers = before.Layers.Add(l2) };
        var h = new History();
        h.Record(new EditorSnapshot(before, 1));
        var undone = h.Undo(new EditorSnapshot(after, 2))!;
        Assert.Same(before, undone.Document);
        Assert.Same(l1.Image, undone.Document.Layers[0].Image);
        var redone = h.Redo(undone)!;
        Assert.Same(after, redone.Document);
        Assert.Same(l1.Image, redone.Document.Layers[0].Image);
        Assert.Same(l2.Image, redone.Document.Layers[1].Image);
    }

    [Fact] // undo-rapid-restore-race: restore is synchronous, so back-to-back undos cannot interleave
    public void Rapid_undo_then_redo_ends_with_every_layer()
    {
        var h = new History();
        var doc = new Document();
        var states = new List<Document> { doc };
        for (int id = 1; id <= 5; id++)
        {
            h.Record(new EditorSnapshot(doc, null));
            doc = doc with { Layers = doc.Layers.Add(L(id)) };
            states.Add(doc);
        }
        var live = new EditorSnapshot(doc, 5);
        for (int i = 0; i < 5; i++) live = h.Undo(live)!;
        Assert.Same(states[0], live.Document);
        for (int i = 0; i < 5; i++) live = h.Redo(live)!;
        Assert.Same(states[5], live.Document);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, live.Document.Layers.Select(l => l.Id));
        Assert.False(h.CanRedo);
    }

    [Fact] // tests-lib-history integration check; the mode switch clears fills like switchMode (editor.js:1711)
    public void Four_kinds_of_edit_undo_to_the_start_and_redo_to_the_end()
    {
        var s0 = new Document();
        var s1 = s0 with { Layers = s0.Layers.Add(L(1)) };
        var s2 = s1 with { BgColor = "#ff0000" };
        var s3 = s2 with { RegionFills = s2.RegionFills.Add(("torso_front", Fill.Solid("#00ff00"))) };
        var s4 = s3 with { Mode = TemplateMode.Pants, RegionFills = ImmutableList<(string, Fill)>.Empty };
        var docs = new[] { s0, s1, s2, s3, s4 };
        var h = new History();
        for (int i = 0; i < 4; i++) h.Record(new EditorSnapshot(docs[i], null));
        var live = new EditorSnapshot(s4, null);
        for (int i = 3; i >= 0; i--)
        {
            live = h.Undo(live)!;
            Assert.Same(docs[i], live.Document);
        }
        Assert.Equal(TemplateMode.Shirt, live.Document.Mode);
        Assert.Empty(live.Document.Layers);
        Assert.Equal("#192e44", live.Document.BgColor);
        for (int i = 1; i <= 4; i++)
        {
            live = h.Redo(live)!;
            Assert.Same(docs[i], live.Document);
        }
        Assert.Equal(TemplateMode.Pants, live.Document.Mode);
        Assert.Empty(live.Document.RegionFills);
    }

    [Fact] // Review Focus: the smallest legal history, one step back and one step forward
    public void Capacity_one_keeps_only_the_latest_step()
    {
        var h = new History(1);
        h.Record(Snap("A"));
        h.Record(Snap("B"));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
        Assert.Equal("B", Label(h.Undo(Snap("C"))));
        Assert.Null(h.Undo(Snap("B")));
        Assert.Equal((0, 1), (h.UndoCount, h.RedoCount));
        Assert.Equal("C", Label(h.Redo(Snap("B"))));
        Assert.Equal((1, 0), (h.UndoCount, h.RedoCount));
    }

    [Fact]
    public void Capacity_must_be_positive_and_snapshots_non_null()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new History(0));
        var h = new History();
        Assert.Throws<ArgumentNullException>(() => h.Record(null!));
        Assert.Throws<ArgumentNullException>(() => h.Undo(null!));
        Assert.Throws<ArgumentNullException>(() => h.Redo(null!));
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: build fails in `RBX15.Engine.Tests` with `error CS0246: The type or namespace name 'History' could not be found` and the same error for `EditorSnapshot`.

- [ ] **Step 3: Implement**

Create `src/RBX15.Engine/History.cs`:

```csharp
namespace RBX15.Engine;

/// <summary>
/// One recoverable editor state: the immutable <see cref="Document"/> and the selected layer id.
/// The document and its layer bitmaps are shared by reference, never copied or re-encoded.
/// 4.1 serialized every layer as a PNG data URL in every snapshot (editor.js:1404-1415); see the
/// approved divergence <c>history-snapshot-memory</c>.
/// </summary>
public sealed record EditorSnapshot(Document Document, int? SelectedLayerId);

/// <summary>
/// Undo and redo stacks, ported from lib/history.js:25-58. The undo stack holds PRIOR states: the
/// caller records the state from before a change, and <see cref="Undo"/>/<see cref="Redo"/> take the
/// live state so they can park it on the opposite stack (lib/history.js:42-53).
/// <para>
/// <b>One step per gesture is the caller's job</b> (the M3/M4 editor session and view-models), not
/// History's. History stores exactly what it is given: it never merges, compares or drops snapshots.
/// Spec §5 ("Undo", docs/superpowers/specs/2026-10-11-native-v5-port-design.md) sets the rule:
/// every document change is exactly one step, with no empty or double steps. A gesture is one
/// complete user action that changes the document: a press-drag-release on the canvas (move,
/// resize, rotate, a shape, an eraser stroke), one slider or color-picker drag from press to
/// release, one key press (nudge, rotate, reset, scale), or one command (a button, a preset, a fill,
/// add, duplicate, delete, reorder, a mode switch). For each gesture the caller calls
/// <see cref="Record"/> once, with the snapshot from before the gesture, and only when the gesture
/// actually changed the document. A preset that runs several placement helpers is still one record.
/// </para>
/// <para>
/// Restore is synchronous: the caller adopts the returned snapshot as its live state, and nothing
/// loads (divergences <c>undo-restore</c>, <c>undo-rapid-restore-race</c>). The returned
/// <see cref="EditorSnapshot.SelectedLayerId"/> is the live selection when that layer exists in the
/// returned document, otherwise null; 4.1 deselected on every restore (editor.js:1451). Layer ids can
/// come back through undo and redo, so the caller's id counter must only grow (editor.js:1465).
/// </para>
/// <para>
/// Undo depth is <see cref="Capacity"/> (30, editor.js:86), evicting the oldest. Redo is capped at
/// the same capacity (divergence <c>history-redo-stack-uncapped</c>). Because each step moves one
/// entry between the stacks and <see cref="Record"/> empties redo, the two counts never sum past the
/// capacity, so the caps are defensive. Not thread-safe; use it from the UI thread.
/// </para>
/// </summary>
public sealed class History
{
    readonly List<EditorSnapshot> _undo = [];
    readonly List<EditorSnapshot> _redo = [];

    /// <param name="capacity">Maximum undo depth (and redo depth). The editor uses 30 (editor.js:86).</param>
    public History(int capacity = 30)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Undo depth, the <c>undo</c> half of 4.1's <c>sizes()</c> (lib/history.js:57).</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Redo depth, the <c>redo</c> half of 4.1's <c>sizes()</c> (lib/history.js:57).</summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// Records the state from BEFORE a change: pushes it onto undo, evicts the oldest past
    /// <see cref="Capacity"/>, and clears redo, because a new edit abandons the redo branch
    /// (lib/history.js:33-36).
    /// </summary>
    public void Record(EditorSnapshot before)
    {
        ArgumentNullException.ThrowIfNull(before);
        PushCapped(_undo, before);
        _redo.Clear();
    }

    /// <summary>
    /// Reverts one step. Returns null, parking nothing, when there is nothing to undo
    /// (lib/history.js:43). Otherwise parks <paramref name="current"/> on redo and returns the
    /// previous state with the selection resolved (lib/history.js:44-45).
    /// </summary>
    public EditorSnapshot? Undo(EditorSnapshot current) => Step(_undo, _redo, current);

    /// <summary>Reapplies one undone step. The mirror image of <see cref="Undo"/> (lib/history.js:49-53).</summary>
    public EditorSnapshot? Redo(EditorSnapshot current) => Step(_redo, _undo, current);

    EditorSnapshot? Step(List<EditorSnapshot> from, List<EditorSnapshot> to, EditorSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (from.Count == 0) return null;
        PushCapped(to, current);
        var target = from[^1];
        from.RemoveAt(from.Count - 1);
        int? selected = current.SelectedLayerId is int id && target.Document.Layers.Exists(l => l.Id == id) ? id : null;
        return target with { SelectedLayerId = selected };
    }

    void PushCapped(List<EditorSnapshot> stack, EditorSnapshot snapshot)
    {
        stack.Add(snapshot);
        if (stack.Count > Capacity) stack.RemoveAt(0);
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~RBX15.Engine.Tests.HistoryTests"`
Expected: `Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20` for `RBX15.Engine.Tests.dll`.

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Engine.Tests `Passed: 187, Failed: 0, Skipped: 62` (Task 6's 167 plus 20). Parity.Tests `Passed: 23, Failed: 0`.

- [ ] **Step 5: Commit**

End the message with your own model's `Co-Authored-By` trailer (global constraint).

```bash
git add src/RBX15.Engine/History.cs src/RBX15.Engine.Tests/HistoryTests.cs
git commit -m "feat(engine): History with capped redo, shared-bitmap snapshots and surviving selection"
```

- [ ] **Step 6: Hand the ledger rows to the closing task**

Don't run `ledger.py done` here; Task 8 closes rows in one batch, as M1's Task 7 did. Rows this task proves, each with its check:

| Row | Proved by |
| --- | --- |
| `undo-model` | the seven ported cases plus `Default_depth_is_30_and_the_31st_record_evicts_the_first` |
| `tests-lib-history` | the seven ported cases plus `Four_kinds_of_edit_undo_to_the_start_and_redo_to_the_end` |
| `history-redo-stack-uncapped` | `Redo_is_capped_and_undo_depth_stays_30`, `Neither_stack_ever_exceeds_capacity` |
| `history-snapshot-memory` | `Default_depth_is_30_and_the_31st_record_evicts_the_first`, `Snapshots_share_documents_and_bitmaps_by_reference` |
| `undo-rapid-restore-race` | `Rapid_undo_then_redo_ends_with_every_layer` |
| `layer-sharing-img` (with Task 6) | `Snapshots_share_documents_and_bitmaps_by_reference` pins bitmap sharing by reference; Task 6's `Layers_sharing_an_image_keep_their_pixels` pins the copy-on-write erase |

These Engine.History rows stay open, because their checks need the caller that records one step per gesture or restores UI state (M3/M4): `history-snapshot-coupling`, `undo-snapshot-contents`, `undo-restore`, `undo-restore-ui-side-effects` (fill controls resync), `undo-coverage-gaps`, `undo-empty-steps`, `fillstyle-undo-snapshot-timing`, `file-undo-snapshot-scope`, `restore-mode-switch-side-effects`, `mode-switch-undo-and-callers`.

---

### Task 8: Ledger close and final verification

**Files:**

- Modify: `docs/port/parity-ledger.json` (the `eraser-apply` divergence, then `ledger.py done`)
- Modify (generated by `ledger.py done`): `docs/port/parity-ledger.md`, `docs/port/divergences.md`
- Test: no new tests. The evidence is the suites Tasks 4-7 wrote: `GoldenRenderTests` (34 scenes), `GoldenAppTests`, `ToolsTests`, `EraserTests`, `SceneLoaderEraseTests`, `HistoryTests`.

**Interfaces:**

- Consumes:
  - The green 34-scene `GoldenRenderTests` gate (Task 4).
  - `Tools.RenderStroke`, `Tools.Autocrop`, `Tools.CommitDrawing`, `Tools.DrawingName`, `GoldenRule`, `GoldenAppTests` (Task 5).
  - `Eraser.Apply`, `Eraser.EraseStroke`, `GoldenAppTests.Production_stroke_once_matches_the_golden_on_two_point_erase_scenes`, and the stroke-once measurement in Task 6's commit body (Task 6).
  - `History`, `EditorSnapshot` (Task 7).
  - `python tools/ledger/ledger.py done <id...>` and `check` (M1 Task 7).
- Produces:
  - The ledger with the 13 rows M2a proves set to `done` (55 done of 407), the stroke-once divergence recorded once on `eraser-apply`, and the list of rows left open, with reasons, in the commit body.
  - A clean, verified branch. Push, PR and dashboard steps belong to the controller and are not part of this task.

**The rule for closing.** The ledger is closed conservatively, and only here. A row closes only if Tasks 4-7 prove its whole behavior text (or, for a `fix` row, its whole v5 divergence sentence) in a unit test or a golden that exists in this plan. A half owned by the UI (`App.Canvas`, `App.Ribbon.*`) or by the caller's one-step-per-gesture semantics (M3/M4, per the History contract) keeps the row open.

- [ ] **Step 1: Confirm that the evidence for each ledger row exists before closing it**

Candidate rows (13):

| Row | Check | Behavior that must be pinned | Pinned by | 4.1 source |
| --- | --- | --- | --- | --- |
| `autocrop` | unit | Empty canvas returns null. Bounds are alpha > 0, padded by 10 and clamped to the canvas. `pad` 0 behaves as 10 (`pad \|\| 10`). The returned X and Y are the crop origin. | `Autocrop_*` (4 facts), `Pen_press_without_movement_draws_a_dot_of_the_brush_size` (Task 5) | `editor.js:144-171` (`:145` pad, `:151` alpha > 0, `:159` null, `:160-163` clamp, `:170` return) |
| `draw-shape-line` | golden | Line geometry: round caps, `fill` ignored. | `shape-line`, `shape-line-thick`, `shape-line-w1`, `shape-line-off-canvas` | `editor.js:1205-1216` |
| `draw-shape-rect` | golden | Normalized rect from any drag direction. Filled means fill only; otherwise a miter stroke centered on the edge. | `shape-rect`, `shape-rect-reverse-w1`, `shape-rect-fill`, `shape-rect-zero-height` | `editor.js:1217-1223` |
| `draw-shape-ellipse` | golden | Bounding-box ellipse, filled or stroked, plus the degenerate case where one radius is 0. | `shape-ellipse`, `shape-ellipse-fill`, `shape-ellipse-reverse-thick`, `shape-ellipse-degenerate` | `editor.js:1224-1233` |
| `draw-canvas-template-clip` | golden | The stroke canvas is 585x559, so a stroke past the edge is clipped. Autocrop pads by 10; the layer has scale 1 at the crop origin. | `shape-line-off-canvas`, plus every shape golden for the crop | `editor.js:1124-1126`, `:1250-1255`, `:144-171`, `:528-540` |
| `eraser-math-inverse` | golden | The inverse-transform mask and the two-step destination-out, under scale, rotation, flipH and flipV. | All 8 erase scenes through the replay; production on `erase-two-point-transformed` | `lib/erase.js:43-73`, `editor.js:210-234` |
| `tests-lib-erase` | unit | The 5 cases of `tests/erase.test.js`, plus a rotated (not a multiple of 90) erase. | The five ported `EraserTests` cases plus `Rotated_30_degrees_the_hole_lands_under_the_cursor` (Task 6) | `tests/erase.test.js:34-71` |
| `layer-sharing-img` | unit | Clones share one image; erasing one clone leaves the others' pixels unchanged; sharing is by reference. | `Layers_sharing_an_image_keep_their_pixels` (Task 6), `Snapshots_share_documents_and_bitmaps_by_reference` (Task 7); per-image RGB-shift cache shipped in M1 `fd2b8c2` | `editor.js:1272-1283`, `:546-553` |
| `undo-model` | unit | Push appends, evicts the oldest past capacity and clears redo. Undo and redo mirror each other. Empty undo returns null. 35 records leave 30 undo steps. | The seven ported `HistoryTests` cases plus `Default_depth_is_30_and_the_31st_record_evicts_the_first` | `lib/history.js:25-58` |
| `tests-lib-history` | unit | The 7 cases of `tests/history.test.js`, plus the integration case: add a layer, change the background, fill a region, switch Shirt to Pants, then Undo 4 times back to the initial state including fills, and Redo 4 times. | The seven ported cases plus `Four_kinds_of_edit_undo_to_the_start_and_redo_to_the_end` | `tests/history.test.js:10-68` |
| `history-snapshot-memory` | unit | Undo depth 30: the 31st record makes the first non-undoable. Redo is capped. A snapshot holds the same `SKImage` instances, by reference. | `Default_depth_is_30_and_the_31st_record_evicts_the_first`, `Snapshots_share_documents_and_bitmaps_by_reference` | `lib/history.js:33-37`, `editor.js:86` |
| `history-redo-stack-uncapped` | unit | 30 records, 5 undos, 5 redos and 1 record give asserted depths. Redo never exceeds capacity. | `Redo_is_capped_and_undo_depth_stays_30`, `Neither_stack_ever_exceeds_capacity` | `lib/history.js:31-56` |
| `undo-rapid-restore-race` | unit | Undo N times then redo N times restores the final document with every layer. Restore is synchronous, so no partial state can be parked. | `Rapid_undo_then_redo_ends_with_every_layer` | `editor.js:1422-1468`, `:1477-1486` |

List the unit tests that Tasks 5-7 wrote:

```bash
grep -c "\[Fact\]\|InlineData" src/RBX15.Engine.Tests/ToolsTests.cs src/RBX15.Engine.Tests/EraserTests.cs src/RBX15.Engine.Tests/HistoryTests.cs
grep -n "public void" src/RBX15.Engine.Tests/ToolsTests.cs src/RBX15.Engine.Tests/EraserTests.cs src/RBX15.Engine.Tests/HistoryTests.cs
```

Expected: `ToolsTests.cs:22`, `EraserTests.cs:19`, `HistoryTests.cs:20`, then the method names. For each `unit` row in the table, find the named tests in the list. Write down each row whose named test is missing.

Check that the golden scenes cover the `golden` rows, and count what `GoldenAppTests` runs and skips:

```bash
mkdir -p .superpowers/logs
python - <<'EOF'
import json, sys
TW, TH = 585, 559
scenes = json.load(open("tests/goldens/app/scenes.json", encoding="utf-8"))
shapes = [a for s in scenes for a in s.get("actions", []) if a["op"] == "shape"]
erases = [(s, a) for s in scenes for a in s.get("actions", []) if a["op"] == "erase"]
def tool(t):
    return [a for a in shapes if a["tool"] == t]
def crosses(a):
    h = a["width"] / 2
    xs, ys = (a["from"][0], a["to"][0]), (a["from"][1], a["to"][1])
    return min(xs) - h < 0 or min(ys) - h < 0 or max(xs) + h > TW or max(ys) + h > TH
def zero_side(a):
    return (a["from"][0] == a["to"][0]) != (a["from"][1] == a["to"][1])
def target(s, a):
    return s["layers"][a["layer"]]
cover = {
    "draw-shape-line": bool(tool("line")),
    "draw-shape-rect": any(a.get("fill") for a in tool("rect")) and any(not a.get("fill") for a in tool("rect"))
        and any(a["to"][0] < a["from"][0] or a["to"][1] < a["from"][1] for a in tool("rect"))
        and any(zero_side(a) for a in tool("rect")),
    "draw-shape-ellipse": any(a.get("fill") for a in tool("ellipse")) and any(not a.get("fill") for a in tool("ellipse"))
        and any(zero_side(a) for a in tool("ellipse")),
    "draw-canvas-template-clip": any(crosses(a) for a in shapes),
    "eraser-math-inverse": all(any(p(target(s, a)) for s, a in erases) for p in (
        lambda l: l.get("scale", 1) != 1,
        lambda l: l.get("rotation", 0) % 90 != 0,
        lambda l: l.get("flipH", False),
        lambda l: l.get("flipV", False))),
}
for row, ok in cover.items():
    print(("covered  " if ok else "MISSING  ") + row)
pending = {"detail", "wordart", "project"}
skipped = [s["name"] for s in scenes if any(a["op"] in pending for a in s.get("actions", []))]
print(f"{len(scenes)} app scenes, {len(skipped)} skipped until M2b, {len(scenes) - len(skipped)} run")
sys.exit(0 if all(cover.values()) else 1)
EOF
dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenAppTests" > .superpowers/logs/app-golden.log 2>&1; tail -30 .superpowers/logs/app-golden.log
```

Expected:

- `covered` for each of `draw-shape-line`, `draw-shape-rect`, `draw-shape-ellipse`, `draw-canvas-template-clip` and `eraser-math-inverse`, and exit 0.
- `82 app scenes, 62 skipped until M2b, 20 run`.
- The `dotnet test` summary shows `Failed: 0, Passed: 25, Skipped: 62`: the 20 run scenes (every erase scene through the replay, none skipped), the 3 production two-point rows and the 2 facts. Each skip reason names `detail`, `wordart` or `project`.

Then apply the gate:

- **A `MISSING` row, a `unit` row whose named test is absent, or any erase scene failing `GoldenAppTests`:** take that row out of the Step 3 command and add it, with the reason, to the "left open" list in the Step 5 commit body. Report it to the controller. Do not write the missing test here; closing the gap belongs to the task that owns the module.
- **`eraser-math-inverse` closes on the replay.** The replay reproduces 4.1's stroking exactly, so a passing replay proves the inverse transform, the mask and the destination-out. The stroke-once divergence lives on `eraser-apply` (Step 2), not on this row.

These rows stay open deliberately. Copy the list into the Step 5 commit body:

| Rows | Owner | Why open |
| --- | --- | --- |
| `select-hit-test`, `select-rotate-drag`, `select-rotate-drag-2`, `select-resize-handles`, `select-rotate-handle-hit`, `select-resize-handle-hit`, `select-resize-drag`, `select-move-hit-and-drag`, `select-hit-test-rect-not-alpha` | Engine.Tools | Select-tool math is not part of the M2a Tools API (Stroke, RenderStroke, Autocrop, CommitDrawing). It lands with the select tool. |
| `draw-begin`, `draw-tool-start` | Engine.Tools | Mousedown setup and undo timing are caller gestures (M3). |
| `draw-pen-incremental-stroke` | Engine.Tools | The pen half has no golden (pen strokes are not frozen). The eraser half is measured in Task 6's commit and recorded on `eraser-apply`. It closes when the canvas wires the mouseup stroke. |
| `draw-pen-single-click` | Engine.Tools | The engine draws the dot, but "a press with no movement" is a canvas gesture (M3). |
| `draw-commit-layer` | Engine.Tools | Pushing on top, selecting the new layer and keeping the tool are App.Canvas behavior. |
| `draw-empty-no-layer` | Engine.Tools | Its behavior text keeps 4.1's empty undo step, which conflicts with the no-empty-steps fix. The undo half is the caller's. |
| `eraser-apply` | Engine.Tools | The no-selection alert, the selection rules and the hidden-layer case are App.Canvas behavior. Step 2 records the stroke-once divergence on it. |
| `canvas-draw-tools` | Engine.Tools | A whole-tool row: undo, cursor and alert are App behavior. |
| `history-snapshot-coupling`, `undo-snapshot-contents` | Engine.History | What goes into the snapshot (tool, zoom, brush and fill-editor state versus `Document`) and the `nextId` rebase are decided when the App wires History. |
| `undo-restore`, `undo-restore-ui-side-effects` | Engine.History | The synchronous restore is proven, but the control and region-button resync and the fill-style controls are App behavior. |
| `restore-mode-switch-side-effects`, `mode-switch-undo-and-callers` | Engine.History | The mode-switch command (clearing fills) is not an M2a engine API. |
| `undo-coverage-gaps`, `undo-empty-steps`, `fillstyle-undo-snapshot-timing`, `file-undo-snapshot-scope` | Engine.History | One step per gesture is the caller's job (M3/M4, per the History contract). |
| `render-live-preview`, `draw-preview-composite` | Engine.Render | The live drawing and eraser preview are view composition (M3). |
| `cursor-eraser`, `eraser-cursor`, `eraser-selection-semantics` | App.Canvas | UI rows. |

The M1-gap goldens close no new rows. They strengthen rows M1 already closed (`layer-draw-transform`, `render-background`, `layers-adjust-pipeline`).

- [ ] **Step 2: Record the eraser's stroke-once divergence, once**

`draw-pen-incremental-stroke` already approves stroking once for "pen and eraser". The eraser's measured consequence is recorded in exactly one place: the `eraser-apply` row's divergence sentence, which `ledger.py` renders into `docs/port/divergences.md`. `eraser-apply` stays open (Step 1).

```bash
python - <<'EOF'
import json
p = "docs/port/parity-ledger.json"
d = json.load(open(p, encoding="utf-8"))
row = next(r for r in d["rows"] if r["id"] == "eraser-apply")
assert row["disposition"] == "keep" and row["divergence"] == "", "eraser-apply was already edited"
row["disposition"] = "fix"
row["divergence"] = ("4.1: the eraser re-strokes its whole path on every mousemove, so the antialiased fringe of a drag "
                     "of three or more points is erased more the more mouse events the drag produced. v5: the eraser "
                     "path is stroked once (the eraser half of draw-pen-incremental-stroke); fully covered pixels and "
                     "two-point drags erase identically, and only that fringe keeps slightly more alpha (measured in "
                     "M2a Task 6; GoldenAppTests replays 4.1's stroking against the frozen goldens).")
open(p, "w", encoding="utf-8", newline="\n").write(json.dumps(d, indent=1, ensure_ascii=False) + "\n")
print("eraser-apply:", row["disposition"])
EOF
git diff --stat -- docs/port/parity-ledger.json
```

Expected: `eraser-apply: fix`, and `1 file changed, 2 insertions(+), 2 deletions(-)` (the `disposition` and `divergence` lines). A larger diff means `json.dumps` reformatted the file: stop, `git checkout -- docs/port/parity-ledger.json`, and report it.

- [ ] **Step 3: Close the proven rows**

Print the state first:

```bash
python -c "import json; d=json.load(open('docs/port/parity-ledger.json',encoding='utf-8')); ids='autocrop draw-shape-line draw-shape-rect draw-shape-ellipse draw-canvas-template-clip eraser-math-inverse tests-lib-erase layer-sharing-img undo-model tests-lib-history history-snapshot-memory history-redo-stack-uncapped undo-rapid-restore-race'.split(); s={r['id']:r['status'] for r in d['rows']}; print(len(d['rows']), 'rows;', sum(r['status']=='done' for r in d['rows']), 'done;', [i+'='+s[i] for i in ids])"
```

Expected: `407 rows; 42 done;` followed by the 13 ids, each `=open`.

Run the command below, minus any id Step 1 removed:

```bash
python tools/ledger/ledger.py done \
  autocrop draw-shape-line draw-shape-rect draw-shape-ellipse draw-canvas-template-clip \
  eraser-math-inverse tests-lib-erase layer-sharing-img \
  undo-model tests-lib-history history-snapshot-memory history-redo-stack-uncapped undo-rapid-restore-race
python tools/ledger/ledger.py check
grep -c '"status": "done"' docs/port/parity-ledger.json
grep -c '^| `eraser-apply` |' docs/port/divergences.md
```

Expected:

- `rendered docs/port/parity-ledger.md and docs/port/divergences.md`.
- `407 rows, 0 problem(s)`.
- `55`: M1's 42 plus the 13 ids closed. If Step 1 removed ids, 55 minus their number.
- `1`: the eraser divergence appears exactly once in `divergences.md`.

- [ ] **Step 4: Final verification**

Run, in order, from the repo root:

```bash
BASE=$(git merge-base HEAD feat/v5-m1-engine-render-core); git log -1 --format='base %h' "$BASE"
dotnet build src/RBX15.slnx -c Release -warnaserror
dotnet test src/RBX15.slnx -c Release
python -m unittest discover -s tools/ledger -p "test_*.py"
python tools/ledger/ledger.py check
node --test "tools/parity-ref/*.test.js"
npm test
git diff --stat "$BASE" -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json "tests/*.test.js" windows
git diff --name-status "$BASE" -- "tests/goldens/render/*.png"
git log --format=%h "$BASE"..HEAD -- "tests/goldens/app/*.png"
git status --short
```

Expected:

- `base f4b147e` (or the M1 tip M2a branched from).
- Build: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: Engine.Tests `Passed: 187, Failed: 0, Skipped: 62`, every skip naming `detail`, `wordart` or `project`. Parity.Tests `Passed: 23, Failed: 0`.
- Ledger unit tests: `Ran 8 tests`, `OK`. `ledger.py check` prints `407 rows, 0 problem(s)`.
- `node --test`: `# tests 13`, `# pass 13`, `# fail 0`. `npm test`: `# tests 31`, `# pass 31`, `# fail 0`.
- The Electron `git diff --stat` prints nothing: Electron is untouched.
- The render-goldens `git diff` prints exactly the four `A` lines from Task 4 Step 1. No M0/M1 golden is modified or deleted.
- The app-goldens `git log` prints exactly one hash: Task 3 Step 16's commit. More than one means an app golden changed after capture. Stop and show `git log --stat "$BASE"..HEAD -- tests/goldens/app` to the controller.
- `git status --short` lists exactly three lines, each `M` (modified, unstaged) for `docs/port/divergences.md`, `docs/port/parity-ledger.json` and `docs/port/parity-ledger.md`. Logs live in the git-ignored `.superpowers/logs/`, and `diff-heatmaps/` is ignored, so neither appears.

- [ ] **Step 5: Commit**

Write the body from Step 1's outcome. The list of rows left open is the fixed table above, plus any row Step 1 removed and its reason. Use a heredoc so the body survives intact, and add your own model's `Co-Authored-By` trailer as its last line (Global Constraints) before running it.

```bash
git add docs/port/parity-ledger.json docs/port/parity-ledger.md docs/port/divergences.md
git commit -F - <<'EOF'
chore(ledger): close the rows M2a proves (Tools, Eraser, History)

Closed by unit tests (Tasks 5-7) and the app goldens (GoldenAppTests):
autocrop, draw-shape-line, draw-shape-rect, draw-shape-ellipse,
draw-canvas-template-clip, eraser-math-inverse, tests-lib-erase,
layer-sharing-img, undo-model, tests-lib-history,
history-snapshot-memory, history-redo-stack-uncapped,
undo-rapid-restore-race.

eraser-apply records the eraser's stroke-once divergence (fix); the
row stays open for its App.Canvas halves.

Left open on purpose: select-tool math (not in the M2a Tools API);
mousedown, press and commit halves owned by App.Canvas (draw-begin,
draw-tool-start, draw-pen-single-click, draw-commit-layer,
draw-empty-no-layer, eraser-apply, canvas-draw-tools);
draw-pen-incremental-stroke (pen half, no golden); snapshot scope,
restore UI resync, mode-switch command and one-step-per-gesture rows
owned by the caller (M3/M4); live preview; eraser cursor rows.

The M1 gate runs 34 scenes (4 M1-gap goldens), all green. The app gate
runs 20 scenes and skips 62 by op name until M2b.
EOF
git log --oneline -1
```

Expected: one commit with the subject `chore(ledger): close the rows M2a proves (Tools, Eraser, History)`. If Step 1 removed any id, delete it from the "Closed" paragraph and add it, with its reason, to the "Left open" paragraph before committing. Do not push. The controller handles push, the PR and the dashboard after final review.
