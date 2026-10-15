using CommunityToolkit.Mvvm.ComponentModel;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Settings group (editor.html:98-107; ledger <c>home-settings-group</c>, keep). The template
/// name is session state: never an undo step, not in the document (editor.html:100). Mask and Grid
/// are document flags, so each toggle is one undo step (interfaces sheet §1,
/// <c>file-undo-snapshot-scope</c> fix; 4.1 captured them but never pushed, editor.js:1494-1495), and
/// undo puts the checkboxes back (editor.js:1433-1436).
/// </summary>
public sealed class SettingsGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    bool _showMask, _showGrid;

    public SettingsGroupViewModel(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _showMask = session.Document.ShowMask;
        _showGrid = session.Document.ShowGrid;
        session.Changed += (_, e) =>
        {
            if (e.What.HasFlag(SessionChange.Title)) OnPropertyChanged(nameof(TemplateName));
            if (!e.What.HasFlag(SessionChange.Document)) return;
            SetProperty(ref _showMask, _session.Document.ShowMask, nameof(ShowMask));
            SetProperty(ref _showGrid, _session.Document.ShowGrid, nameof(ShowGrid));
        };
    }

    /// <summary>#exportTitle (editor.html:100): the export and save file name source.</summary>
    public string TemplateName
    {
        get => _session.TemplateTitle;
        set => _session.TemplateTitle = value ?? "";
    }

    /// <summary>#showMask (editor.html:102, checked by default; ledger <c>overlay-mask-checkbox</c>).</summary>
    public bool ShowMask
    {
        get => _showMask;
        set => _session.Apply(d => d.ShowMask == value ? d : d with { ShowMask = value });
    }

    /// <summary>#showGrid (editor.html:103; ledger <c>overlay-grid-checkbox</c>).</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set => _session.Apply(d => d.ShowGrid == value ? d : d with { ShowGrid = value });
    }
}
