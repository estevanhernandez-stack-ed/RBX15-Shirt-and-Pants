using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using RBX15.Engine;

namespace RBX15.App.Controls;

/// <summary>
/// One visible layer on the canvas as a UI Automation element (M4a contract "Canvas UI Automation"; spec §4
/// accessibility). 4.1's &lt;canvas&gt; (editor.html:330) exposed no layers at all, so a screen reader or a
/// keyboard-only user could not reach them (ledger a11y-focus-rings). Here every visible layer is a child of the
/// canvas peer with a SelectionItem pattern, and the selected one can also be transformed.
/// <list type="bullet">
/// <item>Name is the layer name, AutomationId <c>Layer_&lt;id&gt;</c>, ControlType ListItem, ClassName CanvasLayer. It
///   takes no keyboard focus: the canvas has none either (M3b), and the layers list is the keyboard path.</item>
/// <item>BoundingRectangle is the screen box of the layer's four rotated corners (SelectionGeometry.Corner, the
///   corners the overlay draws, editor.js:1083-1088), so it follows zoom, scroll, rotation and the display scale.</item>
/// <item>Transform works in <b>template pixels</b>: Move(x, y) sets the layer's top-left, Resize(w, h) sets its
///   displayed size, Rotate(degrees) turns it relative to now. This deviates from UIA's screen coordinates on
///   purpose: the contract asks for template units, which are the X and Y the properties panel shows
///   (editor.js:825-826). Each call is one undo step through the session, the same as the matching gesture, and a
///   call that changes nothing records nothing. Only the selected, visible layer can be transformed; any other
///   call throws <see cref="ElementNotEnabledException"/>.</item>
/// </list>
/// The canvas peer creates and caches these by layer id, so a client holding one keeps it across edits.
/// </summary>
public sealed class LayerAutomationPeer : AutomationPeer, ISelectionItemProvider, ITransformProvider
{
    static readonly ResizeCorner[] CornerOrder = [ResizeCorner.TopLeft, ResizeCorner.TopRight, ResizeCorner.BottomRight, ResizeCorner.BottomLeft];

    readonly TemplateCanvasHostAutomationPeer _canvas;

    internal LayerAutomationPeer(TemplateCanvasHostAutomationPeer canvas, int layerId)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        LayerId = layerId;
    }

    /// <summary>The layer this element stands for.</summary>
    public int LayerId { get; }

    TemplateCanvasHost Host => _canvas.Host;

    ICanvasLayerAutomation? Automation => Host.LayerAutomation;

    Layer? Layer => Host.Document?.Layers.Find(l => l.Id == LayerId);

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.SelectionItem or PatternInterface.Transform ? this : null;

    protected override string GetNameCore() => Layer?.Name ?? "";

    protected override string GetAutomationIdCore() => "Layer_" + LayerId.ToString(CultureInfo.InvariantCulture);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => "CanvasLayer";

    protected override Rect GetBoundingRectangleCore() =>
        Layer is { } layer ? Host.ScreenBounds(CornerOrder.Select(c => SelectionGeometry.Corner(layer, c)).Select(p => new Point(p.X, p.Y))) : Rect.Empty;

    protected override Point GetClickablePointCore()
    {
        if (Layer is not { } layer) return new Point(double.NaN, double.NaN);
        var (x, y) = SelectionGeometry.Center(layer);
        return Host.ScreenBounds([new Point(x, y)]) is { IsEmpty: false } r ? r.TopLeft : new Point(double.NaN, double.NaN);
    }

    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override void SetFocusCore() => throw new InvalidOperationException("A canvas layer takes no keyboard focus; the layers list does.");

    protected override bool IsEnabledCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override string GetAcceleratorKeyCore() => "";

    protected override string GetAccessKeyCore() => "";

    protected override string GetHelpTextCore() => "";

    protected override string GetItemStatusCore() => "";

    protected override string GetItemTypeCore() => "";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string GetLocalizedControlTypeCore() => "list item";

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    // ---- SelectionItem ----

    public bool IsSelected => Automation?.SelectedLayerId == LayerId;

    public IRawElementProviderSimple SelectionContainer => _canvas.Provider();

    /// <summary>Selects this layer and sets the Select tool, as a pick in the layers list does.</summary>
    public void Select()
    {
        var automation = Automation ?? throw new ElementNotEnabledException();
        if (Layer is not { Visible: true }) throw new ElementNotAvailableException();
        automation.SelectLayer(LayerId);
    }

    /// <summary>One layer is selected at a time: with nothing (or this layer) selected it selects, otherwise it throws.</summary>
    public void AddToSelection()
    {
        var automation = Automation ?? throw new ElementNotEnabledException();
        if (automation.SelectedLayerId is int other && other != LayerId)
            throw new InvalidOperationException("Only one layer can be selected at a time.");
        Select();
    }

    /// <summary>Deselects when this layer is the selected one; otherwise nothing changes.</summary>
    public void RemoveFromSelection()
    {
        if (IsSelected) Automation!.SelectLayer(null);
    }

    // ---- Transform (template pixels; one undo step per call that changes the layer) ----

    bool Editable => Automation is not null && IsSelected && Layer is { Visible: true };

    public bool CanMove => Editable;

    public bool CanResize => Editable;

    public bool CanRotate => Editable;

    public void Move(double x, double y)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("The position must be finite.");
        automation.MoveLayer(LayerId, x, y);
    }

    public void Resize(double width, double height)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentException("The size must be finite and positive.");
        automation.ResizeLayer(LayerId, width, height);
    }

    public void Rotate(double degrees)
    {
        var automation = RequireEditable();
        if (!double.IsFinite(degrees)) throw new ArgumentException("The angle must be finite.");
        automation.RotateLayer(LayerId, degrees);
    }

    ICanvasLayerAutomation RequireEditable() => Editable ? Automation! : throw new ElementNotEnabledException();
}
