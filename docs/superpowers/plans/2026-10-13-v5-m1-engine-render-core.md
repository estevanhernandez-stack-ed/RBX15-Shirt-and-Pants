# RBX15 v5 M1 (Engine render core) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the engine's render core, Template, Fills, Document, Adjust, Renderer and Export, so that the native engine's export of every one of the 30 frozen 4.1 scenes passes the golden rule. Add a headless export CLI.

**Architecture:** `RBX15.Engine` (`net10.0`, SkiaSharp only) gets plain static modules over immutable records:

- `Template`: regions, semantic slots, mask.
- `Fills`: the spike port.
- `Document` and `Layer`: records.
- `Images`: exact decode, plus a premultiplied copy for drawing.
- `Adjust`: per-stage CSS color filters and the RGB shift.
- `Renderer`: export composition.
- `ExportNaming`: file names.

`RBX15.Parity` gains `SceneLoader` (scene JSON to `Document`), `RepoRoot`, an image-to-bitmap helper and a `render` CLI verb. `RBX15.Engine.Tests` gets unit tests per module, plus a golden theory over `tests/goldens/render/scenes.json`.

**Tech Stack:** .NET 10 (`net10.0`), C# with nullable and warnings as errors, SkiaSharp `4.153.1`, xUnit 2.9.3, Python 3 stdlib (ledger tool).

**Spec:** [`docs/superpowers/specs/2026-10-11-native-v5-port-design.md`](../specs/2026-10-11-native-v5-port-design.md), sections 2 and 6. Ledger: [`docs/port/parity-ledger.json`](../../port/parity-ledger.json). Previous milestone: [M0 plan](2026-10-12-v5-m0-ground.md).

## Global Constraints

- **The engine never references WPF.** `RBX15.Engine` targets `net10.0` and depends only on SkiaSharp `4.153.1`.
- **No obsolete SkiaSharp APIs.** Warnings are errors. These were verified against 4.153.1 on 2026-10-13:
  - Build paths with `SKPathBuilder` (`MoveTo`, `LineTo`, `ArcTo(SKRect, float, float, bool)`, `Detach()`).
  - Draw images with `DrawImage(SKImage, SKRect, SKSamplingOptions, SKPaint)` or `DrawImage(SKImage, float, float, SKSamplingOptions, SKPaint)`.
  - `SKColorFilter.CreateColorMatrix` translate values are in **0..1** units (0.5 produces `0x80`).
  - If any other call is obsolete, use the replacement the warning names. Never suppress the warning.
- **The render order is a contract** (spec section 2): clear, background (a stretched image, or the color, times opacity), region fills, then visible layers bottom to top. Export always applies the mask, regardless of the Mask checkbox.
- **Layer sampling** is `new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)`, matching Canvas2D's default `imageSmoothingQuality = 'low'`.
- **Adjustments chain one color filter per stage**, in CSS order: brightness, contrast, saturate, hue-rotate. Chromium clamps after each stage.
- **RGB shift runs on exact un-premultiplied pixels and skips alpha 0.** This is the approved divergence `rgb-shift-compute`: more precise than 4.1 on low-alpha pixels.
- **Golden rule** (spec section 6): zero interior differences, and edge deltas of 3 or less. Goldens are never regenerated in this milestone. When a scene fails, fix the engine; the test and the golden stay as they are.
- **Region keys stay verbatim** (`torso_front`, `waist_front` and so on); saved projects store them.
- **The Electron app is untouched.**
- **Dashboard:** log forks with project `6KpHItSNg9NYHkJg9thn` and origin `Dunder · Claude Code`.

## Review Focus

- **Region fills naming a region the current mode doesn't have** (for example a `torso_front` fill in a Pants document after a mode switch or a hand-edited project). Expected: skipped silently, no exception, nothing painted. Pinned in Task 2 (`Fills_for_regions_the_mode_lacks_are_skipped`).
- **Background color strings that aren't hex** (`"transparent"`, `""`, garbage from a hand-edited project). Expected: `transparent` and `""` paint nothing. Anything else unparsable paints canvas-default black, because 4.1's `ctx.fillStyle = bgColor` ignores invalid values and keeps the default `#000000`. Pinned in Task 5 (`Background_color_edge_cases_match_canvas`).
- **A visible layer with opacity 0.** Expected: draws nothing, same as `globalAlpha = 0`. Pinned in Task 5 (`Hidden_and_zero_opacity_layers_draw_nothing`).
- **Fill scale outside the slider (0, 1, negative).** Expected: `max(2, scale || 16)`, so 0 and missing become 16, while 1 and negative values become 2. Pinned in Task 2 (`Effective_scale_follows_fills_js`).
- **Rotation beyond ±360 degrees** (projects store raw doubles). Expected: 725 degrees renders the same as 5 degrees. Pinned in Task 5 (`Rotation_wraps_naturally`).

---

## File structure

```text
src/RBX15.Engine/
  RBX15.Engine.csproj        + SkiaSharp 4.153.1
  Template.cs                TemplateMode, BodyPart, Face, Region, Template (regions, Slot, Mask)
  CssColor.cs                hexToRgb / mix / canvas fill parsing
  Fills.cs                   Fill record, PaintRegionFill, PaintRegions, groups, PRNG
  Images.cs                  Decode (exact unpremul), ForDrawing (premul copy, cached)
  Document.cs                Layer and Document records
  Adjust.cs                  HasAdjustments, FilterChain, ShiftRgb (cached per image)
  Renderer.cs                Export, ExportPng, RenderExport, DrawLayer
  ExportNaming.cs            PNG and project file names
src/RBX15.Engine.Tests/
  Px.cs                      test pixel helpers
  TemplateTests.cs  FillsTests.cs  DocumentTests.cs  AdjustTests.cs  RendererTests.cs
  GoldenRenderTests.cs       the 30-scene golden theory
src/RBX15.Parity/
  RepoRoot.cs  SceneLoader.cs  ImageDiff.cs (+ FromImage)  Program.cs (+ render)
src/RBX15.Parity.Tests/SceneLoaderTests.cs
tools/ledger/ledger.py       + `done <id...>`
```

---

### Task 1: Template (regions, slots, mask)

**Files:**

- Modify: `src/RBX15.Engine/RBX15.Engine.csproj` (add SkiaSharp)
- Create: `src/RBX15.Engine/Template.cs`, `src/RBX15.Engine.Tests/Px.cs`, `src/RBX15.Engine.Tests/TemplateTests.cs`

**Interfaces:**

- Consumes: `TemplateSize.Width/Height` (M0).
- Produces:
  - `enum TemplateMode { Shirt, Pants }`; `enum BodyPart { Body, RightLimb, LeftLimb }`; `enum Face { Up, Right, Front, Left, Back, Down }`
  - `readonly record struct Region(string Name, int X, int Y, int W, int H)` with `SKRect Rect`
  - `Template.Regions(TemplateMode) -> IReadOnlyList<Region>` in 4.1 key order
  - `Template.TryFind(TemplateMode, string name, out Region)`
  - `Template.Slot(TemplateMode, BodyPart, Face) -> Region`
  - `Template.Mask(TemplateMode) -> SKImage`: 585x559, opaque white inside regions, transparent elsewhere, cached
  - Test helpers `Px.At(SKImage, x, y) -> SKColor` (un-premultiplied), `Px.Render(w, h, Action<SKCanvas>) -> SKImage`, `Px.Solid(w, h, SKColor) -> SKImage`

- [ ] **Step 1: Branch, add SkiaSharp to the engine, write the test helpers**

Run: `git checkout main && git pull --ff-only && git checkout -b feat/v5-m1-engine-render-core` (skip if executing in a worktree already on this branch), then `dotnet add src/RBX15.Engine package SkiaSharp --version 4.153.1`

Create `src/RBX15.Engine.Tests/Px.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>Pixel helpers. Reads are un-premultiplied, like canvas getImageData.</summary>
static class Px
{
    public static SKColor At(SKImage image, int x, int y)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("ReadPixels failed");
        return bmp.GetPixel(x, y);
    }

    public static SKImage Render(int w, int h, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        draw(surface.Canvas);
        return surface.Snapshot();
    }

    public static SKImage Solid(int w, int h, SKColor c) => Render(w, h, canvas => canvas.Clear(c));

    public static void Near(SKColor expected, SKColor actual, int tolerance = 1)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance &&
                    Math.Abs(expected.Blue - actual.Blue) <= tolerance && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance,
                    $"expected {expected} ±{tolerance}, got {actual}");
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `src/RBX15.Engine.Tests/TemplateTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class TemplateTests
{
    static readonly string[] ShirtNames =
    {
        "torso_up", "torso_right", "torso_front", "torso_left", "torso_back", "torso_down",
        "rarm_up", "rarm_left", "rarm_back", "rarm_right", "rarm_front", "rarm_down",
        "larm_up", "larm_front", "larm_left", "larm_back", "larm_right", "larm_down",
    };

    static readonly string[] PantsNames =
    {
        "waist_up", "waist_right", "waist_front", "waist_left", "waist_back", "waist_down",
        "rleg_up", "rleg_left", "rleg_back", "rleg_right", "rleg_front", "rleg_down",
        "lleg_up", "lleg_front", "lleg_left", "lleg_back", "lleg_right", "lleg_down",
    };

    [Fact]
    public void Shirt_regions_match_the_4_1_table_in_key_order()
    {
        var r = Template.Regions(TemplateMode.Shirt);
        Assert.Equal(ShirtNames, r.Select(x => x.Name));
        Assert.Equal(new Region("torso_front", 231, 74, 128, 128), r[2]);
        Assert.Equal(new Region("rarm_left", 19, 355, 64, 128), r[7]);
        Assert.Equal(new Region("larm_down", 308, 485, 64, 64), r[17]);
    }

    [Fact]
    public void Pants_regions_have_shirt_geometry_with_pants_names()
    {
        var shirt = Template.Regions(TemplateMode.Shirt);
        var pants = Template.Regions(TemplateMode.Pants);
        Assert.Equal(PantsNames, pants.Select(x => x.Name));
        for (int i = 0; i < 18; i++)
            Assert.Equal((shirt[i].X, shirt[i].Y, shirt[i].W, shirt[i].H), (pants[i].X, pants[i].Y, pants[i].W, pants[i].H));
    }

    [Fact]
    public void Slots_resolve_per_mode()
    {
        Assert.Equal("torso_front", Template.Slot(TemplateMode.Shirt, BodyPart.Body, Face.Front).Name);
        Assert.Equal("waist_front", Template.Slot(TemplateMode.Pants, BodyPart.Body, Face.Front).Name);
        Assert.Equal("rarm_back", Template.Slot(TemplateMode.Shirt, BodyPart.RightLimb, Face.Back).Name);
        Assert.Equal("lleg_down", Template.Slot(TemplateMode.Pants, BodyPart.LeftLimb, Face.Down).Name);
    }

    [Fact]
    public void TryFind_misses_names_from_the_other_mode()
    {
        Assert.True(Template.TryFind(TemplateMode.Shirt, "torso_front", out var r));
        Assert.Equal(231, r.X);
        Assert.False(Template.TryFind(TemplateMode.Pants, "torso_front", out _));
    }

    [Fact]
    public void Mask_is_opaque_inside_regions_and_clear_in_gutters()
    {
        var mask = Template.Mask(TemplateMode.Shirt);
        Assert.Equal((585, 559), (mask.Width, mask.Height));
        Assert.Equal(255, Px.At(mask, 295, 138).Alpha);   // torso_front center
        Assert.Equal(0, Px.At(mask, 230, 138).Alpha);     // gutter between torso_right and torso_front
        Assert.Equal(0, Px.At(mask, 0, 0).Alpha);
    }
}
```

- [ ] **Step 3: Run the tests and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors `CS0103`/`CS0246` for `Template`, `Region`, `TemplateMode`.

- [ ] **Step 4: Implement**

Create `src/RBX15.Engine/Template.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

