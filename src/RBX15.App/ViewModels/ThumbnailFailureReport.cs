using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using RBX15.App.Services;

namespace RBX15.App.ViewModels;

/// <summary>
/// Says on the status strip that an asset preview could not be shown (ledger diag-console-only-errors). 4.1 set
/// each thumbnail's <c>img.src</c> to a blob URL with no <c>onerror</c> (editor.js:2473-2475), so an unreadable or
/// oversized file showed a broken image and nothing else. v5 shows one line per loaded set, naming the first file
/// that failed, so a folder of broken files never floods the strip or the screen reader's live region.
/// <para>
/// A set is the tiles one pack or folder load made. A group switch reuses them (interfaces R5), so it never
/// reports again; a new load brings tiles never seen before, which starts a new set and re-arms the report.
/// A failure from a tile of an earlier set is ignored. UI thread only.
/// </para>
/// </summary>
internal sealed class ThumbnailFailureReport
{
    readonly IStatus _status;
    readonly HashSet<AssetTileViewModel> _set = new(ReferenceEqualityComparer.Instance);
    bool _reported;

    ThumbnailFailureReport(AssetLibraryViewModel library, IStatus status)
    {
        _status = status;
        library.Rows.CollectionChanged += OnRowsChanged;
        Rescan(library.Rows);
    }

    /// <summary>Starts reporting for <paramref name="library"/>. Composition calls it once, beside the library's construction.</summary>
    public static ThumbnailFailureReport Attach(AssetLibraryViewModel library, IStatus status)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(status);
        return new ThumbnailFailureReport(library, status);
    }

    /// <summary>The status line. Clicking the tile imports the file, and the import names the exact reason.</summary>
    public static string Message(string fileName) => $"Couldn't show a preview for {fileName}. Click its tile to see why.";

    void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var rows = (IList)sender!;
        // Rows appended to rows already showing belong to the set on screen; anything else is checked from scratch.
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added && rows.Count > added.Count)
            Watch(Tiles(added), sameSet: true);
        else
            Rescan(rows);
    }

    void Rescan(IList rows)
    {
        var tiles = Tiles(rows);
        if (tiles.Count == 0) return;                    // a Clear before the next group or set fills the rows
        Watch(tiles, sameSet: tiles.Exists(_set.Contains));
    }

    static List<AssetTileViewModel> Tiles(IList rows) => rows.Cast<AssetRowViewModel>().SelectMany(r => r.Tiles).ToList();

    void Watch(List<AssetTileViewModel> tiles, bool sameSet)
    {
        if (!sameSet)
        {
            foreach (var old in _set) old.PropertyChanged -= OnTileChanged;
            _set.Clear();
            _reported = false;
        }
        foreach (var tile in tiles)
        {
            if (!_set.Add(tile)) continue;
            tile.PropertyChanged += OnTileChanged;
            if (tile.ThumbnailFailed) Report(tile);
        }
    }

    void OnTileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetTileViewModel.ThumbnailFailed) or null or ""
            && sender is AssetTileViewModel { ThumbnailFailed: true } tile && _set.Contains(tile))
            Report(tile);
    }

    void Report(AssetTileViewModel tile)
    {
        if (_reported) return;
        _reported = true;
        _status.Show(Message(tile.Entry.FileName));
    }
}
