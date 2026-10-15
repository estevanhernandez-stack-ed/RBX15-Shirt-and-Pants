using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's File group in M3a: Add image and Download template (editor.html:85-97; ledger
/// home-file-group). Save work and Open are M4 and stay hidden until they work.
/// </summary>
public sealed class FileGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ImportService _import;

    public FileGroupViewModel(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ImportService import)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(import);
        (_session, _files, _dialogs, _status, _import) = (session, files, dialogs, status, import);
        AddImageCommand = new RelayCommand(AddImage);
        DownloadTemplateCommand = new RelayCommand(DownloadTemplate);
    }

    /// <summary>btnLoadImage (editor.js:2692-2708; project-load-image-layer): pick, then one undo step for all.</summary>
    public IRelayCommand AddImageCommand { get; }

    /// <summary>btnExport (exportPNG, editor.js:1500-1541; canvas-export-pipeline, export-png-guard, io-download-save-dialog).</summary>
    public IRelayCommand DownloadTemplateCommand { get; }

    void AddImage()
    {
        var paths = _files.OpenImages();
        if (paths.Count > 0) _import.Import(paths);
    }

    void DownloadTemplate()
    {
        var doc = _session.Document;
        if (!ExportGuard.HasArt(doc) && !_dialogs.Confirm(ExportGuard.EmptyTemplateMessage)) return;
        var path = _files.SavePng(ExportNaming.PngFileName(_session.TemplateTitle));
        if (path is null) return;
        string name = Path.GetFileName(path);
        try
        {
            File.WriteAllBytes(path, Renderer.ExportPng(doc));
            _status.Show($"Saved {name}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Show($"Couldn't save {name}: {e.Message}");
        }
    }
}
