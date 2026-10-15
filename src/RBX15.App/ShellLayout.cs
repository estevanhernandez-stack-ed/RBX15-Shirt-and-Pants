namespace RBX15.App;

/// <summary>
/// 4.1's layout numbers that are not theme tokens (ledger shell-layout-skeleton,
/// shell-responsive-breakpoint, shell-window-size).
/// </summary>
public static class ShellLayout
{
    /// <summary>The left <c>.panel</c> width (styles.css:247).</summary>
    public const double PanelWidth = 280;

    /// <summary>The left panel width at the breakpoint (styles.css:694-697).</summary>
    public const double NarrowPanelWidth = 200;

    /// <summary><c>@media (max-width: 900px)</c> (styles.css:694): a viewport 900 wide or less is narrow.</summary>
    public const double Breakpoint = 900;

    /// <summary>4.1's window size (main.js:5-13). Also the restore default in <see cref="Services.PlacementGuard"/>.</summary>
    public const double DefaultWidth = 1200, DefaultHeight = 800;

    /// <summary>
    /// The smallest window. 4.1 set none (ledger shell-window-size, quirk: the panels overlap when
    /// shrunk); 640x480 is the floor the row's parity check resizes to.
    /// </summary>
    public const double MinWidth = 640, MinHeight = 480;

    /// <summary>The left panel width for a viewport (window client) width in DIPs, which equal CSS px.</summary>
    public static double PanelWidthFor(double viewportWidth) => viewportWidth <= Breakpoint ? NarrowPanelWidth : PanelWidth;
}
