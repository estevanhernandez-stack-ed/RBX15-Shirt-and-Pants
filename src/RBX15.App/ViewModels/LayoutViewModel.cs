namespace RBX15.App.ViewModels;

/// <summary>
/// The Layout tab (editor.html:112-172). Fill Style and Template Regions stay on Home as the one fill tool (M4b decision F1),
/// so the tab holds Presets (M4b item 5), then Background and BG Settings (item 6). The two background groups are always
/// shown: they edit the document, not a selected layer.
/// </summary>
public sealed class LayoutViewModel
{
    /// <summary>The Presets group, first on the tab.</summary>
    public required PresetsViewModel Presets { get; init; }

    /// <summary>The Background swatches and BG Settings (color, image, opacity), after Presets.</summary>
    public required BackgroundViewModel Background { get; init; }
}
