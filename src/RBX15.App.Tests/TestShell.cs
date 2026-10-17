using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The tests' one MainViewModel builder: the composition root of App (Composition.Build) with fakes in
/// place of the native services. A task that adds a required member to MainViewModel adds it in Composition only; a new native
/// service adds a parameter to the injectable overload and a fake here.
/// </summary>
internal static class TestShell
{
#pragma warning disable CS0028 // The name Main is the interface sheet's; the test SDK's generated Program.Main is the entry point.
    public static MainViewModel Main(EditorSession session)
#pragma warning restore CS0028
    {
        return Composition.Build(session, new FakeFileDialogs(), new FakeDialogService(), new FakeColorPicker(), new FakeSettingsStore());
    }
}
