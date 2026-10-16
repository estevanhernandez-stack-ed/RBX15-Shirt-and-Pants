using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// The layers panel's drag and keys (LayersPanel.xaml.cs; editor.js:584-666; ledger layers-reorder-drag,
/// layers-reorder-drag-2, layer-reorder-dragging-class-timer). WPF ignores posted mouse input and the rules forbid
/// synthetic desktop input, so the tests drive the Handle* seams the mouse and key overrides forward to, with
/// points in panel coordinates taken from the laid-out rows. The panel is laid out off-screen, 280 wide (the panel
/// width, styles.css:247).
/// </summary>
public class LayersPanelDragTests
{
    static SKImage Img()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        surface.Canvas.Clear(SKColors.Red);
        return surface.Snapshot();
    }

    /// <summary>A, B, C (ids 1, 2, 3; C on top and selected), in a laid-out panel. Rows top first: C, B, A.</summary>
    static (EditorSession Session, LayersViewModel Vm, LayersPanel Panel) Panel(int count = 3)
    {
        var session = new EditorSession();
        session.ApplyWithSelection(s =>
        {
            for (int i = 0; i < count; i++) s = LayerOps.AddImage(s, Img(), session.TakeId(), ((char)('A' + i)).ToString());
            return s;
        });
        var vm = new LayersViewModel(session, new ImportService(session, new FakeStatus()), new FakeFileDialogs());
        var panel = new LayersPanel { DataContext = vm };
        Layout(panel);
        return (session, vm, panel);
    }

    static void Layout(FrameworkElement e)
    {
        StaThread.Pump();
        e.Measure(new Size(280, 2000));
        e.Arrange(new Rect(0, 0, 280, 2000));
        e.UpdateLayout();
    }

    static ListBoxItem Item(LayersPanel panel, LayerRowViewModel row) =>
        (ListBoxItem)panel.LayerList.ItemContainerGenerator.ContainerFromItem(row);

    /// <summary>A panel point inside <paramref name="row"/>'s box, <paramref name="fraction"/> of the way down.</summary>
    static Point At(LayersPanel panel, LayerRowViewModel row, double fraction)
    {
        var item = Item(panel, row);
        var top = item.TranslatePoint(new Point(0, 0), panel);
        return new Point(top.X + 40, top.Y + item.ActualHeight * fraction);
    }

    static string Names(LayersViewModel vm) => string.Concat(vm.Rows.Select(r => r.Name));

    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    [Fact] // layers-reorder-drag-2 parityCheck through the mouse path: A onto C's top half is the new top, one step
    public void A_drag_past_the_drag_distance_reorders_on_release_in_one_step() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        var a = vm.Rows[2];
        int steps = session.UndoCount;
        var press = At(panel, a, 0.5);
        panel.HandleRowPress(a, press);
        panel.HandleMove(press + new Vector(0, 2), leftButtonDown: true);
        Assert.False(panel.IsDragging);
        var overC = At(panel, vm.Rows[0], 0.25);
        panel.HandleMove(overC, leftButtonDown: true);
        Assert.True(panel.IsDragging);
        Assert.True(a.IsDragSource);
        Assert.Equal(DropIndicator.Above, vm.Rows[0].DropIndicator);
        panel.HandleRelease(overC);
        Assert.False(panel.IsDragging);
        Assert.Equal("ACB", Names(vm));
        Assert.Equal(steps + 1, session.UndoCount);
        Assert.All(vm.Rows, r => Assert.Equal((false, DropIndicator.None), (r.IsDragSource, r.DropIndicator)));
    });

    [Fact] // the system drag distance (SystemParameters.MinimumVerticalDragDistance) separates a click from a drag
    public void A_move_within_the_drag_distance_stays_a_click() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var press = At(panel, vm.Rows[2], 0.5);
        panel.HandleRowPress(vm.Rows[2], press);
        panel.HandleMove(press + new Vector(SystemParameters.MinimumHorizontalDragDistance / 2, -SystemParameters.MinimumVerticalDragDistance / 2), leftButtonDown: true);
        Assert.False(panel.IsDragging);
        panel.HandleRelease(press);
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // the stale-target fix through the mouse path: below every row there is no target, and the release drops nothing
    public void A_release_off_every_row_drops_nothing() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(At(panel, vm.Rows[0], 0.25), leftButtonDown: true);
        Assert.Equal(DropIndicator.Above, vm.Rows[0].DropIndicator);
        var below = At(panel, vm.Rows[2], 1.0) + new Vector(0, 40);
        panel.HandleMove(below, leftButtonDown: true);
        Assert.Equal(DropIndicator.None, vm.Rows[0].DropIndicator);
        panel.HandleRelease(below);
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // the drop reads the row under the release point, never the last move's (no stale target after a wheel scroll)
    public void The_release_point_picks_the_target() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(At(panel, vm.Rows[0], 0.25), leftButtonDown: true);   // C, top half
        panel.HandleRelease(At(panel, vm.Rows[0], 0.75));                       // C, bottom half: A goes just below C
        Assert.Equal("CAB", Names(vm));
    });

    [Fact] // dragend (editor.js:741): Esc or a lost capture cancels with nothing changed
    public void Esc_and_a_capture_loss_cancel_the_drag() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var overC = At(panel, vm.Rows[0], 0.25);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        Assert.True(panel.HandleKey(Key.Escape, ModifierKeys.None, null));
        Assert.False(panel.IsDragging);
        panel.HandleRelease(overC);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleCaptureLost();
        Assert.False(panel.IsDragging);
        panel.HandleRelease(overC);

        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
        Assert.All(vm.Rows, r => Assert.Equal((false, DropIndicator.None), (r.IsDragSource, r.DropIndicator)));
        Assert.False(panel.HandleKey(Key.Escape, ModifierKeys.None, null));   // Esc with no drag is not the panel's
    });

    [Fact] // a release the panel never saw: the next move with the button up, or the next press, cancels
    public void A_missed_release_cancels_on_the_next_move_or_press() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        var overC = At(panel, vm.Rows[0], 0.25);
        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleMove(overC, leftButtonDown: false);
        Assert.False(panel.IsDragging);

        panel.HandleRowPress(vm.Rows[2], At(panel, vm.Rows[2], 0.5));
        panel.HandleMove(overC, leftButtonDown: true);
        panel.HandleRowPress(vm.Rows[1], At(panel, vm.Rows[1], 0.5));
        Assert.False(panel.IsDragging);
        Assert.All(vm.Rows, r => Assert.False(r.IsDragSource));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // a press on Dupe, Hide or Del is a button press, never a drag; the row body arms one
    public void Only_a_rows_body_arms_a_drag_and_its_buttons_do_not() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        var item = Item(panel, vm.Rows[1]);
        var del = Descendants<Button>(item).Last();
        var name = Descendants<TextBlock>(item).First(t => t.Name == "NameText");
        Assert.Null(LayersPanel.RowFrom((TextBlock)del.Content, stopAtButtons: true));
        Assert.Same(vm.Rows[1], LayersPanel.RowFrom(name, stopAtButtons: true));
        Assert.Same(vm.Rows[1], LayersPanel.RowFrom((TextBlock)del.Content, stopAtButtons: false));   // keys: a focused button still names its row
        Assert.Null(LayersPanel.RowFrom(panel, stopAtButtons: true));
    });

    [Fact] // each row owns half the 4px gap on either side; above = the top half by its own midpoint (editor.js:607-609)
    public void Row_at_splits_each_gap_and_uses_the_rows_midpoint() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        foreach (var row in vm.Rows)
        {
            var item = Item(panel, row);
            double top = item.TranslatePoint(new Point(0, 0), panel).Y, h = item.ActualHeight;
            Assert.Equal((row.Id, true), panel.RowAt(new Point(40, top - LayersPanel.HalfGap)));
            Assert.Equal((row.Id, true), panel.RowAt(new Point(40, top + h / 2 - 0.01)));
            Assert.Equal((row.Id, false), panel.RowAt(new Point(40, top + h / 2)));
            Assert.Equal((row.Id, false), panel.RowAt(new Point(40, top + h + LayersPanel.HalfGap - 0.01)));
        }
        var first = Item(panel, vm.Rows[0]).TranslatePoint(new Point(0, 0), panel).Y;
        Assert.Null(panel.RowAt(new Point(40, first - LayersPanel.HalfGap - 0.5)));
        Assert.Null(panel.RowAt(new Point(-1, first + 10)));
        Assert.Null(panel.RowAt(new Point(panel.LayerList.ActualWidth + 1, first + 10)));
    });

    [Fact] // layers-reorder-drag-2 (keyboard operable): Alt+Up / Alt+Down move the focused row, one step each, none at an end
    public void Alt_up_and_down_move_the_focused_row_one_step_each_and_focus_follows_it() => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        var a = vm.Rows[2];
        int steps = session.UndoCount;
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));
        Assert.Equal("CAB", Names(vm));
        Assert.Equal(1, session.SelectedLayerId);   // the focused row is selected first
        var item = Item(panel, a);
        Assert.NotNull(item);                        // laid out again, so the moved row has a container to focus
        Assert.Same(item, FocusManager.GetFocusedElement(FocusManager.GetFocusScope(item)));
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));
        Assert.Equal("ACB", Names(vm));
        Assert.True(panel.HandleKey(Key.Up, ModifierKeys.Alt, a));   // at the top: used, nothing moves, no step
        Assert.Equal("ACB", Names(vm));
        Assert.True(panel.HandleKey(Key.Down, ModifierKeys.Alt, a));
        Assert.Equal("CAB", Names(vm));
        Assert.Equal(steps + 3, session.UndoCount);
    });

    [Theory] // only Alt+Up / Alt+Down on a row; arrows, Shift and Ctrl stay the list's (selection) and M4's
    [InlineData(Key.Up, ModifierKeys.None)]
    [InlineData(Key.Down, ModifierKeys.None)]
    [InlineData(Key.Up, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.Down, ModifierKeys.Control)]
    [InlineData(Key.Left, ModifierKeys.Alt)]
    public void Other_keys_are_left_to_the_list(Key key, ModifierKeys modifiers) => StaThread.Run(() =>
    {
        var (session, vm, panel) = Panel();
        int steps = session.UndoCount;
        Assert.False(panel.HandleKey(key, modifiers, vm.Rows[1]));
        Assert.False(panel.HandleKey(Key.Up, ModifierKeys.Alt, null));
        Assert.Equal("CBA", Names(vm));
        Assert.Equal(steps, session.UndoCount);
    });

    [Fact] // styles.css:310-312: the source row at opacity .4, a 2px #17d4fa edge on the target's top or bottom
    public void The_drag_dims_the_source_row_and_draws_the_targets_edge() => StaThread.Run(() =>
    {
        var (_, vm, panel) = Panel();
        vm.BeginRowDrag(1);
        vm.DragOverRow(3, above: false);
        Layout(panel);
        Assert.Equal(0.4, Item(panel, vm.Rows[2]).Opacity);
        Assert.Equal(1.0, Item(panel, vm.Rows[0]).Opacity);
        var target = Item(panel, vm.Rows[0]);
        var above = (Rectangle)target.Template.FindName("DropAbove", target);
        var below = (Rectangle)target.Template.FindName("DropBelow", target);
        Assert.Equal((Visibility.Collapsed, Visibility.Visible), (above.Visibility, below.Visibility));
        Assert.Equal((2.0, "#FF17D4FA"), (below.Height, ((SolidColorBrush)below.Fill).Color.ToString()));
        vm.EndRowDrag();
        Layout(panel);
        Assert.Equal(1.0, Item(panel, vm.Rows[2]).Opacity);
        Assert.Equal(Visibility.Collapsed, below.Visibility);
    });
}
