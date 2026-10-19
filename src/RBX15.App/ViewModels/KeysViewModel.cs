using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Keys toggle in Home's Tools group (editor.html:49, editor.js:1766-1773; ledger keys-toggle-button, home-keys-toggle).
/// <see cref="Enabled"/> is 4.1's keyboardShortcutsEnabled: ON at every launch, session only (not saved, not in the
/// document, not an undo step). The ShortcutRouter reads it for bare-key shortcuts only; Ctrl chords never look at it
/// (the fix recorded on both rows: 4.1 turned Undo off too). The button's colors, #2d4a2d on #7fff7f when on and
/// #4a2d2d on #ff7f7f when off, are the view's: it follows <see cref="Enabled"/>.
/// </summary>
public sealed partial class KeysViewModel : ObservableObject
{
    public KeysViewModel() => ToggleCommand = new RelayCommand(() => Enabled = !Enabled);

    /// <summary>Whether bare-key shortcuts work (keyboardShortcutsEnabled). True at launch.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private bool _enabled = true;

    /// <summary>The button's text: "Keys: ON" or "Keys: OFF" (editor.js:1770).</summary>
    public string Label => Enabled ? "Keys: ON" : "Keys: OFF";

    /// <summary>The button's tooltip (editor.html:49).</summary>
    public string ToolTip => "Toggle keyboard shortcuts";

    /// <summary>Flips <see cref="Enabled"/> (editor.js:1767-1772).</summary>
    public IRelayCommand ToggleCommand { get; }
}
