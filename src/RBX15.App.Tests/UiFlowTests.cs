using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using RBX15.App.Canvas;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The thin UI flow suite (spec §6 Tier 3; M5/M6 decision U1; ledger parity-ui-automation-suite) and the window rows
/// (shell-window-size, shell-window-frame). Each fact builds the real MainWindow through Composition.Build over fake
/// dialogs, a fake clipboard and a temp folder, shows it far off-screen on the STA thread and drives it only through
/// UI Automation patterns (<see cref="UiaKit"/>) and the window's own KeyBindings: no mouse, no keyboard device, no
/// synthetic desktop input, no app launch. The native file dialogs are the controller's smoke; here the fakes answer them. Every fact
/// closes its window and writes only under its own temp folders.
/// </summary>
[Collection("MainWindow")] // shows windows on the shared STA thread, like ShellTests and CanvasLayerAutomationTests
public class UiFlowTests
{
    /// <summary>A work area no monitor covers, so the window never appears on the developer's desktop.</summary>
    static readonly Rect OffScreen = new(-12000, -12000, 1920, 1040);

    /// <summary>One shown window over fakes, with the temp folders its files and autosave root live in.</summary>
    sealed class Rig : IDisposable
    {
        public TestFiles Files { get; } = new();
        public AppStorage Storage { get; } = TestShell.Storage();
        public FakeFileDialogs FileDialogs { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public FakeClipboardSource Clipboard { get; } = new();
        public FakeAppLauncher Launcher { get; } = new();
        public EditorSession Session { get; } = new();
        public MainViewModel Main { get; }
        public MainWindow Window { get; }
        public AutomationPeer Root { get; }

        public Rig()
        {
            var settings = new FakeSettingsStore();
            Main = TestShell.Quiet(Composition.Build(Session, FileDialogs, Dialogs, new FakeColorPicker(), settings, Storage,
                new ManualTickSource(), new FakeProcessProbe(), random: () => new Random(1), jumpList: new FakeJumpListSink(), launcher: Launcher,
                clipboard: Clipboard, clipboardDelay: _ => { }));
            Window = new MainWindow(Main, settings, [OffScreen]) { ShowActivated = false, ShowInTaskbar = false };
            Window.FocusedElement = () => null;   // the unactivated window has no keyboard focus; a KeyBinding sees "no text field"
            Window.Show();
            StaThread.Pump();
            Root = UiaKit.PeerOf(Window);
        }

        /// <summary>The element named <paramref name="name"/> in the window's UIA tree.</summary>
        public AutomationPeer El(string name, AutomationControlType? type = null) => UiaKit.Find(Root, name: name, type: type);

        public void Click(string name)
        {
            UiaKit.Invoke(El(name));
            StaThread.Pump();
        }

        /// <summary>Picks a ribbon tab by its UIA name ("Home tab", "Background tab", "Details tab", "Text tab").</summary>
        public void Tab(string name)
        {
            UiaKit.Select(El(name));
            StaThread.Pump();
        }

        /// <summary>Ctrl+V as the window sees it: its KeyBinding for the chord, run behind the text-field guard.</summary>
        public void CtrlV()
        {
            var binding = Assert.Single(Window.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.V && b.Modifiers == ModifierKeys.Control);
            Assert.True(binding.Command.CanExecute(binding.CommandParameter));
            binding.Command.Execute(binding.CommandParameter);
            StaThread.Pump();
        }

        public string Png(string name, int w = 300, int h = 120, SKColor? color = null) => Files.Image(name, w, h, color ?? SKColors.Red);

        /// <summary>The peers inside the Recent drop-down's popup, which the window's own peer tree does not reach.</summary>
        public IReadOnlyList<AutomationPeer> RecentRoots() =>
            UiaKit.RootsWithin(((System.Windows.Controls.Primitives.Popup)First<FileGroup>(Window).FindName("RecentPopup")).Child);

        /// <summary>The first element named <paramref name="name"/> in the Recent drop-down, or null.</summary>
        public AutomationPeer? InRecent(string name) =>
            RecentRoots().Select(r => UiaKit.TryFind(r, name: name)).FirstOrDefault(p => p is not null);

        public void Dispose()
        {
            Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;   // a dirty window closes without asking the fake to cancel
            Window.Close();
            StaThread.Pump();
            Assert.False(Window.IsLoaded);
            Assert.DoesNotContain(Window, Application.Current.Windows.Cast<Window>());
            Files.Dispose();
            if (Directory.Exists(Storage.Root)) Directory.Delete(Storage.Root, recursive: true);
        }
    }

