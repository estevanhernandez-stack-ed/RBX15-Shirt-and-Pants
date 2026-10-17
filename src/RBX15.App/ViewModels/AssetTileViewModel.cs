using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// One thumbnail in the asset browser (showAssetFolder's thumb, editor.js:2468-2541). The first read of
/// <see cref="Thumbnail"/> queues its decode: only realized rows bind it, so only tiles the user can see are ever
/// decoded (interfaces R5). Tiles belong to one loaded set and are reused across that set's group switches, so a
/// switch never decodes again (ledger asset-objecturl-lifecycle, fix: 4.1 revoked and re-created every blob URL).
/// </summary>
public sealed class AssetTileViewModel : ObservableObject
{
    readonly Func<string, Task<BitmapSource?>> _load;
    BitmapSource? _thumbnail;
    bool _requested;
    bool _failed;

    internal AssetTileViewModel(AssetEntry entry, Func<string, Task<BitmapSource?>> load)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _load = load ?? throw new ArgumentNullException(nameof(load));
    }

    public AssetEntry Entry { get; }

    /// <summary>The name overlay (editor.js:2478-2484).</summary>
    public string Caption => Entry.Caption;

    /// <summary>thumb.title = file.name (editor.js:2471).</summary>
    public string ToolTip => Entry.FileName;

    /// <summary>Null until the decode arrives; the first read queues it. PropertyChanged when it arrives.</summary>
    public BitmapSource? Thumbnail
    {
        get
        {
            if (!_requested)
            {
                _requested = true;
                Load();
            }
            return _thumbnail;
        }
    }

    /// <summary>True when the decode failed (over a cap, damaged, or gone): the tile stays empty with its caption.</summary>
    public bool ThumbnailFailed => _failed;

    /// <summary>True once <see cref="Thumbnail"/> was read: what the virtualization test counts.</summary>
    internal bool ThumbnailRequested => _requested;

    async void Load()
    {
        BitmapSource? result;
        try
        {
            result = await _load(Entry.FullPath);
        }
        catch (OperationCanceledException)
        {
            return;   // a newer pack or folder replaced this tile's set
        }
        if (result is null)
        {
            _failed = true;
            OnPropertyChanged(nameof(ThumbnailFailed));
            return;
        }
        _thumbnail = result;
        OnPropertyChanged(nameof(Thumbnail));
    }
}
