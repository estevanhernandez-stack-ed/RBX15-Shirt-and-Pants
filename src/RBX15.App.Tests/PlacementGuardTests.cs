using System.Windows;
using RBX15.App.Services;

namespace RBX15.App.Tests;

/// <summary>
/// PlacementGuard (ledger shell-window-size, fix): 4.1 always opened 1200x800 centered on the primary
/// display (main.js:5-13); v5 restores the saved placement when it is still on a monitor.
/// </summary>
public class PlacementGuardTests
{
    static readonly Rect Primary = new(0, 0, 1920, 1040);          // 1920x1080 minus a 40 DIP taskbar
    static readonly Rect LeftMonitor = new(-1920, 0, 1920, 1080);

    [Fact] // first launch: 1200x800 centered on the primary work area (windows/TEST_PLAN.md:24)
    public void No_saved_placement_gives_1200x800_centered_on_the_primary()
    {
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false), PlacementGuard.Resolve(null, [Primary, LeftMonitor]));
    }

    [Fact] // a saved placement fully on a monitor comes back as saved, maximized flag included
    public void A_visible_placement_is_kept()
    {
        var saved = new WindowPlacement(-1700, 50, 1000, 700, true);
        Assert.Equal(saved, PlacementGuard.Resolve(saved, [Primary, LeftMonitor]));
    }

    [Fact] // Review Focus: the monitor the window was on is unplugged
    public void A_placement_on_a_missing_monitor_falls_back_to_the_default()
    {
        var saved = new WindowPlacement(-1700, 50, 1000, 700, true);
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // partly off the right edge: moved back inside, size kept
    public void A_placement_hanging_off_an_edge_is_moved_inside()
    {
        var saved = new WindowPlacement(1500, 900, 1000, 600, false);
        Assert.Equal(new WindowPlacement(920, 440, 1000, 600, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // larger than the work area (a smaller monitor now): shrunk to it
    public void A_placement_larger_than_the_work_area_is_shrunk_to_fit()
    {
        var saved = new WindowPlacement(-10, -10, 2500, 1400, false);
        Assert.Equal(new WindowPlacement(0, 0, 1920, 1040, false), PlacementGuard.Resolve(saved, [Primary]));
    }

    [Fact] // straddling two monitors: lands on the one it overlaps most
    public void A_straddling_placement_lands_on_the_monitor_it_overlaps_most()
    {
        var saved = new WindowPlacement(-300, 100, 1000, 700, false);
        Assert.Equal(new WindowPlacement(0, 100, 1000, 700, false), PlacementGuard.Resolve(saved, [Primary, LeftMonitor]));
        var mostlyLeft = new WindowPlacement(-800, 100, 1000, 700, false);
        Assert.Equal(new WindowPlacement(-1000, 100, 1000, 700, false), PlacementGuard.Resolve(mostlyLeft, [Primary, LeftMonitor]));
    }

    [Fact] // a small primary: the default shrinks to it
    public void The_default_shrinks_to_a_small_primary()
    {
        Assert.Equal(new WindowPlacement(0, 0, 1024, 728, false), PlacementGuard.Resolve(null, [new Rect(0, 0, 1024, 728)]));
    }

    [Theory] // a corrupt size or position is treated as no placement
    [InlineData(double.NaN, 0, 800, 600)]
    [InlineData(0, double.PositiveInfinity, 800, 600)]
    [InlineData(0, 0, 0, 600)]
    [InlineData(0, 0, 800, -1)]
    [InlineData(0, 0, double.NaN, 600)]
    public void A_corrupt_placement_falls_back_to_the_default(double left, double top, double width, double height)
    {
        Assert.Equal(new WindowPlacement(360, 120, 1200, 800, false),
                     PlacementGuard.Resolve(new WindowPlacement(left, top, width, height, true), [Primary]));
    }

    [Fact]
    public void Work_areas_are_required()
    {
        Assert.Throws<ArgumentException>(() => PlacementGuard.Resolve(null, []));
        Assert.Throws<ArgumentException>(() => PlacementGuard.Resolve(null, [Rect.Empty]));
    }

    [Fact] // a monitor reporting no width or height (disconnecting) is dropped, never handed to Resolve
    public void Degenerate_work_areas_are_skipped_and_the_primary_stays_first()
    {
        var zero = new Rect(3000, 0, 0, 1000);
        var flat = new Rect(0, 2000, 800, 0);
        var usable = MonitorWorkAreas.Usable([(false, LeftMonitor), (false, zero), (true, Primary), (false, flat)], new Rect(0, 0, 640, 480));
        Assert.Equal(new[] { Primary, LeftMonitor }, usable);
        Assert.NotNull(PlacementGuard.Resolve(null, usable));
    }

    [Fact] // nothing usable: the system work area, else a 1024x768 desktop, so startup still places the window
    public void With_no_usable_work_area_the_fallback_is_used()
    {
        var fallback = new Rect(0, 0, 640, 480);
        Assert.Equal(new[] { fallback }, MonitorWorkAreas.Usable([(true, new Rect(0, 0, 0, 0))], fallback));
        Assert.Equal(new[] { fallback }, MonitorWorkAreas.Usable([], fallback));
        Assert.Equal(new[] { new Rect(0, 0, 1024, 768) }, MonitorWorkAreas.Usable([], new Rect(0, 0, 0, 0)));
    }
}
