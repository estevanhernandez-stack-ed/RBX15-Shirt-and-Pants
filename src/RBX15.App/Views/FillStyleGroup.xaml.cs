using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. A slider thumb drag is one undo gesture: DragStarted opens it and
/// DragCompleted (which also fires when capture is lost) closes it (interfaces sheet §1,
/// <c>fillstyle-undo-snapshot-timing</c>). Keyboard and track-click changes arrive outside a drag, so
/// each is its own step.
/// </summary>
public partial class FillStyleGroup : UserControl
{
    IDisposable? _drag;

    public FillStyleGroup() => InitializeComponent();

    void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _drag?.Dispose();
        _drag = (DataContext as FillStyleViewModel)?.BeginSliderDrag();
    }

    void OnSliderDragCompleted(object sender, DragCompletedEventArgs e) => EndDrag();

    // A thumb that loses mouse capture without a DragCompleted (focus stolen mid-drag, a dialog, Alt+Tab)
    // would leave the gesture open and fold every later edit into one undo step. After a normal drag
    // this fires once more with nothing open, which is a no-op.
    void OnSliderLostMouseCapture(object sender, MouseEventArgs e) => EndDrag();

    void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
    }
}
