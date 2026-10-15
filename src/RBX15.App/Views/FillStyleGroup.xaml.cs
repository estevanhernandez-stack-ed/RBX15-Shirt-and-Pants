using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. A slider press is one undo gesture, as in 4.1, which snapshots once on pointerdown
/// whether the press lands on the thumb or the track (editor.js:1611). The press (PreviewMouseLeftButtonDown)
/// opens it; the first of PreviewMouseLeftButtonUp, LostMouseCapture or DragCompleted closes it. On a
/// normal release the button-up fires first, then LostMouseCapture, then the Thumb's DragCompleted; the
/// later ones find nothing open. A press that finds a gesture still open
/// (a missed release) closes the stale one before it opens its own; a fresh press always does.
/// Holding the track fires LargeChange repeatedly, and all of it is the one step (spec §5: no history flood). Keyboard changes arrive outside a press, so each is its own step.
/// </summary>
public partial class FillStyleGroup : UserControl
{
    IDisposable? _drag;
    readonly Func<bool> _leftButtonDown;

    public FillStyleGroup() : this(() => Mouse.LeftButton == MouseButtonState.Pressed) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal FillStyleGroup(Func<bool> leftButtonDown)
    {
        _leftButtonDown = leftButtonDown;
        InitializeComponent();
    }

    void OnSliderPressed(object sender, MouseButtonEventArgs e) => Begin(freshPress: true);

    // Thumb.DragStarted follows the press that already opened the gesture; opening a second one is skipped.
    void OnSliderDragStarted(object sender, DragStartedEventArgs e) => Begin(freshPress: false);

    void OnSliderReleased(object sender, MouseButtonEventArgs e) => EndDrag();

    void OnSliderDragCompleted(object sender, DragCompletedEventArgs e) => EndDrag();

    // A press that loses mouse capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
    // would leave the gesture open and fold every later edit into one undo step. After a normal drag
    // the button-up has already closed it and this finds nothing open.
    void OnSliderLostMouseCapture(object sender, MouseEventArgs e) => EndDrag();

    /// <param name="freshPress">True for the press itself: any gesture still open then missed its release.</param>
    internal void Begin(bool freshPress)
    {
        // A gesture still open when its release never arrived is stale: close it so the new press is its
        // own undo step instead of folding into the old one. At the press itself the button is already
        // down again, so a fresh press treats any open gesture as stale; Thumb.DragStarted (the same press,
        // button down) leaves its gesture alone, and only closes one if the button is already up.
        if (_drag is not null && (freshPress || !_leftButtonDown())) EndDrag();
        _drag ??= (DataContext as FillStyleViewModel)?.BeginSliderDrag();
    }

    void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
    }
}
