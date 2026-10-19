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
/// What a shortcut asks for. M4b item 10 has the six tool toggles (editor.js:1782-1787); item 11 adds the layer keys
/// (arrows, brackets, R, +/-, Delete) and Duplicate/Redo here, with <see cref="ShortcutRouter.Route"/> and
/// <see cref="ShortcutRouter.RouteText"/> returning them.
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
    /// <summary>The layer list or one of its rows: it keeps Up, Down, Alt+Up, Alt+Down and Delete (item 11's keys).</summary>
    LayerList,
    /// <summary>A region tile: it keeps Delete, Shift+F10 and Menu (item 11's keys, and the tile's own bindings).</summary>
    RegionTile,
    /// <summary>The 3D preview viewport: it keeps the arrows and Home (item 11's keys).</summary>
    Viewport,
    /// <summary>Anything else: sliders, checkboxes, swatches, buttons, tabs. They never trap a shortcut (kbd-input-focus-trap).</summary>
    Other,
}

/// <summary>
/// The window's keyboard shortcuts (spec §4 "Keyboard"; M4b K1-K3; ledger kbd-shortcuts and the keys-* rows).
/// <see cref="Route"/> and <see cref="RouteText"/> are pure functions of their inputs: which action a key press (or a typed
/// character) means, given the modifiers, the focused element and whether the Keys toggle is on. <see cref="Execute"/> runs
/// the action. The window feeds the first from <c>PreviewKeyDown</c> and the second from <c>PreviewTextInput</c>:
/// <list type="bullet">
/// <item>Characters (the tool letters here; <c>[ ] { } + - _ r</c> in item 11) come from text input, so they follow the keyboard
/// layout as 4.1's <c>e.key</c> did (K2). Arrows, Delete and Backspace come from key downs.</item>
/// <item>Ctrl chords (Ctrl+Z, Ctrl+Y, Ctrl+D, the file chords) are the window's KeyBindings behind
/// <see cref="TextFieldShortcutCommand"/>, never routed here, so Keys OFF cannot reach them (the fix on keys-toggle-button).</item>
/// <item>Only text fields and an open color picker trap tool letters. The layer list, a region tile and the 3D viewport keep their
/// own keys, none of which are letters; sliders, checkboxes, buttons and tabs trap nothing (K1). Space and Enter are never
/// answered here, so a focused button's own activation stays WPF's (kbd-focused-button-enter-space).</item>
/// <item>Ctrl or Alt held never toggles a tool: Ctrl+V, Ctrl+B, Ctrl+E do nothing to it (K3; 4.1 toggled on Ctrl+V/B/L/U/O).</item>
/// </list>
/// </summary>
public sealed class ShortcutRouter(ToolsViewModel tools)
{
    readonly ToolsViewModel _tools = tools ?? throw new ArgumentNullException(nameof(tools));

    /// <summary>
    /// The action a key down means, or null. No key down means an action in this item (the tool keys are characters and the
    /// chords are KeyBindings); item 11's arrows, Delete and Backspace are answered here.
    /// </summary>
    public static ShortcutAction? Route(Key key, ModifierKeys modifiers, IInputElement? focused, bool keysEnabled) => null;

    /// <summary>
    /// The action a typed character means, or null: one of v b l u o e (either case, so Shift+B is b, as 4.1's
    /// <c>e.key.toLowerCase()</c>), with no Ctrl or Alt held, Keys ON, and focus anywhere but a text field or the color picker.
    /// A Ctrl+letter types a control character (Ctrl+V is U+0016), which is not a tool letter either way.
    /// </summary>
    public static ShortcutAction? RouteText(string text, ModifierKeys modifiers, IInputElement? focused, bool keysEnabled)
    {
        if (!keysEnabled || text is not { Length: 1 }) return null;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return null;
        if (Traps(Classify(focused))) return null;
        return char.ToLowerInvariant(text[0]) switch
        {
            'v' => ShortcutAction.ToggleSelect,
            'b' => ShortcutAction.TogglePen,
            'l' => ShortcutAction.ToggleLine,
            'u' => ShortcutAction.ToggleRect,
            'o' => ShortcutAction.ToggleEllipse,
            'e' => ShortcutAction.ToggleEraser,
            _ => null,
        };
    }

    /// <summary>Runs <paramref name="action"/>. The tool keys are 4.1's toggleTool, the same command the buttons run (tools-toggle-off).</summary>
    public void Execute(ShortcutAction action) => _tools.ToggleCommand.Execute(ToolOf(action));

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

    /// <summary>
    /// Whether the focused element keeps a typed character (K1). A text field and the color picker keep all of them; the layer
    /// list, a region tile and the 3D viewport keep only keys that are not characters here; the rest keep none.
    /// </summary>
    static bool Traps(ShortcutFocus focus) => focus is ShortcutFocus.TextField or ShortcutFocus.ColorPicker;

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
