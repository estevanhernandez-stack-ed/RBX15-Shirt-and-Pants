using System.Windows.Media;
using System.Windows.Media.Imaging;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>
/// The Template Regions grid (buildRegionGrid, editor.js:1615-1687). Ledger rows:
/// regions-grid-layout-shirt/pants, regions-click-paint, regions-click-fit-layer,
/// regions-right-click-clear, regions-button-swatch-paint, fills-to-css, region-front-highlight-lost.
/// </summary>
public class RegionsGroupTests
{
    static (EditorSession Session, RegionsGroupViewModel Regions, FillStyleViewModel FillStyle) Make(EditorSession? session = null)
    {
        session ??= new EditorSession();
        var fillStyle = new FillStyleViewModel(session, new FakeColorPicker());
        return (session, new RegionsGroupViewModel(session, fillStyle), fillStyle);
    }

    static string[] Keys(IEnumerable<RegionTileViewModel> tiles) => tiles.Select(t => t.Region).ToArray();
    static string[] Labels(IEnumerable<RegionTileViewModel> tiles) => tiles.Select(t => t.Label).ToArray();

    [Fact] // editor.js:1625-1648: cross Top/R/Front/L/Back/Bot; right arm L,Bk,R,Fr; left arm Fr,L,Bk,R
    public void Shirt_grid_has_4_1s_fourteen_tiles_in_order()
    {
        var (_, regions, _) = Make();

        Assert.Equal(new[] { "torso_up", "torso_right", "torso_front", "torso_left", "torso_back", "torso_down" }, Keys(regions.Body));
        Assert.Equal(new[] { "Top", "R", "Front", "L", "Back", "Bot" }, Labels(regions.Body));
        Assert.Equal(new[] { "rarm_left", "rarm_back", "rarm_right", "rarm_front" }, Keys(regions.Limb1));
        Assert.Equal(new[] { "L", "Bk", "R", "Fr" }, Labels(regions.Limb1));
        Assert.Equal(new[] { "larm_front", "larm_left", "larm_back", "larm_right" }, Keys(regions.Limb2));
        Assert.Equal(new[] { "Fr", "L", "Bk", "R" }, Labels(regions.Limb2));
        Assert.Equal(("Torso", "Right Arm", "Left Arm"), (regions.BodyLabel, regions.LimbLabel1, regions.LimbLabel2));
        // the side rows stay 4.1's; the limbs' top and bottom are separate tiles (regions-limb-caps)
        Assert.DoesNotContain(regions.Limb1.Concat(regions.Limb2), t => t.Region.EndsWith("_up") || t.Region.EndsWith("_down"));
    }

    [Theory] // regions-limb-caps (fix): each limb gets Top and Bot; 4.1 had no button for its shoulder or hand opening
    [InlineData(false, "rarm", "larm", "Right Arm", "Left Arm")]
    [InlineData(true, "rleg", "lleg", "Right Leg", "Left Leg")]
    public void Each_limb_has_a_top_and_a_bottom_tile(bool pants, string right, string left, string rightLabel, string leftLabel)
    {
        var (session, regions, _) = Make();
        if (pants) session.Apply(DocumentOps.ToggleMode);

        Assert.Equal(new[] { right + "_up", right + "_down", left + "_up", left + "_down" },
            new[] { regions.Limb1Top, regions.Limb1Bot, regions.Limb2Top, regions.Limb2Bot }.Select(t => t.Region));
        Assert.Equal(new[] { "Top", "Bot", "Top", "Bot" },
            new[] { regions.Limb1Top, regions.Limb1Bot, regions.Limb2Top, regions.Limb2Bot }.Select(t => t.Label));
        Assert.Equal($"{rightLabel} Bot (Bottom)", regions.Limb1Bot.AutomationName);
        Assert.Equal($"{leftLabel} Top", regions.Limb2Top.AutomationName);
        Assert.Equal(18, regions.AllTiles.Select(t => t.Region).Distinct().Count());   // every region of the template
    }

