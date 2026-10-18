using CommunityToolkit.Mvvm.ComponentModel;

namespace RBX15.App.ViewModels;

/// <summary>
/// The themed color picker's state (ledger <c>fillstyle-color-pickers</c>, fix; Este 2026-10-14). HSV
/// is the source of truth, so the hue survives a trip through gray or black; <see cref="Hex"/> is derived.
/// Every change of <see cref="Hex"/> calls the preview, so the canvas follows the drag. The view
/// (<see cref="Views.ColorPickerPopup"/>) ends it with <see cref="Commit"/> or <see cref="Cancel"/>;
/// <see cref="Result"/> is the picked lowercase <c>#rrggbb</c>, or null for a cancel.
/// </summary>
public sealed class ColorPickerViewModel : ObservableObject
{
    /// <summary>One arrow-key step on the saturation/value square, and the Shift step.</summary>
    public const double SvStep = 0.01, SvBigStep = 0.1;

    /// <summary>One arrow-key step on the hue strip in degrees, and the Shift step.</summary>
    public const double HueStep = 1, HueBigStep = 10;

    readonly Action<string>? _preview;
    double _hue, _saturation, _value;
    string _hex;
    string _hexText;

    public ColorPickerViewModel(string initialHex, Action<string>? preview = null, IReadOnlyList<string>? palette = null)
    {
        Palette = palette ?? Array.Empty<string>();
        OriginalHex = ColorMath.NormalizeHex(initialHex) ?? throw new ArgumentException($"'{initialHex}' is not #rgb or #rrggbb.", nameof(initialHex));
        _preview = preview;
        ColorMath.TryParseHex(OriginalHex, out var rgb);
        (_hue, _saturation, _value) = ColorMath.RgbToHsv(rgb.R, rgb.G, rgb.B);
        _hex = _hexText = OriginalHex;
    }

    /// <summary>
    /// The colors of the user's art (ledger <c>art-palette</c>), lowercase <c>#rrggbb</c>, as they were when the picker opened.
    /// Empty when there is none; the popup then hides its "From your art" row.
    /// </summary>
    public IReadOnlyList<string> Palette { get; }

    /// <summary>The color the picker opened on, lowercase <c>#rrggbb</c> (the old swatch).</summary>
    public string OriginalHex { get; }

    /// <summary>The current color, lowercase <c>#rrggbb</c> (the new swatch).</summary>
    public string Hex => _hex;

    /// <summary>The fully saturated, full-value color of <see cref="Hue"/>: the square's backdrop.</summary>
    public string HueHex
    {
        get
        {
            var (r, g, b) = ColorMath.HsvToRgb(_hue, 1, 1);
            return ColorMath.ToHex(r, g, b);
        }
    }

    /// <summary>Hue in degrees, clamped to 0 to 360.</summary>
    public double Hue
    {
        get => _hue;
        set => SetHsv(Clamp(value, 360), _saturation, _value);
    }

    /// <summary>Saturation, 0 to 1 (the square's x).</summary>
    public double Saturation
    {
        get => _saturation;
        set => SetHsv(_hue, Clamp(value, 1), _value);
    }

    /// <summary>Value (brightness), 0 to 1 (the square's y, upward).</summary>
    public double Value
    {
        get => _value;
        set => SetHsv(_hue, _saturation, Clamp(value, 1));
    }

    /// <summary>
    /// The hex field's text. Setting it never throws: text that parses (<see cref="ColorMath.TryParseHex"/>)
    /// sets the color at once, anything else is kept as typed and ignored until <see cref="RevertHexText"/>.
    /// </summary>
    public string HexText
    {
        get => _hexText;
        set
        {
            value ??= "";
            if (SetProperty(ref _hexText, value)) TrySetHex(value);
        }
    }

    public bool IsFinished { get; private set; }

    /// <summary>The picked color after <see cref="Commit"/>; null before, and after <see cref="Cancel"/>.</summary>
    public string? Result { get; private set; }

    /// <summary>Moves the square's point by a step, clamped (arrow keys; up is brighter).</summary>
    public void MoveSv(double dSaturation, double dValue) =>
        SetHsv(_hue, Clamp(_saturation + dSaturation, 1), Clamp(_value + dValue, 1));

    /// <summary>Sets the square's point from a position in it (0 to 1 each way, y downward), clamped.</summary>
    public void SetSvFromPoint(double x, double y) => SetHsv(_hue, Clamp(x, 1), Clamp(1 - y, 1));

    /// <summary>Moves the hue by a step, clamped to 0 to 360.</summary>
    public void MoveHue(double degrees) => SetHsv(Clamp(_hue + degrees, 360), _saturation, _value);

    /// <summary>Sets the hue from a position on the strip (0 to 1), clamped.</summary>
    public void SetHueFromPoint(double x) => SetHsv(Clamp(x, 1) * 360, _saturation, _value);

    /// <summary>Applies the hex field's text if it parses; returns whether it did.</summary>
    public bool TrySetHex(string? text)
    {
        if (!ColorMath.TryParseHex(text, out var rgb)) return false;
        var (h, s, v) = ColorMath.RgbToHsv(rgb.R, rgb.G, rgb.B);
        // A gray has no hue, and black no saturation: keep the ones the user had.
        if (s == 0 || v == 0) h = _hue;
        if (v == 0) s = _saturation;
        SetHsv(h, s, v, syncText: false);
        return true;
    }

    /// <summary>
    /// A palette swatch was chosen: sets the color exactly as typing <paramref name="hex"/> in the field would (so the preview
    /// fires), and does not end the pick. Returns false, changing nothing, for text that is not a color or once the pick has ended.
    /// </summary>
    public bool UseSwatch(string hex)
    {
        if (IsFinished || !TrySetHex(hex)) return false;
        SetHexText(_hex);
        return true;
    }

    /// <summary>Puts the hex field back to the current color (focus leaving the field with invalid text).</summary>
    public void RevertHexText() => SetHexText(_hex);

    /// <summary>Ends the pick with the current color. A second end does nothing.</summary>
    public void Commit()
    {
        if (IsFinished) return;
        TrySetHex(_hexText);
        IsFinished = true;
        Result = _hex;
    }

    /// <summary>Ends the pick with no color; the caller reverts. A second end does nothing.</summary>
    public void Cancel()
    {
        if (IsFinished) return;
        IsFinished = true;
        Result = null;
    }

    void SetHsv(double h, double s, double v, bool syncText = true)
    {
        if (IsFinished) return;
        bool hueChanged = h != _hue;
        if (hueChanged) { _hue = h; OnPropertyChanged(nameof(Hue)); OnPropertyChanged(nameof(HueHex)); }
        if (s != _saturation) { _saturation = s; OnPropertyChanged(nameof(Saturation)); }
        if (v != _value) { _value = v; OnPropertyChanged(nameof(Value)); }
        var (r, g, b) = ColorMath.HsvToRgb(_hue, _saturation, _value);
        string hex = ColorMath.ToHex(r, g, b);
        if (syncText) SetHexText(hex);
        if (hex == _hex) return;
        _hex = hex;
        OnPropertyChanged(nameof(Hex));
        _preview?.Invoke(hex);
    }

    void SetHexText(string text)
    {
        if (_hexText == text) return;
        _hexText = text;
        OnPropertyChanged(nameof(HexText));
    }

    static double Clamp(double value, double max) =>
        double.IsNaN(value) ? throw new ArgumentOutOfRangeException(nameof(value), "NaN is not a picker value.") : Math.Clamp(value, 0, max);
}