public enum TemplateMode { Shirt, Pants }
public enum BodyPart { Body, RightLimb, LeftLimb }
public enum Face { Up, Right, Front, Left, Back, Down }

public readonly record struct Region(string Name, int X, int Y, int W, int H)
{
    public SKRect Rect => SKRect.Create(X, Y, W, H);
}

/// <summary>
/// The R15 classic template (editor.js:20-60). Shirt and Pants share geometry and differ only in
/// key names, which saved projects store verbatim. Semantic slots resolve a body part and face to
/// the right key per mode. 4.1 hardcoded torso_*/rarm_* keys, so Pants mode threw
/// (ledger mode-pants-crash-hardcoded-keys).
/// </summary>
public static class Template
{
    static readonly (BodyPart Part, Face Face, int X, int Y, int W, int H)[] Layout =
    {
        (BodyPart.Body, Face.Up, 231, 8, 128, 64),
        (BodyPart.Body, Face.Right, 165, 74, 64, 128),
        (BodyPart.Body, Face.Front, 231, 74, 128, 128),
        (BodyPart.Body, Face.Left, 361, 74, 64, 128),
        (BodyPart.Body, Face.Back, 427, 74, 128, 128),
        (BodyPart.Body, Face.Down, 231, 204, 128, 64),
        (BodyPart.RightLimb, Face.Up, 217, 289, 64, 64),
        (BodyPart.RightLimb, Face.Left, 19, 355, 64, 128),
        (BodyPart.RightLimb, Face.Back, 85, 355, 64, 128),
        (BodyPart.RightLimb, Face.Right, 151, 355, 64, 128),
        (BodyPart.RightLimb, Face.Front, 217, 355, 64, 128),
        (BodyPart.RightLimb, Face.Down, 217, 485, 64, 64),
        (BodyPart.LeftLimb, Face.Up, 308, 289, 64, 64),
        (BodyPart.LeftLimb, Face.Front, 308, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Left, 374, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Back, 440, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Right, 506, 355, 64, 128),
        (BodyPart.LeftLimb, Face.Down, 308, 485, 64, 64),
    };

    static string Prefix(TemplateMode mode, BodyPart part) => (mode, part) switch
    {
        (TemplateMode.Shirt, BodyPart.Body) => "torso",
        (TemplateMode.Shirt, BodyPart.RightLimb) => "rarm",
        (TemplateMode.Shirt, BodyPart.LeftLimb) => "larm",
        (TemplateMode.Pants, BodyPart.Body) => "waist",
        (TemplateMode.Pants, BodyPart.RightLimb) => "rleg",
        _ => "lleg",
    };

    static Region[] Build(TemplateMode mode) =>
        Layout.Select(l => new Region($"{Prefix(mode, l.Part)}_{l.Face.ToString().ToLowerInvariant()}", l.X, l.Y, l.W, l.H))
              .ToArray();

    static readonly Region[] ShirtRegions = Build(TemplateMode.Shirt);
    static readonly Region[] PantsRegions = Build(TemplateMode.Pants);
    static readonly Lazy<SKImage> ShirtMask = new(() => BuildMask(ShirtRegions));
    static readonly Lazy<SKImage> PantsMask = new(() => BuildMask(PantsRegions));

    public static IReadOnlyList<Region> Regions(TemplateMode mode) => mode == TemplateMode.Shirt ? ShirtRegions : PantsRegions;

    public static bool TryFind(TemplateMode mode, string name, out Region region)
    {
        foreach (var r in Regions(mode))
            if (r.Name == name) { region = r; return true; }
        region = default;
        return false;
    }

    public static Region Slot(TemplateMode mode, BodyPart part, Face face)
    {
        int i = Array.FindIndex(Layout, l => l.Part == part && l.Face == face);
        return Regions(mode)[i];
    }

    /// <summary>buildMask (editor.js:116-126): white integer rects on transparent; export clips with destination-in.</summary>
    public static SKImage Mask(TemplateMode mode) => mode == TemplateMode.Shirt ? ShirtMask.Value : PantsMask.Value;

