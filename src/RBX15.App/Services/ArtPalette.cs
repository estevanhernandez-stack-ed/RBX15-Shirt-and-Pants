using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Services;

/// <summary>
/// The colors of the user's art, kept current for every place a color is picked (ledger <c>art-palette</c>, new). It
/// follows the session's document the way the 3D preview does: a change schedules one pass at
/// <see cref="DispatcherPriority.Background"/>, so a drag's many changes cost one look, and the pass runs
/// <see cref="Palette.FromDocument"/> only when the sequence of visible layers' images differs from the last one, so moving
/// a layer, a region fill or a view toggle never recomputes. Session state only: not in the document, not an undo step.
/// </summary>
public sealed class ArtPalette : ObservableObject, IDisposable
{
    readonly EditorSession _session;
    readonly Dispatcher _dispatcher;
    IReadOnlyList<string> _colors = Array.Empty<string>();
    SKImage[] _seen = Array.Empty<SKImage>();
    bool _scheduled, _disposed;

    public ArtPalette(EditorSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dispatcher = Dispatcher.CurrentDispatcher;
        session.Changed += OnSessionChanged;
        Refresh();
    }

    /// <summary>Lowercase <c>#rrggbb</c>, the most used first; empty while no visible layer has an opaque pixel.</summary>
    public IReadOnlyList<string> Colors => _colors;

    /// <summary>How many times the palette has been computed, the first at construction included. Tests observe the coalescing here.</summary>
    public int Recomputes { get; private set; }

    void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (!e.What.HasFlag(SessionChange.Document) || _scheduled || _disposed) return;
        _scheduled = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _scheduled = false;
            if (!_disposed) Refresh();
        });
    }

    /// <summary>Recomputes if the visible layers' images are not the ones last looked at.</summary>
    void Refresh()
    {
        var images = _session.Document.Layers.Where(l => l.Visible).Select(l => l.Image).ToArray();
        if (Recomputes > 0 && images.SequenceEqual(_seen, ReferenceEqualityComparer.Instance)) return;
        _seen = images;
        Recomputes++;
        var next = Palette.Extract(images);
        if (_colors.SequenceEqual(next)) return;
        _colors = next;
        OnPropertyChanged(nameof(Colors));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Changed -= OnSessionChanged;
    }
}
