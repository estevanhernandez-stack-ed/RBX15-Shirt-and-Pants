using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The left panel's asset library (editor.html:291-309; editor.js:2302-2545; ledger panel-asset-library). Load a
/// built-in pack or a folder, filter by subfolder, and add a thumbnail as a layer by click (85% in the front slot),
/// right-click (full template size) or a drag onto the canvas (at the drop point). Every add is one undo step through
/// <see cref="ImportService"/>, which also names a failure on the status strip and reverts the tool to Select.
/// Loading replaces whatever the browser showed (editor.js:2369, :2400). A failed load (a missing or empty pack, a
/// folder with no images, a cancelled picker) leaves it as it was (editor.js:2343-2346, :2363-2366, :2384-2387).
/// </summary>
public sealed class AssetLibraryViewModel : ObservableObject
{
    /// <summary>#assetBrowser's starting max-height, and the resize's start when it is hidden (editor.html:303, editor.js:2724).</summary>
    public const double DefaultBrowserHeight = 180;

    /// <summary>The resize's lower clamp (editor.js:2732).</summary>
    public const double MinBrowserHeight = 80;

    /// <summary>grid-template-columns: repeat(3, 1fr) (editor.html:305).</summary>
    public const int TilesPerRow = 3;

    readonly ImportService _import;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ISettingsStore _settings;
    readonly PackSource _packs;
    readonly AssetThumbnailLoader _thumbnails;
    Dictionary<string, AssetTileViewModel> _tiles = new(StringComparer.Ordinal);
    CancellationTokenSource _setCancel = new();
    bool _browserVisible;
    AssetGroup? _currentGroup;
    string _countText = "";
    double _browserHeight = DefaultBrowserHeight;
    double _maxBrowserHeight = double.PositiveInfinity;

    public AssetLibraryViewModel(ImportService import, IFileDialogs files, IDialogService dialogs, IStatus status,
        ISettingsStore settings, PackSource packs, AssetThumbnailLoader thumbnails)
    {
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _packs = packs ?? throw new ArgumentNullException(nameof(packs));
        _thumbnails = thumbnails ?? throw new ArgumentNullException(nameof(thumbnails));
        LoadDripsCommand = new RelayCommand(() => LoadPack(PackSource.Drips));
        LoadWordArtCommand = new RelayCommand(() => LoadPack(PackSource.WordArt));
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ShowGroupCommand = new RelayCommand<AssetGroup>(g => { if (g is not null) ShowGroup(g); });
        AddCommand = new RelayCommand<AssetTileViewModel>(t => { if (t is not null) _import.Import([t.Entry.FullPath]); });
        AddFullCommand = new RelayCommand<AssetTileViewModel>(t => { if (t is not null) _import.ImportFull(t.Entry.FullPath); });
    }

    /// <summary>Drips (#btnPackDrips, editor.js:2377).</summary>
    public IRelayCommand LoadDripsCommand { get; }

    /// <summary>Word art (#btnPackWordArt, editor.js:2378).</summary>
    public IRelayCommand LoadWordArtCommand { get; }

    /// <summary>Open folder (#btnLoadFolder, editor.js:2316): the native folder picker, starting at the last asset folder.</summary>
    public IRelayCommand OpenFolderCommand { get; }

    /// <summary>A subfolder button (editor.js:2443).</summary>
    public IRelayCommand<AssetGroup> ShowGroupCommand { get; }

    /// <summary>Click a thumbnail: add it fit at 85% in the front slot (thumb.onclick, editor.js:2502-2510; asset-click-add).</summary>
    public IRelayCommand<AssetTileViewModel> AddCommand { get; }

    /// <summary>Right-click a thumbnail: add it at full template size (editor.js:2513-2539; asset-rightclick-full).</summary>
    public IRelayCommand<AssetTileViewModel> AddFullCommand { get; }

