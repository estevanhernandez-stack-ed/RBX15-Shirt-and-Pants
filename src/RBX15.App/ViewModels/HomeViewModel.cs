using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab (editor.html:29-110): the Mode, Tools, Brush, Edit, File and Settings groups, then
/// Fill Style and Template Regions, which 4.1 kept on the Layout tab (editor.html:145-171) and which
/// M3a hosts here until M4 brings the Layout tab back. Placement stays hidden until it works
/// (contract: hide what does not work yet).
/// </summary>
public sealed class HomeViewModel
{
    public HomeViewModel(EditorSession session, IColorPicker colorPicker)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(colorPicker);
        Mode = new ModeGroupViewModel(session);
        FillStyle = new FillStyleViewModel(session, colorPicker);
        Regions = new RegionsGroupViewModel(session, FillStyle);
        Settings = new SettingsGroupViewModel(session);
        Edit = new EditGroupViewModel(session);
    }

    public ModeGroupViewModel Mode { get; }
    public RegionsGroupViewModel Regions { get; }
    public FillStyleViewModel FillStyle { get; }
    public SettingsGroupViewModel Settings { get; }
    public EditGroupViewModel Edit { get; }

    /// <summary>The Tools group (editor.html:41-52), shared with the canvas (M3b).</summary>
    public required ToolsViewModel Tools { get; init; }

    /// <summary>The Brush group (editor.html:53-62), shared with the canvas (M3b).</summary>
    public required BrushViewModel Brush { get; init; }

    /// <summary>The File group (Task 4): Add image and Download template.</summary>
    public required FileGroupViewModel File { get; init; }
}
