using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// The bridge's own keyboard device (M4a interfaces R7). Key events built on it carry its <see cref="Modifiers"/>
/// in <c>e.KeyboardDevice.Modifiers</c>, which is where app key handlers read modifiers. It never touches the
/// system keyboard state, and it is never focused: the bridge sets each event's Source itself.
/// One instance per UI thread (the base constructor subscribes to that thread's InputManager for good).
/// </summary>
internal sealed class BridgeKeyboardDevice : KeyboardDevice
{
    ModifierKeys _modifiers;

    public BridgeKeyboardDevice() : base(InputManager.Current) { }

    /// <summary>The modifiers this device reports as held. KeyboardDevice.Modifiers reads them through GetKeyStatesFromSystem.</summary>
    public new ModifierKeys Modifiers
    {
        get => _modifiers;
        set => _modifiers = value;
    }

    protected override KeyStates GetKeyStatesFromSystem(Key key)
    {
        var held = key switch
        {
            Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
            Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
            Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
            _ => ModifierKeys.None,
        };
        return held != ModifierKeys.None && (_modifiers & held) != 0 ? KeyStates.Down : KeyStates.None;
    }
}
