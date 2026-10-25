using System.Windows;
using System.Windows.Input;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;
using Xunit;
using Point = System.Windows.Point;

namespace RBX15.App.Tests;

/// <summary>
/// M4b item 14: the undo matrix and the Pants matrix, replayed through the real composition (Composition.Build with fakes for
/// the native services, as TestShell does), the window's ShortcutRouter and LayerKeys for the keyboard, and CanvasInteraction's
/// pointer seams for the canvas gestures. Ledger undo-coverage-gaps, undo-empty-steps, file-undo-snapshot-scope,
/// bug-pants-mode-region-keys, mode-pants-crash-hardcoded-keys.
/// </summary>
public sealed partial class LedgerScenarioTests
{
    // ---- the rig ----

    /// <summary>One shell over a new document in <paramref name="mode"/>, with the fakes a scenario drives and the window's keyboard router.</summary>
    sealed class MatrixRig : IDisposable
    {
        readonly List<SKImage> _images = [];

        public MatrixRig(TemplateMode mode, string png)
        {
            Png = png;
            S = new EditorSession(EditorSession.NewDocument() with { Mode = mode });
            Main = TestShell.Quiet(Composition.Build(S, Files, new FakeDialogService(), Picker, new FakeSettingsStore(), () => new Random(7)));
            Keys = new LayerKeys(S);
            Router = new ShortcutRouter(Main.Home.Tools, Keys);
        }

        public string Png { get; }
        public EditorSession S { get; }
        public MainViewModel Main { get; }
        public FakeFileDialogs Files { get; } = new();
        public FakeColorPicker Picker { get; } = new();
        public LayerKeys Keys { get; }
        public ShortcutRouter Router { get; }
        public CanvasInteraction Canvas => Main.Canvas.Interaction;
        public Layer Sel => S.SelectedLayer!;

        /// <summary>A 100x60 layer at (200, 100), selected, as one undo step (the way an import lands).</summary>
        public int AddLayer(string name = "art", double x = 200, double y = 100)
        {
            var image = Solid(100, 60, Slate);
            _images.Add(image);
            return LedgerScenarioTests.AddLayer(S, image, x, y, name);
        }

        /// <summary>A key down through the window's router (Keys ON, the selection as it is, nothing focused).</summary>
        public void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            if (ShortcutRouter.Route(key, modifiers, null, Main.Home.Keys.Enabled, S.SelectedLayerId is not null) is { } action) Router.Execute(action);
        }

        /// <summary>A typed character through the window's router.</summary>
        public void Type(string text, ModifierKeys modifiers = ModifierKeys.None)
        {
            if (ShortcutRouter.RouteText(text, modifiers, null, Main.Home.Keys.Enabled, S.SelectedLayerId is not null) is { } action) Router.Execute(action);
        }

        /// <summary>The body-front region tile (Torso Front, or Waist Front in Pants).</summary>
        public RegionTileViewModel FrontTile => Main.Layout.Regions.Body.Single(t => t.IsFront);

