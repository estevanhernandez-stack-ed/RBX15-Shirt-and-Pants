using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace RBX15.App.Tests;

/// <summary>
/// Runs test code that needs WPF on one shared STA thread with a running <see cref="Dispatcher"/>.
/// That thread owns the process's one <see cref="System.Windows.Application"/>, holding the theme App.xaml merges
/// (Themes/Controls.xaml, which merges Themes/Tokens.xaml), so views built in a test resolve their
/// StaticResource keys as they do in the app. It is a plain Application, not RBX15.App.App: the
/// Application constructor queues its startup on the dispatcher, and App's OnStartup would open the
/// real main window. WPF objects belong to the thread that made them, so a test builds and inspects
/// them inside one call. Calls from parallel test classes queue on the thread and run one at a time.
/// </summary>
internal static class StaThread
{
    /// <summary>The dictionary App.xaml merges, as a component path (the pack scheme exists only once WPF has started).</summary>
    public const string Theme = "/RBX15.App;component/Themes/Controls.xaml";

    static readonly Lazy<Dispatcher> Ui = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void Run(Action body) => Run<object?>(() => { body(); return null; });

    /// <summary>
    /// Runs the current dispatcher's queue once, down to Background priority. A parsed view's bindings
    /// attach at DataBind priority, so inside one Run call they stay Unattached until the queue runs.
    /// View tests call it after setting DataContext, on the StaThread.
    /// </summary>
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

    public static T Run<T>(Func<T> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        Ui.Value.Invoke(() =>
        {
            try { result = body(); }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
        });
        failure?.Throw();
        return result;
    }

    static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri(Theme, UriKind.Relative) });
            }
            catch (Exception e) { error = e; }
            ready.Set();
            if (error is null) Dispatcher.Run();
        })
        { IsBackground = true, Name = "RBX15.App.Tests STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (error is not null) throw new InvalidOperationException("The test STA thread failed to start.", error);
        return dispatcher!;
    }
}
