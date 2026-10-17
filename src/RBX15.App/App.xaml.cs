using System.IO;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;

namespace RBX15.App;

/// <summary>
/// Startup. 4.1's startup state is a fresh document (editor.js:2745-2746; ledger project-startup).
/// The view-models are built in one place, <see cref="Composition.Build"/>; later tasks add their
/// services and view-models there, never here.
/// </summary>
public partial class App : Application
{
    /// <summary>The settings directory: %LOCALAPPDATA%\RBX15 (MSIX redirects it into the package's local data).</summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RBX15");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var session = new EditorSession();
        // The settings store comes first: the composition remembers the last project folder in it (M4a Task 1).
        var settings = new JsonSettingsStore(SettingsDirectory);
        var main = Composition.Build(session, () => Current?.MainWindow, settings);
        MainWindow = new MainWindow(main, settings);
        MainWindow.Show();
    }
}
