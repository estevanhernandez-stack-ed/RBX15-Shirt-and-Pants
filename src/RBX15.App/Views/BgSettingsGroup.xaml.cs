using System.Windows.Controls;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Layout tab's BG Settings group. The Opacity slider has its own <see cref="SliderGestureTracker"/>: a press is one
/// undo gesture, as 4.1 snapshotted once on pointerdown (editor.js:931), and an arrow key records a step of its own.
/// </summary>
public partial class BgSettingsGroup : UserControl
{
    readonly SliderGestureTracker _tracker;

    public BgSettingsGroup() : this(SliderGestureTracker.RealLeftButtonDown) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal BgSettingsGroup(Func<bool> leftButtonDown)
    {
        _tracker = new SliderGestureTracker(() => (DataContext as BackgroundViewModel)?.BeginOpacityDrag(), leftButtonDown);
        InitializeComponent();
        _tracker.Attach(OpacitySlider);
    }

    /// <param name="freshPress">True for the press itself: any gesture still open then missed its release.</param>
    internal void Begin(bool freshPress) => _tracker.Begin(freshPress);
}
