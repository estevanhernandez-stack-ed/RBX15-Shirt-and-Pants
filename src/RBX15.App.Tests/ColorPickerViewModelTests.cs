using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>The themed color picker's state: preview on every color change, keys, the hex field, commit and cancel.</summary>
public class ColorPickerViewModelTests
{
    [Fact]
    public void Opens_on_the_seed_without_previewing()
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#17D4FA", seen.Add);
        Assert.Equal(("#17d4fa", "#17d4fa", "#17d4fa"), (vm.OriginalHex, vm.Hex, vm.HexText));
        Assert.Equal(0.98, vm.Value, 2);
        Assert.Empty(seen);
        Assert.False(vm.IsFinished);
        Assert.Throws<ArgumentException>(() => new ColorPickerViewModel("17d4fa"));
    }

    [Fact]
    public void Every_color_change_previews_once_and_a_no_op_does_not()
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#ff0000", seen.Add);
        vm.Hue = 120;                       // green
        vm.MoveSv(0, 1);                    // already at full value: clamped, nothing changes
        vm.SetSvFromPoint(0, 0);            // top-left: white
        vm.SetHueFromPoint(0.5);            // a white is still white at any hue
        Assert.Equal(new[] { "#00ff00", "#ffffff" }, seen);
        Assert.Equal(180, vm.Hue);
        Assert.Equal("#ffffff", vm.HexText);
    }

    [Fact]
    public void Arrow_steps_move_the_point_and_the_hue_and_clamp()
    {
        var vm = new ColorPickerViewModel("#808080");
        vm.MoveSv(ColorPickerViewModel.SvBigStep, -ColorPickerViewModel.SvStep);
        Assert.Equal(0.1, vm.Saturation, 9);
        Assert.Equal(128 / 255.0 - 0.01, vm.Value, 9);
        vm.MoveHue(-ColorPickerViewModel.HueBigStep);
        Assert.Equal(0, vm.Hue);
        vm.Hue = 355;
        vm.MoveHue(ColorPickerViewModel.HueBigStep);
        Assert.Equal(360, vm.Hue);
        vm.MoveSv(5, 5);
        Assert.Equal((1.0, 1.0), (vm.Saturation, vm.Value));
        Assert.Equal("#ff0000", vm.Hex);   // hue 360 is red
    }

    [Fact]
    public void The_hex_field_applies_valid_text_and_ignores_the_rest()
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#17d4fa", seen.Add);
        vm.HexText = "#12";                 // still typing: ignored, kept as typed
        Assert.Equal(("#17d4fa", "#12"), (vm.Hex, vm.HexText));
        vm.HexText = "#ABC";
        Assert.Equal(("#aabbcc", "#ABC"), (vm.Hex, vm.HexText));
        vm.HexText = "nope";
        vm.RevertHexText();
        Assert.Equal(("#aabbcc", "#aabbcc"), (vm.Hex, vm.HexText));
        Assert.Equal(new[] { "#aabbcc" }, seen);
    }

    [Fact]
    public void A_gray_or_black_typed_in_keeps_the_hue_and_black_keeps_the_saturation()
    {
        var vm = new ColorPickerViewModel("#00ff00");   // hue 120
        vm.HexText = "#777777";
        Assert.Equal((120.0, 0.0), (vm.Hue, vm.Saturation));
        vm.HexText = "#00ff00";
        vm.HexText = "#000";
        Assert.Equal((120.0, 1.0, 0.0), (vm.Hue, vm.Saturation, vm.Value));
        vm.MoveSv(0, 1);
        Assert.Equal("#00ff00", vm.Hex);
    }

    [Fact]
    public void Commit_returns_the_color_and_takes_pending_valid_text()
    {
        var vm = new ColorPickerViewModel("#17d4fa");
        vm.Hue = 0;
        vm.Commit();
        Assert.True(vm.IsFinished);
        Assert.Equal(vm.Hex, vm.Result);

        var typed = new ColorPickerViewModel("#17d4fa");
        typed.HexText = "#0f0";
        typed.Commit();
        Assert.Equal("#00ff00", typed.Result);
    }

    [Fact]
    public void Cancel_returns_null_and_the_first_end_wins()
    {
        var seen = new List<string>();
        var vm = new ColorPickerViewModel("#17d4fa", seen.Add);
        vm.Hue = 0;
        vm.Cancel();
        vm.Commit();                        // a Deactivated after Esc must not turn the cancel into a commit
        vm.Hue = 200;                       // finished: no more previews
        Assert.Null(vm.Result);
        Assert.Single(seen);
    }
}
