using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The asset library's mouse input (interfaces R6). A thumbnail drag is in-app and capture-based, not OLE DoDragDrop:
/// a left press on a tile arms it, a move past the system drag distance starts it and captures the mouse on this
/// panel, and the release hit-tests the window. A <see cref="TemplateCanvasHost"/> under the release point gets the
/// tile at that point (canvas.ondrop's asset branch, editor.js:962-983); anywhere else nothing is added. Esc, a lost
/// capture or a move with the button up ends it with nothing added. The drag lives only in this panel's fields and
/// every end clears them, so a cancelled drag can never hijack a later OS file drop (ledger asset-drag-stale-reference,
/// fix: 4.1's _assetDragFile, editor.js:101-102, :2498, was cleared only by a canvas drop). OS file drops keep the
/// canvas's own OLE path and never see this state. A click that never became a drag is the tile Button's own
/// click (AddCommand). Right-click adds at full size and is handled, so no context menu opens (editor.js:2514). The
/// resize handle is capture-based too: 4.1 lost a mouseup outside the window and stayed resizing (asset-resizable-browser quirk).
/// The overrides only forward to the internal Handle* seams, which tests call directly.
/// </summary>
public partial class AssetLibraryPanel : UserControl
{
    AssetTileViewModel? _pressed;    // the tile a left press armed, until it moves past the drag distance or releases
    Point _pressPoint;
    AssetTileViewModel? _dragging;   // the tile being dragged; the only drag state there is
    Window? _keyWindow;              // the window whose PreviewKeyDown hands Esc to a running drag (T2-R14)
    bool _resizing;
    double _resizeStart;              // browser.offsetHeight || 180 at the press (editor.js:2724)
    double _resizeStartY;

    public AssetLibraryPanel() => InitializeComponent();

    AssetLibraryViewModel? ViewModel => DataContext as AssetLibraryViewModel;

    /// <summary>True between a drag's start and its drop or cancel.</summary>
    internal bool IsDragging => _dragging is not null;