    static T First<T>(DependencyObject root) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>([root]);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) return t;
            for (int i = VisualTreeHelper.GetChildrenCount(d) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        throw new InvalidOperationException("no " + typeof(T).Name);
    }

    static string[] TabNames(Rig rig) =>
        UiaKit.FindAll(rig.Root, type: AutomationControlType.RadioButton).Select(p => p.GetName()).Where(n => n.EndsWith(" tab")).ToArray();

    /// <summary>A layer's row in the Layers list (the canvas exposes the same layers under another class).</summary>
    static AutomationPeer LayerRow(Rig rig, string name) =>
        Assert.Single(UiaKit.FindAll(rig.Root, name: name, type: AutomationControlType.ListItem), p => p.GetClassName() == "ListBoxItem");

    static bool HasStar(string title) => title.StartsWith('*');

    [Fact] // Launch: the title, four tabs, Home active, Undo and Redo disabled, a clean document
    public void Launch_shows_the_title_four_tabs_home_active_and_a_clean_document() => StaThread.Run(() =>
    {
        using var rig = new Rig();

        Assert.Equal("RBX15 Classic Shirt and Pants Maker", rig.Window.Title);
        Assert.Equal(rig.Window.Title, rig.Root.GetName());
        Assert.Equal(["Home tab", "Background tab", "Details tab", "Text tab"], TabNames(rig));
        Assert.True(UiaKit.IsSelected(rig.El("Home tab")));
        Assert.All(["Background tab", "Details tab", "Text tab"], t => Assert.False(UiaKit.IsSelected(rig.El(t))));

        Assert.False(rig.El("Undo").IsEnabled());
        Assert.False(rig.El("Redo").IsEnabled());
        Assert.False(rig.Session.IsDirty);
        Assert.False(HasStar(rig.Window.Title));
        Assert.Empty(rig.Session.Document.Layers);
        // The other tabs' panes are not shown until their tab is picked, so a client (and a flow) must pick the tab first.
        Assert.Null(UiaKit.TryFind(rig.Root, name: "Add to canvas"));
        Assert.Null(UiaKit.TryFind(rig.Root, name: "Torso Front"));
    });

    [Fact] // Add image: Invoke "Add image" with the fake picking a PNG; one layer, selected, in the front slot
    public void Add_image_adds_one_selected_layer_in_the_front_slot() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        string png = rig.Png("one.png");
        rig.FileDialogs.OpenResult = [png];

        rig.Click("Add image");

