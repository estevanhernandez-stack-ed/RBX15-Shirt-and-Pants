using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.Controls;

namespace RBX15.App.Tests;

/// <summary>
/// The one slider-gesture rule for every slider (FillStyleGroup, LayerPropertiesPanel): a press is one undo
/// gesture (editor.js:929-931, :1611; spec §5), closed by the first release event, and a stale gesture never
/// swallows a later edit. Ledger fillstyle-undo-snapshot-timing, props-scale, props-rotation, props-opacity,
/// props-adjust-sliders.
/// </summary>
public class SliderGestureTrackerTests
{
    /// <summary>Counts opens and closes; Open returns a fresh scope each time.</summary>
    sealed class Gestures
    {
        public int Opened, Closed;
        public bool Down = true;
        public IDisposable? Open() { Opened++; return new Scope(this); }
        sealed class Scope(Gestures g) : IDisposable
        {
            bool _done;
            public void Dispose() { if (_done) return; _done = true; g.Closed++; }
        }
    }

    static (Gestures G, SliderGestureTracker T) Make()
    {
        var g = new Gestures();
        return (g, new SliderGestureTracker(g.Open, () => g.Down));
    }

    [Fact]
    public void A_press_opens_one_gesture_and_the_release_closes_it()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        Assert.True(t.IsOpen);
        t.End();
        t.End();                                         // LostMouseCapture and DragCompleted after the button-up find nothing
        Assert.False(t.IsOpen);
        Assert.Equal((1, 1), (g.Opened, g.Closed));
    }

    [Fact] // Thumb.DragStarted follows the press that opened the gesture: the same gesture
    public void DragStarted_after_the_press_keeps_the_same_gesture()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        t.Begin(freshPress: false);
        Assert.Equal((1, 0), (g.Opened, g.Closed));
    }

    [Fact] // a release that never arrived: the next press closes the stale gesture before opening its own
    public void A_fresh_press_closes_a_stale_gesture_first()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        t.Begin(freshPress: true);
        Assert.Equal((2, 1), (g.Opened, g.Closed));
        Assert.True(t.IsOpen);
    }

    [Fact] // the M3a final-review minor: Begin(false) with the button already up used to open a gesture nothing closed
    public void DragStarted_with_the_button_up_closes_a_stale_gesture_and_opens_none()
    {
        var (g, t) = Make();
        t.Begin(freshPress: true);
        g.Down = false;
        t.Begin(freshPress: false);
        Assert.False(t.IsOpen);
        Assert.Equal((1, 1), (g.Opened, g.Closed));
    }

    [Fact]
    public void DragStarted_with_the_button_up_and_nothing_open_opens_nothing()
    {
        var (g, t) = Make();
        g.Down = false;
        t.Begin(freshPress: false);
        Assert.False(t.IsOpen);
        Assert.Equal((0, 0), (g.Opened, g.Closed));
    }

    [Fact] // a view with no view-model yet: open returns null, and nothing throws
    public void A_null_gesture_is_harmless()
    {
        var t = new SliderGestureTracker(() => null, () => true);
        t.Begin(freshPress: true);
        Assert.False(t.IsOpen);
        t.End();
        Assert.Throws<ArgumentNullException>(() => new SliderGestureTracker(null!, () => true));
        Assert.Throws<ArgumentNullException>(() => new SliderGestureTracker(() => null, null!));
    }

    [Fact] // Attach wires the press, DragStarted, the button-up, DragCompleted and LostMouseCapture
    public void Attach_routes_a_sliders_press_drag_and_release() => StaThread.Run(() =>
    {
        var (g, t) = Make();
        var slider = new Slider();
        t.Attach(slider);

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new DragStartedEventArgs(0, 0));
        Assert.Equal((1, 0), (g.Opened, g.Closed));
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        Assert.Equal((1, 1), (g.Opened, g.Closed));

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
        Assert.Equal((2, 2), (g.Opened, g.Closed));

        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Assert.Equal((3, 3), (g.Opened, g.Closed));
        Assert.False(t.IsOpen);
    });
}
