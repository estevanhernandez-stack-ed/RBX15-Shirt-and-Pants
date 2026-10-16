using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RBX15.App.Canvas;

namespace RBX15.App.ViewModels;

/// <summary>
/// The Home tab's Tools group (editor.html:41-52; ledger home-tool-buttons, tools-set-tool, tools-toggle-off).
/// <see cref="Current"/> is 4.1's currentTool: Select at launch (editor.js:479). <see cref="Set"/> is setTool
/// (editor.js:473-478); <see cref="ToggleCommand"/> is toggleTool (editor.js:484-486): pressing the active tool
/// again returns to Select, and Select never toggles off. The tool is view state, never an undo step.
/// </summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    public ToolsViewModel()
    {
        ToggleCommand = new RelayCommand<Tool>(Toggle);
        Buttons =
        [
            // editor.html:43-48: label and title, minus the shortcut text until M4 ships the keyboard (R14)
            new(Tool.Select, "Select", "Select/Move", ToggleCommand),
            new(Tool.Pen, "Draw", "Freehand draw", ToggleCommand),
            new(Tool.Line, "Line", "Line tool", ToggleCommand),
            new(Tool.Rect, "Rect", "Rectangle", ToggleCommand),
            new(Tool.Ellipse, "Ellipse", "Ellipse", ToggleCommand),
            new(Tool.Eraser, "Eraser", "Eraser", ToggleCommand),
        ];
        Buttons[0].IsActive = true;
    }

    /// <summary>The active tool (currentTool, editor.js:89, :474).</summary>
    public Tool Current { get; private set; } = Tool.Select;

    /// <summary>The six buttons in editor.html:43-48 order.</summary>
    public IReadOnlyList<ToolButtonViewModel> Buttons { get; }

    /// <summary>toggleTool (editor.js:484-486): the active tool again returns to Select; Select stays Select.</summary>
    public IRelayCommand<Tool> ToggleCommand { get; }

    /// <summary>Raised after <see cref="Current"/> changes. CanvasInteraction cancels a stroke on it (R9).</summary>
    public event EventHandler? CurrentChanged;

    /// <summary>setTool (editor.js:473-478): one active button (.btn-active). Setting the current tool does nothing.</summary>
    public void Set(Tool tool)
    {
        if (!Enum.IsDefined(tool)) throw new ArgumentOutOfRangeException(nameof(tool), tool, "not a canvas tool");
        if (tool == Current) return;
        Current = tool;
        foreach (var button in Buttons) button.IsActive = button.Tool == tool;
        OnPropertyChanged(nameof(Current));
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    void Toggle(Tool tool) => Set(Current == tool && tool != Tool.Select ? Tool.Select : tool);
}

/// <summary>One tool button: 4.1's label and title (R14: no shortcut text before M4), and whether it is .btn-active.</summary>
public sealed partial class ToolButtonViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    internal ToolButtonViewModel(Tool tool, string label, string toolTip, IRelayCommand<Tool> toggle)
    {
        Tool = tool;
        Label = label;
        ToolTip = toolTip;
        Command = new RelayCommand(() => toggle.Execute(tool));
    }

    public Tool Tool { get; }
    public string Label { get; }
    public string ToolTip { get; }

    /// <summary>The group's ToggleCommand with this button's tool (btn.onclick = () => toggleTool(tool), editor.js:488-490).</summary>
    public IRelayCommand Command { get; }
}
