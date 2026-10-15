using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. A slider press is one undo gesture, as in 4.1, which snapshots once on pointerdown
/// whether the press lands on the thumb or the track (editor.js:1611). The press (PreviewMouseLeftButtonDown)
/// opens it; the first of LostMouseCapture, DragCompleted or the button release closes it. WPF's Thumb
/// releases capture before it raises DragCompleted, so LostMouseCapture normally comes first; the others
/// are backstops. Holding the track fires LargeChange repeatedly, and all of it is the one step (spec §5:
/// no history flood). Keyboard changes arrive outside a press, so each is its own step.
/// </summary>
public partial class FillStyleGroup : UserControl
{
    IDisposable? _drag;

    public FillStyleGroup() => InitializeComponent();

    void OnSliderPressed(object sender, MouseButtonEventArgs e) => Begin();

    // Thumb.DragStarted follows the press that already opened the gesture; opening a second one is skipped.
    void OnSliderDragStarted(object sender, DragStartedEventArgs e) => Begin();

    void OnSliderReleased(object sender, MouseButtonEventArgs e) => EndDrag();

    void OnSliderDragCompleted(object sender, DragCompletedEventArgs e) => EndDrag();

    // A press that loses mouse capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
    // would leave the gesture open and fold every later edit into one undo step. After a normal drag
    // this fires first, and the later events find nothing open.
    void OnSliderLostMouseCapture(object sender, MouseEventArgs e) => EndDrag();

    void Begin() => _drag ??= (DataContext as FillStyleViewModel)?.BeginSliderDrag();

    void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
    }
}