    /// <summary>True between a press on the resize handle and its release.</summary>
    internal bool IsResizing => _resizing;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        HandleTilePress(TileFrom(e.OriginalSource as DependencyObject), e.GetPosition(this));
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        HandleMove(e.GetPosition(this), LeftDown(e));
        if (_dragging is not null) e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool wasDragging = _dragging is not null;
        HandleRelease(e.GetPosition(Root()));
        if (wasDragging) e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // LostMouseCapture bubbles: a tile Button losing its press capture when the drag captures this panel must not cancel it.
        if (ReferenceEquals(e.OriginalSource, this)) HandleCaptureLost();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (HandleKey(e.Key)) e.Handled = true;
    }

    /// <summary>The physical left button. Task 4 Step 5 replaces this read with PointerState.LeftButton(e) (interfaces 3.3; critic C2).</summary>
    static bool LeftDown(MouseEventArgs e) => e.LeftButton == MouseButtonState.Pressed;

    /// <summary>A left press. On a tile it arms a drag; anywhere else it disarms. A drag still running (its release was missed) is cancelled.</summary>
    internal void HandleTilePress(AssetTileViewModel? tile, Point inPanel)
    {
        if (_dragging is not null) Cancel();
        _pressed = tile;
        _pressPoint = inPanel;
    }

    /// <summary>
    /// A move. Armed and past SystemParameters.MinimumHorizontalDragDistance or MinimumVerticalDragDistance, it starts
    /// the drag and captures the mouse. Dragging, it shows Hand over the canvas and No elsewhere. A move with the button
    /// up ends a drag whose release was missed, as a cancel.
    /// </summary>
    internal void HandleMove(Point inPanel, bool leftButtonDown)
    {
        if (!leftButtonDown)
        {
            _pressed = null;
            if (_dragging is not null) Cancel();
            return;
        }
        if (_dragging is null)
        {
            if (_pressed is null) return;
            if (Math.Abs(inPanel.X - _pressPoint.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(inPanel.Y - _pressPoint.Y) <= SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = _pressed;
            _pressed = null;
            CaptureMouse();
            // Esc must reach the drag wherever keyboard focus is, not only inside this panel (T2-R14).
            _keyWindow = Window.GetWindow(this);
            if (_keyWindow is not null) _keyWindow.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
        // WPF has no stock Copy cursor (that one is OLE's drag feedback), so the canvas shows Hand, the tiles' own
        // pointer, and anywhere else shows No (interfaces R6, amended: provisional, M4b look sign-off).
        Cursor = CanvasAt(TranslatePoint(inPanel, Root())) is not null ? Cursors.Hand : Cursors.No;
    }

    /// <summary>
    /// The release, at a point in the root visual's coordinates (the window's client area in the app). A drag ends
    /// here: over a canvas host it adds the tile centered on that point in template pixels and returns true; anywhere
    /// else it adds nothing. Without a drag it only disarms, and the tile Button's click does the click-add.
    /// </summary>
    internal bool HandleRelease(Point inRoot)
    {
        _pressed = null;
        if (_dragging is not { } tile) return false;
        _dragging = null;
        Finish();
        if (CanvasAt(inRoot) is not var (host, inHost) || ViewModel is not { } vm) return false;
        vm.DropOnCanvas(tile, CanvasCoordinates.ToTemplate(inHost, host.Zoom));
        return true;
    }

    /// <summary>Capture lost mid-drag (Alt+Tab, a dialog): cancel, with nothing added.</summary>
    internal void HandleCaptureLost()
    {
        if (_dragging is not null) Cancel();
    }

    /// <summary>Esc cancels a running drag (4.1's dragend, which also ran on Esc, left the stale file behind). True when used.</summary>
    internal bool HandleKey(Key key)
    {
        if (key != Key.Escape || _dragging is null) return false;
        Cancel();
        return true;
    }

    /// <summary>
    /// The canvas host under a root point, and the point in the host's coordinates, or null. A hit on anything that is
    /// not inside a host (the zoom pill, the area around the frame, the panels) is no canvas, as canvas.ondrop fired
    /// only on the canvas element itself. A disabled host (Open running, interfaces R4) takes no drop.
    /// </summary>
    internal (TemplateCanvasHost Host, Point InHost)? CanvasAt(Point inRoot)
    {
        var root = Root();
        if (VisualTreeHelper.HitTest(root, inRoot) is not { VisualHit: DependencyObject hit }) return null;
        for (DependencyObject? d = hit; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is TemplateCanvasHost host)
                return host.IsEnabled ? (host, root.TranslatePoint(inRoot, host)) : null;
        return null;
    }

    /// <summary>The top of this panel's visual tree: the window in the app, the laid-out root in tests.</summary>
    UIElement Root()
    {
        DependencyObject node = this;
        while (VisualTreeHelper.GetParent(node) is { } parent) node = parent;
        return (UIElement)node;
    }

    void Cancel()
    {
        _dragging = null;
        _pressed = null;
        Finish();
    }

    void Finish()
    {
        ClearValue(CursorProperty);
        if (_keyWindow is not null)
        {
            _keyWindow.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _keyWindow = null;
        }
        // _dragging is already null, so the LostMouseCapture this raises is ignored.
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>The window-level Esc while a drag runs (T2-R14); the panel's own override covers focus inside it.</summary>
    void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleKey(e.Key)) e.Handled = true;
    }

    /// <summary>The tile a visual belongs to, or null.</summary>
    internal static AssetTileViewModel? TileFrom(DependencyObject? source)
    {
        for (var d = source; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is Button { Name: "AssetTile", DataContext: AssetTileViewModel tile }) return tile;
            if (d is AssetLibraryPanel) return null;
        }
        return null;
    }

    void OnTileRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // contextmenu is prevented (editor.js:2514)
        if (sender is FrameworkElement { DataContext: AssetTileViewModel tile } && ViewModel is { } vm) vm.AddFullCommand.Execute(tile);
    }

    // ---- the resize handle (editor.js:2713-2743) ----

    /// <summary>
    /// A press on the handle: the start height is the browser's rendered height, or 180 while it is hidden
    /// (<c>browser.offsetHeight || 180</c>, editor.js:2724), and the upper clamp is 70% of the window's client height
    /// (editor.js:2732), read now from the window's content, as 4.1 read innerHeight on every move. Outside a window the
    /// view-model's clamp stays as it is.
    /// </summary>
    internal void HandleResizePress()
    {
        _resizing = true;
        _resizeStart = AssetBrowser.ActualHeight > 0 ? AssetBrowser.ActualHeight : AssetLibraryViewModel.DefaultBrowserHeight;
        if (ViewModel is { } vm && Window.GetWindow(this)?.Content is FrameworkElement { ActualHeight: > 0 } client)
            vm.MaxBrowserHeight = 0.7 * client.ActualHeight;
    }

    /// <summary>A drag of the handle: <paramref name="deltaY"/> DIPs from the press (positive is down); the view-model clamps.</summary>
    internal void HandleResize(double deltaY)
    {
        if (_resizing && ViewModel is { } vm) vm.BrowserHeight = _resizeStart + deltaY;
    }

    /// <summary>The handle's release, or its capture lost: the resize ends where it is.</summary>
    internal void HandleResizeEnd()
    {
        _resizing = false;
        if (ResizeHandle.IsMouseCaptured) ResizeHandle.ReleaseMouseCapture();
    }

    void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // preventDefault (editor.js:2721): no tile press, no text selection
        HandleResizePress();
        _resizeStartY = e.GetPosition(Root()).Y;
        ResizeHandle.CaptureMouse();
    }

    void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_resizing) HandleResize(e.GetPosition(Root()).Y - _resizeStartY);
    }

    void OnHandleUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        e.Handled = true;
        HandleResizeEnd();
    }

    void OnHandleCaptureLost(object sender, MouseEventArgs e)
    {
        if (_resizing) HandleResizeEnd();
    }
}
