using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using RBX15.Engine;

namespace RBX15.App.Services;

/// <summary>
/// Ctrl+V: paste as layer (M5 decisions P1, P2). Always adds new layers on top, selected, as one undo step, and never replaces the selected
/// layer's image. A PNG or bitmap becomes one layer fit in the front slot exactly as Add image places it, named as an import with no name
/// is; a file list goes through <see cref="ImportService"/> as a drop does. Every source meets the import caps. Nothing usable, or a
/// clipboard another app keeps holding, is a line on the status strip and no step.
/// </summary>
public sealed class PasteService
{
    /// <summary>CLIPBRD_E_CANT_OPEN: another app has the clipboard open.</summary>
    public const int ClipboardCantOpen = unchecked((int)0x800401D0);

    /// <summary>Reads per paste: the first plus two retries, about 100 ms in all.</summary>
    public const int Attempts = 3;

    /// <summary>The pause between reads.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    readonly EditorSession _session;
    readonly ImportService _import;
    readonly IClipboardSource _clipboard;
    readonly IStatus _status;
    readonly Action<TimeSpan> _delay;

    /// <param name="delay">Waits between reads; tests pass a no-op.</param>
    public PasteService(EditorSession session, ImportService import, IClipboardSource clipboard, IStatus status, Action<TimeSpan>? delay = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _delay = delay ?? Thread.Sleep;
        PasteCommand = new RelayCommand(() => Paste());
    }

    /// <summary>Raised after a paste added at least one layer: addLayer sets the Select tool.</summary>
    public event EventHandler? LayersAdded;

    /// <summary>Ctrl+V. Always runnable: an empty clipboard answers on the status strip.</summary>
    public IRelayCommand PasteCommand { get; }

    /// <summary>Pastes the clipboard; returns the number of layers added.</summary>
    public int Paste()
    {
        if (!TryRead(out var content))
        {
            _status.Show(DialogText.ClipboardBusy);
            return 0;
        }
        int added = content switch
        {
            ClipboardContent.Png png => PasteEncoded(png.Bytes),
            ClipboardContent.Files files => _import.Import(files.Paths),
            ClipboardContent.Bitmap bitmap => PasteEncoded(bitmap.PngBytes),
            _ => Nothing(),
        };
        if (added > 0) LayersAdded?.Invoke(this, EventArgs.Empty);
        return added;
    }

    bool TryRead(out ClipboardContent content)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                content = _clipboard.Read();
                return true;
            }
            catch (COMException e) when (e.HResult == ClipboardCantOpen)
            {
                if (attempt == Attempts) break;
                _delay(RetryDelay);
            }
        }
        content = new ClipboardContent.None();
        return false;
    }

    int Nothing()
    {
        _status.Show(DialogText.NothingToPaste);
        return 0;
    }

    int PasteEncoded(byte[] bytes)
    {
        SkiaSharp.SKImage image;
        try { image = Images.Decode(bytes); }
        catch (ImageTooLargeException e) { _status.Show(DialogText.PasteFailed(e.Message)); return 0; }
        catch (InvalidDataException) { _status.Show(DialogText.PasteFailed(ImageImporter.UndecodableReason)); return 0; }
        _session.ApplyWithSelection(s => LayerOps.AddImage(s, image, _session.TakeId(), null));
        _status.Clear();
        return 1;
    }
}
