namespace RBX15.App.ViewModels;

/// <summary>
/// One row of the asset grid: up to three tiles (grid-template-columns: repeat(3, 1fr), editor.html:305). The browser
/// virtualizes rows, not tiles, because WPF's virtualizing panel is a stack (interfaces R5).
/// </summary>
public sealed class AssetRowViewModel(IReadOnlyList<AssetTileViewModel> tiles)
{
    public IReadOnlyList<AssetTileViewModel> Tiles { get; } = tiles ?? throw new ArgumentNullException(nameof(tiles));
}
