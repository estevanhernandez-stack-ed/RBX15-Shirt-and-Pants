using System.Windows.Input;

namespace RBX15.App.Controls;

/// <summary>
/// The one place app code reads the physical left-button state (M4a interfaces R8). WPF's
/// <see cref="MouseEventArgs.LeftButton"/> and <see cref="Mouse.LeftButton"/> read the system key state
/// (GetKeyState), which input raised inside the process cannot set, so the test input bridge overrides it here
/// while it drives the app. With no override, both members return exactly what WPF reports. Real input never
/// changes this seam; only <c>RBX15.App.Testing.InputBridgeDispatcher</c> sets
/// <see cref="LeftButtonOverride"/>, and that type exists only in builds made with EnableTestInputBridge.
/// UI thread only.
/// </summary>
internal static class PointerState
{
    /// <summary>Set only by the test input bridge for its lifetime; null in every shipped build.</summary>
    internal static Func<MouseButtonState>? LeftButtonOverride;

    /// <summary>The left button's state for <paramref name="e"/>: the override, else <c>e.LeftButton</c>.</summary>
    public static MouseButtonState LeftButton(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return LeftButtonOverride?.Invoke() ?? e.LeftButton;
    }

    /// <summary>True while the left button is down: the override, else <c>Mouse.LeftButton</c>.</summary>
    public static bool LeftButtonDown() => (LeftButtonOverride?.Invoke() ?? Mouse.LeftButton) == MouseButtonState.Pressed;
}
