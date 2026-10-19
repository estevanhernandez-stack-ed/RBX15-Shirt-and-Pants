using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.ViewModels;

/// <summary>One Details button: its 4.1 label, the data-detail id (<see cref="Details.Id"/>) and the click.</summary>
public sealed class DetailButton
{
    internal DetailButton(string label, DetailType type, bool isAccent, IRelayCommand command)
    {
        Label = label;
        Type = type;
        Id = Details.Id(type);
        IsAccent = isAccent;
        Command = command;
    }

    /// <summary>The button text (editor.html:187-222).</summary>
    public string Label { get; }

    public DetailType Type { get; }

    /// <summary>The 4.1 <c>data-detail</c> id, e.g. <c>collar-v</c>.</summary>
    public string Id { get; }

    /// <summary>The layer name a click creates (<see cref="Details.LayerName"/>).</summary>
    public string LayerName => Details.LayerName(Type);

    /// <summary>True for Full Set: the pink border and text (#f22f89, #ff5aa3).</summary>
    public bool IsAccent { get; }

    public IRelayCommand Command { get; }

    /// <summary>What assistive tech reads: the action and the layer it makes.</summary>
    public string AutomationName => $"Add {LayerName}";
}

/// <summary>A titled group of Details buttons ("Neckline", "Edges", "Seams", "Accents").</summary>
public sealed class DetailGroup
{
    internal DetailGroup(string label, IReadOnlyList<DetailButton> buttons, bool isLast)
    {
        Label = label;
        Buttons = buttons;
        IsLast = isLast;
    }

    /// <summary>The group title as 4.1 wrote it (editor.html:187-222); the ribbon shows it upper case.</summary>
    public string Label { get; }

    public string Caption => Label.ToUpperInvariant();

    public IReadOnlyList<DetailButton> Buttons { get; }

    /// <summary>The last group of the pane has no divider (styles.css:207).</summary>
    public bool IsLast { get; }
}

/// <summary>
/// The Details tab (editor.html:175-222; ledger details-settings-controls, details-buttons-inventory, details-settings,
/// details-buttons, details-layer-creation-pipeline). The Settings group holds the stroke color (default #333333), the width
/// (1 to 8, default 2) and Stitch. Their values are read only when a button is clicked, so changing them never touches a layer
/// already made (editor.js:1840-1842). A click is <see cref="Details.Create"/> and, when something was drawn, one
/// <see cref="EditorSession.ApplyWithSelection"/> that puts the layer on top and selects it, then <see cref="LayersAdded"/>
/// (the composition root sets the Select tool, as 4.1's createDetailLayer did, editor.js:1860-1861). Nothing drawn (a fully
/// transparent color) records no step and takes no id. Pants documents draw on the equivalent slots (details-pants-mode-behavior).
/// </summary>
public sealed class DetailsViewModel : ObservableObject
{
    /// <summary>#detailColor's default (editor.html:179).</summary>
    public const string DefaultColor = "#333333";

    public const int MinWidth = 1;
    public const int MaxWidth = 8;

    /// <summary>#detailWidth's default (editor.html:181).</summary>
    public const int DefaultWidth = 2;

    readonly EditorSession _session;
    readonly IColorPicker _picker;
    string _color = DefaultColor;
    int _width = DefaultWidth;
    bool _stitch;

    public DetailsViewModel(EditorSession session, IColorPicker picker)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        PickColorCommand = new RelayCommand<object?>(PickColor);
        Groups =
        [
            Group("Neckline", false, ("V-Neck", "collar-v"), ("Crew", "collar-crew"), ("Scoop", "collar-scoop"), ("Henley", "collar-henley")),
            Group("Edges", false, ("L Cuff", "cuff-left"), ("R Cuff", "cuff-right"), ("Both", "cuffs-both"), ("Hem", "hem")),
            Group("Seams", false, ("Center", "seam-center"), ("Sides", "seam-sides"), ("Shoulder", "shoulder-stripe"), ("Bands", "arm-bands"), ("Yoke", "yoke")),
            Group("Accents", true, ("Pocket", "pocket"), ("Placket", "placket"), ("Full Set", "full-detail")),
        ];
    }

    /// <summary>Raised after a click added a layer (and selected it).</summary>
    public event EventHandler? LayersAdded;

    /// <summary>The four button groups in 4.1's order.</summary>
    public IReadOnlyList<DetailGroup> Groups { get; }

    /// <summary>
    /// The stroke color: lowercase <c>#rrggbb</c> from the picker, or any canvas fill (<c>#rgb</c>, <c>transparent</c>). A fully
    /// transparent color draws nothing, so a click makes no layer.
    /// </summary>
    public string Color
    {
        get => _color;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!CssColor.TryParseCanvasFill(value, out _)) throw new ArgumentException($"'{value}' is not a color.", nameof(value));
            SetProperty(ref _color, value.ToLowerInvariant());
        }
    }

    /// <summary>The stroke width, 1 to 8 (an integer range, editor.html:181); a value outside is clamped.</summary>
    public int Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Clamp(value, MinWidth, MaxWidth));
    }

    /// <summary>Stitch: a dashed stroke, [2w, 2w] (editor.js:1846).</summary>
    public bool Stitch
    {
        get => _stitch;
        set => SetProperty(ref _stitch, value);
    }

    /// <summary>Opens the themed picker under the anchor (the parameter). The color is a setting, not the document: no undo step; a cancel restores it.</summary>
    public IRelayCommand<object?> PickColorCommand { get; }

    DetailGroup Group(string label, bool last, params (string Label, string Id)[] items) =>
        new(label, items.Select(i =>
        {
            var type = Details.FromId(i.Id);
            return new DetailButton(i.Label, type, type == DetailType.FullDetail, new RelayCommand(() => Create(type)));
        }).ToList(), last);

    void PickColor(object? anchor)
    {
        string before = _color;
        string seed = IsPlainHex(before) ? before : DefaultColor;
        string? picked = null;
        try { picked = _picker.Pick(seed, hex => Color = hex, anchor); }
        finally
        {
            if (picked is null) Color = before;
            else Color = picked;
        }
    }

    static bool IsPlainHex(string c) => c.Length == 7 && c[0] == '#' && CssColor.TryParseCanvasFill(c, out _);

    /// <summary>One click: createDetailLayer (editor.js:1834-1865). The style is read now; the layer lands on top, selected.</summary>
    void Create(DetailType type)
    {
        CssColor.TryParseCanvasFill(_color, out SKColor ink);
        var style = new DetailStyle(ink, _width, _stitch);
        int id = _session.NextId;
        var layer = Details.Create(type, _session.Document.Mode, style, id);
        if (layer is null) return;   // nothing drawn: no layer, no step, no id taken (4.1 left a dangling undo entry; ledger details-buttons)
        _session.TakeId();
        _session.ApplyWithSelection(s => LayerOps.AddDrawing(s, layer));
        LayersAdded?.Invoke(this, EventArgs.Empty);
    }
}
