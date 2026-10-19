using RBX15.App.Canvas;
using RBX15.App.ViewModels;

namespace RBX15.App.Tests;

/// <summary>The Tools group's model: setTool and toggleTool (editor.js:464-490; tools-set-tool, tools-toggle-off, home-tool-buttons).</summary>
public class ToolsViewModelTests
{
    [Fact] // editor.html:43-48 order and labels; titles carry the shortcut letter (M4b item 10); editor.js:479 starts on Select
    public void Starts_on_select_with_six_buttons_in_4_1_order_with_their_shortcut_letters()
    {
        var vm = new ToolsViewModel();
        Assert.Equal(Tool.Select, vm.Current);
        Assert.Equal(new[] { Tool.Select, Tool.Pen, Tool.Line, Tool.Rect, Tool.Ellipse, Tool.Eraser }, vm.Buttons.Select(b => b.Tool));
        Assert.Equal(new[] { "Select", "Draw", "Line", "Rect", "Ellipse", "Eraser" }, vm.Buttons.Select(b => b.Label));
        Assert.Equal(new[] { "Select/Move (V)", "Freehand draw (B)", "Line tool (L)", "Rectangle (U)", "Ellipse (O)", "Eraser (E)" }, vm.Buttons.Select(b => b.ToolTip));
        Assert.Equal(new[] { true, false, false, false, false, false }, vm.Buttons.Select(b => b.IsActive));
    }

    [Fact] // tools-set-tool: one .btn-active at a time; setting the current tool raises nothing
    public void Set_makes_exactly_one_button_active_and_raises_once_per_change()
    {
        var vm = new ToolsViewModel();
        int changed = 0;
        vm.CurrentChanged += (_, _) => changed++;
        var names = HomeTestKit.Track(vm);
        vm.Set(Tool.Eraser);
        Assert.Equal(Tool.Eraser, vm.Current);
        Assert.Equal(new[] { false, false, false, false, false, true }, vm.Buttons.Select(b => b.IsActive));
        Assert.Equal(1, changed);
        Assert.Equal(new[] { nameof(ToolsViewModel.Current) }, names);
        vm.Set(Tool.Eraser);
        Assert.Equal(1, changed);
    }

    [Fact] // tools-toggle-off parityCheck: B twice gives pen then select; V twice stays select (editor.js:484-486)
    public void Pressing_the_active_tool_again_returns_to_select_and_select_never_toggles_off()
    {
        var vm = new ToolsViewModel();
        var draw = vm.Buttons.Single(b => b.Tool == Tool.Pen);
        draw.Command.Execute(null);
        Assert.Equal(Tool.Pen, vm.Current);
        draw.Command.Execute(null);
        Assert.Equal(Tool.Select, vm.Current);
        vm.ToggleCommand.Execute(Tool.Select);
        vm.ToggleCommand.Execute(Tool.Select);
        Assert.Equal(Tool.Select, vm.Current);
        vm.ToggleCommand.Execute(Tool.Rect);
        vm.ToggleCommand.Execute(Tool.Line);                     // another tool switches, it does not toggle off
        Assert.Equal(Tool.Line, vm.Current);
    }

    [Fact]
    public void An_unknown_tool_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolsViewModel().Set((Tool)42));
}