    [Fact] // a cap paints like any tile, and the tile follows the document
    public void A_limb_cap_paints_its_region()
    {
        var (session, regions, _) = Make();
        regions.Limb1Top.PaintCommand.Execute(null);
        Assert.NotNull(DocumentOps.RegionFill(session.Document, "rarm_up"));
        Assert.NotNull(regions.Limb1Top.Fill);
        regions.Limb1Top.ClearCommand.Execute(null);
        Assert.Null(DocumentOps.RegionFill(session.Document, "rarm_up"));
    }

    [Fact] // regions-grid-layout-pants: same structure, waist/rleg/lleg keys, Leg headings, rebuilt on toggle
    public void Pants_grid_rebuilds_with_pants_keys_and_headings()
    {
        var (session, regions, _) = Make();
        var raised = Track(regions);

        session.Apply(DocumentOps.ToggleMode);

        Assert.Equal(new[] { "waist_up", "waist_right", "waist_front", "waist_left", "waist_back", "waist_down" }, Keys(regions.Body));
        Assert.Equal(new[] { "rleg_left", "rleg_back", "rleg_right", "rleg_front" }, Keys(regions.Limb1));
        Assert.Equal(new[] { "lleg_front", "lleg_left", "lleg_back", "lleg_right" }, Keys(regions.Limb2));
        Assert.Equal(("Waist", "Right Leg", "Left Leg"), (regions.BodyLabel, regions.LimbLabel1, regions.LimbLabel2));
        Assert.Contains(nameof(RegionsGroupViewModel.Body), raised);
        Assert.Contains(nameof(RegionsGroupViewModel.LimbLabel1), raised);
    }

    [Fact] // spec §4: every tile has an automation name, unique within the grid
    public void Tiles_have_unique_part_and_face_automation_names()
    {
        var (_, regions, _) = Make();
        var names = regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Select(t => t.AutomationName).ToArray();

        Assert.Equal(14, names.Distinct().Count());
        Assert.Equal("Torso Front", Tile(regions, "torso_front").AutomationName);
        Assert.Equal("Torso Top", Tile(regions, "torso_up").AutomationName);
        Assert.Equal("Torso Bot (Bottom)", Tile(regions, "torso_down").AutomationName);
        Assert.Equal("Torso R (Right)", Tile(regions, "torso_right").AutomationName);
        Assert.Equal("Right Arm Bk (Back)", Tile(regions, "rarm_back").AutomationName);
        Assert.Equal("Left Arm Fr (Front)", Tile(regions, "larm_front").AutomationName);
    }

