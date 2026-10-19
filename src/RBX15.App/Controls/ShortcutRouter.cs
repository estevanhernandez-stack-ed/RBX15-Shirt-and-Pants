using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using RBX15.App.Canvas;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App.Controls;

/// <summary>
/// What a shortcut asks for: the six tool toggles (editor.js:1782-1787) and the layer keys (editor.js:1801-1827), which need a
/// selection. <see cref="ShortcutRouter.Route"/> and <see cref="ShortcutRouter.RouteText"/> return them. The Ctrl chords (Undo, Redo,
/// Duplicate and the file chords) are the window's KeyBindings, not actions here.
/// </summary>
public enum ShortcutAction
{
    /// <summary>V (toggleTool('select')): Select never toggles off.</summary>
    ToggleSelect,
    /// <summary>B (toggleTool('pen')).</summary>
    TogglePen,
    /// <summary>L (toggleTool('line')).</summary>
    ToggleLine,
    /// <summary>U (toggleTool('rect')).</summary>
    ToggleRect,
    /// <summary>O (toggleTool('circle'), the Ellipse tool).</summary>
    ToggleEllipse,
    /// <summary>E (toggleTool('eraser')).</summary>
    ToggleEraser,

    /// <summary>Arrow Left: x - 1 template pixel.</summary>
    NudgeLeft,
    /// <summary>Arrow Right: x + 1.</summary>
    NudgeRight,
    /// <summary>Arrow Up: y - 1.</summary>
    NudgeUp,
    /// <summary>Arrow Down: y + 1.</summary>
    NudgeDown,
    /// <summary>Shift+Arrow Left: x - 10.</summary>
    NudgeLeftBig,
    /// <summary>Shift+Arrow Right: x + 10.</summary>
    NudgeRightBig,
    /// <summary>Shift+Arrow Up: y - 10.</summary>
    NudgeUpBig,
    /// <summary>Shift+Arrow Down: y + 10.</summary>
    NudgeDownBig,

    /// <summary>[: rotation - 5 degrees.</summary>
    RotateBack,
    /// <summary>]: rotation + 5 degrees.</summary>
    RotateForward,
    /// <summary>{ or Shift+[: rotation - 15 degrees.</summary>
    RotateBackBig,
    /// <summary>} or Shift+]: rotation + 15 degrees.</summary>
    RotateForwardBig,
    /// <summary>R: rotation 0.</summary>
    ResetRotation,

    /// <summary>+ or =: scale x 1.1, capped at 5.</summary>
    ScaleUp,
    /// <summary>- or _: scale x 0.9, floored at 0.01.</summary>
    ScaleDown,

    /// <summary>Delete or Backspace: removes the selected layer.</summary>
    DeleteLayer,
}

/// <summary>Where keyboard focus is, as far as the shortcuts care (K1's exemption list).</summary>
public enum ShortcutFocus
{
    /// <summary>Nothing has focus, or something no shortcut cares about.</summary>
    None,
    /// <summary>A text field, any <see cref="System.Windows.Controls.Primitives.TextBoxBase"/>: it keeps every key.</summary>
    TextField,
    /// <summary>Inside the color picker popup: it keeps every key.</summary>
    ColorPicker,
    /// <summary>The layer list or one of its rows: it keeps Up, Down, Alt+Up, Alt+Down and Delete.</summary>
    LayerList,
    /// <summary>A region tile: it keeps Delete, Shift+F10 and Menu (the tile's own bindings).</summary>
    RegionTile,
    /// <summary>The 3D preview viewport: it keeps the arrows and Home.</summary>
    Viewport,
    /// <summary>Anything else: sliders, checkboxes, swatches, buttons, tabs. They never trap a shortcut (kbd-input-focus-trap).</summary>
    Other,
}

