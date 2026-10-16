using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>The Brush group's model (editor.html:53-62, editor.js:90-92, :492-494; draw-brush-controls, home-brush-group, tools-draw-props).</summary>
public class BrushViewModelTests
{
    [Fact] // draw-brush-controls parityCheck: defaults on first launch equal 4.1's
    public void Defaults_are_4_1s()
    {
        var vm = new BrushViewModel(new FakeColorPicker());
        Assert.Equal(("#17d4fa", 4, false), (vm.Color, vm.Size, vm.Fill));
        Assert.Equal(new SKColor(0x17, 0xd4, 0xfa), vm.SkColor);
    }

    [Fact] // draw-brush-controls: range 1..50 enforced; wheel-brush-size: +1 / -1 per event
    public void Size_is_clamped_to_1_through_50_and_steps_by_one()
    {
        var vm = new BrushViewModel(new FakeColorPicker());
        vm.Size = 0;
        Assert.Equal(1, vm.Size);
        vm.Size = 51;
        Assert.Equal(50, vm.Size);
        vm.StepSize(+1);
        Assert.Equal(50, vm.Size);
        vm.Size = 4;
        vm.StepSize(-1);
        Assert.Equal(3, vm.Size);
        vm.StepSize(0);                                          // sign 0 counts as up, as deltaY 0 does (editor.js:1316)
        Assert.Equal(4, vm.Size);
    }

    [Fact] // tools-draw-props: drawColor follows every input event; the themed picker previews live; cancel restores
    public void The_picker_previews_live_stores_lowercase_and_cancel_restores()
    {
        var picker = new FakeColorPicker().Answer("#FF0000", "#00FF00", "#0000ff").Answer(null, "#123456");
        var vm = new BrushViewModel(picker);
        var seen = new List<string>();
        picker.DuringPick = () => seen.Add(vm.Color);
        var names = HomeTestKit.Track(vm);
        var anchor = new object();

        vm.PickColorCommand.Execute(anchor);
        Assert.Equal("#ff0000", vm.Color);
        Assert.Equal(new[] { "#0000ff" }, seen);                 // the last preview, while the picker was open
        Assert.Equal(("#17d4fa", anchor), (picker.Seeds[0], picker.Anchors[0]));
        Assert.Contains(nameof(BrushViewModel.SkColor), names);

        vm.PickColorCommand.Execute(null);
        Assert.Equal("#ff0000", vm.Color);                       // cancelled after a preview: the seed again
        Assert.Equal("#ff0000", picker.Seeds[1]);
    }

    [Theory]
    [InlineData("17d4fa")]
    [InlineData("#17d4f")]
    [InlineData("#17d4fz")]
    public void A_color_that_is_not_hex_is_refused(string hex) =>
        Assert.Throws<ArgumentException>(() => new BrushViewModel(new FakeColorPicker()).Color = hex);
}
