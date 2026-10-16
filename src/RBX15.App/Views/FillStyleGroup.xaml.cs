using System.Windows.Controls;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Fill Style group. Its two sliders share one <see cref="SliderGestureTracker"/>: a slider press is
/// one undo gesture, as in 4.1, which snapshots once on pointerdown (editor.js:1611). The tracker holds the
/// open, close and stale-gesture rules for every slider in the app (FillStyleGroup and LayerPropertiesPanel).
/// </summary>
public partial class FillStyleGroup : UserControl
{
    readonly SliderGestureTracker _tracker;

    public FillStyleGroup() : this(SliderGestureTracker.RealLeftButtonDown) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal FillStyleGroup(Func<bool> leftButtonDown)
    {
        _tracker = new SliderGestureTracker(() => (DataContext as FillStyleViewModel)?.BeginSliderDrag(), leftButtonDown);
        InitializeComponent();
        // The same press, release, drag and capture-loss routing as LayerPropertiesPanel, press included when a
        // template marks it handled (final fix wave 15; the five XAML forwarding handlers are gone).
        _tracker.Attach(AngleSlider);
        _tracker.Attach(ScaleSlider);
    }

    /// <param name="freshPress">True for the press itself: any gesture still open then missed its release.</param>
    internal void Begin(bool freshPress) => _tracker.Begin(freshPress);
}
