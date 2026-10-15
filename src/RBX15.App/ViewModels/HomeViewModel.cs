using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab (editor.html:29-110). M3a shows the Mode, Edit, File (Task 4) and Settings groups,
/// then Fill Style and Template Regions, which 4.1 kept on the Layout tab (editor.html:145-171) and
/// which M3a hosts here until M4 brings the Layout tab back. Tools, Brush and Placement stay hidden
/// until they work (contract: hide what does not work yet).
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

    /// <summary>The File group (Task 4): Add image and Download template.</summary>
    public required FileGroupViewModel File { get; init; }
}
