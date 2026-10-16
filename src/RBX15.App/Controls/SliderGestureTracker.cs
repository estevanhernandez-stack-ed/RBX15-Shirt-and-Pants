using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RBX15.App.Controls;

/// <summary>
/// One slider press is one undo gesture, as in 4.1, which snapshots once on pointerdown whether the
/// press lands on the thumb or the track (editor.js:929-931, :1611). The press
/// (PreviewMouseLeftButtonDown) opens it; the first of PreviewMouseLeftButtonUp, LostMouseCapture or
/// Thumb.DragCompleted closes it. On a normal release the button-up fires first, then LostMouseCapture,
/// then DragCompleted; the later ones find nothing open. Holding the track fires LargeChange
/// repeatedly, and all of it is the one step (spec §5: no history flood). Keyboard changes arrive
/// outside a press, so each is its own step.
/// <para>
/// A gesture still open when its release never arrived is stale. A fresh press closes it before it
/// opens its own. Thumb.DragStarted (the same press) leaves an open gesture alone while the button is
/// down; with the button already up it closes any stale gesture and opens none (the M3a final-review
/// minor: the old code opened a new gesture there that nothing would close until the next press).
/// </para>
/// One tracker serves every slider of one view, so at most one slider gesture is open per view.
/// </summary>
public sealed class SliderGestureTracker
{
    readonly Func<IDisposable?> _open;
    readonly Func<bool> _leftButtonDown;
    IDisposable? _gesture;

    /// <param name="open">Opens the gesture (a view-model's BeginSliderDrag); null when there is nothing to edit.</param>
    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    public SliderGestureTracker(Func<IDisposable?> open, Func<bool> leftButtonDown)
    {
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _leftButtonDown = leftButtonDown ?? throw new ArgumentNullException(nameof(leftButtonDown));
    }

    /// <summary>The real mouse: <c>Mouse.LeftButton == Pressed</c>.</summary>
    public static bool RealLeftButtonDown() => Mouse.LeftButton == MouseButtonState.Pressed;

    /// <summary>True while a gesture is open.</summary>
    public bool IsOpen => _gesture is not null;

    /// <param name="freshPress">True for the press itself; false for Thumb.DragStarted, which follows it.</param>
    public void Begin(bool freshPress)
    {
        bool down = freshPress || _leftButtonDown();
        if (_gesture is not null && (freshPress || !down)) End();
        if (!down) return;
        _gesture ??= _open();
    }

    /// <summary>Closes the open gesture, if any. Calling it again does nothing.</summary>
    public void End()
    {
        var g = _gesture;
        _gesture = null;
        g?.Dispose();
    }

    /// <summary>Routes <paramref name="slider"/>'s press, release, drag and capture-loss events to this tracker.</summary>
    public void Attach(Slider slider)
    {
        ArgumentNullException.ThrowIfNull(slider);
        // handledEventsToo: a slider template or class handler that marks the press handled must not skip the gesture.
        slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, _) => Begin(freshPress: true)), handledEventsToo: true);
        slider.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => End()), handledEventsToo: true);
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => Begin(freshPress: false)));
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => End()));
        // A press that loses capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
        // would leave the gesture open and fold every later edit into one step.
        slider.LostMouseCapture += (_, _) => End();
    }
}
