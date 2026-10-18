using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// The 3D avatar preview (v5.1 spec, App; ledger <c>avatar-preview-3d</c>, new). It follows the session's document:
/// a change marks the texture stale and schedules one rebuild at <see cref="DispatcherPriority.Background"/>, so a
/// drag's many changes cost one render per idle moment. A mode change rebuilds <see cref="Boxes"/> in the same
/// rebuild. While <see cref="IsVisible"/> is false nothing renders; showing the panel again rebuilds once if the
/// document moved meanwhile. Session state only: not in the document, not an undo step.
/// <para>
/// The view turns the avatar (<see cref="Yaw"/> about Y, <see cref="Pitch"/> about X, degrees, pitch clamped to
/// +/-30). The idle spin is a pure method, <see cref="Advance"/>, so the 2 s rule is tested with a fake clock: the view
/// feeds it frame deltas from <c>CompositionTarget.Rendering</c> only while loaded and visible.
/// </para>
/// </summary>
public sealed class AvatarPreviewViewModel : ObservableObject, IDisposable
{
    /// <summary>Idle spin speed (spec, Interaction).</summary>
    public const double SpinDegreesPerSecond = 20;

    /// <summary>The spin resumes this long after the last release or key press (spec, Interaction).</summary>
    public static readonly TimeSpan SpinResumeDelay = TimeSpan.FromSeconds(2);

    /// <summary>Degrees of turn per DIP of drag (spec, Interaction).</summary>
    public const double DegreesPerDip = 0.5;

    /// <summary>One Left / Right arrow press (spec, Panel).</summary>
    public const double KeyTurnDegrees = 15;

    public const double PitchLimit = 30;

    readonly EditorSession _session;
    readonly Func<bool> _animationsEnabled;
    readonly Func<TimeSpan> _clock;
    readonly Dispatcher _dispatcher;
    TimeSpan _lastInteraction = TimeSpan.MinValue;   // release or key press; MinValue = never
    bool _stale = true, _scheduled, _visible = true, _dragging, _disposed;
    double _yaw, _pitch;
    TemplateMode _mode;
    AvatarBody _body = AvatarBody.Classic;
    IReadOnlyList<AvatarBox> _boxes;
    BitmapSource? _texture;

    public AvatarPreviewViewModel(EditorSession session) : this(session, null, null) { }

    /// <param name="session">The shared editing state.</param>
    /// <param name="animationsEnabled">Whether Windows "Show animations" is on; default <see cref="SystemParameters.ClientAreaAnimation"/>.</param>
    /// <param name="clock">Monotonic time for the 2 s rule; tests inject a fake.</param>
    public AvatarPreviewViewModel(EditorSession session, Func<bool>? animationsEnabled, Func<TimeSpan>? clock = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _animationsEnabled = animationsEnabled ?? (() => SystemParameters.ClientAreaAnimation);
        if (clock is null)
        {
            var watch = Stopwatch.StartNew();
            clock = () => watch.Elapsed;
        }
        _clock = clock;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _mode = session.Document.Mode;
        _boxes = Avatar.Build(_mode, _body);
        session.Changed += OnSessionChanged;
        HideCommand = new RelayCommand(() => IsVisible = false);
        ShowCommand = new RelayCommand(() => IsVisible = true);
        Rebuild();
    }

    /// <summary>The panel header's close button: hides the panel, as unchecking "3D preview" does.</summary>
    public IRelayCommand HideCommand { get; }

    /// <summary>The "3D" edge tab shown while the panel is hidden: shows it again.</summary>
    public IRelayCommand ShowCommand { get; }

    public TemplateMode Mode => _mode;

    /// <summary>The classic six boxes or the approximate R15 fifteen for <see cref="Mode"/> (<see cref="Avatar.Build"/>).</summary>
    public IReadOnlyList<AvatarBox> Boxes => _boxes;

    /// <summary>The note under the viewport, per <see cref="Body"/>.</summary>
    public const string ClassicNote = "Classic body. R15 and Rthro avatars stretch the texture.";

    public const string R15Note = "R15 (approximate): parts split where Roblox cuts the template. Rthro and other mesh bodies differ.";