    [Fact] // WCAG 2.5.3: the accessible name contains the visible label, in both modes
    public void Every_automation_name_contains_the_visible_label()
    {
        var (session, regions, _) = Make();
        foreach (var _ in new[] { 0, 1 })
        {
            var tiles = regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).ToArray();
            Assert.All(tiles, t => Assert.Contains(t.Label, t.AutomationName, StringComparison.OrdinalIgnoreCase));
            session.Apply(DocumentOps.ToggleMode);
        }
    }

    [Fact] // only the body front carries the resting tint (editor.js:1627)
    public void Only_the_body_front_is_the_front_tile()
    {
        var (session, regions, _) = Make();
        Assert.Equal(new[] { "torso_front" }, regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Where(t => t.IsFront).Select(t => t.Region));
        session.Apply(DocumentOps.ToggleMode);
        Assert.True(Tile(regions, "waist_front").IsFront);
    }

    [Fact] // editor.js:1668-1674 plus the regions-click-paint fix: an identical repaint is no step
    public void Click_with_nothing_selected_paints_the_current_fill_once()
    {
        var (session, regions, fillStyle) = Make();
        var front = Tile(regions, "torso_front");

        front.PaintCommand.Execute(null);

        Assert.Equal(fillStyle.Current, front.Fill);
        Assert.True(front.IsFilled);
        Assert.Equal(("torso_front", FillStyleViewModel.DefaultFill), Assert.Single(session.Document.RegionFills));
        Assert.Equal(1, session.UndoCount);

        front.PaintCommand.Execute(null);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact] // claim-region-click-color-picker: with a layer selected the click fits it; fills untouched
    public void Click_with_a_layer_selected_fits_it_and_leaves_fills_alone()
    {
        var session = SessionWith(LayerOf(1, 200, 100));
        session.Select(1);
        var (_, regions, _) = Make(session);

        Tile(regions, "torso_front").PaintCommand.Execute(null);

        var layer = session.Document.Layers[0];
        Assert.Equal((0.544, 240.6, 110.8), (Math.Round(layer.Scale, 9), Math.Round(layer.X, 9), Math.Round(layer.Y, 9)));
        Assert.Empty(session.Document.RegionFills);
        Assert.Null(Tile(regions, "torso_front").Fill);
        Assert.Equal(1, session.UndoCount);
        Assert.Equal(1, session.SelectedLayerId);
    }

    [Fact] // regions-right-click-clear: unpainted -> no step; painted -> one step and the swatch goes
    public void Right_click_clears_a_painted_tile_in_one_step_and_ignores_an_empty_one()
    {
        var (session, regions, _) = Make();
        var back = Tile(regions, "torso_back");

        back.ClearCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);

        back.PaintCommand.Execute(null);
        var raised = Track(back);
        back.ClearCommand.Execute(null);

        Assert.Null(back.Fill);
        Assert.False(back.IsFilled);
        Assert.Null(back.Swatch);
        Assert.Equal(2, session.UndoCount);
        Assert.Contains(nameof(RegionTileViewModel.Swatch), raised);
    }

    [Fact] // undo and redo repaint the tiles (editor.js:1445-1448)
    public void Undo_and_redo_repaint_tiles()
    {
        var (session, regions, _) = Make();
        var up = Tile(regions, "torso_up");
        up.PaintCommand.Execute(null);

        session.Undo();
        Assert.Null(up.Fill);

        session.Redo();
        Assert.Equal(FillStyleViewModel.DefaultFill, up.Fill);
    }

    [Fact] // Pants tiles write pants keys (mode-pants-crash-hardcoded-keys)
    public void Pants_tiles_paint_pants_keys()
    {
        var (session, regions, _) = Make(new EditorSession(new Document { Mode = TemplateMode.Pants }));
        Tile(regions, "lleg_back").PaintCommand.Execute(null);
        Assert.Equal("lleg_back", Assert.Single(session.Document.RegionFills).Region);
    }

    static byte[] Bgra(BitmapSource bmp)
    {
        var bytes = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(bytes, bmp.PixelWidth * 4, 0);
        return bytes;
    }

    [Theory] // fills-to-css fix: the swatch is the canvas painter's own pixels for the region
    [InlineData("solid")]
    [InlineData("linear")]
    [InlineData("radial")]
    [InlineData("stripes")]
    [InlineData("checker")]
    [InlineData("dots")]
    [InlineData("camo")]
    public void Swatch_is_the_canvas_painter_cropped_to_the_region(string type) => StaThread.Run(() =>
    {
        var fill = FillStyleViewModel.DefaultFill with { Type = type, Angle = 30, Scale = 12 };
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", fill));
        var (_, regions, _) = Make(session);

        var swatch = Assert.IsAssignableFrom<BitmapSource>(Tile(regions, "torso_front").Swatch);

        Assert.True(swatch.IsFrozen);
        Assert.Equal(PixelFormats.Pbgra32, swatch.Format);
        Assert.Equal((128, 128), (swatch.PixelWidth, swatch.PixelHeight));
        var info = new SKImageInfo(TemplateSize.Width, TemplateSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        Template.TryFind(TemplateMode.Shirt, "torso_front", out var r);
        Fills.PaintRegionFill(surface.Canvas, r.Rect, fill);
        using var full = surface.Snapshot();
        using var crop = full.Subset(SKRectI.Create(r.X, r.Y, r.W, r.H));
        var expected = new byte[r.W * r.H * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(expected, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { Assert.True(crop.ReadPixels(new SKImageInfo(r.W, r.H, SKColorType.Bgra8888, SKAlphaType.Premul), handle.AddrOfPinnedObject(), r.W * 4, 0, 0)); }
        finally { handle.Free(); }
        Assert.Equal(expected, Bgra(swatch));
    });

    [Fact] // fills-to-css divergence: canvas angle 0 runs left to right (CSS 0deg would run bottom to top)
    public void A_zero_degree_gradient_swatch_runs_left_to_right() => StaThread.Run(() =>
    {
        var fill = new Fill("linear", "#ff0000", "#0000ff", 0, 16);
        var session = new EditorSession();
        session.Apply(d => DocumentOps.PaintRegion(d, "torso_front", fill));
        var (_, regions, _) = Make(session);
        var px = Bgra((BitmapSource)Tile(regions, "torso_front").Swatch!);

        (byte B, byte G, byte R) At(int x, int y) { int i = (y * 128 + x) * 4; return (px[i], px[i + 1], px[i + 2]); }

        Assert.True(At(0, 64).R > 240 && At(0, 64).B < 15, $"left {At(0, 64)}");
        Assert.True(At(127, 64).B > 240 && At(127, 64).R < 15, $"right {At(127, 64)}");
        Assert.Equal(At(64, 0), At(64, 127));
    });
    [Theory] // regions-fill-all: All paints every region of the mode with the current fill, one undo step
    [InlineData(false)]
    [InlineData(true)]
    public void Fill_all_paints_every_region_in_one_step(bool pants)
    {
        var (session, regions, fillStyle) = Make();
        if (pants) session.Apply(DocumentOps.ToggleMode);
        int undo = session.UndoCount;

        regions.FillAllCommand.Execute(null);

        Assert.Equal(undo + 1, session.UndoCount);
        Assert.Equal(Template.Regions(session.Document.Mode).Select(r => r.Name).Order(),
            session.Document.RegionFills.Select(f => f.Item1).Order());
        Assert.All(session.Document.RegionFills, f => Assert.Equal(fillStyle.Current, f.Item2));
        Assert.All(regions.AllTiles, t => Assert.NotNull(t.Fill));
        Assert.True(session.Undo());
        Assert.Empty(session.Document.RegionFills);
    }

    [Fact] // All paints even with a layer selected: it never fits the layer, unlike a region tile
    public void Fill_all_with_a_layer_selected_still_paints()
    {
        var session = SessionWith(LayerOf(1, 200, 100));
        session.Select(1);
        var (_, regions, _) = Make(session);
        var layer = session.Document.Layers[0];

        regions.FillAllCommand.Execute(null);

        Assert.Equal(18, session.Document.RegionFills.Count);
        Assert.Same(layer, session.Document.Layers[0]);
    }

    [Fact] // right-click on All clears every fill in one step; with nothing filled it records nothing
    public void Clear_all_clears_every_fill_in_one_step()
    {
        var (session, regions, _) = Make();
        regions.ClearAllCommand.Execute(null);
        Assert.Equal(0, session.UndoCount);

        Tile(regions, "torso_front").PaintCommand.Execute(null);
        regions.Limb1Top.PaintCommand.Execute(null);
        int undo = session.UndoCount;
        regions.ClearAllCommand.Execute(null);

        Assert.Empty(session.Document.RegionFills);
        Assert.Equal(undo + 1, session.UndoCount);
        Assert.All(regions.AllTiles, t => Assert.Null(t.Fill));
    }
}
