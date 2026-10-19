using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>One Background swatch: 4.1's <c>BG_COLORS</c> entry (editor.js:66-71), shown with its name as the tooltip.</summary>
public sealed record BgSwatch(string Name, string Hex)
{
    /// <summary>What assistive tech reads (a focusable button in v5; 4.1's swatches were plain divs).</summary>
    public string AutomationName => $"Background {Name}";
}

/// <summary>
/// The Layout tab's Background and BG Settings groups (editor.html:125-144, editor.js:916-946; ledger bg-palette,
/// bg-swatches, layout-background-swatches, bg-color-opacity, bg-image, layout-bg-settings, bg-color-pick-clears-image).
/// <para>
/// <see cref="Color"/> and <see cref="OpacityPercent"/> are read from the document, so Undo, Redo and Open re-sync them at once
/// (4.1's restoreState re-set both inputs, editor.js:1431-1432). Every edit is one undo step: a swatch, a picked color, a
/// picked image and each opacity change are one <see cref="EditorSession.Apply"/>; a color pick and an opacity drag are one
/// gesture each (4.1 snapshotted on pointerdown, editor.js:929-931), and a cancelled pick records nothing (<c>layout-bg-settings</c>
/// fix: no empty undo steps). A keyboard change on the opacity slider is a step of its own, which 4.1 skipped.
/// </para>
/// <para>
/// B1 (M4b decision, Este 2026-10-18): a new document starts transparent (BgOpacity 0, <c>doc-new-transparent-background</c>),
/// and 4.1 never touched opacity, so a swatch, color or image pick would change nothing visible. When the opacity is 0 the pick
/// also sets it to 100%, in the same undo step. An opacity above 0 is left alone.
/// </para>
/// </summary>
public sealed class BackgroundViewModel : ObservableObject
{
    /// <summary>The initial bgColor (editor.js:78), Navy; the color well's seed when the document's color is not a plain hex.</summary>
    public const string DefaultColor = "#192e44";

    /// <summary>BG_COLORS in 4.1's order (editor.js:66-71).</summary>
    public static IReadOnlyList<BgSwatch> StandardSwatches { get; } =
    [
        new("Navy", "#192e44"), new("Navy Deep", "#0f1f31"), new("Black", "#050c18"),
        new("Charcoal", "#2d2d2d"), new("Wine", "#4a1942"), new("Forest", "#1b4332"),
        new("Ocean", "#0c2461"), new("Storm", "#2c3e50"), new("White", "#f5f5f5"),
        new("Red", "#b71c1c"), new("Magenta", "#f22f89"), new("Cyan", "#17d4fa"),
    ];

    readonly EditorSession _session;
    readonly IColorPicker _picker;
    readonly IFileDialogs _dialogs;
    readonly IStatus _status;
    IDisposable? _opacityDrag;

    public BackgroundViewModel(EditorSession session, IColorPicker picker, IFileDialogs dialogs, IStatus status)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        SetSwatchCommand = new RelayCommand<string>(hex => SetColor(hex ?? throw new ArgumentNullException(nameof(hex))));
        PickColorCommand = new RelayCommand<object?>(PickColor);
        PickImageCommand = new RelayCommand(PickImage);
        _session.Changed += (_, e) =>
        {
            if ((e.What & SessionChange.Document) == 0) return;
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(OpacityPercent));
        };
    }

    public IReadOnlyList<BgSwatch> Swatches => StandardSwatches;

    /// <summary>
    /// The color well's color, lowercase <c>#rrggbb</c>: the document's background color (kept while an image is set, as 4.1's
    /// input kept its value). A color that is not a plain hex (a project's "transparent", a short form) reads as <see cref="DefaultColor"/>.
    /// </summary>
    public string Color => ColorOf(_session.Document.BgColor);

    /// <summary>The Opacity slider's value, 0 to 100 (editor.html:140; bgOpacity * 100, editor.js:1432). Setting it is one undo step.</summary>
    public double OpacityPercent
    {
        get => Math.Round(_session.Document.BgOpacity * 100, MidpointRounding.AwayFromZero);
        set
        {
            if (double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), "NaN is not a slider value.");
            double percent = Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
            if (percent == OpacityPercent) return;   // a re-sync echo (a project's 0.555 shows 56) never rewrites the document
            _session.Apply(d => DocumentOps.SetBackgroundOpacity(d, percent / 100));
        }
    }

    /// <summary>Sets the background to a swatch's hex (the parameter): the color, no image, one step. Under B1 it also reveals a 0% background.</summary>
    public IRelayCommand<string> SetSwatchCommand { get; }

    /// <summary>Opens the themed picker under the anchor (the parameter); the pick is one gesture with a live preview.</summary>
    public IRelayCommand<object?> PickColorCommand { get; }

    /// <summary>The "Background" button: one image through the single-select picker, decoded under the image caps.</summary>
    public IRelayCommand PickImageCommand { get; }

    /// <summary>
    /// Opens one slider gesture (Thumb.DragStarted). Dispose it on DragCompleted: the drag records one undo step iff the opacity
    /// changed. Wraps <see cref="EditorSession.BeginGesture"/>.
    /// </summary>
    public IDisposable BeginOpacityDrag() => _opacityDrag = _session.BeginGesture();

    void SetColor(string hex) => _session.Apply(d => Reveal(DocumentOps.SetBackgroundColor(d, hex)));

    /// <summary>
    /// A pick as one gesture. Each preview recomputes the background from the document as it was at the open, so a preview
    /// never compounds. A cancel, a picker that throws, and a pick that ends where it started (no input event in 4.1) put the
    /// opening document back, so nothing is recorded and an image the pick would have dropped stays.
    /// </summary>
    void PickColor(object? anchor)
    {
        // A slider gesture still open (its release never arrived) is stale: close it so the pick is its own step.
        _opacityDrag?.Dispose();
        _opacityDrag = null;
        var start = _session.Document;
        string seed = Color;
        bool moved = false;
        using (_session.BeginGesture())
        {
            void Show(string hex)
            {
                if (!string.Equals(hex, seed, StringComparison.OrdinalIgnoreCase)) moved = true;
                _session.Apply(_ => Reveal(DocumentOps.SetBackgroundColor(start, hex)));
            }
            string? picked = null;
            try { picked = _picker.Pick(seed, Show, anchor); }
            finally
            {
                if (picked is null || (!moved && string.Equals(picked, seed, StringComparison.OrdinalIgnoreCase)))
                    _session.Apply(_ => start);
                else Show(picked);
            }
        }
    }

    void PickImage()
    {
        string? path = _dialogs.OpenImage();
        if (path is null) return;
        var result = ImageImporter.Load([path]);
        if (result.Images.Count == 0)
        {
            var failure = result.Failures[0];
            _status.Show($"Couldn't use {failure.FileName} as the background: {failure.Reason}");
            return;
        }
        var image = result.Images[0].Image;
        _session.Apply(d => Reveal(DocumentOps.SetBackgroundImage(d, image)));
        _status.Clear();
    }

    /// <summary>B1: a pick on a 0% background also sets it to 100%.</summary>
    static Document Reveal(Document doc) => doc.BgOpacity == 0 ? DocumentOps.SetBackgroundOpacity(doc, 1) : doc;

    static string ColorOf(string bgColor) =>
        bgColor.Length == 7 && bgColor[0] == '#' && CssColor.TryParseCanvasFill(bgColor, out _) ? bgColor.ToLowerInvariant() : DefaultColor;
}
