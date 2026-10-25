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
    public static MainViewModel Main(EditorSession session, bool previewVisible = false, Func<Random>? random = null)
#pragma warning restore CS0028
    {
        var main = Composition.Build(session, new FakeFileDialogs(), new FakeDialogService(), new FakeColorPicker(), new FakeSettingsStore(), Storage(), new ManualTickSource(), random);
        main.AvatarPreview.IsVisible = previewVisible;
        main.Palette.Dispose();   // see Quiet: the palette's idle pass would read layer images a test may have freed
        return main;
    }

    /// <summary>
    /// A storage root no other test shares, under the temp folder and never created unless a test writes to it (autosave). Tests never
    /// touch %LOCALAPPDATA%\RBX15 (M5 constraint). A test that writes deletes its root.
    /// </summary>
    public static AppStorage Storage() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rbx15-app-tests", "storage-" + Guid.NewGuid().ToString("N")));

    /// <summary>
    /// Turns the 3D preview off in a shell a test built with Composition.Build directly. The preview rebuilds its texture
    /// on a later dispatcher turn; a test that disposes its layer images before that turn would have the rebuild render
    /// freed native bitmaps (an access violation in a later test), so the kit's shells start with the preview off and the
    /// preview's own tests ask for it on. The art palette is the same: its idle pass reads the visible layers' images, so a
    /// quiet shell stops it following (its colors stay as they were); the palette's own tests build an ArtPalette directly.
    /// </summary>
    public static MainViewModel Quiet(MainViewModel main)
    {
        main.AvatarPreview.IsVisible = false;
        main.Palette.Dispose();
        return main;
    }
}