/// <summary>
/// The window's keyboard shortcuts (spec §4 "Keyboard"; M4b K1-K3; ledger kbd-shortcuts and the keys-* rows).
/// <see cref="Route"/> and <see cref="RouteText"/> are pure functions of their inputs: which action a key press (or a typed
/// character) means, given the modifiers, the focused element, whether the Keys toggle is on and whether a layer is selected.
/// <see cref="Execute"/> runs the action. The window feeds the first from <c>PreviewKeyDown</c> and the second from <c>PreviewTextInput</c>:
/// <list type="bullet">
/// <item>Characters (the tool letters and <c>[ ] { } + = - _ r</c>) come from text input, so they follow the keyboard layout as 4.1's
/// <c>e.key</c> did (K2). Arrows, Delete and Backspace come from key downs.</item>
/// <item>AltGr arrives as Ctrl+Alt, so a character typed with both held is an AltGr character (<c>[</c> on a German layout) and routes
/// like its bare form. A real Ctrl chord (Ctrl without Alt) and a lone Alt never route (D9).</item>
/// <item>Ctrl chords (Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Ctrl+D, the file chords) are the window's KeyBindings behind
/// <see cref="TextFieldShortcutCommand"/>, never routed here, so Keys OFF cannot reach them (the fix on keys-toggle-button).</item>
/// <item>K1's trap table: a text field and an open color picker keep every key. The layer list keeps Up, Down, Alt+Up, Alt+Down and
/// Delete; a region tile keeps Delete, Shift+F10 and Menu; the 3D viewport keeps the arrows and Home. Everything else (sliders, checkboxes,
/// swatches, buttons, tabs) traps nothing (kbd-input-focus-trap). Space and Enter are never answered here, so a focused button's own
/// activation stays WPF's (kbd-focused-button-enter-space).</item>
/// <item>The layer keys need a selection; the tool keys do not. Ctrl or the Windows key held never routes a layer key (4.1 returned on
/// ctrlKey or metaKey); Alt does not stop an arrow or Delete, as 4.1.</item>
/// </list>
/// </summary>
public sealed class ShortcutRouter
{
    readonly ToolsViewModel _tools;
    readonly LayerKeys _layers;

