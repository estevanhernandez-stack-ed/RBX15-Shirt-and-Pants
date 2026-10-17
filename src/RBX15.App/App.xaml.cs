using System.IO;
using System.Windows;
using RBX15.App.Services;
using RBX15.App.ViewModels;
#if TEST_INPUT_BRIDGE
using RBX15.App.Testing;
#endif

namespace RBX15.App;

/// <summary>
/// Startup. 4.1's startup state is a fresh document (editor.js:2745-2746; ledger project-startup).
/// The view-models are built in one place, <see cref="Composition.Build"/>; later tasks add their
/// services and view-models there, never here.
/// <para>
/// M4a: the test input bridge's two hook sites are the <c>#if TEST_INPUT_BRIDGE</c> blocks below. They compile only
/// with <c>-p:EnableTestInputBridge=true</c> (interfaces R9), and the bridge starts only when that build is also
/// launched with <c>--test-input-bridge</c>.
/// </para>
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
#if TEST_INPUT_BRIDGE
        if (TestInputBridge.Requested(e.Args))
        {
            StartWithBridge(session);
            return;
        }
#endif
        var settings = new JsonSettingsStore(SettingsDirectory);
        var main = Composition.Build(session, () => Current?.MainWindow, settings);
        MainWindow = new MainWindow(main, settings);
        MainWindow.Show();
    }

#if TEST_INPUT_BRIDGE
    /// <summary>
    /// The bridged launch (interfaces R10): file dialogs answer from the bridge's queue, never natively; settings go to
    /// %TEMP%\rbx15-bridge-&lt;pid&gt;, so the user's settings.json is never read or written; themed dialogs and the
    /// color picker stay real app windows. The pipe line goes to stdout once the window is shown.
    /// </summary>
    void StartWithBridge(EditorSession session)
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "rbx15-bridge-" + Environment.ProcessId);
        Directory.CreateDirectory(settingsDirectory);
        var settings = new JsonSettingsStore(settingsDirectory);
        var files = new BridgeFileDialogs(Console.Out);
        Func<Window?> owner = () => Current?.MainWindow;
        var main = Composition.Build(session, files, new ThemedDialogService(owner), new ThemedColorPicker(owner), settings);
        var window = new MainWindow(main, settings);
        MainWindow = window;
        window.Show();
        var bridge = TestInputBridge.Start(window, Console.Out, files);
        Exit += (_, _) =>
        {
            bridge.Dispose();
            // The window saved its placement on Closing, before Exit; nothing reads this folder again (critic L12).
            try { Directory.Delete(settingsDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        };
    }
#endif
}
