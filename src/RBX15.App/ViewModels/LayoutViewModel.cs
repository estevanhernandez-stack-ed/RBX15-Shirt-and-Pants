namespace RBX15.App.ViewModels;

/// <summary>
/// The Layout tab (editor.html:112-172). Fill Style and Template Regions stay on Home as the one fill tool (M4b decision F1),
/// so the tab holds Presets now (M4b item 5) and gains Background and BG Settings with item 6.
/// </summary>
public sealed class LayoutViewModel
{
    /// <summary>The Presets group, first on the tab.</summary>
    public required PresetsViewModel Presets { get; init; }
}
