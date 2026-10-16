using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The "Selected layer" panel. Every slider shares one <see cref="SliderGestureTracker"/>, so a slider drag
/// is one undo step. The three text fields commit on Enter, on focus loss, and on any mouse press elsewhere
/// in the window: a browser blurs a focused input on a press anywhere, so 4.1's change event fired before
/// the press acted (props-name-xy), but the WPF canvas takes no keyboard focus, and without this a typed
/// name or position would be overwritten, uncommitted, when a canvas click selects another layer.
/// </summary>
public partial class LayerPropertiesPanel : UserControl
{
    readonly SliderGestureTracker _tracker;
    Window? _window;

    public LayerPropertiesPanel() : this(SliderGestureTracker.RealLeftButtonDown) { }

    /// <param name="leftButtonDown">Whether the left button is down right now; tests inject it.</param>
    internal LayerPropertiesPanel(Func<bool> leftButtonDown)
    {
        _tracker = new SliderGestureTracker(() => (DataContext as LayerPropertiesViewModel)?.BeginSliderDrag(), leftButtonDown);
        InitializeComponent();
        foreach (var slider in Sliders) _tracker.Attach(slider);
        TextFields = [NameBox, XBox, YBox];
        Loaded += (_, _) => HookWindow(Window.GetWindow(this));
        Unloaded += (_, _) => { HookWindow(null); _tracker.End(); };
        DataContextChanged += (_, e) =>
        {
            _tracker.End();
            if (e.OldValue is LayerPropertiesViewModel old) old.HasUncommittedTyping = null;
            if (e.NewValue is LayerPropertiesViewModel vm) vm.HasUncommittedTyping = HasUncommittedTyping;
        };
    }

    /// <summary>
    /// Whether the field bound to <paramref name="property"/> holds typing not yet committed (its binding is dirty).
    /// A field commits on Enter, focus loss or a press elsewhere, so a dirty field is the one being typed in. The panel
    /// keeps that typing over a Document-only change of the same layer (final fix wave 3b): the view-model skips raising
    /// the field rather than the panel committing it on the session's Changing, because a commit there would record
    /// half-typed text as a step the user never confirmed, and the canvas edit would then overwrite the field anyway.
    /// </summary>
    bool HasUncommittedTyping(string property) => property switch
    {
        nameof(LayerPropertiesViewModel.Name) => IsDirty(NameBox),
        nameof(LayerPropertiesViewModel.XText) => IsDirty(XBox),
        nameof(LayerPropertiesViewModel.YText) => IsDirty(YBox),
        _ => false,
    };

    static bool IsDirty(TextBox box) => box.GetBindingExpression(TextBox.TextProperty)?.IsDirty == true;

    /// <summary>The ten sliders, in panel order.</summary>
    internal IReadOnlyList<Slider> Sliders =>
        [ScaleSlider, RotationSlider, OpacitySlider, BrightnessSlider, ContrastSlider, SaturateSlider, HueSlider, RSlider, GSlider, BSlider];

    /// <summary>Name, X and Y.</summary>
    internal IReadOnlyList<TextBox> TextFields { get; }

    internal SliderGestureTracker Tracker => _tracker;

    void HookWindow(Window? window)
    {
        if (_window is not null) _window.PreviewMouseDown -= OnWindowPreviewMouseDown;
        _window = window;
        if (_window is not null) _window.PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e) => CommitTextEdits(e.OriginalSource as DependencyObject);

    void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleTextKey((TextBox)sender, e.Key)) e.Handled = true;
    }

    /// <summary>Enter commits the field (4.1's change event fires on Enter). True when handled.</summary>
    internal static bool HandleTextKey(TextBox box, Key key)
    {
        if (key != Key.Enter) return false;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        return true;
    }

    /// <summary>
    /// Commits every text field with uncommitted typing, except the one <paramref name="pressed"/> is inside.
    /// A field with nothing typed is left alone; its commit would change nothing anyway.
    /// </summary>
    internal void CommitTextEdits(DependencyObject? pressed)
    {
        foreach (var box in TextFields)
        {
            if (pressed is not null && IsWithin(pressed, box)) continue;
            if (box.GetBindingExpression(TextBox.TextProperty) is { IsDirty: true } binding) binding.UpdateSource();
        }
    }

    static bool IsWithin(DependencyObject node, DependencyObject ancestor)
    {
        for (var d = node; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (ReferenceEquals(d, ancestor)) return true;
        return false;
    }
}
