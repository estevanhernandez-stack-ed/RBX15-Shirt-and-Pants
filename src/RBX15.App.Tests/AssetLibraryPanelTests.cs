using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The asset library panel (editor.html:291-309; editor.js:2452-2545, :2713-2743; styles.css:649-673). WPF ignores
/// posted mouse input and the rules forbid synthetic desktop input, so the drag and the resize run through the
/// Handle* seams the overrides forward to, with points taken from the laid-out tree. Standalone panels are laid out
/// at 251 DIP, the 280 panel's content width (280 - 2 x 14 padding - the 1 px right border, styles.css:246-253); the
/// canvas drops run in the whole main window laid out off-screen at the 1184x761 client, as ShellLookTests does.
/// </summary>
[Collection("MainWindow")] // builds MainWindow, like ShellTests and ShellLookTests
public class AssetLibraryPanelTests
{
    const double PanelWidth = 251;

    static AssetLibraryViewModel Vm(EditorSession session, string? packs = null, FakeDialogService? dialogs = null) =>
        new(new ImportService(session, new FakeStatus()), new FakeFileDialogs(), dialogs ?? new FakeDialogService(), new FakeStatus(),
            new FakeSettingsStore(), new PackSource(packs ?? PackSource.DefaultDirectory), new AssetThumbnailLoader());

