using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RBX15.App.Controls;

/// <summary>
/// A window shortcut's command that stands aside while a text field has keyboard focus (spec §4: text fields trap
/// shortcuts). A focused TextBox handles its own Ctrl+Z first, but with an empty undo stack it leaves the key
/// unhandled, and the window's KeyBinding would then undo the document. Wrapped in this command, the binding cannot
/// execute while <see cref="TextFieldHasFocus"/> is true, so WPF leaves the key unhandled and it stays the text
/// field's. M3b's Ctrl+Z uses it; M4's shortcuts wrap their commands the same way.
/// </summary>
public sealed class TextFieldShortcutCommand : ICommand
{
    readonly Func<IInputElement?> _focused;

    /// <param name="inner">The shortcut's command.</param>
    /// <param name="focused">The element with keyboard focus; null reads <see cref="Keyboard.FocusedElement"/>. Tests inject it.</param>
    public TextFieldShortcutCommand(ICommand inner, Func<IInputElement?>? focused = null)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _focused = focused ?? (() => Keyboard.FocusedElement);
    }

    /// <summary>The wrapped command.</summary>
    public ICommand Inner { get; }

    /// <summary>True when <paramref name="focused"/> is a text field (any <see cref="TextBoxBase"/>: TextBox, RichTextBox).</summary>
    public static bool TextFieldHasFocus(IInputElement? focused) => focused is TextBoxBase;

    public event EventHandler? CanExecuteChanged
    {
        add => Inner.CanExecuteChanged += value;
        remove => Inner.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter) => !TextFieldHasFocus(_focused()) && Inner.CanExecute(parameter);

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) Inner.Execute(parameter);
    }
}
