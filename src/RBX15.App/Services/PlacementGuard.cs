using System.Windows;

namespace RBX15.App.Services;

/// <summary>
/// Turns a saved <see cref="WindowPlacement"/> into one that is safe to restore (pure; the window
/// passes the monitors' work areas). 4.1 always opened 1200x800 centered on the primary display
/// (main.js:5-13, windows/TEST_PLAN.md:24; ledger shell-window-size, fix: v5 remembers size and
/// position).
/// </summary>
public static class PlacementGuard
{
    /// <summary>
    /// <list type="bullet">
    /// <item>No saved placement, a size that is not positive and finite, or a rectangle that overlaps
    /// no work area (its monitor was unplugged or rearranged): the default size, shrunk to fit, centered
    /// on <c>workAreas[0]</c> (the primary monitor), not maximized.</item>
    /// <item>Otherwise the saved placement, moved and if needed shrunk to lie wholly inside the work
    /// area it overlaps most. <see cref="WindowPlacement.Maximized"/> is kept: the window maximizes on
    /// that monitor and restores to these bounds.</item>
    /// </list>
    /// </summary>
    /// <param name="workAreas">Every monitor's work area in DIPs, the primary first. Must not be empty.</param>
    public static WindowPlacement Resolve(WindowPlacement? saved, IReadOnlyList<Rect> workAreas, double defaultWidth = 1200, double defaultHeight = 800)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0 || workAreas.Any(a => a.IsEmpty || a.Width <= 0 || a.Height <= 0))
            throw new ArgumentException("At least one non-empty work area is required.", nameof(workAreas));

        if (saved is not null && double.IsFinite(saved.Left) && double.IsFinite(saved.Top) &&
            double.IsFinite(saved.Width) && double.IsFinite(saved.Height) && saved.Width > 0 && saved.Height > 0)
        {
            var rect = new Rect(saved.Left, saved.Top, saved.Width, saved.Height);
            Rect? best = null;
            double bestArea = 0;
            foreach (var area in workAreas)
            {
                var overlap = Rect.Intersect(rect, area);
                double size = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
                if (size > bestArea) (best, bestArea) = (area, size);
            }
            if (best is Rect target)
                return Fit(saved.Left, saved.Top, saved.Width, saved.Height, target) with { Maximized = saved.Maximized };
        }

        var primary = workAreas[0];
        double w = Math.Min(defaultWidth, primary.Width), h = Math.Min(defaultHeight, primary.Height);
        return new WindowPlacement(primary.Left + (primary.Width - w) / 2, primary.Top + (primary.Height - h) / 2, w, h, false);
    }

    static WindowPlacement Fit(double left, double top, double width, double height, Rect area)
    {
        double w = Math.Min(width, area.Width), h = Math.Min(height, area.Height);
        double x = Math.Clamp(left, area.Left, area.Right - w), y = Math.Clamp(top, area.Top, area.Bottom - h);
        return new WindowPlacement(x, y, w, h, false);
    }
}
