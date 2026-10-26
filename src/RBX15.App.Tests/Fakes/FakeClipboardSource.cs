using System.Runtime.InteropServices;
using RBX15.App.Services;

namespace RBX15.App.Tests.Fakes;

/// <summary>An <see cref="IClipboardSource"/> that returns what the test sets and never touches the system clipboard.</summary>
sealed class FakeClipboardSource : IClipboardSource
{
    /// <summary>What the next reads return.</summary>
    public ClipboardContent Content { get; set; } = new ClipboardContent.None();

    /// <summary>How many reads throw CLIPBRD_E_CANT_OPEN before one succeeds.</summary>
    public int BusyReads { get; set; }

    /// <summary>The number of times <see cref="Read"/> was called.</summary>
    public int Reads { get; private set; }

    public ClipboardContent Read()
    {
        Reads++;
        if (BusyReads > 0)
        {
            BusyReads--;
            throw new COMException("Clipboard is held by another app.", PasteService.ClipboardCantOpen);
        }
        return Content;
    }
}
