using System.Windows.Threading;

namespace RBX15.App.Services;

/// <summary>
/// The autosave clock (M5 item 2): raises <see cref="Tick"/> on the UI thread, once per interval. A seam so the tests drive
/// autosave by hand and never wait on a real 60 s timer.
/// </summary>
public interface ITickSource
{
    event EventHandler? Tick;
}

/// <summary>The app's tick source: a <see cref="DispatcherTimer"/> on the thread that creates it, running from construction.</summary>
public sealed class DispatcherTimerTickSource : ITickSource
{
    readonly DispatcherTimer _timer;

    public DispatcherTimerTickSource(TimeSpan interval)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = interval };
        _timer.Tick += (_, e) => Tick?.Invoke(this, e);
        _timer.Start();
    }

    public event EventHandler? Tick;
}
