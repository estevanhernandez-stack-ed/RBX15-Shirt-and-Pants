using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;

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
        var canvas = new CanvasViewModel(session);
        var status = new StatusViewModel();
        var fileDialogs = new FakeFileDialogs();
        var import = new ImportService(session, status);
        var fileGroup = new FileGroupViewModel(session, fileDialogs, new FakeDialogService(), status, import);
        var layers = new LayersViewModel(session, import, fileDialogs);
        canvas.DropHandler = d => import.Import(d.Paths, (d.TemplatePoint.X, d.TemplatePoint.Y));
        canvas.ClickHandler = p => session.Select(LayerOps.HitTest(session.Document, p.X, p.Y));
        var home = new HomeViewModel(session, new FakeColorPicker())
        {
            File = fileGroup,
        };
        // M3a: construction lines above this line.
        return new MainViewModel(session)
        {
            Canvas = canvas,
            Home = home,
            Layers = layers,
            Status = status,
            // M3a: required members above this line.
        };
    }
}
