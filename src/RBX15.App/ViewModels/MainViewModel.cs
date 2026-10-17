using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The window's view-model: the shell text and one sub-view-model per region (Canvas, Home, Layers, Status,
/// LayerProperties), plus the project files (M4a Task 1).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    readonly ProjectFileService _files = null!;
    string _title;

    public MainViewModel(EditorSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _title = WindowTitle(session.CurrentFile, session.IsDirty);
        session.Changed += (_, e) =>
        {
            if ((e.What & (SessionChange.Document | SessionChange.File)) == 0) return;
            string next = WindowTitle(Session.CurrentFile, Session.IsDirty);
            if (next == _title) return;
            _title = next;
            OnPropertyChanged(nameof(Title));
        };
    }

    public EditorSession Session { get; }

    /// <summary>
    /// The window title (interfaces R3): 4.1's product name while the document is untitled and clean (main.js:8,
    /// editor.html:6; ledger shell-window-title, keep), otherwise the Windows convention
    /// "[*]&lt;name&gt; - RBX15 Classic Shirt and Pants Maker". Raised only when the text changes.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// R3's formula. <paramref name="currentFile"/> null and clean is the bare product name; otherwise the file name
    /// (or "Untitled"), prefixed with "*" when <paramref name="dirty"/>, then " - " and the product name. "*" rather than
    /// "•": Narrator reads it as "star", it is the classic Win32 marker, and it survives every font.
    /// </summary>
    public static string WindowTitle(string? currentFile, bool dirty) =>
        currentFile is null && !dirty
            ? AppInfo.ProductName
            : (dirty ? "*" : "") + ProjectFileService.DocumentName(currentFile) + " - " + AppInfo.ProductName;

    /// <summary>The header h1, verbatim (editor.html:16, <c>&amp;amp;</c> decoded; ledger shell-header).</summary>
    public string HeaderTitle => "RBX15 Classic Shirt & Pants Maker";

    /// <summary>
    /// The header byline. 4.1 hand-typed "ItsjustEste | v4.1" (editor.html:17). v5 shows the 626Labs mark in place of
    /// the name (Este, 2026-10-17; ledger shell-header) and keeps the version, built from the assembly's major.minor
    /// (ledger shell-header-version-hardcoded). The view upper-cases it (styles.css:153).
    /// </summary>
    public string HeaderByline => "v" + MajorMinor(AppInfo.Version);

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

    /// <summary>True while an Open reads its file (R4); forwards <see cref="ProjectFileService.IsBusy"/>.</summary>
    public bool IsBusy => _files.IsBusy;

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

    /// <summary>Save, Save As, Open and the unsaved-changes guard (M4a Task 1). The window's close runs the guard.</summary>
    public required ProjectFileService Files
    {
        get => _files;
        init
        {
            _files = value ?? throw new ArgumentNullException(nameof(value));
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProjectFileService.IsBusy)) OnPropertyChanged(nameof(IsBusy));
            };
        }
    }

    /// <summary>The left panel's asset library (M4a Task 2; editor.html:291-309).</summary>
    public required AssetLibraryViewModel Assets { get; init; }
}
