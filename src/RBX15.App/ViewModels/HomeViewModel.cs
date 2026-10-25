using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab (editor.html:29-110): the Mode, Tools, Brush, Edit, File and Settings groups. Placement follows Brush
/// and shows only with a selected layer; the Keys toggle is the last button of the Tools group (M4b item 10). Fill Style
/// and Template Regions, hosted here from M3a until Este's 2026-10-25 ruling, are on the Layout tab, as in 4.1.
/// </summary>
public sealed class HomeViewModel
{
    public HomeViewModel(EditorSession session, IColorPicker colorPicker)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(colorPicker);
        Mode = new ModeGroupViewModel(session);
        Settings = new SettingsGroupViewModel(session);
        Edit = new EditGroupViewModel(session);
    }

    public ModeGroupViewModel Mode { get; }
    public SettingsGroupViewModel Settings { get; }
    public EditGroupViewModel Edit { get; }

    /// <summary>The Tools group (editor.html:41-52), shared with the canvas (M3b).</summary>
    public required ToolsViewModel Tools { get; init; }

    /// <summary>The Keys: ON/OFF toggle in the Tools group (editor.html:49), shared with the window's shortcut router (M4b item 10).</summary>
    public required KeysViewModel Keys { get; init; }

    /// <summary>The Brush group (editor.html:53-62), shared with the canvas (M3b).</summary>
    public required BrushViewModel Brush { get; init; }

    /// <summary>The Placement group (editor.html:63-76), after Brush; shown only while a layer is selected (M4b item 5).</summary>
    public required PlacementViewModel Placement { get; init; }

    /// <summary>The File group (Task 4): Add image and Download template.</summary>
    public required FileGroupViewModel File { get; init; }

    /// <summary>The v5.1 3D preview, for the Settings group's "3D preview" box (set once by the composition root).</summary>
    public AvatarPreviewViewModel AvatarPreview
    {
        get => Settings.Preview ?? throw new InvalidOperationException("AvatarPreview was not wired.");
        init => Settings.Preview = value ?? throw new ArgumentNullException(nameof(value));
    }

}
