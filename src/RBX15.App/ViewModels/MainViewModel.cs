using CommunityToolkit.Mvvm.ComponentModel;

namespace RBX15.App.ViewModels;

/// <summary>
/// The window's view-model: the shell text and, from later tasks, one sub-view-model per region
/// (Canvas in Task 2, Home in Task 3, Layers and Status in Task 4).
/// </summary>
public sealed partial class MainViewModel(EditorSession session) : ObservableObject
{
    public EditorSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));

    /// <summary>The window title (main.js:8, editor.html:6; ledger shell-window-title, keep).</summary>
    public string Title => AppInfo.ProductName;

    /// <summary>The header h1, verbatim (editor.html:16, <c>&amp;amp;</c> decoded; ledger shell-header).</summary>
    public string HeaderTitle => "RBX15 Classic Shirt & Pants Maker";

    /// <summary>
    /// The header byline. 4.1 hand-typed "ItsjustEste | v4.1" (editor.html:17); v5 builds the same shape
    /// from the assembly's major.minor (ledger shell-header-version-hardcoded, fix, whose parity check
    /// reads 'ItsjustEste | v&lt;major.minor&gt;'). The view upper-cases it (styles.css:153).
    /// </summary>
    public string HeaderByline => "ItsjustEste | v" + MajorMinor(AppInfo.Version);

    /// <summary>
    /// The footer (ledger shell-footer, fix): 4.1's "Developed at 626 Labs LLC" (editor.html:343) reads
    /// "Developed at 626Labs" in v5 (Este, 2026-10-14). The view upper-cases it (styles.css:686).
    /// </summary>
    public string FooterText => "Developed at 626Labs";

    static string MajorMinor(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : version;
    }

    /// <summary>The canvas area (Task 2). Required, so the composition root cannot forget it.</summary>
    public required CanvasViewModel Canvas { get; init; }

    /// <summary>The Home tab (Task 3).</summary>
    public required HomeViewModel Home { get; init; }

    /// <summary>The drop zone and layers panel (Task 4).</summary>
    public required LayersViewModel Layers { get; init; }

    /// <summary>The status strip (Task 4).</summary>
    public required StatusViewModel Status { get; init; }

    /// <summary>The left panel's "Selected layer" section (M3b Task 3; editor.html:320-323).</summary>
    public required LayerPropertiesViewModel LayerProperties { get; init; }
}
