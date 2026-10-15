using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Parity;

namespace RBX15.App.Tests;

/// <summary>
/// The themed color picker popup, checked without showing it (ledger fillstyle-color-pickers, fix):
/// its controls and names, keys, markers, placement, and theme-only styling. Showing it is the smoke step.
/// </summary>
public class ColorPickerPopupTests
{
    static readonly string Xaml = Path.Combine(RepoRoot.Find(AppContext.BaseDirectory), "src", "RBX15.App", "Views", "ColorPickerPopup.xaml");

    [Fact]
    public void Every_control_has_an_automation_name_and_the_swatches_show_old_and_new() => StaThread.Run(() =>
    {
        var vm = new ColorPickerViewModel("#17d4fa");
        var p = new ColorPickerPopup(vm);
        StaThread.Pump();
        vm.Hue = 0;
        StaThread.Pump();
        Assert.Equal(WindowStyle.None, p.WindowStyle);
        Assert.False(p.ShowInTaskbar);
        foreach (var (element, name) in new (FrameworkElement, string)[]
                 {
                     (p, "Color picker"), (p.SvSquare, "Saturation and brightness"), (p.HueStrip, "Hue"),
                     (p.HexBox, "Hex color"),
                     (p.OkButton, "OK"), (p.CancelButton, "Cancel"),
                 })
            Assert.Equal(name, AutomationProperties.GetName(element));
        Assert.True(p.SvSquare.Focusable && p.HueStrip.Focusable);
        Assert.Equal(Color.FromRgb(0x17, 0xd4, 0xfa), ((SolidColorBrush)p.OldSwatch.Background).Color);
        var now = ((SolidColorBrush)p.NewSwatch.Background).Color;
        Assert.Equal(vm.Hex, ColorMath.ToHex(now.R, now.G, now.B));
        Assert.NotEqual("#17d4fa", vm.Hex);
        Assert.Equal(vm.Hex, p.HexBox.Text);
        Assert.Equal("New color; original color #17d4fa", AutomationProperties.GetHelpText(p.HexBox));   // the swatches' meaning
    });

    [Fact] // spec §4: the square and the strip reach UI Automation with a name and a value
    public void The_square_and_the_strip_have_peers_with_readable_values() => StaThread.Run(() =>
    {
        var vm = new ColorPickerViewModel("#ff0000");
        var p = new ColorPickerPopup(vm);
        StaThread.Pump();
        var sv = UIElementAutomationPeer.CreatePeerForElement(p.SvSquare);
        var hue = UIElementAutomationPeer.CreatePeerForElement(p.HueStrip);
        Assert.NotNull(sv);
        Assert.NotNull(hue);
        Assert.Equal("Saturation and brightness", sv.GetName());
        Assert.Equal("Hue", hue.GetName());
        Assert.Equal(AutomationControlType.Slider, hue.GetAutomationControlType());

        var value = Assert.IsAssignableFrom<IValueProvider>(sv.GetPattern(PatternInterface.Value));
        Assert.Equal("saturation 100%, brightness 100%", value.Value);
        Assert.True(value.IsReadOnly);
        vm.SetSvFromPoint(0.5, 0.25);
        Assert.Equal("saturation 50%, brightness 75%", value.Value);
        Assert.Throws<ElementNotEnabledException>(() => value.SetValue("x"));

        var range = Assert.IsAssignableFrom<IRangeValueProvider>(hue.GetPattern(PatternInterface.RangeValue));
        Assert.Equal((0.0, 360.0, 1.0, 10.0, 0.0, false), (range.Minimum, range.Maximum, range.SmallChange, range.LargeChange, range.Value, range.IsReadOnly));
        range.SetValue(120);                                       // settable: the color follows
        Assert.Equal(120, vm.Hue);
        Assert.Equal(120, range.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(361));
        vm.Cancel();
        Assert.True(range.IsReadOnly);
    });

    [Fact]
    public void Arrow_keys_move_the_focused_square_or_strip_and_shift_takes_the_big_step() => StaThread.Run(() =>
    {
        var vm = new ColorPickerViewModel("#808080");
        var p = new ColorPickerPopup(vm);
        Assert.True(p.HandleKey(Key.Right, ModifierKeys.None, p.SvSquare));
        Assert.True(p.HandleKey(Key.Up, ModifierKeys.Shift, p.SvSquare));
        Assert.Equal(0.01, vm.Saturation, 9);
        Assert.Equal(128 / 255.0 + 0.1, vm.Value, 9);
        Assert.True(p.HandleKey(Key.Right, ModifierKeys.Shift, p.HueStrip));
        Assert.True(p.HandleKey(Key.Down, ModifierKeys.None, p.HueStrip));
        Assert.Equal(9, vm.Hue);
        Assert.False(p.HandleKey(Key.Right, ModifierKeys.None, p.HexBox));   // the text field keeps its caret keys
        Assert.False(vm.IsFinished);
    });

