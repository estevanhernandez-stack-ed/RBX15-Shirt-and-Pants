using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App.Services;

/// <summary>
/// <see cref="IColorPicker"/> over <see cref="ColorPickerPopup"/>, owned by the main window when it is
/// showing and shown with ShowDialog, so <see cref="Pick"/> is synchronous. A click on the main window
/// while the popup is open commits it: the owner is disabled by the modal popup, so Windows tells it
/// about the click only through WM_SETCURSOR with HTERROR, which an owner hook turns into a commit.
/// Clicking any other app commits through the popup's Deactivated. UI thread only.
/// </summary>
public sealed class ThemedColorPicker(Func<Window?> owner, Func<IReadOnlyList<Rect>>? workAreas = null) : IColorPicker
{
    const int WM_SETCURSOR = 0x0020;
    const int HTERROR = -2;
    const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;

    readonly Func<IReadOnlyList<Rect>> _workAreas = workAreas ?? MonitorWorkAreas.Get;

    public string? Pick(string initialHex, Action<string>? preview = null, object? anchor = null)
    {
        var viewModel = new ColorPickerViewModel(initialHex, preview);
        var popup = new ColorPickerPopup(viewModel);
        var o = owner();
        if (o is { IsVisible: true }) popup.Owner = o;
        popup.PlaceBy(anchor as FrameworkElement ?? Keyboard.FocusedElement as FrameworkElement, _workAreas());
        CaptionTheme.Apply(popup);

        HwndSource? source = popup.Owner is { } w ? HwndSource.FromHwnd(new WindowInteropHelper(w).Handle) : null;
        HwndSourceHook hook = (IntPtr _, int msg, IntPtr _, IntPtr lParam, ref bool handled) =>
        {
            if (IsClickOnDisabledOwner(msg, lParam) && !viewModel.IsFinished)
                popup.Dispatcher.BeginInvoke(() => popup.Finish(commit: true));
            return IntPtr.Zero;
        };
        source?.AddHook(hook);
        try
        {
            popup.ShowDialog();
        }
        finally
        {
            source?.RemoveHook(hook);
        }
        if (!viewModel.IsFinished) viewModel.Cancel();
        return viewModel.Result;
    }

    /// <summary>
    /// True for the message a modal popup's disabled owner gets when it is clicked: WM_SETCURSOR whose
    /// lParam has the hit test HTERROR in its low word and a button-down message in its high word.
    /// </summary>
    internal static bool IsClickOnDisabledOwner(int msg, IntPtr lParam)
    {
        if (msg != WM_SETCURSOR) return false;
        long value = lParam.ToInt64();
        short hitTest = unchecked((short)(value & 0xFFFF));
        int mouse = (int)((value >> 16) & 0xFFFF);
        return hitTest == HTERROR && mouse is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN;
    }
}