    /// <summary>
    /// A standalone panel has no window, so its display scale is the system's unless a test sets it. The facts that depend
    /// on the scale run as theories at 96, 120 and 144 DPI (100%, 125%, 150%), set as the panel's root DPI after the first
    /// layout built its template (DeviceRounding.SetRootDpi), and kept on every tile a later layout realizes (Layout).
    /// </summary>
    static AssetLibraryPanel Panel(AssetLibraryViewModel vm, double dpi = 96)
    {
        var panel = new AssetLibraryPanel { DataContext = vm };
        Layout(panel);
        DeviceRounding.SetRootDpi(panel, dpi);
        Layout(panel);
        Assert.Equal(dpi / 96, DeviceRounding.ScaleOf(panel.AssetBrowser));
        return panel;
    }

    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        for (int pass = 0; pass < 2; pass++)
        {
            e.Measure(new Size(PanelWidth, 2000));
            e.Arrange(new Rect(0, 0, PanelWidth, Math.Max(e.DesiredSize.Height, 1)));
            e.UpdateLayout();
            DeviceRounding.Propagate(e);   // tiles made by this pass start at the default scale; give them the panel's
        }
    }

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    static List<Button> Tiles(DependencyObject root) => Descendants<Button>(root).Where(b => b.Name == "AssetTile").ToList();

    static Point Center(FrameworkElement e, Visual relativeTo) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight / 2), (UIElement)relativeTo);

    [Fact] // panel-asset-library: 4.1's heading, labels, buttons and tooltips; the browser is hidden until a load, the handle is not
    public void The_panel_shows_4_1s_controls_and_the_browser_only_after_a_load() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        var panel = Panel(vm);
        string Text(string name) => ((TextBlock)panel.FindName(name)).Text;
        (string, string) Btn(string name) => (((Button)panel.FindName(name)).Content.ToString()!, ((Button)panel.FindName(name)).ToolTip.ToString()!);
        Assert.Equal(("Asset library", "BUILT-IN PACKS", "LOAD FOLDER"), (Text("AssetHeading"), Text("PacksLabel"), Text("FolderLabel")));
        Assert.Equal(("Drips", "Load the bundled blood / liquid drip pack."), Btn("DripsButton"));
        Assert.Equal(("Word art", "Load the bundled word art pack."), Btn("WordArtButton"));
        Assert.Equal(("Open folder", "Pick a folder of PNGs to browse."), Btn("OpenFolderButton"));
        Assert.Equal(Visibility.Collapsed, panel.AssetBrowser.Visibility);
        Assert.Equal(Visibility.Collapsed, panel.CountLabel.Visibility);
        Assert.Equal((Visibility.Visible, "Drag to resize"), (panel.ResizeHandle.Visibility, panel.ResizeHandle.ToolTip));   // shown even while hidden (quirk, keep)
        var drips = (Button)panel.FindName("DripsButton");
        var words = (Button)panel.FindName("WordArtButton");
        Assert.Equal(drips.ActualWidth, words.ActualWidth, 1);                 // .row > * { flex: 1 }
        Assert.Equal(PanelWidth, drips.ActualWidth + words.ActualWidth + 4, 1); // gap 4px

        vm.LoadDripsCommand.Execute(null);
        Layout(panel);
        Assert.Equal(Visibility.Visible, panel.AssetBrowser.Visibility);
        Assert.Equal("156 images in built-in drips pack", panel.CountLabel.Text);
        Assert.Equal(Visibility.Collapsed, panel.GroupButtonsPanel.Visibility);   // a pack has no subfolder buttons
        Assert.Equal(180, panel.AssetBrowser.ActualHeight, 1);                    // max-height 180px with 52 rows of content
    });

    [Fact] // asset-grid-thumbnails: 3 square columns with a 4 px gap, 1px #333 border, #111, a 7px #ccc caption; R5 virtualizes rows
    public void Tiles_are_square_three_to_a_row_and_only_visible_rows_are_realized_and_decoded() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        vm.LoadDripsCommand.Execute(null);
        var panel = Panel(vm);
        var tiles = Tiles(panel);
        var scroller = Descendants<ScrollViewer>(panel.AssetGrid).Single();
        Assert.Equal(PanelWidth - 8, scroller.ViewportWidth, 3);  // the 8 DIP thin scrollbar takes its width, as overflow-y: auto did
        double side = (PanelWidth - 8 - 2 * 4) / 3;              // 78.33 DIP
        Assert.All(tiles, t => Assert.Equal((side, side), (t.ActualWidth, t.ActualHeight)));
        var row = tiles.Take(3).Select(t => t.TranslatePoint(new Point(0, 0), panel).X).ToArray();
        Assert.Equal([0, side + 4, 2 * (side + 4)], row);
        var next = tiles[3].TranslatePoint(new Point(0, 0), panel).Y - tiles[0].TranslatePoint(new Point(0, 0), panel).Y;
        Assert.Equal(side + 4, next, 3);

        int realized = Enumerable.Range(0, vm.Rows.Count).Count(i => panel.AssetGrid.ItemContainerGenerator.ContainerFromIndex(i) is not null);
        Assert.InRange(realized, 3, 8);                          // the 180 DIP view's rows (3 measured), never all 52
        Assert.Equal(realized * 3, tiles.Count);
        var requested = vm.Rows.SelectMany(r => r.Tiles).Count(t => t.ThumbnailRequested);
        Assert.Equal(tiles.Count, requested);                    // only realized tiles ever asked for a decode

        var border = (Border)VisualTreeHelper.GetChild(tiles[0], 0);
        Assert.Equal(("#FF111111", "#FF333333", 1.0, 4.0), (((SolidColorBrush)border.Background).Color.ToString(),
            ((SolidColorBrush)border.BorderBrush).Color.ToString(), border.BorderThickness.Left, border.CornerRadius.TopLeft));
        var caption = Descendants<TextBlock>(tiles[0]).Single(t => t.Name == "CaptionText");
        Assert.Equal((7.0, "#FFCCCCCC", TextTrimming.CharacterEllipsis, "drip_blood_front_flipped"),
            (caption.FontSize, ((SolidColorBrush)caption.Foreground).Color.ToString(), caption.TextTrimming, caption.Text));
        Assert.Equal(("drip_blood_front_flipped.png", "drip_blood_front_flipped"), (tiles[0].ToolTip, AutomationProperties.GetName(tiles[0])));
    });

    [Fact] // asset-folder-grouping: 9px buttons labelled All/Root/name (n), title = key, the one on show #2d4a2d on #7fff7f
    public void Group_buttons_show_their_labels_and_highlight_the_group_on_show() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        foreach (var rel in new[] { "art/top.png", "art/s/1.png", "art/t/2.png" })
        {
            string path = Path.Combine(files.Dir, rel.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(files.Image("src.png", 4, 4, SKColors.Red), path);
        }
        var session = new EditorSession();
        var dialogs = new FakeFileDialogs { PickFolderResult = Path.Combine(files.Dir, "art") };
        var vm = new AssetLibraryViewModel(new ImportService(session, new FakeStatus()), dialogs, new FakeDialogService(), new FakeStatus(),
            new FakeSettingsStore(), new PackSource(files.Dir), new AssetThumbnailLoader());
        vm.OpenFolderCommand.Execute(null);
        var panel = Panel(vm);
        var buttons = Descendants<Button>(panel.GroupButtonsPanel).ToList();
        Assert.Equal(["All (3)", "Root (1)", "s (1)", "t (1)"], buttons.Select(b => (string)b.Content));
        Assert.Equal(["(all)", "(root)", "s", "t"], buttons.Select(b => (string)b.ToolTip));
        Assert.All(buttons, b => Assert.Equal((9.0, new Thickness(6, 2, 6, 2)), (b.FontSize, b.Padding)));
        string Look(Button b) => (b.Background is SolidColorBrush bg ? bg.Color.ToString() : "gradient") + "/" + ((SolidColorBrush)b.Foreground).Color;
        Assert.Equal("#FF2D4A2D/#FF7FFF7F", Look(buttons[0]));
        Assert.NotEqual("#FF2D4A2D/#FF7FFF7F", Look(buttons[2]));
        buttons[2].Command.Execute(buttons[2].CommandParameter);
        Layout(panel);
        Assert.Equal("#FF2D4A2D/#FF7FFF7F", Look(buttons[2]));
        Assert.NotEqual("#FF2D4A2D/#FF7FFF7F", Look(buttons[0]));
        Assert.Equal("1 images in s", panel.CountLabel.Text);
        Assert.Equal("1", AutomationProperties.GetName(Assert.Single(Tiles(panel))));
    });

    [Fact] // asset-click-add: a tile is a button; Invoke (a click, Enter, Space, UI Automation) adds it at 85%, one step
    public void Invoking_a_tile_adds_it_fit_in_the_front_slot() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var tile = Tiles(panel)[0];
        ((IInvokeProvider)new ButtonAutomationPeer(tile).GetPattern(PatternInterface.Invoke)).Invoke();
        StaThread.Pump();
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal(("word_626_anton", 0.5494949494949495), (l.Name, l.Scale));   // 198x125: min(128/198, 128/125) * 0.85, as node printed it
        Assert.Equal(1, session.UndoCount);
    });

    [Fact] // asset-rightclick-full: the right button's release adds at full size and is handled, so no context menu opens
    public void A_right_click_on_a_tile_adds_it_at_full_size_and_is_handled() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var tile = Tiles(panel)[0];
        var e = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent };
        tile.RaiseEvent(e);
        Assert.True(e.Handled);
        var l = Assert.Single(session.Document.Layers);
        Assert.Equal(("word_626_anton (full)", 0.0, 0.0, 4.472), (l.Name, l.X, l.Y, l.Scale));   // 198x125: max(585/198, 559/125), as node printed it
        Assert.Equal(1, session.UndoCount);
        Assert.False(panel.IsDragging);
    });

    [Theory] // R6: only a move past the system distance starts a drag; a release off the canvas adds nothing and clears the state
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    public void A_drag_starts_past_the_distance_and_a_release_off_the_canvas_adds_nothing(double dpi) => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm, dpi);
        var tile = vm.Rows[0].Tiles[0];
        var at = Center(Tiles(panel)[0], panel);
        panel.HandleTilePress(tile, at);
        // Exactly the distance away, which is still a click. The system distance is 3.2 DIPs at 125%, so "at.X + 3.2" can sit an ulp
        // past it; AtMost puts the probe on the boundary without going over (not a tolerance).
        panel.HandleMove(new Point(DeviceRounding.AtMost(at.X, SystemParameters.MinimumHorizontalDragDistance), at.Y), leftButtonDown: true);
        Assert.False(panel.IsDragging);                                       // within the distance: still a click
        panel.HandleMove(new Point(at.X + 20, at.Y + 20), leftButtonDown: true);
        Assert.True(panel.IsDragging);
        Assert.Equal(Cursors.No, panel.Cursor);                               // no canvas in this tree
        Assert.False(panel.HandleRelease(new Point(at.X + 20, at.Y + 20)));
        Assert.False(panel.IsDragging);
        Assert.Null(panel.ReadLocalValue(FrameworkElement.CursorProperty) as Cursor);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    });

    [Theory] // R6: Esc, a lost capture, a move with the button up, or a new press each end a drag with nothing added
    [InlineData("escape")]
    [InlineData("capture")]
    [InlineData("buttonup")]
    [InlineData("press")]
    public void Every_cancel_ends_the_drag_with_nothing_added(string how) => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var vm = Vm(session);
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        var at = Center(Tiles(panel)[0], panel);
        panel.HandleTilePress(vm.Rows[0].Tiles[0], at);
        panel.HandleMove(new Point(at.X + 30, at.Y), leftButtonDown: true);
        Assert.True(panel.IsDragging);
        switch (how)
        {
            case "escape": Assert.True(panel.HandleKey(Key.Escape)); break;
            case "capture": panel.HandleCaptureLost(); break;
            case "buttonup": panel.HandleMove(new Point(at.X + 40, at.Y), leftButtonDown: false); break;
            case "press": panel.HandleTilePress(null, at); break;
        }
        Assert.False(panel.IsDragging);
        Assert.False(panel.HandleRelease(at));
        Assert.False(panel.HandleKey(Key.Escape));                           // with no drag, Esc is left to others
        Assert.Empty(session.Document.Layers);
    });

    [Fact] // a press off every tile arms nothing (the handle, the buttons, the empty grid)
    public void A_press_off_the_tiles_never_drags() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        vm.LoadWordArtCommand.Execute(null);
        var panel = Panel(vm);
        Assert.Null(AssetLibraryPanel.TileFrom((Button)panel.FindName("DripsButton")));
        Assert.Null(AssetLibraryPanel.TileFrom(panel.ResizeBar));
        var caption = Descendants<TextBlock>(Tiles(panel)[1]).Single(t => t.Name == "CaptionText");
        Assert.Same(vm.Rows[0].Tiles[1], AssetLibraryPanel.TileFrom(caption));   // a press on the caption is a press on the tile
        panel.HandleTilePress(null, new Point(5, 5));
        panel.HandleMove(new Point(100, 100), leftButtonDown: true);
        Assert.False(panel.IsDragging);
    });

    [Theory] // asset-resizable-browser, panel-asset-resize-handle: 1:1 from the rendered height, clamped to 80 and 70% of the client
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    public void The_handle_resizes_the_browser_from_its_rendered_height_within_the_clamps(double dpi) => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession());
        var panel = Panel(vm, dpi);
        panel.HandleResizePress();                       // hidden: offsetHeight is 0, so it starts from 180 (editor.js:2724)
        panel.HandleResize(20);
        Assert.Equal(200, vm.BrowserHeight);
        panel.HandleResizeEnd();
        Assert.False(panel.IsResizing);

        vm.LoadDripsCommand.Execute(null);
        vm.BrowserHeight = 180;
        Layout(panel);
        panel.HandleResizePress();
        panel.HandleResize(100);
        Layout(panel);
        Assert.Equal((280.0, 280.0), (vm.BrowserHeight, Math.Round(panel.AssetBrowser.ActualHeight, 3)));
        panel.HandleResize(-1000);
        Assert.Equal(80, vm.BrowserHeight);
        panel.HandleResize(-40);
        Assert.Equal(140, vm.BrowserHeight);              // from the press, not from the last move
        panel.HandleResizeEnd();
        panel.HandleResize(300);                          // no press: nothing
        Assert.Equal(140, vm.BrowserHeight);
    });

    [Fact] // asset-resizable-browser parityCheck: "cursor shows ns-resize; after release no stuck cursor"
    public void The_handle_shows_the_ns_resize_cursor_and_restores_it_after_release() => StaThread.Run(() =>
    {
        var panel = Panel(Vm(new EditorSession()));
        var handle = panel.ResizeHandle;
        Assert.Equal(Cursors.SizeNS, handle.Cursor);                       // over the handle, before any press (4.1: cursor ns-resize)

        MouseButtonEventArgs Button(RoutedEvent routed) =>
            new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routed, Source = handle };
        handle.RaiseEvent(Button(UIElement.MouseLeftButtonDownEvent));
        Assert.True(panel.IsResizing);
        Assert.Equal(Cursors.SizeNS, handle.Cursor);                       // the whole drag: the handle's own cursor
        Assert.Null(Mouse.OverrideCursor);                                 // 4.1 set the body's cursor; WPF needs no document-wide override that could stick
        Assert.Null(panel.ReadLocalValue(FrameworkElement.CursorProperty) as Cursor);

        handle.RaiseEvent(Button(UIElement.MouseLeftButtonUpEvent));
        Assert.False(panel.IsResizing);
        Assert.Equal(Cursors.SizeNS, handle.Cursor);                       // still the handle's, and nothing else holds a cursor after the release
        Assert.Null(Mouse.OverrideCursor);
        Assert.Null(panel.ReadLocalValue(FrameworkElement.CursorProperty) as Cursor);
        Assert.False(handle.IsMouseCaptured);
    });

    [Fact] // with only a few thumbnails, max-height changes nothing visible: the drag starts from the rendered height (quirk, keep)
    public void A_short_grid_resizes_from_its_rendered_height() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        Directory.CreateDirectory(Path.Combine(files.Dir, "drips"));
        File.Copy(files.Image("one.png", 4, 4, SKColors.Red), Path.Combine(files.Dir, "drips", "one.png"));
        var vm = Vm(new EditorSession(), files.Dir);
        vm.LoadDripsCommand.Execute(null);
        var panel = Panel(vm);
        double rendered = panel.AssetBrowser.ActualHeight;
        Assert.Equal(85, rendered, 3);                    // one 81 DIP row (no scrollbar) and its 4 DIP row gap, under the 180 max
        panel.HandleResizePress();
        panel.HandleResize(10);
        Assert.Equal(rendered + 10, vm.BrowserHeight, 6);
    });

    // ---- the canvas drop, in the whole window ----

    sealed record Shell(MainViewModel Main, EditorSession Session, FrameworkElement Root, AssetLibraryPanel Panel, TemplateCanvasHost Host);

    /// <summary>The window is built but never shown, so a test may set its root DPI (a shown window takes its monitor's).</summary>
    static Shell InWindow(double dpi = 96)
    {
        var session = new EditorSession();
        var main = TestShell.Main(session);
        var w = new MainWindow(main, new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        main.Assets.LoadWordArtCommand.Execute(null);
        var root = (FrameworkElement)w.Content;
        Lay(root);
        DeviceRounding.SetRootDpi(root, dpi);   // the window was never shown, so its content is the visual root
        Lay(root);
        Assert.Equal(dpi / 96, DeviceRounding.ScaleOf(root));
        return new Shell(main, session, root, Descendants<AssetLibraryPanel>(root).Single(), Descendants<TemplateCanvasHost>(root).Single());
    }

    static void Lay(FrameworkElement root)
    {
        StaThread.Pump();
        for (int pass = 0; pass < 2; pass++)
        {
            root.Measure(new Size(1184, 761));
            root.Arrange(new Rect(0, 0, 1184, 761));
            root.UpdateLayout();
            DeviceRounding.Propagate(root);
        }
    }

    static void StartDrag(Shell s)
    {
        var at = Center(Tiles(s.Panel)[0], s.Panel);
        s.Panel.HandleTilePress(s.Main.Assets.Rows[0].Tiles[0], at);
        s.Panel.HandleMove(new Point(at.X + 30, at.Y + 30), leftButtonDown: true);
        Assert.True(s.Panel.IsDragging);
    }

    [Theory] // asset-drag-to-canvas parityCheck: dropped at template (300, 200) at zoom 1 and 2, the layer centers there; one undo restores
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void A_drag_onto_the_canvas_adds_the_tile_centered_on_the_drop_point(double zoom) => StaThread.Run(() =>
    {
        var s = InWindow();
        s.Main.Canvas.Zoom = zoom;
        Lay(s.Root);
        var inHost = CanvasCoordinates.ToFrame(new Point(300, 200), s.Host.Zoom);
        var inRoot = s.Host.TranslatePoint(inHost, s.Root);
        StartDrag(s);
        s.Panel.HandleMove(s.Root.TranslatePoint(inRoot, s.Panel), leftButtonDown: true);
        Assert.Equal(Cursors.Hand, s.Panel.Cursor);                          // over the canvas (R6, amended)
        Assert.True(s.Panel.HandleRelease(inRoot));
        var l = Assert.Single(s.Session.Document.Layers);
        double scale = 0.5494949494949495;                                    // word_626_anton is 198x125
        Assert.Equal(("word_626_anton", 300.0, 200.0), (l.Name, Math.Round(l.X + 198 * scale / 2, 9), Math.Round(l.Y + 125 * scale / 2, 9)));
        Assert.Equal(1, s.Session.UndoCount);
        Assert.True(s.Session.Undo());
        Assert.Empty(s.Session.Document.Layers);
        Assert.False(s.Panel.IsDragging);
    });

    [Fact] // asset-drag-stale-reference parityCheck: drop a thumbnail on the layer list, then an OS file on the canvas: the OS file is added
    public void A_drag_dropped_elsewhere_never_hijacks_the_next_os_file_drop() => StaThread.Run(() =>
    {
        using var files = new TestFiles();
        var s = InWindow();
        var layers = Descendants<LayersPanel>(s.Root).Single();
        var overLayers = layers.TranslatePoint(new Point(40, 30), s.Root);
        StartDrag(s);
        s.Panel.HandleMove(s.Root.TranslatePoint(overLayers, s.Panel), leftButtonDown: true);
        Assert.Equal(Cursors.No, s.Panel.Cursor);
        Assert.False(s.Panel.HandleRelease(overLayers));
        Assert.Empty(s.Session.Document.Layers);

        var data = new DataObject(DataFormats.FileDrop, new[] { files.Image("os.png", 20, 20, SKColors.Blue) });
        Assert.True(s.Host.HandleDrop(data, new Point(100, 100)));
        Assert.Equal(["os"], s.Session.Document.Layers.Select(l => l.Name));
    });

    [Fact] // a disabled canvas (Open running, interfaces R4) and the zoom pill over it take no drop
    public void A_disabled_canvas_or_the_zoom_pill_takes_no_drop() => StaThread.Run(() =>
    {
        var s = InWindow();
        var zoomIn = Descendants<Button>(s.Root).Single(b => b.Name == "ZoomInButton");
        StartDrag(s);
        Assert.False(s.Panel.HandleRelease(Center(zoomIn, s.Root)));
        s.Host.IsEnabled = false;
        StartDrag(s);
        var inRoot = s.Host.TranslatePoint(new Point(50, 50), s.Root);
        Assert.False(s.Panel.HandleRelease(inRoot));
        Assert.Empty(s.Session.Document.Layers);
    });

    /// <summary>A PresentationSource for key events raised in a window that was never shown (KeyEventArgs requires one).</summary>
    sealed class NoSource : PresentationSource
    {
        public override Visual RootVisual { get => null!; set { } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    [Fact] // critic M8 (T2-R14): Esc cancels a running drag from anywhere in the window, not only with keyboard focus in the panel
    public void Esc_anywhere_in_the_window_cancels_a_drag() => StaThread.Run(() =>
    {
        var s = InWindow();
        StartDrag(s);
        var esc = new KeyEventArgs(Keyboard.PrimaryDevice, new NoSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        s.Root.RaiseEvent(esc);                                              // the window's content: the route never passes through the panel
        Assert.True(esc.Handled);
        Assert.False(s.Panel.IsDragging);
        Assert.False(s.Panel.HandleRelease(s.Host.TranslatePoint(new Point(50, 50), s.Root)));
        Assert.Empty(s.Session.Document.Layers);
        var again = new KeyEventArgs(Keyboard.PrimaryDevice, new NoSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        s.Root.RaiseEvent(again);
        Assert.False(again.Handled);                                         // the handler left with the drag
    });

    [Theory] // the resize's upper clamp is 70% of the window's client height (window.innerHeight * 0.7, editor.js:2732)
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    public void In_the_window_the_resize_clamps_at_70_percent_of_the_client(double dpi) => StaThread.Run(() =>
    {
        var s = InWindow(dpi);
        s.Panel.HandleResizePress();
        s.Panel.HandleResize(5000);
        // The client is 761 DIPs asked for, and layout rounding gives it the nearest whole number of device pixels
        // (760.8 at 125%): the clamp is 70% of the height the root really has.
        double client = DeviceRounding.Snap(761, dpi / 96);
        Assert.Equal(client, s.Root.ActualHeight, 9);
        Assert.Equal(0.7 * client, s.Main.Assets.BrowserHeight, 9);
        s.Panel.HandleResizeEnd();
    });
}
