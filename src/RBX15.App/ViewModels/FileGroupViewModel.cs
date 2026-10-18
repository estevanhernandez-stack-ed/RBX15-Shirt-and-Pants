using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Services;
using RBX15.Engine;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's File group (editor.html:85-97; ledger home-file-group): Save work, Open and Add image, then
/// Download template. Save As has no button (interfaces §2.1); the window binds it to Ctrl+Shift+S.
/// </summary>
public sealed class FileGroupViewModel : ObservableObject
{
    readonly EditorSession _session;
    readonly IFileDialogs _files;
    readonly IDialogService _dialogs;
    readonly IStatus _status;
    readonly ImportService _import;

    public FileGroupViewModel(EditorSession session, IFileDialogs files, IDialogService dialogs, IStatus status, ImportService import,
        ProjectFileService projectFiles)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(projectFiles);
        (_session, _files, _dialogs, _status, _import) = (session, files, dialogs, status, import);
        AddImageCommand = new RelayCommand(AddImage);
        DownloadTemplateCommand = new RelayCommand(DownloadTemplate);
        SaveCommand = new RelayCommand(() => projectFiles.Save(), () => !projectFiles.IsBusy);
        SaveAsCommand = new RelayCommand(() => projectFiles.SaveAs(), () => !projectFiles.IsBusy);
        OpenCommand = new AsyncRelayCommand(projectFiles.OpenAsync, () => !projectFiles.IsBusy);
        projectFiles.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ProjectFileService.IsBusy)) return;
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsCommand.NotifyCanExecuteChanged();
            OpenCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>btnLoadImage (editor.js:2692-2708; project-load-image-layer): pick, then one undo step for all.</summary>
    public IRelayCommand AddImageCommand { get; }

    /// <summary>btnExport (exportPNG, editor.js:1500-1541; canvas-export-pipeline, export-png-guard, io-download-save-dialog).</summary>
    public IRelayCommand DownloadTemplateCommand { get; }

    /// <summary>btnSaveProject, "Save work" (editor.html:88, editor.js:2682): the current file, or Save As. Waits while an Open reads.</summary>
    public IRelayCommand SaveCommand { get; }

    /// <summary>Save As, Ctrl+Shift+S only (spec §3, interfaces §2.1). Waits while an Open reads.</summary>
    public IRelayCommand SaveAsCommand { get; }

    /// <summary>btnLoadProject, "Open" (editor.html:89, editor.js:2683). One run at a time; waits while an Open reads.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

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
        // Atomic: write beside the target, then move over it, so a failed write never truncates an existing file.
        string tmp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, Renderer.ExportPng(doc));
            File.Move(tmp, path, overwrite: true);
            _status.Show($"Saved {name}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(tmp); } catch (Exception) { /* best effort; the save error below is what matters */ }
            _status.Show($"Couldn't save {name}: {e.Message}");
        }
    }
}