    [Fact]
    public void The_markers_follow_the_color() => StaThread.Run(() =>
    {
        var vm = new ColorPickerViewModel("#ff0000");
        var p = new ColorPickerPopup(vm);
        Assert.Equal((200 - 6.0, -6.0), (Canvas.GetLeft(p.SvMarker), Canvas.GetTop(p.SvMarker)));
        vm.SetSvFromPoint(0.5, 0.25);
        vm.Hue = 180;
        Assert.Equal((100 - 6.0, 35 - 6.0), (Canvas.GetLeft(p.SvMarker), Canvas.GetTop(p.SvMarker)));
        Assert.Equal(100 - 3.0, Canvas.GetLeft(p.HueMarker));
    });

    [Theory]
    [InlineData(Key.Escape, false, null)]
    [InlineData(Key.Enter, false, "#00ffff")]
    [InlineData(Key.Enter, true, null)]
    public void Esc_cancels_Enter_commits_and_Enter_on_Cancel_cancels(Key key, bool onCancel, string? result) => StaThread.Run(() =>
    {
        var vm = new ColorPickerViewModel("#ff0000");
        var p = new ColorPickerPopup(vm);
        vm.Hue = 180;
        Assert.True(p.HandleKey(key, ModifierKeys.None, onCancel ? p.CancelButton : p.SvSquare));
        Assert.True(vm.IsFinished);
        Assert.Equal(result, vm.Result);
        p.Finish(commit: true);                                   // a late Deactivated changes nothing
        Assert.Equal(result, vm.Result);
    });

    [Theory] // under the anchor, above it when the bottom is short, always inside the work area
    [InlineData(100, 100, 100, 130)]       // room below: left-aligned, 4 DIP gap
    [InlineData(100, 800, 100, 544)]       // no room below: above the anchor (800 - 4 - 252)
    [InlineData(1800, 100, 1680, 130)]     // off the right edge: pulled in
    [InlineData(-50, 100, 0, 130)]         // off the left edge: pulled in
    public void The_popup_opens_by_its_anchor_and_stays_on_screen(double ax, double ay, double x, double y)
    {
        var at = ColorPickerPopup.Place(new Rect(ax, ay, 26, 26), new Size(240, 252), new Rect(0, 0, 1920, 1040));
        Assert.Equal((x, y), (at.X, at.Y));
    }

    [Fact]
    public void A_popup_taller_than_the_work_area_keeps_its_top_on_screen()
    {
        var at = ColorPickerPopup.Place(new Rect(10, 10, 26, 22), new Size(240, 600), new Rect(0, 0, 800, 500));
        Assert.Equal(new Point(10, 0), at);
    }

    [Fact]
    public void Without_an_anchor_on_screen_it_centers_and_sizes_to_its_content() => StaThread.Run(() =>
    {
        var p = new ColorPickerPopup(new ColorPickerViewModel("#17d4fa"));
        p.PlaceBy(new Button(), [new Rect(0, 0, 1920, 1040)]);
        Assert.Equal(WindowStartupLocation.CenterScreen, p.WindowStartupLocation);
        Assert.True(p.Width >= ColorPickerPopup.SquareWidth + 24 && p.Height > ColorPickerPopup.SquareHeight + 24);
    });

    [Fact] // styled only from theme tokens: every brush is a resource or a binding, except the HSV gradients
    public void Brushes_come_from_the_theme()
    {
        var text = File.ReadAllText(Xaml);
        var literal = Regex.Matches(text, @"\b(Background|Foreground|BorderBrush|Stroke|Fill|CaretBrush|SelectionBrush)=""(?!\{)([^""]*)""")
                           .Select(m => m.Value).Where(v => !v.EndsWith("\"Transparent\"", StringComparison.Ordinal)).ToList();
        Assert.Empty(literal);
    }

    [Theory] // a click on the modal popup's disabled owner arrives as WM_SETCURSOR, HTERROR, button-down
    [InlineData(0x0020, unchecked((int)0x0201FFFE), true)]
    [InlineData(0x0020, unchecked((int)0x0204FFFE), true)]
    [InlineData(0x0020, unchecked((int)0x0207FFFE), true)]
    [InlineData(0x0020, unchecked((int)0x020BFFFE), true)]    // X buttons
    [InlineData(0x0020, unchecked((int)0x0200FFFE), false)]   // a mouse move over it
    [InlineData(0x0020, unchecked((int)0x02010001), false)]   // HTCLIENT: an enabled window's ordinary click
    [InlineData(0x0201, unchecked((int)0x0201FFFE), false)]
    public void A_click_on_the_disabled_owner_commits_and_is_handled_so_windows_does_not_beep(int msg, int lParam, bool expected)
    {
        Assert.Equal(expected, ThemedColorPicker.IsClickOnDisabledOwner(msg, new IntPtr(lParam)));
        int commits = 0;
        bool handled = false;
        var result = ThemedColorPicker.OwnerHook(msg, new IntPtr(lParam), ref handled, () => commits++);
        Assert.Equal((expected ? 1 : 0, expected, new IntPtr(expected ? 1 : 0)), (commits, handled, result));
    }
}