    /// <summary>
    /// A thumbnail dropped on the canvas: centered on <paramref name="templatePoint"/>, in template pixels
    /// (canvas.ondrop's asset branch, editor.js:965-983; asset-drag-to-canvas).
    /// </summary>
    public void DropOnCanvas(AssetTileViewModel tile, Point templatePoint)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _import.Import([tile.Entry.FullPath], (templatePoint.X, templatePoint.Y));
    }

    /// <summary>False until the first successful load (#assetBrowser display: none, editor.html:303; block at editor.js:2371, :2421).</summary>
    public bool IsBrowserVisible
    {
        get => _browserVisible;
        private set => SetProperty(ref _browserVisible, value);
    }

    /// <summary>The subfolder buttons: every group, only when a folder has more than two (editor.js:2432); none for a pack (editor.js:2372).</summary>
    public ObservableCollection<AssetGroup> GroupButtons { get; } = [];

    /// <summary>The group on show; its button is highlighted (editor.js:2463-2466).</summary>
    public AssetGroup? CurrentGroup
    {
        get => _currentGroup;
        private set => SetProperty(ref _currentGroup, value);
    }

    /// <summary>The current group's tiles, three to a row.</summary>
    public ObservableCollection<AssetRowViewModel> Rows { get; } = [];

    /// <summary>
    /// #assetCount (editor.html:307). Right after a load it is the load's summary, "N unique images (D duplicates
    /// removed)" or "N images in built-in &lt;pack&gt; pack", which 4.1 overwrote in the same tick (ledger
    /// asset-count-label-overwritten and asset-count-label, fix); after a group switch it is showAssetFolder's text (editor.js:2544).
    /// </summary>
    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    /// <summary>
    /// The browser's max-height (max-height: 180px, editor.html:303; resized at editor.js:2732-2733). Setting it clamps
    /// to [<see cref="MinBrowserHeight"/>, <see cref="MaxBrowserHeight"/>], the lower bound winning as Math.max does;
    /// a non-finite value is ignored. Not saved (asset-resizable-browser, keep).
    /// </summary>
    public double BrowserHeight
    {
        get => _browserHeight;
        set
        {
            if (!double.IsFinite(value)) return;
            SetProperty(ref _browserHeight, Math.Max(MinBrowserHeight, Math.Min(_maxBrowserHeight, value)));
        }
    }

    /// <summary>70% of the window's client height (window.innerHeight * 0.7, editor.js:2732); the view sets it as a resize starts.</summary>
    public double MaxBrowserHeight
    {
        get => _maxBrowserHeight;
        set => SetProperty(ref _maxBrowserHeight, value);
    }

    /// <summary>The loaded set, or null before the first load.</summary>
    internal AssetSet? Set { get; private set; }

    void LoadPack(string name)
    {
        var files = _packs.Files(name);
        if (files is null)
        {
            _dialogs.Alert(DialogText.PackNotFound(name));
            return;
        }
        var set = AssetScan.FromPack(name, files);
        if (set.Entries.IsEmpty)
        {
            _dialogs.Alert(DialogText.PackEmpty(name));
            return;
        }
        Show(set);
    }

    void OpenFolder()
    {
        string? root = _files.PickFolder(_settings.LoadFolder(SettingsFolder.Asset));
        if (root is null) return;
        List<(string FullPath, string RelativePath)> found;
        try
        {
            found = Enumerate(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Show(FolderFailedMessage(root, e.Message));
            return;
        }
        var set = AssetScan.FromFolder(found);
        if (set.Entries.IsEmpty)
        {
            _dialogs.Alert(DialogText.NoImagesInFolder);
            return;
        }
        _settings.SaveFolder(SettingsFolder.Asset, root);
        Show(set);
    }

    /// <summary>The status line for a folder that cannot be listed (new in v5: Chromium either listed a folder or showed nothing).</summary>
    internal static string FolderFailedMessage(string root, string reason) => $"Couldn't open {new DirectoryInfo(root).Name}: {reason}";

    /// <summary>
    /// Every file under <paramref name="root"/>, recursively, skipping folders it cannot enter, with a
    /// webkitRelativePath: the picked folder's name, then the path below it with '/' separators (editor.js:2405-2406).
    /// </summary>
    internal static List<(string FullPath, string RelativePath)> Enumerate(string root)
    {
        string name = new DirectoryInfo(root).Name;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(root, "*", options)
            .Select(p => (p, name + "/" + Path.GetRelativePath(root, p).Replace('\\', '/')))
            .ToList();
    }

    void Show(AssetSet set)
    {
        _setCancel.Cancel();
        _setCancel.Dispose();
        _setCancel = new CancellationTokenSource();
        var token = _setCancel.Token;
        _tiles = set.Entries.ToDictionary(e => e.FullPath, e => new AssetTileViewModel(e, p => _thumbnails.LoadAsync(p, token)), StringComparer.Ordinal);
        Set = set;
        GroupButtons.Clear();
        if (set.ShowGroupButtons)
            foreach (var g in set.Groups) GroupButtons.Add(g);
        IsBrowserVisible = true;
        ShowGroup(set.Groups[0]);
        CountText = set.Summary;
    }

    void ShowGroup(AssetGroup group)
    {
        if (Set is null || !Set.Groups.Contains(group)) return;
        CurrentGroup = group;
        Rows.Clear();
        foreach (var chunk in group.Entries.Chunk(TilesPerRow))
            Rows.Add(new AssetRowViewModel(chunk.Select(e => _tiles[e.FullPath]).ToArray()));
        CountText = AssetScan.CountText(group);
    }
}
