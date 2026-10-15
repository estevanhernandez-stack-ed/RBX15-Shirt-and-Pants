using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>
/// The tests' one MainViewModel builder: the composition root of App (Composition.Build) with fakes in
/// place of the native services. Every later task that adds a required member to MainViewModel adds
/// its construction line and its member here, at the two anchors, so no earlier test breaks (CS9035).
/// </summary>
internal static class TestShell
{
#pragma warning disable CS0028 // The name Main is the interface sheet's; the test SDK's generated Program.Main is the entry point.
    public static MainViewModel Main(EditorSession session)
#pragma warning restore CS0028
    {
        ArgumentNullException.ThrowIfNull(session);
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            // M3a: required members above this line.
        };
    }
}
