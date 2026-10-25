namespace RBX15.App.ViewModels;

/// <summary>
/// 4.1's Layout tab (editor.html:112-172), shown as "Background" since Este's 2026-10-25 ruling: Presets (M4b item 5),
/// Background and BG Settings (item 6), then Fill Style and Template Regions, back where 4.1 had them (the ruling reverses F1,
/// which had kept them on Home). The code keeps the name Layout. Every group but Presets is always shown: they edit the
/// document, not a selected layer.
/// </summary>
public sealed class LayoutViewModel
{
    /// <summary>The Presets group, first on the tab.</summary>
    public required PresetsViewModel Presets { get; init; }

    /// <summary>The Background swatches and BG Settings (color, image, opacity), after Presets.</summary>
    public required BackgroundViewModel Background { get; init; }

    /// <summary>The Fill Style group (editor.html:145-160), after BG Settings.</summary>
    public required FillStyleViewModel FillStyle { get; init; }

    /// <summary>The Template Regions grid (editor.html:161-171), last; it fills with <see cref="FillStyle"/>.</summary>
    public required RegionsGroupViewModel Regions { get; init; }
}
