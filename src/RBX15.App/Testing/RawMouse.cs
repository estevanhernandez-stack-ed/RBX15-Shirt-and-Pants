using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace RBX15.App.Testing;

/// <summary>
/// WPF's own raw mouse report, built by reflection and pushed through <see cref="InputManager.ProcessInput"/>
/// (M4a interfaces R8). This is the path WPF's HwndMouseInputProvider feeds from WM_MOUSE* messages, entered one
/// step later and in-process, so WPF does its own hit testing, MouseEnter and MouseLeave, the promotion to
/// MouseLeftButtonDown, capture routing and the wheel. Nothing leaves the process and no Windows input API is
/// called. Coordinates are device pixels in the source's client area. Every report this class builds carries
/// <see cref="InputBridgeDispatcher.Marker"/> in its extraInformation, which is how the bridge tells its own reports
/// from the hardware's. The internal types are looked up once, by name, and a missing one fails loudly.
/// </summary>
internal static class RawMouse
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Assembly Core = typeof(InputManager).Assembly;
    static readonly Type ReportType = Core.GetType("System.Windows.Input.RawMouseInputReport", throwOnError: true)!;
    static readonly Type ActionsType = Core.GetType("System.Windows.Input.RawMouseActions", throwOnError: true)!;
    static readonly Type ArgsType = Core.GetType("System.Windows.Input.InputReportEventArgs", throwOnError: true)!;
    static readonly Type InputReportType = Core.GetType("System.Windows.Input.InputReport", throwOnError: true)!;

    static readonly ConstructorInfo ReportCtor = ReportType.GetConstructor(Any, [typeof(InputMode), typeof(int), typeof(PresentationSource), ActionsType, typeof(int), typeof(int), typeof(int), typeof(IntPtr)])
        ?? throw new MissingMethodException("RawMouseInputReport(InputMode, int, PresentationSource, RawMouseActions, int, int, int, IntPtr)");
    static readonly ConstructorInfo ArgsCtor = ArgsType.GetConstructor(Any, [typeof(InputDevice), InputReportType])
        ?? throw new MissingMethodException("InputReportEventArgs(InputDevice, InputReport)");
    static readonly PropertyInfo ReportProperty = ArgsType.GetProperty("Report", Any) ?? throw new MissingMemberException("InputReportEventArgs.Report");
    static readonly PropertyInfo ActionsProperty = ReportType.GetProperty("Actions", Any) ?? throw new MissingMemberException("RawMouseInputReport.Actions");
    static readonly PropertyInfo ExtraProperty = ReportType.GetProperty("ExtraInformation", Any) ?? throw new MissingMemberException("RawMouseInputReport.ExtraInformation");
    static readonly PropertyInfo SourceProperty = InputReportType.GetProperty("InputSource", Any) ?? throw new MissingMemberException("InputReport.InputSource");
    static readonly FieldInfo SynchronizeField = ReportType.GetField("_isSynchronize", Any) ?? throw new MissingFieldException("RawMouseInputReport._isSynchronize");

    /// <summary>InputManager.PreviewInputReportEvent, the routed event every raw report travels as.</summary>
    internal static readonly RoutedEvent PreviewInputReportEvent =
        (RoutedEvent)(typeof(InputManager).GetField("PreviewInputReportEvent", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new MissingFieldException("InputManager.PreviewInputReportEvent"));

    /// <summary>
    /// Pushes one report. <paramref name="actions"/> is RawMouseActions in Enum.Parse form ("AbsoluteMove",
    /// "Button1Press", "Activate, AbsoluteMove"); <paramref name="wheel"/> is the wheel delta for VerticalWheelRotate.
    /// </summary>
    public static void Inject(PresentationSource source, string actions, Point devicePixels, int wheel, nint extraInformation = InputBridgeDispatcher.Marker)
    {
        ArgumentNullException.ThrowIfNull(source);
        InputManager.Current.ProcessInput(Create(source, actions, devicePixels, wheel, extraInformation));
    }

    /// <summary>The report as input event args, ready for ProcessInput or a PushInput from a PreProcessInput handler.</summary>
    internal static InputEventArgs Create(PresentationSource source, string actions, Point devicePixels, int wheel, nint extraInformation = InputBridgeDispatcher.Marker)
    {
        object report = ReportCtor.Invoke([InputMode.Foreground, Environment.TickCount, source, Enum.Parse(ActionsType, actions),
            (int)Math.Round(devicePixels.X), (int)Math.Round(devicePixels.Y), wheel, (IntPtr)extraInformation]);
        var args = (InputEventArgs)ArgsCtor.Invoke([Mouse.PrimaryDevice, report]);
        args.RoutedEvent = PreviewInputReportEvent;
        return args;
    }

    /// <summary>Reads a raw mouse report: its actions (Enum.ToString form), extraInformation and source. False for anything else.</summary>
    internal static bool TryRead(InputEventArgs input, out string actions, out nint extraInformation, out PresentationSource? source) =>
        TryRead(input, out actions, out extraInformation, out source, out _);

    /// <summary>
    /// As above, plus whether the report is WPF's own MouseDevice.Synchronize replay. That report is built in-process
    /// at MouseDevice's last known position (its _lastPosition, never the system cursor) and is the only kind that
    /// hit-tests a move to an unchanged point, so it sets Mouse.DirectlyOver after an Activate or a capture change.
    /// </summary>
    internal static bool TryRead(InputEventArgs input, out string actions, out nint extraInformation, out PresentationSource? source, out bool synchronize)
    {
        actions = "";
        extraInformation = 0;
        source = null;
        synchronize = false;
        if (input.RoutedEvent != PreviewInputReportEvent || !ArgsType.IsInstanceOfType(input)) return false;
        object? report = ReportProperty.GetValue(input);
        if (report is null || !ReportType.IsInstanceOfType(report)) return false;
        actions = ActionsProperty.GetValue(report)!.ToString()!;
        extraInformation = (IntPtr)ExtraProperty.GetValue(report)!;
        source = SourceProperty.GetValue(report) as PresentationSource;
        synchronize = (bool)SynchronizeField.GetValue(report)!;
        return true;
    }
}
