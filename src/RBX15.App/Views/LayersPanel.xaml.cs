using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The layers panel's input: drag a row to reorder (editor.js:584-666) and Alt+Up / Alt+Down on the focused row.
/// The drag is in-process and capture-based, not OLE DoDragDrop: a press on a row's body arms it, a move past the
/// system drag distance starts it and captures the mouse on this panel, every move re-reads the row under the
/// pointer by its midpoint (handleLayerDragOver, editor.js:607-609), the release drops on the row under the release
/// point only, and Esc or a capture loss cancels. A press on a row's Dupe, Hide or Del button never starts a drag.
/// The overrides only forward to the internal Handle* seams, which tests call with panel coordinates.
/// </summary>
public partial class LayersPanel : UserControl
{
    /// <summary>Half the 4 px gap between rows (.layer-list gap 4px, styles.css:287): a row owns the gap's half next to it.</summary>
    internal const double HalfGap = 2;

    LayerRowViewModel? _pressed;   // the row a left press armed, until it moves or releases
    Point _pressPoint;
    bool _dragging;

    public LayersPanel() => InitializeComponent();

    LayersViewModel? ViewModel => DataContext as LayersViewModel;

    /// <summary>True between the drag's start and its drop or cancel.</summary>
    internal bool IsDragging => _dragging;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        HandleRowPress(RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: true), e.GetPosition(this));
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        HandleMove(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed);
        if (_dragging) e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool wasDragging = _dragging;
        HandleRelease(e.GetPosition(this));
        if (wasDragging) e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // LostMouseCapture bubbles: the list losing its own press capture when the drag captures this panel must not cancel it.
        if (ReferenceEquals(e.OriginalSource, this)) HandleCaptureLost();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandleKey(key, Keyboard.Modifiers, RowFrom(e.OriginalSource as DependencyObject, stopAtButtons: false))) e.Handled = true;
    }

    /// <summary>A left press. On a row's body it arms a drag; anywhere else it disarms. A drag still running (its release was missed) is cancelled.</summary>
    internal void HandleRowPress(LayerRowViewModel? row, Point inPanel)
    {
        if (_dragging) Cancel();
        _pressed = row;
        _pressPoint = inPanel;
    }

    /// <summary>
    /// A move. Armed and past the system drag distance (SystemParameters.MinimumHorizontalDragDistance and
    /// MinimumVerticalDragDistance), it starts the drag and captures the mouse. Dragging, it sets the target to the
    /// row under the point, or clears it off every row. A move with the button up ends a drag whose release was
    /// missed, as a cancel.
    /// </summary>
    internal void HandleMove(Point inPanel, bool leftButtonDown)
    {
        if (!leftButtonDown)
        {
            _pressed = null;
            if (_dragging) Cancel();
            return;
        }
        if (!_dragging)
        {
            if (_pressed is null || ViewModel is not { } vm) return;
            if (Math.Abs(inPanel.X - _pressPoint.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(inPanel.Y - _pressPoint.Y) <= SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = true;
            vm.BeginRowDrag(_pressed.Id);
            Cursor = Cursors.Hand;   // .layer-item:active is cursor: grabbing (styles.css:303); WPF has none, Hand is provisional (M3b look sign-off)
            CaptureMouse();
        }
        Retarget(inPanel);
    }

    /// <summary>The release: a drag drops on the row under <paramref name="inPanel"/> (re-read there, never a stale one) and ends.</summary>
    internal void HandleRelease(Point inPanel)
    {
        _pressed = null;
        if (!_dragging || ViewModel is not { } vm) return;
        Retarget(inPanel);
        _dragging = false;
        vm.CompleteRowDrop();
        Finish();
    }

    /// <summary>Capture lost mid-drag (Alt+Tab, a dialog): cancel, as 4.1's dragend cleanup (editor.js:741).</summary>
    internal void HandleCaptureLost()
    {
        if (_dragging) Cancel();
    }

    /// <summary>
    /// Esc cancels a running drag. Alt+Up / Alt+Down on a focused row select it and run MoveUpCommand /
    /// MoveDownCommand (one undo step each, none at an end), then keep the keyboard focus on that row.
    /// True when the key was used.
    /// </summary>
    internal bool HandleKey(Key key, ModifierKeys modifiers, LayerRowViewModel? focusedRow)
    {
        if (key == Key.Escape && _dragging)
        {
            Cancel();
            return true;
        }
        if (modifiers != ModifierKeys.Alt || key is not (Key.Up or Key.Down) || focusedRow is null || ViewModel is not { } vm) return false;
        if (_dragging) Cancel();
        vm.Selected = focusedRow;
        var command = key == Key.Up ? vm.MoveUpCommand : vm.MoveDownCommand;
        if (command.CanExecute(null)) command.Execute(null);
        // The collection's Move drops the row's container and its keyboard focus; the next layout pass makes a new
        // one. Lay out now so the focus lands on the moved row and a held Alt+Up keeps moving it.
        LayerList.UpdateLayout();
        if (LayerList.ItemContainerGenerator.ContainerFromItem(focusedRow) is ListBoxItem item) item.Focus();
        return true;
    }

    /// <summary>
    /// The row under a panel point and its half: each row's band is its box plus half the gap on either side, so
    /// the list has no dead stripes between rows; above is the top half, by the row's own midpoint (editor.js:607-609).
    /// Null outside the list's width or past the first and last rows.
    /// </summary>
    internal (int Id, bool Above)? RowAt(Point inPanel)
    {
        var inList = TranslatePoint(inPanel, LayerList);
        if (inList.X < 0 || inList.X > LayerList.ActualWidth) return null;
        foreach (var row in LayerList.Items.OfType<LayerRowViewModel>())
        {
            if (LayerList.ItemContainerGenerator.ContainerFromItem(row) is not ListBoxItem item) continue;
            double top = item.TranslatePoint(new Point(0, 0), this).Y, height = item.ActualHeight;
            if (inPanel.Y >= top - HalfGap && inPanel.Y < top + height + HalfGap) return (row.Id, inPanel.Y < top + height / 2);
        }
        return null;
    }

    void Retarget(Point inPanel)
    {
        if (ViewModel is not { } vm) return;
        if (RowAt(inPanel) is (int id, bool above)) vm.DragOverRow(id, above);
        else vm.ClearDropTarget();
    }

    void Cancel()
    {
        _dragging = false;
        _pressed = null;
        ViewModel?.EndRowDrag();
        Finish();
    }

    void Finish()
    {
        ClearValue(CursorProperty);
        // _dragging is already false, so the LostMouseCapture this raises is ignored.
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>The row a visual belongs to, or null. With <paramref name="stopAtButtons"/>, a row's own buttons count as no row.</summary>
    internal static LayerRowViewModel? RowFrom(DependencyObject? source, bool stopAtButtons)
    {
        for (var d = source; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (stopAtButtons && d is ButtonBase) return null;
            if (d is ListBoxItem { DataContext: LayerRowViewModel row }) return row;
        }
        return null;
    }
}