    static SKImage BuildMask(IEnumerable<Region> regions)
    {
        using var surface = SKSurface.Create(new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var white = new SKPaint { Color = SKColors.White };
        foreach (var r in regions) surface.Canvas.DrawRect(r.Rect, white);
        return surface.Snapshot();
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 6` (5 new + `TemplateSizeTests`); Parity.Tests 9 passed.

- [ ] **Step 6: Commit**

```bash
git add src
git commit -m "feat(engine): Template regions, semantic slots and mask"
```

---

### Task 2: CssColor and Fills

**Files:**

- Create: `src/RBX15.Engine/CssColor.cs`, `src/RBX15.Engine/Fills.cs`, `src/RBX15.Engine.Tests/FillsTests.cs`

**Interfaces:**

- Consumes: `Region`, `Template.Regions/TryFind`, `TemplateMode` (Task 1); `Px` (Task 1).
- Produces:
  - `CssColor.HexToRgb(string?) -> (int R, int G, int B)`
  - `CssColor.Mix(string a, string b, double t) -> string` (lowercase `#rrggbb`)
  - `CssColor.ToSk(string hex) -> SKColor` (opaque)
  - `CssColor.TryParseCanvasFill(string?, out SKColor)`: true for `#rgb`, `#rrggbb` or `transparent`
  - `sealed record Fill(string Type, string? C1 = null, string? C2 = null, double? Angle = null, double? Scale = null)` with `Fill.Solid(string)`, `string Key`, `static string JsNumber(double)`
  - `Fills.EffectiveScale(Fill) -> double`
  - `Fills.PaintRegionFill(SKCanvas, SKRect region, Fill, SKRect? extent = null)`
  - `Fills.PaintRegions(SKCanvas, TemplateMode, IReadOnlyList<(string Region, Fill Fill)>)`
  - `Fills.ConnectedRegionGroups(IReadOnlyList<SKRect>, float tolerance = 4) -> List<List<SKRect>>`
  - `Fills.Seeded(uint) -> Func<double>`, `Fills.SeedFrom(string) -> uint`

- [ ] **Step 1: Write the failing tests**

Port of `tests/fills.test.js`, plus the PRNG reference values computed from shipped `lib/fills.js` on 2026-10-13. Create `src/RBX15.Engine.Tests/FillsTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class FillsTests
{
    static readonly SKRect R40 = SKRect.Create(0, 0, 40, 40);
    static SKImage Paint(Fill f, SKRect? region = null) => Px.Render(40, 40, c => Fills.PaintRegionFill(c, region ?? R40, f));
    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255), Green = new(0, 255, 0);

    [Fact]
    public void Legacy_string_fill_paints_solid() => Assert.Equal(Red, Px.At(Paint(Fill.Solid("#ff0000")), 20, 20));

    [Fact]
    public void Solid_fills_the_whole_region()
    {
        var img = Paint(new Fill("solid", "#00ff00"));
        Assert.Equal(Green, Px.At(img, 5, 5));
        Assert.Equal(Green, Px.At(img, 35, 35));
    }

    [Fact]
    public void Linear_gradient_runs_c1_to_c2()
    {
        var img = Paint(new Fill("linear", "#ff0000", "#0000ff", Angle: 0));
        var left = Px.At(img, 1, 20);
        var right = Px.At(img, 39, 20);
        Assert.True(left.Red > 200 && left.Blue < 60, $"left {left}");
        Assert.True(right.Blue > 200 && right.Red < 60, $"right {right}");
    }

    [Fact]
    public void Stripes_alternate_on_the_global_lattice()
    {
        var img = Paint(new Fill("stripes", "#ff0000", "#0000ff", Angle: 0, Scale: 8));
        Assert.Equal(Blue, Px.At(img, 2, 20));
        Assert.Equal(Red, Px.At(img, 11, 20));
    }

    [Fact]
    public void Patterns_are_continuous_across_adjacent_regions()
    {
        var f = new Fill("dots", "#ff0000", "#0000ff", Scale: 16);
        var img = Px.Render(80, 40, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 40), f);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 40), f);
        });
        Assert.Equal(Blue, Px.At(img, 56, 8));
        Assert.Equal(Red, Px.At(img, 48, 8));
    }

    [Fact]
    public void Checker_alternates() =>
        Assert.NotEqual(Px.At(Paint(new Fill("checker", "#ff0000", "#0000ff", Scale: 10)), 5, 5),
                        Px.At(Paint(new Fill("checker", "#ff0000", "#0000ff", Scale: 10)), 15, 5));

    [Fact]
    public void Dots_put_c2_at_centers_and_c1_in_gaps()
    {
        var img = Paint(new Fill("dots", "#ff0000", "#0000ff", Scale: 16));
        Assert.Equal(Blue, Px.At(img, 8, 8));
        Assert.Equal(Red, Px.At(img, 0, 0));
    }

    [Fact]
    public void Camo_is_deterministic()
    {
        var f = new Fill("camo", "#33421a", "#7a8a4a", Scale: 14);
        var a = Paint(f);
        var b = Paint(f);
        foreach (var (x, y) in new[] { (5, 5), (20, 20), (33, 12), (10, 30) })
            Assert.Equal(Px.At(a, x, y), Px.At(b, x, y));
    }

    [Fact]
    public void Fill_is_clipped_to_the_region()
    {
        var img = Paint(new Fill("solid", "#00ff00"), SKRect.Create(10, 10, 10, 10));
        Assert.Equal(Green, Px.At(img, 15, 15));
        Assert.Equal(0, Px.At(img, 2, 2).Alpha);
    }

    [Fact]
    public void Linear_gradient_spans_a_shared_extent()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Angle: 0);
        var extent = SKRect.Create(0, 0, 80, 20);
        var img = Px.Render(80, 20, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 20), f, extent);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 20), f, extent);
        });
        var a = Px.At(img, 38, 10);
        var b = Px.At(img, 42, 10);
        Assert.True(Math.Abs(a.Red - b.Red) < 25 && Math.Abs(a.Blue - b.Blue) < 25, $"{a} vs {b}");
    }

    [Fact]
    public void Without_an_extent_each_region_restarts()
    {
        var f = new Fill("linear", "#ff0000", "#0000ff", Angle: 0);
        var img = Px.Render(80, 20, c =>
        {
            Fills.PaintRegionFill(c, SKRect.Create(0, 0, 40, 20), f);
            Fills.PaintRegionFill(c, SKRect.Create(40, 0, 40, 20), f);
        });
        Assert.True(Math.Abs(Px.At(img, 38, 10).Blue - Px.At(img, 42, 10).Blue) > 150);
    }

    [Fact]
    public void Fill_key_matches_fills_js()
    {
        Assert.Equal("solid|#aabbcc||0|0", Fill.Solid("#aabbcc").Key);
        Assert.Equal("dots|#ff0000|#0000ff|0|16", new Fill("dots", "#ff0000", "#0000ff", Scale: 16).Key);
        Assert.Equal(new Fill("linear", "#000000", "#ffffff", 45).Key, new Fill("linear", "#000000", "#ffffff", 45).Key);
        Assert.NotEqual(new Fill("linear", "#000000", "#ffffff", 45).Key, new Fill("linear", "#000000", "#ffffff", 90).Key);
    }

    [Fact]
    public void Prng_matches_fills_js()
    {
        Assert.Equal(1865758740u, Fills.SeedFrom("#556b2f#c2b28016_-1_3"));
        var r = Fills.Seeded(12345);
        Assert.Equal(0.97972826776094735, r());
        Assert.Equal(0.30675226449966431, r());
        Assert.Equal(0.48420542152598500, r());
    }

    [Fact]
    public void Mix_matches_fills_js()
    {
        Assert.Equal("#800080", CssColor.Mix("#ff0000", "#0000ff", 0.5));
        Assert.Equal("#37461f", CssColor.Mix("#556b2f", "#000000", 0.35));
        Assert.Equal((0xaa, 0xbb, 0xcc), CssColor.HexToRgb("#abc"));
    }

    [Fact]
    public void Effective_scale_follows_fills_js()
    {
        Assert.Equal(16, Fills.EffectiveScale(new Fill("dots")));
        Assert.Equal(16, Fills.EffectiveScale(new Fill("dots", Scale: 0)));
        Assert.Equal(2, Fills.EffectiveScale(new Fill("dots", Scale: 1)));
        Assert.Equal(2, Fills.EffectiveScale(new Fill("dots", Scale: -5)));
        Assert.Equal(24, Fills.EffectiveScale(new Fill("dots", Scale: 24)));
    }

    [Fact]
    public void Shirt_regions_group_into_torso_and_two_arms()
    {
        var groups = Fills.ConnectedRegionGroups(Template.Regions(TemplateMode.Shirt).Select(r => r.Rect).ToList());
        Assert.Equal(new[] { 6, 6, 6 }, groups.Select(g => g.Count).OrderBy(n => n));
    }

    [Fact]
    public void Fills_for_regions_the_mode_lacks_are_skipped()
    {
        var img = Px.Render(585, 559, c =>
            Fills.PaintRegions(c, TemplateMode.Pants, new List<(string, Fill)> { ("torso_front", Fill.Solid("#ff0000")) }));
        Assert.Equal(0, Px.At(img, 295, 138).Alpha);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors for `Fill`, `Fills`, `CssColor`.

- [ ] **Step 3: Implement CssColor**

Create `src/RBX15.Engine/CssColor.cs`:

```csharp
using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>Color handling from lib/fills.js and the canvas fillStyle rules 4.1 relies on.</summary>
public static class CssColor
{
    /// <summary>hexToRgb: #rgb or #rrggbb; unparsable reads as black (JS parseInt -> NaN -> 0).</summary>
    public static (int R, int G, int B) HexToRgb(string? hex)
    {
        var h = (hex ?? "#000000").Replace("#", "");
        if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
        if (!int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int n)) n = 0;
        return ((n >> 16) & 255, (n >> 8) & 255, n & 255);
    }

    /// <summary>JS Math.round rounds .5 up; values here are non-negative, so floor(v + 0.5).</summary>
    static string RgbToHex(double r, double g, double b)
    {
        static string To(double v) => ((int)Math.Max(0, Math.Min(255, Math.Floor(v + 0.5)))).ToString("x2", CultureInfo.InvariantCulture);
        return "#" + To(r) + To(g) + To(b);
    }

    public static string Mix(string a, string b, double t)
    {
        var x = HexToRgb(a);
        var y = HexToRgb(b);
        return RgbToHex(x.R + (y.R - x.R) * t, x.G + (y.G - x.G) * t, x.B + (y.B - x.B) * t);
    }

    public static SKColor ToSk(string hex)
    {
        var (r, g, b) = HexToRgb(hex);
        return new SKColor((byte)r, (byte)g, (byte)b);
    }

    /// <summary>The subset of CSS colors 4.1 ever assigns to fillStyle: #rgb, #rrggbb, transparent.</summary>
    public static bool TryParseCanvasFill(string? s, out SKColor color)
    {
        color = SKColors.Transparent;
        if (s is null) return false;
        if (s == "transparent") return true;
        if (s.Length is not (4 or 7) || s[0] != '#') return false;
        foreach (char ch in s.AsSpan(1))
            if (!Uri.IsHexDigit(ch)) return false;
        color = ToSk(s);
        return true;
    }
}
```

- [ ] **Step 4: Implement Fills**

Create `src/RBX15.Engine/Fills.cs`. The spike port, with `SKPathBuilder` in place of the obsolete `SKPath` mutators:

```csharp
using System.Globalization;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>A region fill (lib/fills.js descriptor). Legacy v4.0 string fills are Fill.Solid.</summary>
public sealed record Fill(string Type, string? C1 = null, string? C2 = null, double? Angle = null, double? Scale = null)
{
    public static Fill Solid(string color) => new("solid", color);

    /// <summary>fillKey: [type, c1||'', c2||'', angle||0, scale||0].join('|').</summary>
    public string Key => string.Join('|', Type, C1 ?? "", C2 ?? "", JsNumber(Angle ?? 0), JsNumber(Scale ?? 0));

    /// <summary>JS Number to string for the values fills carry (integers and plain decimals).</summary>
    public static string JsNumber(double v) =>
        double.IsNaN(v) ? "0" :
        v == Math.Floor(v) && Math.Abs(v) < 1e15 ? ((long)v).ToString(CultureInfo.InvariantCulture)
                                                 : v.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>Port of lib/fills.js paintRegionFill and editor.js paintRegions (proved pixel-exact by the 2026-10-11 spike).</summary>
public static class Fills
{
    public static double EffectiveScale(Fill f) => Math.Max(2, f.Scale is { } s && s != 0 && !double.IsNaN(s) ? s : 16);

    /// <summary>mulberry32 in uint arithmetic (JS Math.imul, >>> and ^ reduce to the low 32 bits).</summary>
    public static Func<double> Seeded(uint seed)
    {
        uint s = seed;
        return () =>
        {
            unchecked
            {
                s += 0x6D2B79F5;
                uint t = (s ^ (s >> 15)) * (1 | s);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        };
    }

    /// <summary>FNV-1a over UTF-16 code units, as JS charCodeAt sees them.</summary>
    public static uint SeedFrom(string str)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in str) { h ^= ch; h *= 16777619; }
            return h;
        }
    }

    static SKPaint FillPaint(SKColor c) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Fill };

    // Canvas arc(cx, cy, r, 0, 2PI) appended to the current subpath: a line to the arc start, then the
    // circle as two 180-degree arcs. Chromium-family Skia canvases emit a full arc this way.
    static void FullArc(SKPathBuilder b, float cx, float cy, float r)
    {
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        b.LineTo(cx + r, cy);
        b.ArcTo(oval, 0, 180, false);
        b.ArcTo(oval, 180, 180, false);
    }

