using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>One waFont option: the CSS family the render uses and the label the select shows (editor.html:230-246).</summary>
public sealed record FontChoice(string Family, string Label);

/// <summary>One Word Art style button: the engine style and its 4.1 label (editor.html:269-274).</summary>
public sealed record WordArtStyleOption(WordArtStyle Style, string Label);

/// <summary>
/// The Text tab (editor.html:226-283, editor.js:2108-2300; ledger wordart-state-defaults, text-inputs, text-preview,
/// wordart-preview-live, wordart-font-loading-race, text-add-to-canvas, wordart-add-to-canvas). Every control is a setting, not
/// the document: nothing here is an undo step until <see cref="AddCommand"/> puts a layer on the canvas.
/// <para>
/// Size is a number box: typed text is read live (the preview follows each keystroke) and settles on commit
/// (<see cref="CommitSize"/>, which the view calls on lost focus and Enter). The value is +v || 64, then clamped to 8 through 300
/// (<c>wordart-state-defaults</c>, approved divergence: 4.1 accepted any typed number).
/// </para>
/// <para>
/// <see cref="Preview"/> (ruling T2) is the 200x80 DIP box's content drawn at device pixels, sharp at 125% and above, and
/// cropped to the content so the view centers it (<c>text-preview</c> fix: 4.1 drew at the top-left). It renders on first read,
/// which is before the first frame since the pane binds it, with no font gate (<c>wordart-font-loading-race</c>: the
/// fonts are vendored). Each later change re-renders once, coalesced at <see cref="DispatcherPriority.Background"/>, so a
/// slider drag or a burst of keystrokes costs one render per dispatcher pass.
/// </para>
/// <para>
/// Add (<c>wordart-add-to-canvas</c>, <c>text-add-to-canvas</c>) is <see cref="WordArt.CreateLayer"/> in one
/// <see cref="EditorSession.ApplyWithSelection"/>, then <see cref="LayersAdded"/> (the composition root sets the Select tool).
/// Text too big for the image caps still adds one layer at the requested size, with no message (ruling T1).
/// </para>
/// </summary>
public sealed class TextViewModel : ObservableObject
{
    /// <summary>#waText's initial value (editor.html:228).</summary>
    public const string DefaultText = "RIZZ";

    /// <summary>#waText's placeholder (editor.html:228).</summary>
    public const string Placeholder = "Type text...";

    public const string DefaultFont = "Anton";
    public const double DefaultSize = 64;
    public const string DefaultFill = "#17d4fa";
    public const string DefaultStroke = "#000000";
    public const string DefaultGrad = "#f22f89";
    public const int DefaultStrokeWidth = 3;
    public const int DefaultAngle = 0;

    /// <summary>#waStrokeW's range (editor.html:263).</summary>
    public const int MinStrokeWidth = 0, MaxStrokeWidth = 20;

    /// <summary>#waAngle's range (editor.html:264).</summary>
    public const int MinAngle = 0, MaxAngle = 360;

    /// <summary>updateWordArtPreview's maxima (editor.js:2249), in DIPs.</summary>
    public const int PreviewMaxWidth = 200, PreviewMaxHeight = 80;

    readonly EditorSession _session;
    readonly IColorPicker _picker;
    readonly IStatus _status;
    readonly Dispatcher _dispatcher;
    string _text = DefaultText;
    string _font = DefaultFont;
    WordArtStyle _style = WordArtStyle.Solid;
    double _size = DefaultSize;
    string _sizeText = FormatSize(DefaultSize);
    string _fill = DefaultFill;
    string _stroke = DefaultStroke;
    string _grad = DefaultGrad;
    int _strokeWidth = DefaultStrokeWidth;
    int _angle = DefaultAngle;
    double _deviceScale = 1;
    ImageSource? _preview;
    bool _scheduled;

    public TextViewModel(EditorSession session, IColorPicker picker, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _dispatcher = Dispatcher.CurrentDispatcher;
        Fonts = WordArt.FontOptions.Select(o => new FontChoice(o.Family, o.Label)).ToList();
        Styles =
        [
            new(WordArtStyle.Solid, "Solid"), new(WordArtStyle.Gradient, "Gradient"), new(WordArtStyle.Neon, "Neon"),
            new(WordArtStyle.Shadow, "Shadow"), new(WordArtStyle.Outline, "Outline"), new(WordArtStyle.Retro, "Retro"),
        ];
        PickFillCommand = new RelayCommand<object?>(anchor => Pick(() => FillColor, v => FillColor = v, anchor));
        PickStrokeCommand = new RelayCommand<object?>(anchor => Pick(() => StrokeColor, v => StrokeColor = v, anchor));
        PickGradCommand = new RelayCommand<object?>(anchor => Pick(() => GradColor, v => GradColor = v, anchor));
        AddCommand = new RelayCommand(Add);
    }

