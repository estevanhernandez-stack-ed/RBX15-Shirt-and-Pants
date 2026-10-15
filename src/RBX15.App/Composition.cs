using System.Windows;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Builds the view-models over one <see cref="EditorSession"/>. Each task that adds a required member
/// to MainViewModel adds its construction line and its member here, at the two anchors, and the same
/// lines with fakes in the tests' TestShell.
/// </summary>
internal static class Composition
{
    public static MainViewModel Build(EditorSession session, Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(owner);
        var canvas = new CanvasViewModel(session);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            // M3a: required members above this line.
        };
    }
}