    public static void PaintRegionFill(SKCanvas ctx, SKRect region, Fill f, SKRect? extent = null)
    {
        var ext = extent ?? region;
        string c1 = f.C1 ?? "#000000";
        string c2 = f.C2 ?? "#ffffff";
        double scaleD = EffectiveScale(f);
        float scale = (float)scaleD;
        double angle = (f.Angle ?? 0) * Math.PI / 180;
        float x = region.Left, y = region.Top, w = region.Width, h = region.Height;

        ctx.Save();
        ctx.ClipRect(region, SKClipOperation.Intersect, true);
        switch (f.Type)
        {
            case "linear":
            {
                float cx = ext.Left + ext.Width / 2, cy = ext.Top + ext.Height / 2;
                float dx = (float)(Math.Cos(angle) * ext.Width / 2), dy = (float)(Math.Sin(angle) * ext.Height / 2);
                using var sh = SKShader.CreateLinearGradient(new SKPoint(cx - dx, cy - dy), new SKPoint(cx + dx, cy + dy),
                    new[] { CssColor.ToSk(c1), CssColor.ToSk(c2) }, new float[] { 0, 1 }, SKShaderTileMode.Clamp);
                using var p = new SKPaint { Shader = sh, IsAntialias = true };
                ctx.DrawRect(region, p);
                break;
            }
            case "radial":
            {
                float cx = ext.Left + ext.Width / 2, cy = ext.Top + ext.Height / 2;
                using var sh = SKShader.CreateRadialGradient(new SKPoint(cx, cy), Math.Max(ext.Width, ext.Height) / 2,
                    new[] { CssColor.ToSk(c1), CssColor.ToSk(c2) }, new float[] { 0, 1 }, SKShaderTileMode.Clamp);
                using var p = new SKPaint { Shader = sh, IsAntialias = true };
                ctx.DrawRect(region, p);
                break;
            }
            case "stripes":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                ctx.RotateRadians((float)angle);
                double period = scaleD * 2;
                double big = Math.Ceiling(1000 / period) * period;
                for (double i = -big; i < big; i += period)
                    ctx.DrawRect(SKRect.Create((float)i, (float)-big, scale, (float)(big * 2)), p2);
                break;
            }
            case "checker":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                int gx0 = (int)Math.Floor(x / scaleD), gx1 = (int)Math.Ceiling((x + w) / scaleD);
                int gy0 = (int)Math.Floor(y / scaleD), gy1 = (int)Math.Ceiling((y + h) / scaleD);
                for (int gy = gy0; gy < gy1; gy++)
                    for (int gx = gx0; gx < gx1; gx++)
                        if (((gx + gy) & 1) == 0) ctx.DrawRect(SKRect.Create(gx * scale, gy * scale, scale, scale), p2);
                break;
            }
            case "dots":
            {
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                using var p2 = FillPaint(CssColor.ToSk(c2));
                float r = scale * 0.3f;
                int gx0 = (int)Math.Floor(x / scaleD), gx1 = (int)Math.Ceiling((x + w) / scaleD);
                int gy0 = (int)Math.Floor(y / scaleD), gy1 = (int)Math.Ceiling((y + h) / scaleD);
                for (int gy = gy0; gy <= gy1; gy++)
                    for (int gx = gx0; gx <= gx1; gx++)
                    {
                        float cx = gx * scale + scale / 2, cy = gy * scale + scale / 2;
                        using var pb = new SKPathBuilder();
                        pb.MoveTo(cx + r, cy);
                        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
                        pb.ArcTo(oval, 0, 180, false);
                        pb.ArcTo(oval, 180, 180, false);
                        using var path = pb.Detach();
                        ctx.DrawPath(path, p2);
                    }
                break;
            }
            case "camo":
            {
                string[] tones = { c1, c2, CssColor.Mix(c1, c2, 0.5), CssColor.Mix(c1, "#000000", 0.35) };
                string current = tones[0];
                using (var b = FillPaint(CssColor.ToSk(current))) ctx.DrawRect(region, b);
                double cell = scaleD * 2;
                int gx0 = (int)Math.Floor(x / cell) - 1, gx1 = (int)Math.Ceiling((x + w) / cell) + 1;
                int gy0 = (int)Math.Floor(y / cell) - 1, gy1 = (int)Math.Ceiling((y + h) / cell) + 1;
                for (int gy = gy0; gy < gy1; gy++)
                    for (int gx = gx0; gx < gx1; gx++)
                    {
                        var rand = Seeded(SeedFrom(c1 + c2 + Fill.JsNumber(scaleD) + "_" + gx + "_" + gy));
                        for (int bi = 0; bi < 2; bi++)
                        {
                            // JS: tones[1 + ((gx+gy+b) % 3)]. JS % keeps the sign; a negative index is
                            // undefined, and assigning undefined to fillStyle keeps the previous color.
                            // Carried verbatim; it cannot change pixels in the UI's 4..48 scale range.
                            int idx = 1 + ((gx + gy + bi) % 3);
                            if (idx >= 0 && idx < tones.Length) current = tones[idx];
                            double bx = (gx + rand()) * cell, by = (gy + rand()) * cell;
                            double br = scaleD * (0.6 + rand() * 1.1);
                            using var pb = new SKPathBuilder();
                            for (int k = 0; k < 4; k++)
                            {
                                double ox = (rand() - 0.5) * br, oy = (rand() - 0.5) * br;
                                pb.MoveTo((float)(bx + ox + br), (float)(by + oy));
                                FullArc(pb, (float)(bx + ox), (float)(by + oy), (float)(br * (0.6 + rand() * 0.4)));
                            }
                            using var path = pb.Detach();
                            using var p = FillPaint(CssColor.ToSk(current));
                            ctx.DrawPath(path, p);
                        }
                    }
                break;
            }
            default:
                using (var b = FillPaint(CssColor.ToSk(c1))) ctx.DrawRect(region, b);
                break;
        }
        ctx.Restore();
    }

    static SKRect Union(IEnumerable<SKRect> rects)
    {
        float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
        foreach (var r in rects)
        {
            x0 = Math.Min(x0, r.Left); y0 = Math.Min(y0, r.Top);
            x1 = Math.Max(x1, r.Right); y1 = Math.Max(y1, r.Bottom);
        }
        return new SKRect(x0, y0, x1, y1);
    }

    /// <summary>connectedRegionGroups (editor.js): rects whose boxes, inflated by tolerance, touch.</summary>
    public static List<List<SKRect>> ConnectedRegionGroups(IReadOnlyList<SKRect> rects, float tolerance = 4)
    {
        bool Touch(SKRect a, SKRect b) =>
            a.Left - tolerance < b.Right && b.Left - tolerance < a.Right && a.Top - tolerance < b.Bottom && b.Top - tolerance < a.Bottom;
        var groups = new List<List<SKRect>>();
        var seen = new HashSet<int>();
        for (int i = 0; i < rects.Count; i++)
        {
            if (!seen.Add(i)) continue;
            var comp = new List<SKRect>();
            var stack = new Stack<int>();
            stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop();
                comp.Add(rects[j]);
                for (int k = 0; k < rects.Count; k++)
                    if (!seen.Contains(k) && Touch(rects[j], rects[k])) { seen.Add(k); stack.Push(k); }
            }
            groups.Add(comp);
        }
        return groups;
    }

    /// <summary>paintRegions (editor.js): group same-key fills, split into connected groups, gradients span each group.</summary>
    public static void PaintRegions(SKCanvas canvas, TemplateMode mode, IReadOnlyList<(string Region, Fill Fill)> fills)
    {
        var byKey = new List<(string Key, Fill Fill, List<SKRect> Rects)>();
        foreach (var (name, fill) in fills)
        {
            if (!Template.TryFind(mode, name, out var region)) continue;
            int at = byKey.FindIndex(e => e.Key == fill.Key);
            if (at < 0) byKey.Add((fill.Key, fill, new List<SKRect> { region.Rect }));
            else byKey[at].Rects.Add(region.Rect);
        }
        foreach (var (_, fill, rects) in byKey)
            foreach (var group in ConnectedRegionGroups(rects))
            {
                var extent = Union(group);
                foreach (var r in group) PaintRegionFill(canvas, r, fill, extent);
            }
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 23` (6 + 17 new).

- [ ] **Step 6: Commit**

```bash
git add src
git commit -m "feat(engine): Fills port with SKPathBuilder, CssColor helpers"
```

---

### Task 3: Images, Layer and Document

**Files:**

- Create: `src/RBX15.Engine/Images.cs`, `src/RBX15.Engine/Document.cs`, `src/RBX15.Engine.Tests/DocumentTests.cs`

**Interfaces:**

- Consumes: `TemplateMode`, `Fill` (Tasks 1-2).
- Produces:
  - `Images.Decode(byte[] encoded) -> SKImage`: raster, `Rgba8888`, `Unpremul`, exact file pixels; throws `InvalidDataException`
  - `Images.ForDrawing(SKImage) -> SKImage`: a premultiplied raster copy, cached per image
  - `sealed record Layer`: `required int Id`, `required SKImage Image`, `Name = "Layer"`, `Visible = true`, `Opacity = 1`, `X`, `Y`, `Scale = 1`, `Rotation`, `FlipH`, `FlipV`, `Brightness = 100`, `Contrast = 100`, `Saturate = 100`, `HueRotate`, `RShift`, `GShift`, `BShift` (numeric properties are `double`, shifts are `int`)
  - `sealed record Document`: `Mode = Shirt`, `BgColor = "#192e44"`, `BgOpacity = 1`, `SKImage? BgImage`, `ImmutableList<(string Region, Fill Fill)> RegionFills`, `ImmutableList<Layer> Layers`, `ShowMask = true`, `ShowGrid = false`

- [ ] **Step 1: Write the failing tests**

Create `src/RBX15.Engine.Tests/DocumentTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class DocumentTests
{
    static byte[] Png(SKColor c, int w = 1, int h = 1)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) bmp.SetPixel(x, y, c);
        using var img = SKImage.FromPixelCopy(bmp.PeekPixels());
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void Document_defaults_match_the_4_1_editor_state()
    {
        var d = new Document();
        Assert.Equal(TemplateMode.Shirt, d.Mode);
        Assert.Equal("#192e44", d.BgColor);
        Assert.Equal(1, d.BgOpacity);
        Assert.Null(d.BgImage);
        Assert.Empty(d.RegionFills);
        Assert.Empty(d.Layers);
        Assert.True(d.ShowMask);
        Assert.False(d.ShowGrid);
    }

    [Fact]
    public void Layer_defaults_match_makeLayer()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Equal("Layer", l.Name);
        Assert.True(l.Visible);
        Assert.Equal((1.0, 0.0, 0.0, 1.0, 0.0), (l.Opacity, l.X, l.Y, l.Scale, l.Rotation));
        Assert.False(l.FlipH || l.FlipV);
        Assert.Equal((100.0, 100.0, 100.0, 0.0), (l.Brightness, l.Contrast, l.Saturate, l.HueRotate));
        Assert.Equal((0, 0, 0), (l.RShift, l.GShift, l.BShift));
    }

    [Fact]
    public void Decode_keeps_low_alpha_pixels_exact()
    {
        var img = Images.Decode(Png(new SKColor(200, 100, 50, 3)));
        Assert.Equal(SKAlphaType.Unpremul, img.AlphaType);
        Assert.Equal(new SKColor(200, 100, 50, 3), Px.At(img, 0, 0));
    }

    [Fact]
    public void Decode_rejects_non_images() =>
        Assert.Throws<InvalidDataException>(() => Images.Decode("not an image"u8.ToArray()));

    [Fact]
    public void ForDrawing_is_a_cached_premultiplied_copy()
    {
        var img = Images.Decode(Png(new SKColor(10, 20, 30, 128), 3, 2));
        var a = Images.ForDrawing(img);
        Assert.Equal(SKAlphaType.Premul, a.AlphaType);
        Assert.Equal((3, 2), (a.Width, a.Height));
        Assert.Same(a, Images.ForDrawing(img));
    }

    [Fact]
    public void With_expressions_share_the_bitmap()
    {
        var l = new Layer { Id = 1, Image = Images.Decode(Png(SKColors.Red)) };
        Assert.Same(l.Image, (l with { Id = 2, Name = "copy" }).Image);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors for `Document`, `Layer`, `Images`.

- [ ] **Step 3: Implement**

Create `src/RBX15.Engine/Images.cs`:

```csharp
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer images are held exactly as the file says (un-premultiplied), so RGB shift is exact
/// (ledger rgb-shift-compute). Drawing goes through a premultiplied copy, because Chromium decodes
/// img elements to premultiplied pixels and canvas sampling filters those. Same input, same edges.
/// </summary>
public static class Images
{
    static readonly ConditionalWeakTable<SKImage, SKImage> Premultiplied = new();

    public static SKImage Decode(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("not a supported image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException($"image decode failed: {result}");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    public static SKImage ForDrawing(SKImage image) =>
        image.AlphaType == SKAlphaType.Premul ? image : Premultiplied.GetValue(image, ToPremul);

    static SKImage ToPremul(SKImage source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        if (!source.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("premultiply failed");
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }
}
```

Create `src/RBX15.Engine/Document.cs`:

```csharp
using System.Collections.Immutable;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>One layer (makeLayer, editor.js:134-142). Images are shared, immutable decoded bitmaps.</summary>
public sealed record Layer
{
    public required int Id { get; init; }
    public required SKImage Image { get; init; }
    public string Name { get; init; } = "Layer";
    public bool Visible { get; init; } = true;
    public double Opacity { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Scale { get; init; } = 1;
    public double Rotation { get; init; }
    public bool FlipH { get; init; }
    public bool FlipV { get; init; }
    public double Brightness { get; init; } = 100;
    public double Contrast { get; init; } = 100;
    public double Saturate { get; init; } = 100;
    public double HueRotate { get; init; }
    public int RShift { get; init; }
    public int GShift { get; init; }
    public int BShift { get; init; }
}

/// <summary>The whole editable document (editor.js:76-107 defaults). Immutable; every edit is a new record.</summary>
public sealed record Document
{
    public TemplateMode Mode { get; init; } = TemplateMode.Shirt;
    public string BgColor { get; init; } = "#192e44";
    public double BgOpacity { get; init; } = 1;
    public SKImage? BgImage { get; init; }
    public ImmutableList<(string Region, Fill Fill)> RegionFills { get; init; } = ImmutableList<(string, Fill)>.Empty;
    public ImmutableList<Layer> Layers { get; init; } = ImmutableList<Layer>.Empty;
    public bool ShowMask { get; init; } = true;
    public bool ShowGrid { get; init; }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 29`.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat(engine): exact image decode, Layer and Document records"
```

---

### Task 4: Adjust (CSS filter chain, RGB shift)

**Files:**

- Create: `src/RBX15.Engine/Adjust.cs`, `src/RBX15.Engine.Tests/AdjustTests.cs`

**Interfaces:**

- Consumes: `Layer` (Task 3); `Px` (Task 1).
- Produces:
  - `Adjust.HasAdjustments(Layer) -> bool`: true when any of brightness, contrast, saturate, hue or the RGB shifts is off default (`has-adjustments`)
  - `Adjust.FilterChain(double brightnessPct, double contrastPct, double saturatePct, double hueDeg) -> SKColorFilter`
  - `Adjust.ShiftRgb(SKImage source, int r, int g, int b) -> SKImage`: un-premultiplied raster, alpha-0 pixels untouched, cached per source image

- [ ] **Step 1: Write the failing tests**

Create `src/RBX15.Engine.Tests/AdjustTests.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class AdjustTests
{
    static SKColor Through(SKColorFilter f, SKColor c) =>
        Px.At(Px.Render(1, 1, canvas => { using var p = new SKPaint { Color = c, ColorFilter = f }; canvas.DrawRect(0, 0, 1, 1, p); }), 0, 0);

    static SKImage Raster(params SKColor[] pixels)
    {
        var info = new SKImageInfo(pixels.Length, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int i = 0; i < pixels.Length; i++) bmp.SetPixel(i, 0, pixels[i]);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    [Fact]
    public void Brightness_scales_rgb() =>
        Px.Near(new SKColor(150, 150, 150), Through(Adjust.FilterChain(150, 100, 100, 0), new SKColor(100, 100, 100)));

    [Fact]
    public void Contrast_zero_is_mid_gray() =>
        Px.Near(new SKColor(128, 128, 128), Through(Adjust.FilterChain(100, 0, 100, 0), new SKColor(10, 200, 90)));

    [Fact]
    public void Saturate_zero_is_spec_luma() =>
        Px.Near(new SKColor(54, 54, 54), Through(Adjust.FilterChain(100, 100, 0, 0), new SKColor(255, 0, 0)));

    [Fact]
    public void Hue_rotate_180_uses_the_spec_matrix() =>
        Px.Near(new SKColor(0, 109, 109), Through(Adjust.FilterChain(100, 100, 100, 180), new SKColor(255, 0, 0)));

    [Fact]
    public void Each_stage_clamps_before_the_next()
    {
        // brightness(200%) on 200 -> 400 clamps to 255; contrast(50%) -> 0.5*1 + 0.25 = 0.75 -> 191.
        // Without the clamp it would come out 255.
        Px.Near(new SKColor(191, 191, 191), Through(Adjust.FilterChain(200, 50, 100, 0), new SKColor(200, 200, 200)));
    }

    [Fact]
    public void Filters_leave_alpha_alone() =>
        Assert.InRange(Through(Adjust.FilterChain(200, 100, 100, 0), new SKColor(100, 100, 100, 128)).Alpha, (byte)127, (byte)129);

    [Fact]
    public void Rgb_only_layers_count_as_adjusted()
    {
        var img = Raster(SKColors.Red);
        Assert.False(Adjust.HasAdjustments(new Layer { Id = 1, Image = img }));
        Assert.True(Adjust.HasAdjustments(new Layer { Id = 1, Image = img, GShift = -1 }));
        Assert.True(Adjust.HasAdjustments(new Layer { Id = 1, Image = img, HueRotate = 10 }));
    }

    [Fact]
    public void Rgb_shift_is_exact_clamped_and_skips_transparent_pixels()
    {
        var src = Raster(new SKColor(200, 100, 50, 3), new SKColor(10, 10, 10, 0));
        var shifted = Adjust.ShiftRgb(src, 10, -20, 250);
        Assert.Equal(new SKColor(210, 80, 255, 3), Px.At(shifted, 0, 0));
        Assert.Equal(0, Px.At(shifted, 1, 0).Alpha);
    }

    [Fact]
    public void Rgb_shift_is_cached_per_image_and_values()
    {
        var src = Raster(new SKColor(1, 2, 3));
        var a = Adjust.ShiftRgb(src, 5, 5, 5);
        Assert.Same(a, Adjust.ShiftRgb(src, 5, 5, 5));
        Assert.NotSame(a, Adjust.ShiftRgb(src, 6, 5, 5));
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors for `Adjust`.

- [ ] **Step 3: Implement**

Create `src/RBX15.Engine/Adjust.cs`:

```csharp
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// Layer adjustments (editor.js:173-234). 4.1 sets ctx.filter = 'brightness(B%) contrast(C%) saturate(S%)
/// hue-rotate(Hdeg)' whenever any adjustment, including RGB shift, is off default. Chromium applies the
/// four functions in order and clamps to [0,1] after each, so this builds one color filter per stage
/// (Filter Effects spec matrices, translate in 0..1 units) instead of one multiplied matrix.
/// </summary>
public static class Adjust
{
    public static bool HasAdjustments(Layer l) =>
        l.Brightness != 100 || l.Contrast != 100 || l.Saturate != 100 || l.HueRotate != 0 ||
        l.RShift != 0 || l.GShift != 0 || l.BShift != 0;

    public static SKColorFilter FilterChain(double brightnessPct, double contrastPct, double saturatePct, double hueDeg)
    {
        var brightness = SKColorFilter.CreateColorMatrix(Brightness((float)(brightnessPct / 100)));
        var contrast = SKColorFilter.CreateColorMatrix(Contrast((float)(contrastPct / 100)));
        var saturate = SKColorFilter.CreateColorMatrix(Saturate((float)(saturatePct / 100)));
        var hue = SKColorFilter.CreateColorMatrix(HueRotate(hueDeg));
        // CreateCompose(outer, inner) = outer(inner(x)): brightness runs first, hue-rotate last.
        return SKColorFilter.CreateCompose(hue, SKColorFilter.CreateCompose(saturate, SKColorFilter.CreateCompose(contrast, brightness)));
    }

    static float[] Brightness(float b) => new[]
    {
        b, 0, 0, 0, 0,
        0, b, 0, 0, 0,
        0, 0, b, 0, 0,
        0, 0, 0, 1, 0,
    };

    static float[] Contrast(float c)
    {
        float t = 0.5f - 0.5f * c;
        return new[]
        {
            c, 0, 0, 0, t,
            0, c, 0, 0, t,
            0, 0, c, 0, t,
            0, 0, 0, 1, 0,
        };
    }

    static float[] Saturate(float s) => new[]
    {
        0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s, 0, 0,
        0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s, 0, 0,
        0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s, 0, 0,
        0, 0, 0, 1, 0,
    };

    static float[] HueRotate(double degrees)
    {
        float cos = (float)Math.Cos(degrees * Math.PI / 180), sin = (float)Math.Sin(degrees * Math.PI / 180);
        return new[]
        {
            0.213f + cos * 0.787f - sin * 0.213f, 0.715f - cos * 0.715f - sin * 0.715f, 0.072f - cos * 0.072f + sin * 0.928f, 0, 0,
            0.213f - cos * 0.213f + sin * 0.143f, 0.715f + cos * 0.285f + sin * 0.140f, 0.072f - cos * 0.072f - sin * 0.283f, 0, 0,
            0.213f - cos * 0.213f - sin * 0.787f, 0.715f - cos * 0.715f + sin * 0.715f, 0.072f + cos * 0.928f + sin * 0.072f, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    sealed record ShiftEntry(int R, int G, int B, SKImage Result);
    static readonly ConditionalWeakTable<SKImage, ShiftEntry> ShiftCache = new();

    /// <summary>
    /// applyRGBShift on exact un-premultiplied pixels: add per channel, clamp 0..255, skip alpha 0,
    /// leave alpha alone. Cached per source image. Images are immutable, so a pixel edit is a new
    /// image and can never be served a stale result (ledger rgb-shift-cache).
    /// </summary>
    public static SKImage ShiftRgb(SKImage source, int r, int g, int b)
    {
        if (ShiftCache.TryGetValue(source, out var hit) && hit.R == r && hit.G == g && hit.B == b) return hit.Result;
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (!source.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
            throw new InvalidOperationException("RGB shift: cannot read pixels");
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.Alpha == 0) continue;
                bmp.SetPixel(x, y, new SKColor(Clamp(p.Red + r), Clamp(p.Green + g), Clamp(p.Blue + b), p.Alpha));
            }
        var result = SKImage.FromPixelCopy(bmp.PeekPixels());
        ShiftCache.AddOrUpdate(source, new ShiftEntry(r, g, b, result));
        return result;
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 38`. If `Hue_rotate_180_uses_the_spec_matrix` or `Each_stage_clamps_before_the_next` fails, inspect the composed result first (`CreateCompose` argument order, translate units). Don't change the expected values: they're the Filter Effects spec arithmetic.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat(engine): per-stage CSS filter chain and exact RGB shift"
```

---

### Task 5: Renderer and ExportNaming

**Files:**

- Create: `src/RBX15.Engine/Renderer.cs`, `src/RBX15.Engine/ExportNaming.cs`, `src/RBX15.Engine.Tests/RendererTests.cs`

**Interfaces:**

- Consumes: `Template`, `Fills`, `CssColor` (Tasks 1-2); `Images`, `Document`, `Layer` (Task 3); `Adjust` (Task 4).
- Produces:
  - `Renderer.LayerSampling` (`SKSamplingOptions`)
  - `Renderer.RenderExport(SKCanvas, Document)`
  - `Renderer.DrawLayer(SKCanvas, Layer)` (public, for M3's view render)
  - `Renderer.Export(Document) -> SKImage` (585x559, premultiplied)
  - `Renderer.ExportPng(Document) -> byte[]`
  - `ExportNaming.PngFileName(string? title)`, `ExportNaming.ProjectFileName(string? title)`

- [ ] **Step 1: Write the failing tests**

Create `src/RBX15.Engine.Tests/RendererTests.cs`:

```csharp
using System.Collections.Immutable;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.Engine.Tests;

public class RendererTests
{
    static readonly SKColor Navy = new(0x19, 0x2e, 0x44);
    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255);

    static SKImage Solid(int w, int h, SKColor c)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) bmp.SetPixel(x, y, c);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    static SKImage HalfRedHalfBlue()
    {
        var info = new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 20; x++) bmp.SetPixel(x, y, x < 10 ? Red : Blue);
        return SKImage.FromPixelCopy(bmp.PeekPixels());
    }

    static Document With(params Layer[] layers) => new() { Layers = layers.ToImmutableList() };

    [Fact]
    public void Empty_document_is_navy_inside_regions_and_clear_outside()
    {
        var img = Renderer.Export(new Document());
        Assert.Equal((585, 559), (img.Width, img.Height));
        Assert.Equal(Navy, Px.At(img, 295, 138));
        Assert.Equal(0, Px.At(img, 230, 138).Alpha);
        Assert.Equal(0, Px.At(img, 0, 0).Alpha);
    }

    [Fact]
    public void Export_masks_even_when_show_mask_is_off() =>
        Assert.Equal(0, Px.At(Renderer.Export(new Document { ShowMask = false }), 230, 138).Alpha);

    [Fact]
    public void Background_color_edge_cases_match_canvas()
    {
        Assert.Equal(0, Px.At(Renderer.Export(new Document { BgColor = "transparent" }), 295, 138).Alpha);
        Assert.Equal(0, Px.At(Renderer.Export(new Document { BgColor = "" }), 295, 138).Alpha);
        Assert.Equal(new SKColor(0, 0, 0), Px.At(Renderer.Export(new Document { BgColor = "not-a-color" }), 295, 138));
        Assert.InRange(Px.At(Renderer.Export(new Document { BgOpacity = 0.5 }), 295, 138).Alpha, 127, 128);
    }

    [Fact]
    public void Region_fills_paint_over_the_background()
    {
        var doc = new Document { RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#ff0000"))) };
        Assert.Equal(Red, Px.At(Renderer.Export(doc), 295, 138));
        Assert.Equal(Navy, Px.At(Renderer.Export(doc), 200, 138));
    }

    [Fact]
    public void Layers_draw_at_their_position()
    {
        var img = Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74 }));
        Assert.Equal(Red, Px.At(img, 235, 78));
        Assert.Equal(Navy, Px.At(img, 245, 78));
    }

    [Fact]
    public void Hidden_and_zero_opacity_layers_draw_nothing()
    {
        Assert.Equal(Navy, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74, Visible = false })), 235, 78));
        Assert.Equal(Navy, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74, Opacity = 0 })), 235, 78));
    }

    [Fact]
    public void Later_layers_draw_on_top()
    {
        var img = Renderer.Export(With(
            new Layer { Id = 1, Image = Solid(10, 10, Red), X = 231, Y = 74 },
            new Layer { Id = 2, Image = Solid(10, 10, Blue), X = 231, Y = 74 }));
        Assert.Equal(Blue, Px.At(img, 235, 78));
    }

    [Fact]
    public void FlipH_mirrors_the_layer()
    {
        Assert.Equal(Red, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 231, Y = 74 })), 233, 79));
        Assert.Equal(Blue, Px.At(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 231, Y = 74, FlipH = true })), 233, 79));
    }

    [Fact]
    public void Rotation_wraps_naturally()
    {
        var at5 = ImageDiff.FromImage(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 260, Y = 120, Scale = 2, Rotation = 5 })));
        var at725 = ImageDiff.FromImage(Renderer.Export(With(new Layer { Id = 1, Image = HalfRedHalfBlue(), X = 260, Y = 120, Scale = 2, Rotation = 725 })));
        Assert.True(ImageDiff.Compare(at5, at725).Pass);
    }

    [Fact]
    public void Export_png_decodes_to_the_template_size()
    {
        using var decoded = SKBitmap.Decode(Renderer.ExportPng(new Document()));
        Assert.Equal((585, 559), (decoded.Width, decoded.Height));
    }

    [Theory]
    [InlineData(null, "roblox_shirt_roblox.png")]
    [InlineData("", "roblox_shirt_roblox.png")]
    [InlineData("My Shirt!", "My_Shirt__roblox.png")]
    [InlineData("  ", "___roblox.png")]
    [InlineData("a-b_c9", "a-b_c9_roblox.png")]
    [InlineData("\U0001F600", "___roblox.png")]
    public void Png_file_name_matches_4_1(string? title, string expected) => Assert.Equal(expected, ExportNaming.PngFileName(title));

    [Fact]
    public void Project_file_name_matches_4_1()
    {
        Assert.Equal("untitled.r15proj", ExportNaming.ProjectFileName(""));
        Assert.Equal("My_Shirt_.r15proj", ExportNaming.ProjectFileName("My Shirt!"));
    }
}
```

This task's tests use `RBX15.Parity.ImageDiff.FromImage`, which Task 5 adds to Parity along with the Engine.Tests reference (Step 3).

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors for `Renderer`, `ExportNaming`, the `RBX15.Parity` namespace, and `ImageDiff.FromImage`.

- [ ] **Step 3: Implement**

Run: `dotnet add src/RBX15.Engine.Tests reference src/RBX15.Parity` and `dotnet add src/RBX15.Parity reference src/RBX15.Engine`.

Add to `src/RBX15.Parity/ImageDiff.cs`, inside `ImageDiff`:

```csharp
    /// <summary>Reads any SKImage (for example a premultiplied engine render) as an un-premultiplied
    /// RGBA bitmap, the same conversion canvas.toDataURL performs before encoding.</summary>
    public static SKBitmap FromImage(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bmp = new SKBitmap(info);
        if (!image.ReadPixels(info, bmp.GetPixels(), bmp.RowBytes, 0, 0))
        {
            bmp.Dispose();
            throw new InvalidOperationException("cannot read image pixels");
        }
        return bmp;
    }