    public ShortcutRouter(ToolsViewModel tools, LayerKeys layers)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    /// <summary>
    /// The action a key down means, or null: an arrow (nudge, 10 with Shift), Delete or Backspace (remove). Needs Keys ON and a selected
    /// layer, no Ctrl and no Windows key (Alt is allowed, as 4.1), and a focus that does not keep the key (K1). The tool keys are
    /// characters and the chords are KeyBindings, so no other key down is an action.
    /// </summary>
    public static ShortcutAction? Route(Key key, ModifierKeys modifiers, IInputElement? focused, bool keysEnabled, bool hasSelection)
    {
        if (!keysEnabled || !hasSelection) return null;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) != 0) return null;
        if (Keeps(Classify(focused), key, modifiers)) return null;
        bool big = (modifiers & ModifierKeys.Shift) != 0;
        return key switch
        {
            Key.Left => big ? ShortcutAction.NudgeLeftBig : ShortcutAction.NudgeLeft,
            Key.Right => big ? ShortcutAction.NudgeRightBig : ShortcutAction.NudgeRight,
            Key.Up => big ? ShortcutAction.NudgeUpBig : ShortcutAction.NudgeUp,
            Key.Down => big ? ShortcutAction.NudgeDownBig : ShortcutAction.NudgeDown,
            Key.Delete or Key.Back => ShortcutAction.DeleteLayer,
            _ => null,
        };
    }

    /// <summary>
    /// The action a typed character means, or null. One of v b l u o e (either case, so Shift+B is b, as 4.1's <c>e.key.toLowerCase()</c>),
    /// or a layer key: <c>[ ]</c> rotate 5 degrees (15 with Shift), <c>{ }</c> 15, <c>r R</c> reset, <c>+ =</c> grow, <c>- _</c> shrink; the
    /// layer keys need a selection. Needs Keys ON, a focus that is not a text field or the color picker, and no Ctrl-only, Alt-only or
    /// Windows modifier. Ctrl and Alt together is AltGr (D9) and counts as neither. A Ctrl+letter types a control character (Ctrl+V is
    /// U+0016), which is no shortcut either way.
    /// </summary>
    public static ShortcutAction? RouteText(string text, ModifierKeys modifiers, IInputElement? focused, bool keysEnabled, bool hasSelection)
    {
        if (!keysEnabled || text is not { Length: 1 }) return null;
        if (!IsBareOrAltGr(modifiers)) return null;
        if (Traps(Classify(focused))) return null;
        char c = text[0];
        var tool = char.ToLowerInvariant(c) switch
        {
            'v' => ShortcutAction.ToggleSelect,
            'b' => ShortcutAction.TogglePen,
            'l' => ShortcutAction.ToggleLine,
            'u' => ShortcutAction.ToggleRect,
            'o' => ShortcutAction.ToggleEllipse,
            'e' => ShortcutAction.ToggleEraser,
            _ => (ShortcutAction?)null,
        };
        if (tool is not null) return tool;
        if (!hasSelection) return null;
        // rotStep is 15 with Shift (editor.js:1807). { and } are the Shift forms of the brackets on a US layout and are typed with AltGr
        // elsewhere, with no Shift held, so the character itself means the big step too.
        bool big = (modifiers & ModifierKeys.Shift) != 0;
        return c switch
        {
            '[' => big ? ShortcutAction.RotateBackBig : ShortcutAction.RotateBack,
            ']' => big ? ShortcutAction.RotateForwardBig : ShortcutAction.RotateForward,
            '{' => ShortcutAction.RotateBackBig,
            '}' => ShortcutAction.RotateForwardBig,
            'r' or 'R' => ShortcutAction.ResetRotation,
            '+' or '=' => ShortcutAction.ScaleUp,
            '-' or '_' => ShortcutAction.ScaleDown,
            _ => null,
        };
    }

    /// <summary>
    /// True for no Ctrl and no Alt (Shift is free), no Windows key, or for Ctrl and Alt together, which is how Windows reports AltGr (D9).
    /// Ctrl alone and Alt alone are shortcut chords (K3), not typed characters.
    /// </summary>
    static bool IsBareOrAltGr(ModifierKeys modifiers)
    {
        if ((modifiers & ModifierKeys.Windows) != 0) return false;
        bool ctrl = (modifiers & ModifierKeys.Control) != 0, alt = (modifiers & ModifierKeys.Alt) != 0;
        return ctrl == alt;
    }

    /// <summary>Runs <paramref name="action"/>. The tool keys are 4.1's toggleTool, the same command the buttons run (tools-toggle-off).</summary>
    public void Execute(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.NudgeLeft: _layers.Nudge(-LayerKeys.NudgeStep, 0); break;
            case ShortcutAction.NudgeRight: _layers.Nudge(LayerKeys.NudgeStep, 0); break;
            case ShortcutAction.NudgeUp: _layers.Nudge(0, -LayerKeys.NudgeStep); break;
            case ShortcutAction.NudgeDown: _layers.Nudge(0, LayerKeys.NudgeStep); break;
            case ShortcutAction.NudgeLeftBig: _layers.Nudge(-LayerKeys.NudgeStepBig, 0); break;
            case ShortcutAction.NudgeRightBig: _layers.Nudge(LayerKeys.NudgeStepBig, 0); break;
            case ShortcutAction.NudgeUpBig: _layers.Nudge(0, -LayerKeys.NudgeStepBig); break;
            case ShortcutAction.NudgeDownBig: _layers.Nudge(0, LayerKeys.NudgeStepBig); break;
            case ShortcutAction.RotateBack: _layers.Rotate(-LayerKeys.RotateStep); break;
            case ShortcutAction.RotateForward: _layers.Rotate(LayerKeys.RotateStep); break;
            case ShortcutAction.RotateBackBig: _layers.Rotate(-LayerKeys.RotateStepBig); break;
            case ShortcutAction.RotateForwardBig: _layers.Rotate(LayerKeys.RotateStepBig); break;
            case ShortcutAction.ResetRotation: _layers.ResetRotation(); break;
            case ShortcutAction.ScaleUp: _layers.Scale(LayerKeys.ScaleUpFactor); break;
            case ShortcutAction.ScaleDown: _layers.Scale(LayerKeys.ScaleDownFactor); break;
            case ShortcutAction.DeleteLayer: _layers.Delete(); break;
            default: _tools.ToggleCommand.Execute(ToolOf(action)); break;
        }
    }

    /// <summary>The tool a toggle action selects.</summary>
    public static Tool ToolOf(ShortcutAction action) => action switch
    {
        ShortcutAction.ToggleSelect => Tool.Select,
        ShortcutAction.TogglePen => Tool.Pen,
        ShortcutAction.ToggleLine => Tool.Line,
        ShortcutAction.ToggleRect => Tool.Rect,
        ShortcutAction.ToggleEllipse => Tool.Ellipse,
        ShortcutAction.ToggleEraser => Tool.Eraser,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "not a tool toggle"),
    };

    /// <summary>Whether the focused element keeps every typed character (K1): a text field and the color picker. The rest keep only keys that are not characters.</summary>
    static bool Traps(ShortcutFocus focus) => focus is ShortcutFocus.TextField or ShortcutFocus.ColorPicker;

    /// <summary>
    /// K1's per-key trap table: whether <paramref name="focus"/> keeps <paramref name="key"/> for its own use. A text field and the color
    /// picker keep all; the layer list keeps Up and Down (plain, or with Alt, which reorders) and Delete; a region tile keeps Delete,
    /// Shift+F10 and Menu; the 3D viewport keeps the arrows and Home. Nothing else traps (kbd-input-focus-trap).
    /// </summary>
    public static bool Keeps(ShortcutFocus focus, Key key, ModifierKeys modifiers) => focus switch
    {
        ShortcutFocus.TextField or ShortcutFocus.ColorPicker => true,
        ShortcutFocus.LayerList => key == Key.Delete || (key is Key.Up or Key.Down && (modifiers & ~ModifierKeys.Alt) == ModifierKeys.None),
        ShortcutFocus.RegionTile => key is Key.Delete or Key.Apps || (key == Key.F10 && (modifiers & ModifierKeys.Shift) != 0),
        ShortcutFocus.Viewport => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home,
        _ => false,
    };

    /// <summary>Which of K1's exemptions <paramref name="focused"/> is, if any.</summary>
    public static ShortcutFocus Classify(IInputElement? focused)
    {
        if (focused is not DependencyObject element) return ShortcutFocus.None;
        if (TextFieldShortcutCommand.TextFieldHasFocus(focused)) return ShortcutFocus.TextField;
        for (DependencyObject? at = element; at is not null; at = Parent(at))
        {
            switch (at)
            {
                case ColorPickerPopup:
                    return ShortcutFocus.ColorPicker;
                case ListBox { Name: "LayerList" }:
                    return ShortcutFocus.LayerList;
                case Viewport3D:
                    return ShortcutFocus.Viewport;
                case Button { DataContext: RegionTileViewModel } or Button { Name: "FillAllTile" }:
                    return ShortcutFocus.RegionTile;
            }
        }
        return ShortcutFocus.Other;
    }

    /// <summary>The visual parent, or the logical one where the visual tree ends (a Window's content, a popup's child).</summary>
    static DependencyObject? Parent(DependencyObject child)
    {
        if (child is Visual or Visual3D && VisualTreeHelper.GetParent(child) is { } visual) return visual;
        return LogicalTreeHelper.GetParent(child);
    }
}