    /// <summary>
    /// Which body the preview wears; classic on every launch. Session state only: not in the document, not an undo step, not
    /// saved. A change rebuilds <see cref="Boxes"/> (the texture is the same either way).
    /// </summary>
    public AvatarBody Body
    {
        get => _body;
        set
        {
            if (_body == value) return;
            _body = value;
            _boxes = Avatar.Build(_mode, _body);
            OnPropertyChanged(nameof(Body));
            OnPropertyChanged(nameof(BodyNote));
            OnPropertyChanged(nameof(Boxes));
        }
    }

    public string BodyNote => _body == AvatarBody.R15 ? R15Note : ClassicNote;

    /// <summary>The document on the body color (<see cref="Avatar.RenderTexture"/>) as a frozen 96 DPI bitmap; null before the first build.</summary>
    public BitmapSource? Texture => _texture;

    /// <summary>How many times the texture has been built, the first build at construction included. Tests observe the coalescing here.</summary>
    public int TextureBuilds { get; private set; }

    public double Yaw
    {
        get => _yaw;
        set => SetProperty(ref _yaw, WrapYaw(value));
    }

    public double Pitch
    {
        get => _pitch;
        set => SetProperty(ref _pitch, Math.Clamp(value, -PitchLimit, PitchLimit));
    }

    /// <summary>False while the panel is toggled off or not shown: no rebuild, no spin.</summary>
    public bool IsVisible
    {
        get => _visible;
        set
        {
            if (!SetProperty(ref _visible, value)) return;
            if (value && _stale) Rebuild();
        }
    }

    public bool IsDragging => _dragging;

    /// <summary>A pointer drag is under way: the spin pauses.</summary>
    public void BeginDrag()
    {
        _dragging = true;
        OnPropertyChanged(nameof(IsDragging));
    }

    /// <summary>Turns by a pointer move in DIPs: right drag turns the front toward the viewer's right; drag down tips the head toward the viewer.</summary>
    public void DragTo(double dx, double dy)
    {
        Yaw += dx * DegreesPerDip;
        Pitch += dy * DegreesPerDip;
    }

    public void EndDrag()
    {
        _dragging = false;
        _lastInteraction = _clock();
        OnPropertyChanged(nameof(IsDragging));
    }

    /// <summary>Left / Right arrow: a turn of <paramref name="degrees"/>, and the spin pauses for 2 s like a release.</summary>
    public void TurnBy(double degrees)
    {
        _lastInteraction = _clock();
        Yaw += degrees;
    }

    /// <summary>Home and double-click: the front view.</summary>
    public void ResetView()
    {
        _lastInteraction = _clock();
        Yaw = 0;
        Pitch = 0;
    }

    /// <summary>
    /// One frame of the idle spin: adds <see cref="SpinDegreesPerSecond"/> times <paramref name="elapsed"/> to
    /// <see cref="Yaw"/>, unless dragging, within <see cref="SpinResumeDelay"/> of the last release or key press, hidden,
    /// or Windows animations are off.
    /// </summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || !_visible || _dragging || !_animationsEnabled()) return;
        if (_lastInteraction != TimeSpan.MinValue && _clock() - _lastInteraction < SpinResumeDelay) return;
        Yaw += SpinDegreesPerSecond * elapsed.TotalSeconds;
    }

    static double WrapYaw(double degrees)
    {
        double y = degrees % 360;
        if (y > 180) y -= 360;
        else if (y <= -180) y += 360;
        return y;
    }

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (!e.What.HasFlag(SessionChange.Document)) return;
        _stale = true;
        if (_visible) Schedule();
    }

    void Schedule()
    {
        if (_scheduled || _disposed) return;
        _scheduled = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _scheduled = false;
            if (_stale && _visible && !_disposed) Rebuild();
        });
    }

    /// <summary>Takes the session's mode and rebuilds the boxes if it moved, then the texture. Only the visible panel calls it.</summary>
    void Rebuild()
    {
        _stale = false;
        var doc = _session.Document;
        bool modeMoved = doc.Mode != _mode;
        if (modeMoved)
        {
            _mode = doc.Mode;
            _boxes = Avatar.Build(_mode, _body);
        }
        using (SKImage image = Avatar.RenderTexture(doc))
            _texture = AssetThumbnailLoader.ToBitmapSource(image);
        TextureBuilds++;
        if (modeMoved)
        {
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Boxes));
        }
        OnPropertyChanged(nameof(Texture));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Changed -= OnSessionChanged;
    }
}