        var layer = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal(layer.Id, rig.Session.SelectedLayerId);
        Assert.Equal(Tool.Select, rig.Main.Home.Tools.Current);
        Assert.Equal(1, rig.Session.UndoCount);
        var reference = new EditorSession();
        new ImportService(reference, new FakeStatus()).Import([png]);
        var expected = Assert.Single(reference.Document.Layers);
        Assert.Equal((expected.X, expected.Y, expected.Scale), (layer.X, layer.Y, layer.Scale));   // as the front slot places an import
        Assert.True(UiaKit.IsSelected(LayerRow(rig, layer.Name)));
        Assert.True(HasStar(rig.Window.Title));
        Assert.Equal(1, rig.FileDialogs.OpenCalls);
    });

    [Fact] // Region paint: with nothing selected, Invoke the Torso Front tile; its fill is the current Fill Style; no dialog
    public void A_region_tile_with_nothing_selected_paints_the_current_fill_style() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        Assert.Null(rig.Session.SelectedLayerId);

        rig.Tab("Background tab");
        rig.Click("Torso Front");

        var fill = DocumentOps.RegionFill(rig.Session.Document, "torso_front");
        Assert.NotNull(fill);
        Assert.Equal(rig.Main.Layout.FillStyle.Current, fill);
        Assert.Single(rig.Session.Document.RegionFills);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Empty(rig.Dialogs.Alerts);
        Assert.Empty(rig.Dialogs.Confirms);
        Assert.Empty(rig.Dialogs.SaveChanges);
        Assert.Equal(0, rig.FileDialogs.OpenCalls + rig.FileDialogs.OpenImageCalls + rig.FileDialogs.OpenProjectDirs.Count);
    });

    [Fact] // Word Art: set the text through ValuePattern, Invoke "Add to canvas"; one layer named "sparkle <text> [Anton]"
    public void Word_art_text_added_to_the_canvas_is_one_named_layer() => StaThread.Run(() =>
    {
        using var rig = new Rig();

        rig.Tab("Text tab");
        UiaKit.SetValue(rig.El("Word Art text"), "HELLO");
        StaThread.Pump();
        Assert.Equal("HELLO", UiaKit.GetValue(rig.El("Word Art text")));
        rig.Click("Add to canvas");

        var layer = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal("✨ HELLO [Anton]", layer.Name);
        Assert.Equal(WordArt.LayerName("HELLO", "Anton"), layer.Name);
        Assert.Equal(layer.Id, rig.Session.SelectedLayerId);
        Assert.Equal(1, rig.Session.UndoCount);
    });

    [Fact] // Undo: Invoke Undo three times; the document returns to new
    public void Undo_three_times_returns_the_document_to_new() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        rig.FileDialogs.OpenResult = [rig.Png("one.png")];
        rig.Tab("Background tab");
        rig.Click("Torso Front");                    // 1: a region fill (nothing is selected yet)
        rig.Tab("Home tab");
        rig.Click("Add image");                      // 2: a layer
        rig.Tab("Text tab");
        UiaKit.SetValue(rig.El("Word Art text"), "UNDO");
        StaThread.Pump();
        rig.Click("Add to canvas");                  // 3: a Word Art layer
        rig.Tab("Home tab");
        Assert.Equal(3, rig.Session.UndoCount);
        Assert.True(rig.El("Undo").IsEnabled());
        Assert.False(rig.El("Redo").IsEnabled());

        for (int expected = 2; expected >= 0; expected--)
        {
            rig.Click("Undo");
            Assert.Equal(expected, rig.Session.UndoCount);
        }

        Assert.True(rig.Session.Document.ContentEquals(EditorSession.NewDocument()));
        Assert.False(rig.El("Undo").IsEnabled());
        Assert.True(rig.El("Redo").IsEnabled());
        Assert.False(rig.Session.IsDirty);
        Assert.False(HasStar(rig.Window.Title));
    });

    [Fact] // Save and reopen: Save to a fake path, change state, Open that path; the saved document is back and the title has no "*"
    public void Save_then_open_brings_back_the_saved_document() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        string path = Path.Combine(rig.Files.Dir, "work.r15proj");
        rig.FileDialogs.SaveProjectResult = path;
        rig.FileDialogs.OpenProjectResult = path;

        // Stage 1: a region fill only (no images), so the reopened document is ContentEquals the saved one.
        rig.Tab("Background tab");
        rig.Click("Torso Front");
        rig.Tab("Home tab");
        rig.Click("Save work");
        Assert.True(File.Exists(path));
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.False(HasStar(rig.Window.Title));
        Assert.StartsWith("work.r15proj - ", rig.Window.Title);
        var saved = rig.Session.Document;

        rig.Click("Undo");                           // the "new" state: unsaved changes now
        Assert.True(rig.Session.IsDirty);
        Assert.True(HasStar(rig.Window.Title));
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        rig.Click("Open");
        UiaKit.PumpUntil(() => !rig.Main.Files.IsBusy && !rig.Session.IsDirty);
        Assert.True(saved.ContentEquals(rig.Session.Document));
        Assert.False(HasStar(rig.Window.Title));
        Assert.Equal(0, rig.Session.UndoCount);

        // Stage 2: with an image layer. Images are compared by reference in ContentEquals and the old document's are released
        // by an Open, so the reopened document is compared by what it draws and by every other field of its layers.
        rig.FileDialogs.OpenResult = [rig.Png("one.png")];
        rig.Click("Add image");
        rig.Click("Save work");
        var layersBefore = rig.Session.Document.Layers.Select(NoImage).ToList();
        var pngBefore = Renderer.ExportPng(rig.Session.Document);
        var fillsBefore = rig.Session.Document.RegionFills.ToList();
        rig.Click("Undo");
        Assert.True(HasStar(rig.Window.Title));
        rig.Click("Open");
        UiaKit.PumpUntil(() => !rig.Main.Files.IsBusy && !rig.Session.IsDirty);
        Assert.Equal(layersBefore, rig.Session.Document.Layers.Select(NoImage).ToList());
        Assert.Equal(fillsBefore, rig.Session.Document.RegionFills.ToList());
        Assert.Equal(pngBefore, Renderer.ExportPng(rig.Session.Document));
        Assert.False(HasStar(rig.Window.Title));
        Assert.Empty(rig.Dialogs.Alerts);
    });

    static Layer NoImage(Layer layer) => layer with { Image = default! };

    [Fact] // Export: Invoke Download template; the fake path holds a 585x559 PNG, byte-equal to Renderer.ExportPng of the document
    public void Download_template_writes_the_rendered_585x559_png() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        string target = Path.Combine(rig.Files.Dir, "shirt.png");
        rig.FileDialogs.SaveResult = target;
        rig.FileDialogs.OpenResult = [rig.Png("one.png")];
        rig.Tab("Background tab");
        rig.Click("Torso Front");
        rig.Tab("Home tab");
        rig.Click("Add image");

        rig.Click("Download template");

        Assert.Single(rig.FileDialogs.SaveSuggestions);
        byte[] written = File.ReadAllBytes(target);
        Assert.Equal(Renderer.ExportPng(rig.Session.Document), written);
        using var bitmap = SKBitmap.Decode(written);
        Assert.Equal((585, 559), (bitmap.Width, bitmap.Height));
        Assert.Equal($"Saved {Path.GetFileName(target)}.", rig.Main.Status.Message);
        Assert.Empty(rig.Dialogs.Confirms);          // the document has art, so the empty-template question never shows
        Assert.Empty(rig.Dialogs.Alerts);
    });

    [Fact] // New: Invoke "New" and press Ctrl+N (also with Keys OFF); the launcher is asked for a new window each time and this document is untouched
    public void New_asks_the_launcher_for_a_new_window_and_changes_nothing_here() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        string title = rig.Main.Title;
        rig.Click("New");
        Assert.Equal(1, rig.Launcher.NewWindows);

        var binding = Assert.Single(rig.Window.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.N && b.Modifiers == ModifierKeys.Control);
        rig.Main.Home.Keys.ToggleCommand.Execute(null);   // Keys OFF does not block the chord
        Assert.True(binding.Command.CanExecute(binding.CommandParameter));
        binding.Command.Execute(binding.CommandParameter);
        StaThread.Pump();
        Assert.Equal(2, rig.Launcher.NewWindows);
        Assert.Empty(rig.Launcher.Opened);
        Assert.Equal(0, rig.Session.UndoCount);
        Assert.Equal(title, rig.Main.Title);
        Assert.Empty(rig.Session.Document.Layers);
    });

    [Fact] // Paste and Recent: Ctrl+V through the window's KeyBinding pastes a layer; after a Save the Recent drop-down lists the file
    public void Ctrl_v_pastes_a_layer_and_the_recent_drop_down_lists_the_saved_file() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        using (var surface = SKSurface.Create(new SKImageInfo(40, 30, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            surface.Canvas.Clear(new SKColor(255, 0, 0, 128));
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            rig.Clipboard.Content = new ClipboardContent.Png(data.ToArray());
        }

        rig.CtrlV();

        var layer = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal(layer.Id, rig.Session.SelectedLayerId);
        Assert.Equal(Tool.Select, rig.Main.Home.Tools.Current);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(1, rig.Clipboard.Reads);
        Assert.True(UiaKit.IsSelected(LayerRow(rig, layer.Name)));

        string path = Path.Combine(rig.Files.Dir, "pasted.r15proj");
        rig.FileDialogs.SaveProjectResult = path;
        rig.Click("Save work");
        Assert.Equal(path, rig.Session.CurrentFile);

        rig.Click("Recent projects");                // the button opens the drop-down and re-checks the files
        UiaKit.PumpUntil(() => rig.InRecent("pasted.r15proj") is not null);
        var entry = rig.InRecent("pasted.r15proj")!;
        Assert.Equal(AutomationControlType.Button, entry.GetAutomationControlType());
        Assert.Equal(path, entry.GetHelpText());
        Assert.NotNull(rig.InRecent("Clear recent list"));
        Assert.Equal([path], rig.Main.Home.File.Recent.Items.Select(i => i.Path));
        Assert.Equal([path], rig.Main.Recents.Items);
    });

    /// <summary>The window's rectangle in device pixels, which is what the desktop sees.</summary>
    static (int Left, int Top, int Width, int Height) PixelRect(Window window)
    {
        Assert.True(GetWindowRect(new WindowInteropHelper(window).Handle, out var r));
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [Fact] // shell-window-size: with no saved placement the window opens 1200x800 DIP, centered in the primary work area, at the real DPI
    public void The_window_opens_1200_by_800_centered_in_the_work_area_at_the_real_dpi() => StaThread.Run(() =>
    {
        using var rig = new Rig();   // FakeSettingsStore holds no placement: the first launch (PlacementGuardTests pins the rule itself)
        var w = rig.Window;
        Assert.Equal((1200.0, 800.0), (w.Width, w.Height));
        // Windows caps a window at the screen's maximum track size (a CI runner's screen is about 1024x768, giving 1044x788),
        // so the laid-out size is 1200x800 only where the screen allows it; the requested size above is 1200x800 everywhere.
        double expectW = Math.Min(1200, SystemParameters.MaximumWindowTrackWidth), expectH = Math.Min(800, SystemParameters.MaximumWindowTrackHeight);
        Assert.Equal((expectW, expectH), (w.ActualWidth, w.ActualHeight));
        Assert.Equal((OffScreen.Left + (OffScreen.Width - 1200) / 2, OffScreen.Top + (OffScreen.Height - 800) / 2), (w.Left, w.Top));
        Assert.Equal(WindowState.Normal, w.WindowState);

        var dpi = VisualTreeHelper.GetDpi(w);
        var px = PixelRect(w);
        Assert.InRange(px.Width, Math.Round(expectW * dpi.DpiScaleX) - 1, Math.Round(expectW * dpi.DpiScaleX) + 1);
        Assert.InRange(px.Height, Math.Round(expectH * dpi.DpiScaleY) - 1, Math.Round(expectH * dpi.DpiScaleY) + 1);
        Assert.InRange(px.Left, Math.Round(w.Left * dpi.DpiScaleX) - 1, Math.Round(w.Left * dpi.DpiScaleX) + 1);
        Assert.InRange(px.Top, Math.Round(w.Top * dpi.DpiScaleY) - 1, Math.Round(w.Top * dpi.DpiScaleY) + 1);
    });

    [Fact] // shell-window-size: resizing to 640x480 lays out with no exception and no negative column; below the minimum the window refuses
    public void Resizing_to_640_by_480_lays_out_and_below_the_minimum_the_window_refuses() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        var w = rig.Window;
        Assert.Equal((640.0, 480.0), (w.MinWidth, w.MinHeight));

        w.Width = 640;
        w.Height = 480;
        w.UpdateLayout();
        StaThread.Pump();
        Assert.Equal((640.0, 480.0), (w.ActualWidth, w.ActualHeight));
        var columns = ((System.Windows.Controls.Grid)w.FindName("AppRow")).ColumnDefinitions;
        Assert.All(columns, c => Assert.True(c.ActualWidth >= 0, "a column laid out with a negative width"));
        Assert.Equal(200, ((System.Windows.Controls.ColumnDefinition)w.FindName("PanelColumn")).ActualWidth);   // narrow at 900 or less
        Assert.True(((FrameworkElement)w.FindName("CanvasArea")).ActualWidth > 0);
        Assert.True(((FrameworkElement)w.FindName("RibbonPane")).ActualHeight > 0);
        Assert.NotEmpty(UiaKit.FindAll(rig.Root));   // the whole tree still builds at the floor
        rig.Click("Add image");                      // and the window still works: the fake picked nothing, so nothing was added
        Assert.Empty(rig.Session.Document.Layers);

        w.Width = 300;
        w.Height = 200;
        w.UpdateLayout();
        StaThread.Pump();
        Assert.Equal((640.0, 480.0), (w.ActualWidth, w.ActualHeight));
        var dpi = VisualTreeHelper.GetDpi(w);
        var px = PixelRect(w);
        Assert.True(px.Width >= Math.Round(640 * dpi.DpiScaleX) - 1 && px.Height >= Math.Round(480 * dpi.DpiScaleY) - 1,
            $"the window rectangle shrank below 640x480 DIP: {px.Width}x{px.Height} px at {dpi.DpiScaleX:0.##}x");
    });

    [Fact] // shell-window-frame: the title, no menu, and the close guard. The themed caption's attributes are CaptionThemeTests'; the snap flyout and color are MAN-18
    public void The_frame_has_the_title_no_menu_and_asks_before_closing_unsaved_work() => StaThread.Run(() =>
    {
        using var rig = new Rig();
        Assert.Equal(AppInfo.ProductName, rig.Window.Title);
        Assert.Empty(UiaKit.FindAll(rig.Root, type: AutomationControlType.Menu));
        Assert.Empty(UiaKit.FindAll(rig.Root, type: AutomationControlType.MenuBar));
        Assert.Empty(UiaKit.FindAll(rig.Root, type: AutomationControlType.MenuItem));

        rig.FileDialogs.OpenResult = [rig.Png("one.png")];
        rig.Click("Add image");
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        rig.Window.Close();
        StaThread.Pump();
        Assert.True(rig.Window.IsLoaded);            // Cancel keeps the window open
        Assert.Single(rig.Dialogs.SaveChanges);      // asked once, naming the document
        Assert.Empty(rig.Dialogs.Alerts);
    });
}