    /// <summary>Raised after Add put a layer on the canvas and selected it.</summary>
    public event EventHandler? LayersAdded;

    /// <summary>The 15 font options in 4.1's order. The select does not draw an option in its own face.</summary>
    public IReadOnlyList<FontChoice> Fonts { get; }

    /// <summary>The six style buttons in 4.1's order; <see cref="Style"/> is the one active.</summary>
    public IReadOnlyList<WordArtStyleOption> Styles { get; }

    /// <summary>The text, as typed; several lines are several lines of art. Empty renders "TEXT" (editor.js:2121).</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? "")) Invalidate();
        }
    }

    /// <summary>The CSS family, one of <see cref="Fonts"/> (the value, not the label).</summary>
    public string Font
    {
        get => _font;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!Fonts.Any(f => f.Family == value)) throw new ArgumentException($"'{value}' is not one of the Word Art fonts.", nameof(value));
            if (SetProperty(ref _font, value)) Invalidate();
        }
    }

    public WordArtStyle Style
    {
        get => _style;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _style, value)) Invalidate();
        }
    }

    /// <summary>The font size, 8 through 300 (NaN or 0 is 64, as 4.1's <c>+v || 64</c>).</summary>
    public double Size
    {
        get => _size;
        set
        {
            double next = NormalizeSize(value);
            string text = FormatSize(next);
            bool textMoved = _sizeText != text;
            bool moved = _size != next;
            _size = next;
            _sizeText = text;
            if (moved) OnPropertyChanged(nameof(Size));
            if (textMoved) OnPropertyChanged(nameof(SizeText));
            if (moved) Invalidate();
        }
    }

    /// <summary>
    /// What the size box shows. Setting it reads the text live and leaves it as typed, so "5" does not become "8" under the caret;
    /// <see cref="CommitSize"/> settles it.
    /// </summary>
    public string SizeText
    {
        get => _sizeText;
        set
        {
            value ??= "";
            if (_sizeText == value) return;
            _sizeText = value;
            OnPropertyChanged(nameof(SizeText));
            double next = NormalizeSize(double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN);
            if (_size == next) return;
            _size = next;
            OnPropertyChanged(nameof(Size));
            Invalidate();
        }
    }

    /// <summary>Settles the size box on the clamped value: "5" becomes "8", "" and "abc" become "64".</summary>
    public void CommitSize()
    {
        string text = FormatSize(_size);
        if (_sizeText == text) return;
        _sizeText = text;
        OnPropertyChanged(nameof(SizeText));
    }

    /// <summary>The fill (waColor), lowercase <c>#rrggbb</c>.</summary>
    public string FillColor
    {
        get => _fill;
        set
        {
            if (SetProperty(ref _fill, NormalizeHex(value))) Invalidate();
        }
    }

    /// <summary>The stroke (waStroke), lowercase <c>#rrggbb</c>.</summary>
    public string StrokeColor
    {
        get => _stroke;
        set
        {
            if (SetProperty(ref _stroke, NormalizeHex(value))) Invalidate();
        }
    }

    /// <summary>The gradient end (waColor2), lowercase <c>#rrggbb</c>.</summary>
    public string GradColor
    {
        get => _grad;
        set
        {
            if (SetProperty(ref _grad, NormalizeHex(value))) Invalidate();
        }
    }

    /// <summary>The stroke width, whole numbers 0 through 20 (editor.html:263).</summary>
    public int StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            if (SetProperty(ref _strokeWidth, Math.Clamp(value, MinStrokeWidth, MaxStrokeWidth))) Invalidate();
        }
    }

    /// <summary>The gradient angle in degrees, whole numbers 0 through 360 (editor.html:264).</summary>
    public int Angle
    {
        get => _angle;
        set
        {
            if (SetProperty(ref _angle, Math.Clamp(value, MinAngle, MaxAngle))) Invalidate();
        }
    }

    /// <summary>The Word Art settings the controls hold right now (what the preview draws and Add renders).</summary>
    public WordArtSettings Settings => new(_text, _font, _style, (float)_size, CssColor.ToSk(_fill), CssColor.ToSk(_stroke),
                                           CssColor.ToSk(_grad), _strokeWidth, _angle);

    /// <summary>
    /// Device pixels per DIP of the display the pane is on (1 at 100%). The view sets it; a change re-renders the preview
    /// at once, since a DPI change is not a stream of edits.
    /// </summary>
    public double DeviceScale
    {
        get => _deviceScale;
        set
        {
            if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_deviceScale == value) return;
            _deviceScale = value;
            if (_preview is not null) Render();
        }
    }

    /// <summary>
    /// The live preview: the art at device pixels, cropped to the content, at the bitmap's DPI so it is <see cref="DeviceScale"/>
    /// times fewer DIPs than pixels. Never null; the first read renders it.
    /// </summary>
    public ImageSource Preview
    {
        get
        {
            if (_preview is null) Render();
            return _preview!;
        }
    }

    /// <summary>How many times the preview has been drawn. Tests observe the coalescing here.</summary>
    public int PreviewRenders { get; private set; }

    /// <summary>Opens the themed picker under the anchor (the parameter) for the fill; a cancel restores it.</summary>
    public IRelayCommand<object?> PickFillCommand { get; }

    /// <summary>Opens the themed picker under the anchor for the stroke.</summary>
    public IRelayCommand<object?> PickStrokeCommand { get; }

    /// <summary>Opens the themed picker under the anchor for the gradient end.</summary>
    public IRelayCommand<object?> PickGradCommand { get; }

    /// <summary>"Add to canvas": one layer on top, selected, in one undo step.</summary>
    public IRelayCommand AddCommand { get; }

    static string FormatSize(double size) => size.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>+waSize || 64 (editor.js:2125), then clamped to the input's 8..300.</summary>
    static double NormalizeSize(double v) => double.IsNaN(v) || v == 0 ? DefaultSize : Math.Clamp(v, WordArt.MinSize, WordArt.MaxSize);

    static string NormalizeHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 7 || value[0] != '#' || !value.Skip(1).All(Uri.IsHexDigit))
            throw new ArgumentException($"'{value}' is not a #rrggbb color.", nameof(value));
        return value.ToLowerInvariant();
    }

    void Pick(Func<string> get, Action<string> set, object? anchor)
    {
        string before = get();
        string? picked = null;
        try { picked = _picker.Pick(before, set, anchor); }
        finally { set(picked ?? before); }
    }

    /// <summary>A control changed: the preview, if anyone has looked at it, is redrawn once at Background priority.</summary>
    void Invalidate()
    {
        if (_preview is null || _scheduled) return;
        _scheduled = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _scheduled = false;
            Render();
        });
    }

    void Render()
    {
        var (image, contentWidth, contentHeight, contentScale) = WordArt.Render(Settings,
            (int)Math.Round(PreviewMaxWidth * _deviceScale), (int)Math.Round(PreviewMaxHeight * _deviceScale), _deviceScale);
        using (image)
        {
            var full = AssetThumbnailLoader.ToBitmapSource(image, 96 * _deviceScale);
            // The surface is min(content, maximum) wide; the content is drawn scaled from its top-left. Crop to the content so
            // the view's centering centers the art (text-preview fix).
            int w = Math.Clamp((int)Math.Ceiling(contentWidth * contentScale), 1, full.PixelWidth);
            int h = Math.Clamp((int)Math.Ceiling(contentHeight * contentScale), 1, full.PixelHeight);
            var cropped = new CroppedBitmap(full, new Int32Rect(0, 0, w, h));
            cropped.Freeze();
            _preview = cropped;
        }
        PreviewRenders++;
        OnPropertyChanged(nameof(Preview));
    }

    /// <summary>Add to canvas (editor.js:2257-2291).</summary>
    void Add()
    {
        int id = _session.NextId;
        Layer layer;
        try { layer = WordArt.CreateLayer(Settings, _session.Document.Mode, id); }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or OutOfMemoryException)
        {
            // Text beyond the image caps does not come here (ruling T1: it degrades); this is a machine that could not render.
            _status.Show($"Word Art could not be added: {e.Message}");
            return;
        }
        _session.TakeId();
        _session.ApplyWithSelection(s => LayerOps.AddDrawing(s, layer));
        LayersAdded?.Invoke(this, EventArgs.Empty);
    }
}