        public void Dispose()
        {
            foreach (var image in _images) image.Dispose();
        }
    }

    /// <summary>The center of a layer's unrotated box on the template.</summary>
    static Point CenterOf(Layer l) => new(l.X + l.Image.Width * l.Scale / 2, l.Y + l.Image.Height * l.Scale / 2);

    /// <summary>The bottom-right resize handle of an unrotated layer.</summary>
    static Point CornerOf(Layer l) => new(l.X + l.Image.Width * l.Scale, l.Y + l.Image.Height * l.Scale);

    /// <summary>The rotate knob: 30 template px beyond the top edge, turned with the layer.</summary>
    static Point KnobOf(Layer l)
    {
        var c = CenterOf(l);
        return At(c.X, c.Y, l.Rotation, l.Image.Height * l.Scale / 2 + 30);
    }

    sealed record Scenario(Action<MatrixRig>? Setup, Action<MatrixRig> Act, Action<MatrixRig>[] NoOps);

    static readonly Lazy<IReadOnlyDictionary<string, Scenario>> MatrixOnce = new(BuildMatrix);   // after the other statics, whatever the partials' order

    static IReadOnlyDictionary<string, Scenario> Matrix => MatrixOnce.Value;

    static readonly string[] DetailIds =
    [
        "collar-v", "collar-crew", "collar-scoop", "collar-henley", "cuff-left", "cuff-right", "cuffs-both", "hem",
        "seam-center", "seam-sides", "shoulder-stripe", "arm-bands", "yoke", "pocket", "placket", "full-detail",
    ];

    public static IEnumerable<object[]> MatrixRows() =>
        Matrix.Keys.SelectMany(name => new[] { TemplateMode.Shirt, TemplateMode.Pants }.Select(mode => new object[] { name, mode }));

    /// <summary>The actions a Pants document must run without an exception (bug-pants-mode-region-keys, mode-pants-crash-hardcoded-keys).</summary>
    public static IEnumerable<object[]> PantsRows() =>
        Matrix.Keys
            .Where(n => n.StartsWith("add image", StringComparison.Ordinal) || n.StartsWith("placement ", StringComparison.Ordinal)
                        || n.StartsWith("preset ", StringComparison.Ordinal) || n.StartsWith("details ", StringComparison.Ordinal)
                        || n is "center on front torso" or "word art add" or "canvas drop")
            .Select(n => new object[] { n });

    static Action<MatrixRig> NoSelection(Action<MatrixRig> act) => r => { r.S.Select(null); act(r); };

    static Action<MatrixRig> KeysOff(Action<MatrixRig> act) => r => { r.Main.Home.Keys.ToggleCommand.Execute(null); act(r); };

    static readonly Action<MatrixRig> WithLayer = r => r.AddLayer();

    static IReadOnlyDictionary<string, Scenario> BuildMatrix()
    {
        var m = new Dictionary<string, Scenario>();
        void Add(string name, Action<MatrixRig>? setup, Action<MatrixRig> act, params Action<MatrixRig>[] noOps) =>
            m.Add(name, new Scenario(setup, act, noOps));

        // -- images in and layers around --
        void Import(MatrixRig r, Action go) { r.Files.OpenResult = [r.Png]; go(); }
        Add("add image (Add image button)", null, r => Import(r, () => r.Main.Home.File.AddImageCommand.Execute(null)),
            r => { r.Files.OpenResult = []; r.Main.Home.File.AddImageCommand.Execute(null); });
        Add("add image (drop zone)", null, r => r.Main.Layers.DropFiles([r.Png]), r => r.Main.Layers.DropFiles([]));
        Add("canvas drop", null, r => r.Main.Canvas.DropCommand.Execute(new CanvasDrop([r.Png], new Point(300, 300))),
            r => r.Main.Canvas.DropCommand.Execute(new CanvasDrop([], new Point(300, 300))));
        Add("duplicate (row button)", WithLayer, r => r.Main.Layers.Rows[0].DuplicateCommand.Execute(null));
        Add("duplicate (Selected layer panel)", WithLayer, r => r.Main.LayerProperties.DuplicateCommand.Execute(null));
        Add("duplicate (Ctrl+D)", WithLayer, r => r.Keys.DuplicateCommand.Execute(null), NoSelection(r => r.Keys.DuplicateCommand.Execute(null)));
        Add("delete (row button)", WithLayer, r => r.Main.Layers.Rows[0].DeleteCommand.Execute(null));
        Add("delete (Delete key)", WithLayer, r => r.Press(Key.Delete), NoSelection(r => r.Press(Key.Delete)), KeysOff(r => r.Press(Key.Delete)));
        Add("delete (Backspace key)", WithLayer, r => r.Press(Key.Back), NoSelection(r => r.Press(Key.Back)));
        Add("clear layers", WithLayer, r => r.Main.Home.Edit.ClearCommand.Execute(null), r => r.Main.Home.Edit.ClearCommand.Execute(null));
        Add("hide layer", WithLayer, r => r.Main.Layers.Rows[0].ToggleVisibilityCommand.Execute(null));
        Add("reorder (Move down)", r => { r.AddLayer("a"); r.AddLayer("b", 250, 150); },
            r => r.Main.Layers.MoveDownCommand.Execute(null), r => r.Main.Layers.MoveDownCommand.Execute(null));
        Add("reorder (Move up)", r => { int a = r.AddLayer("a"); r.AddLayer("b", 250, 150); r.S.Select(a); },
            r => r.Main.Layers.MoveUpCommand.Execute(null), r => r.Main.Layers.MoveUpCommand.Execute(null));
        Add("reorder (Selected layer panel Move down)", r => { r.AddLayer("a"); r.AddLayer("b", 250, 150); },
            r => r.Main.LayerProperties.MoveDownCommand.Execute(null), r => r.Main.LayerProperties.MoveDownCommand.Execute(null));
        Add("reorder (drag a row)", r => { r.AddLayer("a"); r.AddLayer("b", 250, 150); },
            r => DragRow(r, from: "a", onto: "b", above: true), r => DragRow(r, from: "a", onto: "b", above: true));

        // -- Placement and Presets, Center --
        void Placement(string name, Func<MatrixRig, ICommand> command, bool idempotent)
        {
            var go = (Action<MatrixRig>)(r => command(r).Execute(null));
            if (idempotent) Add(name, WithLayer, go, NoSelection(go), go);
            else Add(name, WithLayer, go, NoSelection(go));
        }
        Placement("placement Fit Front", r => r.Main.Home.Placement.FitFrontCommand, idempotent: true);
        Placement("placement Front + Back", r => r.Main.Home.Placement.FrontBackCommand, idempotent: false);
        Placement("placement Stretch", r => r.Main.Home.Placement.StretchCommand, idempotent: false);
        Placement("placement Allover", r => r.Main.Home.Placement.AlloverCommand, idempotent: false);
        Placement("placement Scatter", r => r.Main.Home.Placement.ScatterCommand, idempotent: false);
        Placement("preset Meme Tee", r => r.Main.Layout.Presets.MemeCommand, idempotent: true);
        Placement("preset Allover", r => r.Main.Layout.Presets.AlloverCommand, idempotent: false);
        Placement("preset Sleeve Pop", r => r.Main.Layout.Presets.SleevePopCommand, idempotent: false);
        Placement("preset Mirror F+B", r => r.Main.Layout.Presets.MirrorCommand, idempotent: false);
        Placement("preset Scatter", r => r.Main.Layout.Presets.ScatterCommand, idempotent: false);
        Placement("preset Fullbleed", r => r.Main.Layout.Presets.FullbleedCommand, idempotent: true);
        Placement("center on front torso", r => r.Main.LayerProperties.CenterFrontCommand, idempotent: true);

        // -- Details and Word Art --
        foreach (string id in DetailIds)
        {
            string detail = id;
            void Click(MatrixRig r) => r.Main.Details.Groups.SelectMany(g => g.Buttons).Single(b => b.Id == detail).Command.Execute(null);
            Add("details " + detail, null, Click, r => { r.Main.Details.Color = "transparent"; Click(r); });
        }
        Add("word art add", null, r => r.Main.Text.AddCommand.Execute(null));

        // -- Background --
        Add("bg swatch", null, r => r.Main.Layout.Background.SetSwatchCommand.Execute("#b71c1c"), r => r.Main.Layout.Background.SetSwatchCommand.Execute("#b71c1c"));
        Add("bg color pick", null, r => { r.Picker.Answer("#336699", "#223344"); r.Main.Layout.Background.PickColorCommand.Execute(null); },
            r => { r.Picker.Answer(null, "#445566"); r.Main.Layout.Background.PickColorCommand.Execute(null); });
        Add("bg opacity", null, r => r.Main.Layout.Background.OpacityPercent = 50, r => r.Main.Layout.Background.OpacityPercent = 50);
        Add("bg opacity drag", null, r => { using (r.Main.Layout.Background.BeginOpacityDrag()) { r.Main.Layout.Background.OpacityPercent = 30; r.Main.Layout.Background.OpacityPercent = 60; } },
            r => { using (r.Main.Layout.Background.BeginOpacityDrag()) r.Main.Layout.Background.OpacityPercent = 60; });
        Add("bg image", null, r => { r.Files.OpenImageResult = r.Png; r.Main.Layout.Background.PickImageCommand.Execute(null); },
            r => { r.Files.OpenImageResult = null; r.Main.Layout.Background.PickImageCommand.Execute(null); });

        // -- Mode, Mask, Grid --
        Add("mode toggle", null, r => r.Main.Home.Mode.ToggleCommand.Execute(null));
        Add("mask", null, r => r.Main.Home.Settings.ShowMask = false, r => r.Main.Home.Settings.ShowMask = false);
        Add("grid", null, r => r.Main.Home.Settings.ShowGrid = true, r => r.Main.Home.Settings.ShowGrid = true);

        // -- Template regions and Fill Style --
        Add("region paint", null, r => r.FrontTile.PaintCommand.Execute(null), r => r.FrontTile.PaintCommand.Execute(null));
        Add("region fit layer", WithLayer, r => r.FrontTile.PaintCommand.Execute(null), r => r.FrontTile.PaintCommand.Execute(null));
        Add("region clear", r => r.FrontTile.PaintCommand.Execute(null), r => r.FrontTile.ClearCommand.Execute(null), r => r.FrontTile.ClearCommand.Execute(null));
        Add("region fill ALL", null, r => r.Main.Layout.Regions.FillAllCommand.Execute(null), r => r.Main.Layout.Regions.FillAllCommand.Execute(null));
        Add("region clear ALL", r => r.Main.Layout.Regions.FillAllCommand.Execute(null), r => r.Main.Layout.Regions.ClearAllCommand.Execute(null),
            r => r.Main.Layout.Regions.ClearAllCommand.Execute(null));
        Action<MatrixRig> painted = r => { r.Main.Layout.FillStyle.Type = "linear"; r.FrontTile.PaintCommand.Execute(null); };
        Add("fill edit type", painted, r => r.Main.Layout.FillStyle.Type = "radial", r => r.Main.Layout.FillStyle.Type = "radial");
        Add("fill edit color pick", painted, r => { r.Picker.Answer("#ff0000", "#00ff00"); r.Main.Layout.FillStyle.PickColor1Command.Execute(null); },
            r => { r.Picker.Answer(null, "#123456"); r.Main.Layout.FillStyle.PickColor1Command.Execute(null); });
        Add("fill edit angle drag", painted, r => { using (r.Main.Layout.FillStyle.BeginSliderDrag()) { r.Main.Layout.FillStyle.Angle = 90; r.Main.Layout.FillStyle.Angle = 135; } },
            r => { using (r.Main.Layout.FillStyle.BeginSliderDrag()) r.Main.Layout.FillStyle.Angle = 135; });
        Add("fill edit scale", painted, r => r.Main.Layout.FillStyle.Scale = 30, r => r.Main.Layout.FillStyle.Scale = 30);
        Add("fill edit add stop", painted, r => r.Main.Layout.FillStyle.AddStopCommand.Execute(null));

        // -- the Selected layer panel --
        Add("flip horizontal", WithLayer, r => r.Main.LayerProperties.FlipHCommand.Execute(null));
        Add("flip vertical", WithLayer, r => r.Main.LayerProperties.FlipVCommand.Execute(null));
        Add("adjust slider drag", WithLayer,
            r => { using (r.Main.LayerProperties.BeginSliderDrag()) { r.Main.LayerProperties.Brightness = 150; r.Main.LayerProperties.Brightness = 170; } },
            r => { using (r.Main.LayerProperties.BeginSliderDrag()) r.Main.LayerProperties.Brightness = 170; });
        Add("reset adjustments", r => { r.AddLayer(); r.Main.LayerProperties.Brightness = 150; },
            r => r.Main.LayerProperties.ResetAdjustmentsCommand.Execute(null), r => r.Main.LayerProperties.ResetAdjustmentsCommand.Execute(null));

        // -- every keyboard transform --
        void Key1(string name, Action<MatrixRig> press, Action<MatrixRig>? setup = null) =>
            Add(name, setup ?? WithLayer, press, NoSelection(press), KeysOff(press));
        Key1("key Left", r => r.Press(Key.Left));
        Key1("key Right", r => r.Press(Key.Right));
        Key1("key Up", r => r.Press(Key.Up));
        Key1("key Down", r => r.Press(Key.Down));
        Key1("key Shift+Left", r => r.Press(Key.Left, ModifierKeys.Shift));
        Key1("key Shift+Right", r => r.Press(Key.Right, ModifierKeys.Shift));
        Key1("key Shift+Up", r => r.Press(Key.Up, ModifierKeys.Shift));
        Key1("key Shift+Down", r => r.Press(Key.Down, ModifierKeys.Shift));
        Key1("key [", r => r.Type("["));
        Key1("key ]", r => r.Type("]"));
        Key1("key {", r => r.Type("{", ModifierKeys.Shift));
        Key1("key }", r => r.Type("}", ModifierKeys.Shift));
        Add("key R", r => { r.AddLayer(); r.Type("]"); }, r => r.Type("r"), r => r.Type("r"), NoSelection(r => r.Type("r")));
        Key1("key +", r => r.Type("+"));
        Key1("key -", r => r.Type("-"));

        // -- the canvas: draw commit and eraser, then move, resize, rotate and wheel bursts --
        foreach (var (name, tool) in new[] { ("pen", Tool.Pen), ("line", Tool.Line), ("rect", Tool.Rect), ("ellipse", Tool.Ellipse) })
        {
            var t = tool;
            void Stroke(MatrixRig r)
            {
                r.Main.Home.Tools.Set(t);
                r.Canvas.PointerDown(new Point(100, 100), 1);
                r.Canvas.PointerMove(new Point(150, 140), 1);
                r.Canvas.PointerUp(new Point(150, 140), 1);
            }
            void Abandon(MatrixRig r)
            {
                r.Main.Home.Tools.Set(t);
                r.Canvas.PointerDown(new Point(300, 300), 1);
                r.Canvas.PointerMove(new Point(340, 340), 1);
                r.Main.Home.Tools.Set(Tool.Select);   // R9: the stroke is discarded
            }
            void Click(MatrixRig r)
            {
                r.Main.Home.Tools.Set(t);
                r.Canvas.PointerDown(new Point(300, 300), 1);
                r.Canvas.PointerUp(new Point(300, 300), 1);
            }
            if (t == Tool.Pen) Add("draw " + name, null, Stroke, Abandon);   // a pen click is a dot, so it is not a no-op
            else Add("draw " + name, null, Stroke, Abandon, Click);
        }
        Action<MatrixRig> eraserOn = r => { r.AddLayer(); r.Main.Home.Tools.Set(Tool.Eraser); };
        Add("eraser", eraserOn,
            r =>
            {
                r.Canvas.PointerDown(new Point(210, 110), 1);
                r.Canvas.PointerMove(new Point(290, 150), 1);
                r.Canvas.PointerUp(new Point(290, 150), 1);
            },
            NoSelection(r => { r.Canvas.PointerDown(new Point(210, 110), 1); r.Canvas.PointerUp(new Point(290, 150), 1); }),
            r =>
            {
                r.Canvas.PointerDown(new Point(-100, -100), 1);   // off the template: nothing erased
                r.Canvas.PointerMove(new Point(-60, -60), 1);
                r.Canvas.PointerUp(new Point(-60, -60), 1);
            });
        Add("move drag", WithLayer,
            r =>
            {
                var c = CenterOf(r.Sel);
                r.Canvas.PointerDown(c, 1);
                r.Canvas.PointerMove(new Point(c.X + 15, c.Y - 10), 1);
                r.Canvas.PointerUp(new Point(c.X + 30, c.Y - 20), 1);
            },
            r => { var c = CenterOf(r.Sel); r.Canvas.PointerDown(c, 1); r.Canvas.PointerUp(c, 1); });
        Add("resize drag", WithLayer,
            r =>
            {
                var corner = CornerOf(r.Sel);
                r.Canvas.PointerDown(corner, 1);
                r.Canvas.PointerMove(new Point(corner.X + 12.5, corner.Y + 7.5), 1);
                r.Canvas.PointerUp(new Point(corner.X + 25, corner.Y + 15), 1);
            },
            r => { var corner = CornerOf(r.Sel); r.Canvas.PointerDown(corner, 1); r.Canvas.PointerUp(corner, 1); });
        Add("rotate drag", WithLayer,
            r =>
            {
                var knob = KnobOf(r.Sel);
                var c = CenterOf(r.Sel);
                r.Canvas.PointerDown(knob, 1);
                r.Canvas.PointerMove(At(c.X, c.Y, 20, 80), 1);
                r.Canvas.PointerUp(At(c.X, c.Y, 50, 80), 1);
            },
            r => { var knob = KnobOf(r.Sel); r.Canvas.PointerDown(knob, 1); r.Canvas.PointerUp(knob, 1); });
        Action<MatrixRig> wheel = r => { for (int i = 0; i < 3; i++) r.Canvas.Wheel(+1); r.Canvas.EndWheelBurst(); };
        Add("wheel burst", WithLayer, wheel, NoSelection(wheel));
        return m;
    }

    static void DragRow(MatrixRig r, string from, string onto, bool above)
    {
        int dragged = r.S.Document.Layers.Single(l => l.Name == from).Id;
        int target = r.S.Document.Layers.Single(l => l.Name == onto).Id;
        r.Main.Layers.BeginRowDrag(dragged);
        r.Main.Layers.DragOverRow(target, above);
        r.Main.Layers.CompleteRowDrop();
    }

    // ---- the undo matrix ----

    [Theory]
    [MemberData(nameof(MatrixRows))]
    public void Every_action_is_exactly_one_undo_step(string action, TemplateMode mode) => StaThread.Run(() =>
    {
        // undo-coverage-gaps, undo-empty-steps (editor.js:501-630, :922-938, :1117, :1327-1394, :1734-1749): the action runs
        // once through the shell, records exactly one step and one undo restores the document; where it has a no-op version,
        // that records nothing and leaves the redo stack alone.
        var scenario = Matrix[action];
        using var r = new MatrixRig(mode, WritePng("matrix.png", 60, 40, Slate));
        var edit = r.Main.Home.Edit;
        scenario.Setup?.Invoke(r);
        var before = r.S.Document;
        int steps = r.S.UndoCount;

        scenario.Act(r);
        Assert.Equal(steps + 1, r.S.UndoCount);
        var after = r.S.Document;
        Assert.False(after.ContentEquals(before), "the action recorded a step but changed nothing");

        Assert.True(edit.UndoCommand.CanExecute(null));
        edit.UndoCommand.Execute(null);            // what Ctrl+Z runs
        Assert.Equal(steps, r.S.UndoCount);
        Assert.True(r.S.Document.ContentEquals(before), "one undo did not restore the document");
        Assert.True(edit.RedoCommand.CanExecute(null));
        edit.RedoCommand.Execute(null);
        Assert.Equal(steps + 1, r.S.UndoCount);
        Assert.True(r.S.Document.ContentEquals(after), "redo did not bring the action back");

        foreach (var noOp in scenario.NoOps)
        {
            // From the state after the action (a repeat of the action is a no-op there; the rest are no-ops anywhere).
            int undos = r.S.UndoCount;
            var current = r.S.Document;
            noOp(r);
            Assert.Equal(undos, r.S.UndoCount);
            Assert.True(r.S.Document.ContentEquals(current), "a no-op changed the document");
        }
    });

    // ---- what undo never restores ----

    sealed record SettingCase(Action<MatrixRig> Change, Func<MatrixRig, object?> Read, object? Expected);

    static readonly IReadOnlyDictionary<string, SettingCase> Settings = new Dictionary<string, SettingCase>
    {
        ["tool"] = new(r => r.Main.Home.Tools.Set(Tool.Ellipse), r => r.Main.Home.Tools.Current, Tool.Ellipse),
        ["zoom"] = new(r => { r.Main.Canvas.ZoomInCommand.Execute(null); r.Main.Canvas.ZoomInCommand.Execute(null); }, r => r.Main.Canvas.Zoom, 1.5),
        ["brush size"] = new(r => r.Main.Home.Brush.Size = 17, r => r.Main.Home.Brush.Size, 17),
        ["brush color"] = new(r => r.Main.Home.Brush.Color = "#123456", r => r.Main.Home.Brush.Color, "#123456"),
        ["brush fill"] = new(r => r.Main.Home.Brush.Fill = true, r => r.Main.Home.Brush.Fill, true),
        ["fill style type"] = new(r => r.Main.Layout.FillStyle.Type = "checker", r => r.Main.Layout.FillStyle.Type, "checker"),
        ["fill style colors"] = new(r => { r.Main.Layout.FillStyle.Color1 = "#102030"; r.Main.Layout.FillStyle.Color2 = "#405060"; },
            r => (r.Main.Layout.FillStyle.Color1, r.Main.Layout.FillStyle.Color2), ("#102030", "#405060")),
        ["fill style angle and scale"] = new(r => { r.Main.Layout.FillStyle.Angle = 77; r.Main.Layout.FillStyle.Scale = 22; },
            r => (r.Main.Layout.FillStyle.Angle, r.Main.Layout.FillStyle.Scale), (77.0, 22.0)),
        ["detail color"] = new(r => r.Main.Details.Color = "#ff8800", r => r.Main.Details.Color, "#ff8800"),
        ["detail width and stitch"] = new(r => { r.Main.Details.Width = 6; r.Main.Details.Stitch = true; },
            r => (r.Main.Details.Width, r.Main.Details.Stitch), (6, true)),
        ["word art text"] = new(r => r.Main.Text.Text = "HELLO", r => r.Main.Text.Text, "HELLO"),
        ["word art font and style"] = new(r => { r.Main.Text.Font = r.Main.Text.Fonts[3].Family; r.Main.Text.Style = WordArtStyle.Neon; },
            r => (r.Main.Text.Font, r.Main.Text.Style), (WordArt.FontOptions[3].Family, WordArtStyle.Neon)),
        ["word art size and colors"] = new(r => { r.Main.Text.Size = 120; r.Main.Text.FillColor = "#aa0000"; r.Main.Text.StrokeColor = "#00aa00"; r.Main.Text.GradColor = "#0000aa"; },
            r => (r.Main.Text.Size, r.Main.Text.FillColor, r.Main.Text.StrokeColor, r.Main.Text.GradColor), (120.0, "#aa0000", "#00aa00", "#0000aa")),
        ["word art stroke width and angle"] = new(r => { r.Main.Text.StrokeWidth = 9; r.Main.Text.Angle = 200; },
            r => (r.Main.Text.StrokeWidth, r.Main.Text.Angle), (9, 200)),
        ["template title"] = new(r => r.Main.Home.Settings.TemplateName = "My Shirt", r => r.Main.Home.Settings.TemplateName, "My Shirt"),
    };

    public static IEnumerable<object[]> SettingRows() =>
        Settings.Keys.SelectMany(name => new[] { TemplateMode.Shirt, TemplateMode.Pants }.Select(mode => new object[] { name, mode }));

    [Theory]
    [MemberData(nameof(SettingRows))]
    public void Undo_and_redo_never_restore_a_setting_that_is_not_in_the_document(string setting, TemplateMode mode) => StaThread.Run(() =>
    {
        // file-undo-snapshot-scope: "NOT captured: tool, zoom, brush/fill/detail/word-art settings, template name". The setting changes
        // between two edits (one of them a region paint, so a Fill Style change retargets it); every undo and every redo leaves it.
        var c = Settings[setting];
        using var r = new MatrixRig(mode, WritePng("matrix.png", 60, 40, Slate));
        r.AddLayer();
        r.FrontTile.PaintCommand.Execute(null);   // with a layer selected this fits it; either way a step
        var before = r.S.Document;
        r.Main.Layout.Background.SetSwatchCommand.Execute("#b71c1c");
        c.Change(r);
        Assert.Equal(c.Expected, c.Read(r));
        r.Main.Layout.Background.SetSwatchCommand.Execute("#0c2461");
        var edited = r.S.Document;

        Assert.True(r.S.Undo());
        Assert.Equal(c.Expected, c.Read(r));
        while (r.S.CanUndo) Assert.True(r.S.Undo());
        Assert.Equal(c.Expected, c.Read(r));
        Assert.False(r.S.Document.ContentEquals(edited));
        while (r.S.CanRedo) Assert.True(r.S.Redo());
        Assert.Equal(c.Expected, c.Read(r));
        Assert.True(r.S.Document.ContentEquals(edited));
        Assert.NotNull(before);
    });

    [Fact]
    public void Undoing_a_fill_style_edit_puts_the_regions_back_and_leaves_Fill_Style_where_it_is() => StaThread.Run(() =>
    {
        // file-undo-snapshot-scope: the edit that retargets painted regions is a step; the setting it changed is not part of that step.
        using var r = new MatrixRig(TemplateMode.Shirt, WritePng("matrix.png", 60, 40, Slate));
        var fillStyle = r.Main.Layout.FillStyle;
        fillStyle.Type = "linear";
        r.FrontTile.PaintCommand.Execute(null);
        var painted = r.S.Document;

        fillStyle.Type = "stripes";                 // retargets the painted region: one step
        Assert.Equal(2, r.S.UndoCount);
        Assert.Equal("stripes", DocumentOps.RegionFill(r.S.Document, r.FrontTile.Region)!.Type);

        Assert.True(r.S.Undo());
        Assert.True(r.S.Document.ContentEquals(painted));
        Assert.Equal("linear", DocumentOps.RegionFill(r.S.Document, r.FrontTile.Region)!.Type);
        Assert.Equal("stripes", fillStyle.Type);    // the control did not follow the document back
        Assert.Equal("stripes", fillStyle.Current.Type);
    });

    // ---- the Pants matrix ----

    [Theory]
    [MemberData(nameof(PantsRows))]
    public void Pants_mode_runs_every_layer_action_without_an_exception(string action) => StaThread.Run(() =>
    {
        // bug-pants-mode-region-keys, mode-pants-crash-hardcoded-keys (editor.js:502, :855, :1328, :1339, :1741-1744, :1869-2060, :2272):
        // 4.1 threw on all of these in Pants mode after pushing an undo entry. v5 resolves regions through semantic slots.
        var scenario = Matrix[action];
        using var r = new MatrixRig(TemplateMode.Pants, WritePng("matrix.png", 60, 40, Slate));
        scenario.Setup?.Invoke(r);
        var before = r.S.Document;
        int steps = r.S.UndoCount;

        scenario.Act(r);                             // no exception

        Assert.Equal(TemplateMode.Pants, r.S.Document.Mode);
        Assert.Equal(steps + 1, r.S.UndoCount);      // and no dangling step: one real step, as 4.1's failed click was not
        Assert.False(r.S.Document.ContentEquals(before));
        Assert.NotEmpty(Renderer.ExportPng(r.S.Document));   // the result renders
    });
}