```

Create `src/RBX15.Engine/Renderer.cs`:

```csharp
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The export composition (editor.js exportPNG, lines 1512-1534). The order is a contract (spec
/// section 2): clear, background (stretched image, or color, times opacity), region fills, visible
/// layers bottom to top, then the mask with destination-in. Export always masks.
/// </summary>
public static class Renderer
{
    /// <summary>Canvas2D default smoothing ('low'): bilinear, no mipmaps.</summary>
    public static readonly SKSamplingOptions LayerSampling = new(SKFilterMode.Linear, SKMipmapMode.None);
    static readonly SKSamplingOptions MaskSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    public static SKImage Export(Document doc)
    {
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("cannot create export surface");
        RenderExport(surface.Canvas, doc);
        return surface.Snapshot();
    }

    public static byte[] ExportPng(Document doc)
    {
        using var image = Export(doc);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void RenderExport(SKCanvas canvas, Document doc)
    {
        canvas.Clear(SKColors.Transparent);
        DrawBackground(canvas, doc);
        Fills.PaintRegions(canvas, doc.Mode, doc.RegionFills);
        foreach (var layer in doc.Layers) DrawLayer(canvas, layer);
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(Template.Mask(doc.Mode), 0, 0, MaskSampling, mask);
    }

    static void DrawBackground(SKCanvas canvas, Document doc)
    {
        var full = SKRect.Create(0, 0, TemplateSize.Width, TemplateSize.Height);
        float alpha = (float)doc.BgOpacity;
        if (doc.BgImage is { } bg)
        {
            using var p = new SKPaint { ColorF = new SKColorF(0, 0, 0, alpha) };
            canvas.DrawImage(Images.ForDrawing(bg), full, LayerSampling, p);
            return;
        }
        if (doc.BgColor is "transparent" or "") return;
        // An unparsable fillStyle is ignored by canvas, which keeps its default #000000.
        if (!CssColor.TryParseCanvasFill(doc.BgColor, out var c)) c = SKColors.Black;
        using var fill = new SKPaint { ColorF = new SKColorF(c.Red / 255f, c.Green / 255f, c.Blue / 255f, alpha) };
        canvas.DrawRect(full, fill);
    }

    /// <summary>drawLayerWithAdjustments: translate to center, rotate, then flip, opacity and filter.</summary>
    public static void DrawLayer(SKCanvas canvas, Layer layer)
    {
        if (!layer.Visible) return;
        var source = layer.RShift != 0 || layer.GShift != 0 || layer.BShift != 0
            ? Adjust.ShiftRgb(layer.Image, layer.RShift, layer.GShift, layer.BShift)
            : layer.Image;
        float w = (float)(layer.Image.Width * layer.Scale), h = (float)(layer.Image.Height * layer.Scale);
        canvas.Save();
        canvas.Translate((float)(layer.X + layer.Image.Width * layer.Scale / 2), (float)(layer.Y + layer.Image.Height * layer.Scale / 2));
        canvas.RotateRadians((float)(layer.Rotation * Math.PI / 180));
        if (layer.FlipH) canvas.Scale(-1, 1);
        if (layer.FlipV) canvas.Scale(1, -1);
        using var paint = new SKPaint { ColorF = new SKColorF(0, 0, 0, (float)layer.Opacity) };
        if (Adjust.HasAdjustments(layer))
            paint.ColorFilter = Adjust.FilterChain(layer.Brightness, layer.Contrast, layer.Saturate, layer.HueRotate);
        canvas.DrawImage(Images.ForDrawing(source), SKRect.Create(-w / 2, -h / 2, w, h), LayerSampling, paint);
        canvas.Restore();
    }
}
```

Create `src/RBX15.Engine/ExportNaming.cs`:

```csharp
using System.Text;

namespace RBX15.Engine;

/// <summary>4.1 file naming (ledger io-export-filename, kept): title || default, each UTF-16 unit
/// outside [a-zA-Z0-9_-] becomes '_', not trimmed.</summary>
public static class ExportNaming
{
    public static string PngFileName(string? title) => Sanitize(string.IsNullOrEmpty(title) ? "roblox_shirt" : title) + "_roblox.png";

    public static string ProjectFileName(string? title) => Sanitize(string.IsNullOrEmpty(title) ? "untitled" : title) + ".r15proj";

    static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s) sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 55` (38 + 11 facts + 6 InlineData cases).

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat(engine): export renderer and 4.1 file naming"
```

---

### Task 6: Scene loader, render CLI and the 30-scene golden gate

**Files:**

- Create: `src/RBX15.Parity/RepoRoot.cs`, `src/RBX15.Parity/SceneLoader.cs`, `src/RBX15.Parity.Tests/SceneLoaderTests.cs`, `src/RBX15.Engine.Tests/GoldenRenderTests.cs`
- Modify: `src/RBX15.Parity/Program.cs` (add `render`), `.gitignore` (add `diff-heatmaps/`)

**Interfaces:**

- Consumes: `Document`, `Layer`, `Fill`, `Images`, `Renderer` (Tasks 1-5); `ImageDiff` (M0 + Task 5); `tests/goldens/render/scenes.json` + PNGs (M0). The scene format is defined in `tools/parity-ref/README.md`.
- Produces:
  - `RepoRoot.Find(string start) -> string`: the nearest ancestor containing `global.json`
  - `SceneLoader.Scene(string Name, Document Document)` and `SceneLoader.LoadAll(string scenesJson, string repoRoot) -> IReadOnlyList<Scene>`
  - `SceneLoader.Parse(JsonElement scene, string repoRoot) -> Scene`
  - CLI: `RBX15.Parity render <scenes.json> <outDir> [--only a,b]`
  - The golden theory, run in CI on every PR

**The rule for RGB-shift scenes:** the approved divergence `rgb-shift-compute` makes v5 more precise than 4.1 on semi-transparent pixels. Scenes with any RGB shift are therefore held to:

1. The full golden rule on every pixel whose golden alpha is 255.
2. An alpha delta of at most 1 everywhere, since the shift never touches alpha.

Every other scene gets the full rule on every pixel.

**When a scene fails:**

- Use superpowers:systematic-debugging, starting from the heatmap the test writes to `diff-heatmaps/<scene>.png`.
- Fix the engine. Never edit a golden or loosen the rule.
- If a difference turns out to be an unbridgeable Chromium-versus-Skia behavior, stop and report it with the heatmap and the measured numbers. That calls for a spec-level decision (a new golden plus a divergence row), not an implementer's call.

- [ ] **Step 1: Write the failing scene-loader tests**

Create `src/RBX15.Parity.Tests/SceneLoaderTests.cs`:

```csharp
using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static SceneLoader.Scene Load(string json) => SceneLoader.Parse(JsonDocument.Parse(json).RootElement, Root);

    [Fact]
    public void Defaults_match_the_4_1_editor()
    {
        var s = Load("""{ "name": "x" }""");
        Assert.Equal("x", s.Name);
        Assert.Equal(new Document().BgColor, s.Document.BgColor);
        Assert.Equal(TemplateMode.Shirt, s.Document.Mode);
        Assert.Empty(s.Document.Layers);
    }

    [Fact]
    public void Region_fills_keep_their_order_and_accept_legacy_strings()
    {
        var s = Load("""{ "name": "x", "mode": "Pants", "regionColors": { "waist_back": "#123", "waist_front": { "type": "dots", "c1": "#000", "c2": "#fff", "scale": 10 } } }""");
        Assert.Equal(TemplateMode.Pants, s.Document.Mode);
        Assert.Equal(new[] { "waist_back", "waist_front" }, s.Document.RegionFills.Select(f => f.Region));
        Assert.Equal(Fill.Solid("#123"), s.Document.RegionFills[0].Fill);
        Assert.Equal(new Fill("dots", "#000", "#fff", null, 10), s.Document.RegionFills[1].Fill);
    }

    [Fact]
    public void Layers_get_makeLayer_defaults_and_sequential_ids()
    {
        var s = Load("""{ "name": "x", "layers": [ { "image": "packs/drips/drip_cyan_front_v1.png", "x": 5, "rShift": 7 }, { "image": "packs/drips/drip_cyan_front_v1.png", "visible": false } ] }""");
        var (a, b) = (s.Document.Layers[0], s.Document.Layers[1]);
        Assert.Equal((1, 2), (a.Id, b.Id));
        Assert.Equal((5.0, 0.0, 1.0, 1.0, 7), (a.X, a.Y, a.Scale, a.Opacity, a.RShift));
        Assert.False(b.Visible);
        Assert.Equal((128, 150), (a.Image.Width, a.Image.Height));
    }

    [Fact]
    public void All_frozen_scenes_load()
    {
        var scenes = SceneLoader.LoadAll(Path.Combine(Root, "tests", "goldens", "render", "scenes.json"), Root);
        Assert.Equal(30, scenes.Count);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test src/RBX15.slnx`
Expected: build errors for `RepoRoot` and `SceneLoader`.

- [ ] **Step 3: Implement RepoRoot and SceneLoader**

Create `src/RBX15.Parity/RepoRoot.cs`:

```csharp
namespace RBX15.Parity;

public static class RepoRoot
{
    /// <summary>The nearest ancestor of <paramref name="start"/> holding global.json (the repo root).</summary>
    public static string Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
        throw new DirectoryNotFoundException($"no global.json above {start}");
    }
}
```

Create `src/RBX15.Parity/SceneLoader.cs`:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using RBX15.Engine;

namespace RBX15.Parity;

/// <summary>
/// Turns a parity scene (tools/parity-ref/README.md) into an engine Document. Defaults equal the
/// reference renderer's, which equal makeLayer (editor.js:134-142) and the editor state (:76-107).
/// </summary>
public static class SceneLoader
{
    public sealed record Scene(string Name, Document Document);

    public static IReadOnlyList<Scene> LoadAll(string scenesJson, string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(scenesJson));
        return doc.RootElement.EnumerateArray().Select(e => Parse(e, repoRoot)).ToList();
    }

    public static Scene Parse(JsonElement e, string repoRoot)
    {
        string? Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Num(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        bool? Bool(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        SkiaSharp.SKImage Image(string rel) => Images.Decode(File.ReadAllBytes(Path.Combine(repoRoot, rel)));

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
        return new Scene(Str(e, "name") ?? throw new InvalidDataException("scene without a name"), document);
    }
}
```

- [ ] **Step 4: Run the loader tests**

Run: `dotnet test src/RBX15.slnx -c Release`
Expected: Parity.Tests `Passed: 13` (9 + 4 new).

- [ ] **Step 5: Write the golden gate**

Add `diff-heatmaps/` to `.gitignore` under the `.NET (v5 native port)` block.

Create `src/RBX15.Engine.Tests/GoldenRenderTests.cs`:

```csharp
using RBX15.Parity;
using SkiaSharp;

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

    static SKBitmap OpaqueOnly(SKBitmap source, SKBitmap golden)
    {
        var copy = source.Copy();
        for (int y = 0; y < golden.Height; y++)
            for (int x = 0; x < golden.Width; x++)
                if (golden.GetPixel(x, y).Alpha < 255) copy.SetPixel(x, y, SKColors.Transparent);
        return copy;
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Engine_export_matches_the_frozen_4_1_golden(string name)
    {
        var scene = Scenes.Value.Single(s => s.Name == name);
        using var golden = ImageDiff.Load(Path.Combine(Dir, name + ".png"));
        using var rendered = Renderer.Export(scene.Document);
        using var candidate = ImageDiff.FromImage(rendered);

        bool rgbShift = scene.Document.Layers.Any(l => l.RShift != 0 || l.GShift != 0 || l.BShift != 0);
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
            var heatDir = Path.Combine(Root, "diff-heatmaps");
            Directory.CreateDirectory(heatDir);
            using var heat = ImageDiff.Heatmap(golden, candidate);
            using var data = SKImage.FromBitmap(heat).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(heatDir, name + ".png"), data.ToArray());
        }
        Assert.True(result.Pass, $"{name}: {result}");
    }
}
```

- [ ] **Step 6: Run the gate**

Run: `dotnet test src/RBX15.slnx -c Release --filter "FullyQualifiedName~GoldenRenderTests" > golden.log 2>&1; tail -30 golden.log`
Expected: `Passed: 30`. Follow the "When a scene fails" rule above for anything else.

These are the first engine-versus-Chromium comparisons for anything beyond fills, so failures here are findings, not test bugs. In order of likelihood:

- **Layer edges.** Sampling, or premultiplied versus un-premultiplied source.
- **Adjusted scenes.** Filter stage order, or translate units.
- **Background image.** Sampling at the stretch.

Each heatmap shows which.

- [ ] **Step 7: Add the render CLI and prove it**

In `src/RBX15.Parity/Program.cs`, before the `diff` usage check, add the `render` verb:

```csharp
if (args.Length >= 3 && args[0] == "render")
{
    var root = RBX15.Parity.RepoRoot.Find(Directory.GetCurrentDirectory());
    var scenes = RBX15.Parity.SceneLoader.LoadAll(Path.GetFullPath(args[1]), root);
    int only = Array.IndexOf(args, "--only");
    var names = only > 0 && only + 1 < args.Length ? args[only + 1].Split(',').ToHashSet() : null;
    Directory.CreateDirectory(args[2]);
    foreach (var scene in scenes.Where(s => names is null || names.Contains(s.Name)))
    {
        File.WriteAllBytes(Path.Combine(args[2], scene.Name + ".png"), RBX15.Engine.Renderer.ExportPng(scene.Document));
        Console.WriteLine($"rendered {scene.Name}");
    }
    return 0;
}
```

Update the usage line to `usage: RBX15.Parity diff <reference.png> <candidate.png> [--heatmap <out.png>] | render <scenes.json> <outDir> [--only a,b]`.

Run:

```bash
dotnet run --project src/RBX15.Parity -c Release -- render tests/goldens/render/scenes.json .engine-render --only full-scene
dotnet run --project src/RBX15.Parity -c Release -- diff tests/goldens/render/full-scene.png .engine-render/full-scene.png
rm -rf .engine-render
```

Expected: `rendered full-scene`, then `... pass=True`, exit 0.

- [ ] **Step 8: Run everything and commit**

Run: `dotnet build src/RBX15.slnx -c Release -warnaserror` then `dotnet test src/RBX15.slnx -c Release`
Expected: 0 warnings; Engine.Tests `Passed: 85` (55 + 30 golden); Parity.Tests `Passed: 13`.

```bash
git add src .gitignore
git commit -m "feat(engine): scene loader, render CLI and the 30-scene golden gate"
```

---

### Task 7: Close the M1 ledger rows and open the PR

**Files:**

- Modify: `tools/ledger/ledger.py` (add `done`), `tools/ledger/test_ledger.py`, `docs/port/parity-ledger.json`, `docs/port/parity-ledger.md`

**Interfaces:**

- Consumes: everything above; `tools/ledger` (M0).
- Produces: `python tools/ledger/ledger.py done <id> [<id>...]` sets the rows to `done` (unknown IDs exit 1 and change nothing), rewrites the JSON with `indent=1` / UTF-8 / LF, and re-renders. Python `mark_done(data, ids) -> dict` raises `KeyError` on unknown IDs. M2 to M6 use it.

- [ ] **Step 1: Write the failing test**

Append to `tools/ledger/test_ledger.py`, inside `CheckTests`:

```python
    def test_mark_done_sets_status_and_rejects_unknown_ids(self):
        d = data(row(), row(id="fills-camo"))
        out = ledger.mark_done(d, ["fills-camo"])
        self.assertEqual([r["status"] for r in out["rows"]], ["open", "done"])
        with self.assertRaises(KeyError):
            ledger.mark_done(d, ["no-such-row"])
```

- [ ] **Step 2: Run it and see it fail**

Run: `python -m unittest discover -s tools/ledger -p "test_*.py"`
Expected: `AttributeError: module 'ledger' has no attribute 'mark_done'`.

- [ ] **Step 3: Implement**

In `tools/ledger/ledger.py`, add above `def main`:

```python
def mark_done(data: dict, ids: list[str]) -> dict:
    known = {r["id"] for r in data["rows"]}
    unknown = [i for i in ids if i not in known]
    if unknown:
        raise KeyError(f"unknown ledger ids: {', '.join(unknown)}")
    wanted = set(ids)
    for r in data["rows"]:
        if r["id"] in wanted:
            r["status"] = "done"
    return data
```

In `main`, before the `if cmd == "render":` branch:

```python
    if cmd == "done":
        try:
            mark_done(data, argv[2:])
        except KeyError as e:
            print(e.args[0])
            return 1
        (PORT / "parity-ledger.json").write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n",
                                                 encoding="utf-8", newline="\n")
        cmd = "render"
