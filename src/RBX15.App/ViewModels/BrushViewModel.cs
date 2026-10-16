using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's Brush group (editor.html:53-62; ledger home-brush-group, draw-brush-controls,
/// tools-draw-props): the draw color, the brush size and the shape fill. Defaults are 4.1's:
/// #17d4fa, 4 and off (editor.js:90-92, editor.html:56-59). None of it is document state, so no
/// change here is ever an undo step (draw-brush-controls; interfaces R7). The color opens the themed
/// picker with live preview; a cancel puts the seed back (IColorPicker contract).
/// </summary>
public sealed partial class BrushViewModel : ObservableObject
{
    public const string DefaultColor = "#17d4fa";

    readonly IColorPicker _picker;
    string _color = DefaultColor;
    int _size = Tools.DefaultBrushSize;

    public BrushViewModel(IColorPicker picker)
    {
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        PickColorCommand = new RelayCommand<object?>(Pick);
    }

    /// <summary>drawColor (editor.js:492): lowercase #rrggbb, as an input of type color reports it.</summary>
    public string Color
    {
        get => _color;
        set
        {
            var next = NormalizeHex(value);
            if (SetProperty(ref _color, next)) OnPropertyChanged(nameof(SkColor));
        }
    }

    /// <summary>drawSize (editor.js:493): whole px, clamped to the range input's 1..50 (editor.html:57).</summary>
    public int Size
    {
        get => _size;
        set => SetProperty(ref _size, Math.Clamp(value, Tools.MinBrushSize, Tools.MaxBrushSize));
    }

    /// <summary>drawFill (editor.js:494): Rect and Ellipse fill instead of stroking (editor.js:1222, :1231).</summary>
    [ObservableProperty]
    private bool _fill;

    /// <summary><see cref="Color"/> as the opaque Skia color strokes are drawn in.</summary>
    public SKColor SkColor => SKColor.Parse(_color);

    /// <summary>Opens the picker under the anchor (the swatch); every preview sets Color, and a cancel restores the seed.</summary>
    public IRelayCommand<object?> PickColorCommand { get; }

    /// <summary>The wheel's brush step (editor.js:1316-1318): +1 for sign &gt;= 0, else -1, clamped 1..50.</summary>
    public void StepSize(int sign) => Size = Tools.StepBrushSize(Size, sign);

    void Pick(object? anchor)
    {
        var seed = _color;
        string? picked = null;
        try { picked = _picker.Pick(seed, hex => Color = hex, anchor); }
        finally { Color = picked ?? seed; }   // a cancel, or a picker that throws, reverts
    }

    static string NormalizeHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#' || hex.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF"))
            throw new ArgumentException($"'{hex}' is not #rrggbb.", nameof(hex));
        return hex.ToLowerInvariant();
    }
}