```

Run: `python -m unittest discover -s tools/ledger -p "test_*.py"`
Expected: `Ran 8 tests ... OK`.

- [ ] **Step 4: Close the rows M1 proves**

These 42 rows each have their check in this milestone: unit tests in Tasks 1-5 and the golden gate in Task 6.

```bash
python tools/ledger/ledger.py done \
  tpl-canvas-size tpl-shirt-regions tpl-pants-regions \
  fills-normalize fills-hex-parse fills-prng fills-defaults fills-solid fills-linear fills-radial fills-stripes \
  fills-checker fills-dots fills-camo fills-key region-union-rect region-connected-groups paint-regions-grouping \
  project-and-export-dependency-note tests-lib-fills \
  mask-build layer-draw-transform layer-skip-rules render-offscreen render-background render-region-fills-step \
  render-mask-step render-order-summary \
  rgb-shift-compute rgb-shift-cache has-adjustments css-filter-chain rgb-shift-draw-order imgdata-premult-note \
  layers-adjust-pipeline \
  export-composite-order export-png-encoding shell-gpu-canvas-edge-drift export-filename-save io-export-filename \
  state-defaults layer-factory
python tools/ledger/ledger.py check
```

Expected: `rendered ...`, then `407 rows, 0 problem(s)`. `grep -c '"status": "done"' docs/port/parity-ledger.json` prints `42`.

- [ ] **Step 5: Final verification**

Run, in order:

- `dotnet build src/RBX15.slnx -c Release -warnaserror`
- `dotnet test src/RBX15.slnx -c Release`
- `python -m unittest discover -s tools/ledger -p "test_*.py"`
- `python tools/ledger/ledger.py check`
- `npm test`
- `git diff --stat origin/main -- main.js editor.js editor.html styles.css lib packs package.json package-lock.json tests/*.test.js windows tests/goldens`

Expected: all green; the last command prints nothing, which proves Electron and the goldens are untouched.

- [ ] **Step 6: Commit, push and open the PR**

```bash
git add tools/ledger docs/port
git commit -m "chore(ledger): ledger.py done; close the 42 rows M1 proves"
git push -u origin feat/v5-m1-engine-render-core
gh pr create --title "feat(engine): M1 render core passes all 30 frozen 4.1 goldens" --body "Implements docs/superpowers/plans/2026-10-13-v5-m1-engine-render-core.md. Refs #38.

- Engine: Template (regions, semantic slots, mask), Fills (spike port, SKPathBuilder), exact image decode, Layer/Document records, per-stage CSS filter chain, exact RGB shift, export Renderer, 4.1 file naming.
- Parity: SceneLoader, render CLI, and the golden gate: the engine export of all 30 frozen 4.1 scenes passes (zero interior differences, edges within 3; RGB-shift scenes strict on opaque pixels under the approved divergence).
- Ledger: 42 rows closed via the new ledger.py done.

Electron and goldens untouched."
```

- [ ] **Step 7: Log on the dashboard**

`manage_decisions log`, project `6KpHItSNg9NYHkJg9thn`, origin `Dunder · Claude Code`, category `architecture`. Record that M1 landed, the golden result (30/30), any engine-versus-Chromium findings from Task 6 and how they were resolved, and the next step: the M2 plan (Tools, eraser, History, ProjectIO, Details, WordArt, plus the M2 goldens added to `tools/parity-ref` first).
