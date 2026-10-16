using System.ComponentModel;
using System.Windows;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using static RBX15.App.Tests.HomeTestKit;

namespace RBX15.App.Tests;

/// <summary>The zoom pill (zoom-controls, canvas-zoom-controls) and the document the canvas shows.</summary>
public class CanvasViewModelTests
{
    /// <summary>A canvas view-model with its own tool, brush and status strip.</summary>
    internal static CanvasViewModel Vm(EditorSession session, ToolsViewModel? tools = null) =>
        new(session, tools ?? new ToolsViewModel(), new BrushViewModel(new FakeColorPicker()), new FakeStatus());

    [Fact] // editor.js:81 zoom = 1; editor.html:334 "100%"
    public void Starts_at_100_percent_showing_the_session_document()
    {
        var session = new EditorSession();
        var vm = Vm(session);
        Assert.Equal(1, vm.Zoom);
        Assert.Equal("100%", vm.ZoomLabel);
        Assert.Same(session.Document, vm.Document);
        Assert.True(vm.ZoomInCommand.CanExecute(null));
        Assert.True(vm.ZoomOutCommand.CanExecute(null));
    }

    [Fact] // zoom-controls parityCheck: the label for all 12 levels; editor.js:1490-1491
    public void Zoom_steps_by_a_quarter_from_25_to_300_percent_with_4_1_labels()
    {
        var vm = Vm(new EditorSession());
        var labels = new List<string>();
        while (vm.ZoomOutCommand.CanExecute(null)) vm.ZoomOutCommand.Execute(null);
        labels.Add(vm.ZoomLabel);
        while (vm.ZoomInCommand.CanExecute(null))
        {
            vm.ZoomInCommand.Execute(null);
            labels.Add(vm.ZoomLabel);
        }
        Assert.Equal(new[] { "25%", "50%", "75%", "100%", "125%", "150%", "175%", "200%", "225%", "250%", "275%", "300%" }, labels);
        Assert.Equal(CanvasViewModel.MaxZoom, vm.Zoom);
    }

    [Fact] // divergence zoom-pill-limits: a click that would clamp to a no-op is disabled instead
    public void Each_button_is_disabled_at_its_limit_and_comes_back()
    {
        var vm = Vm(new EditorSession());
        int inChanged = 0;
        vm.ZoomInCommand.CanExecuteChanged += (_, _) => inChanged++;
        for (int i = 0; i < 8; i++) vm.ZoomInCommand.Execute(null);
        Assert.Equal(3, vm.Zoom);
        Assert.False(vm.ZoomInCommand.CanExecute(null));

        vm.ZoomInCommand.Execute(null); // a programmatic Execute past the limit still clamps (editor.js:1490)
        Assert.Equal(3, vm.Zoom);

        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(2.75, vm.Zoom);
        Assert.True(vm.ZoomInCommand.CanExecute(null));
        Assert.True(inChanged > 0);

        for (int i = 0; i < 20; i++) vm.ZoomOutCommand.Execute(null);
        Assert.Equal(CanvasViewModel.MinZoom, vm.Zoom);
        Assert.False(vm.ZoomOutCommand.CanExecute(null));
    }

    [Fact] // zoom-controls: not in undo, not in the document
    public void Zoom_never_touches_the_session()
    {
        var session = new EditorSession();
        int changes = 0;
        session.Changed += (_, _) => changes++;
        var vm = Vm(session);
        var before = session.Document;
        vm.ZoomInCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0, changes);
        Assert.Same(before, session.Document);
    }

    [Fact] // the host re-renders on Document (render-pipeline-canvas-reset); undo and redo raise Document too
    public void Document_follows_every_session_change_that_touches_it()
    {
        var session = new EditorSession();
        var vm = Vm(session);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.True(session.Apply(d => d with { ShowGrid = true }));
        Assert.Same(session.Document, vm.Document);
        Assert.True(vm.Document.ShowGrid);
        Assert.Equal(new[] { nameof(CanvasViewModel.Document) }, raised);

        raised.Clear();
        session.TemplateTitle = "Drip set"; // a Title-only change leaves the canvas alone
        Assert.Empty(raised);
    }

    [Fact] // drop-canvas: Task 4 sets DropHandler to ImportService.Import
    public void Drop_runs_the_handler_and_is_disabled_without_one()
    {
        var vm = Vm(new EditorSession());
        var drop = new CanvasDrop(new[] { @"C:\art\a.png", @"C:\art\b.png" }, new Point(120, 90));
        Assert.False(vm.DropCommand.CanExecute(drop));

        int changed = 0;
        vm.DropCommand.CanExecuteChanged += (_, _) => changed++;
        CanvasDrop? got = null;
        vm.DropHandler = d => got = d;
        Assert.Equal(1, changed);
        Assert.True(vm.DropCommand.CanExecute(drop));
        Assert.False(vm.DropCommand.CanExecute(null));

        vm.DropCommand.Execute(drop);
        Assert.Same(drop, got);
    }

    [Fact] // editor.js:362: the overlay only in the Select tool; the live preview while a stroke runs (render-live-preview)
    public void The_overlay_shows_the_selection_only_in_select_and_carries_the_live_preview()
    {
        var session = SessionWith(LayerOf(1) with { X = 100, Y = 100 });
        var tools = new ToolsViewModel();
        var vm = Vm(session, tools);
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        session.Select(1);
        Assert.Equal(new ViewOverlay(1, null), vm.Overlay);
        tools.Set(Tool.Pen);
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        vm.Interaction.PointerDown(new Point(10, 10), 1);
        vm.Interaction.PointerMove(new Point(30, 10), 1);
        Assert.Same(vm.Interaction.Preview, vm.Overlay.Preview);
        Assert.NotNull(vm.Overlay.Preview);
        Assert.Equal(CanvasCursor.Crosshair, vm.Cursor);
        tools.Set(Tool.Eraser);                                  // R9 discards the stroke
        Assert.Equal(ViewOverlay.None, vm.Overlay);
        Assert.Equal(CanvasCursor.Ring(4), vm.Cursor);
    }

    [Fact] // canvas automation peer: Value is the selected layer's name, or ""
    public void AutomationValue_is_the_selected_layers_name()
    {
        var session = SessionWith(LayerOf(1) with { Name = "Taft" });
        var vm = Vm(session);
        Assert.Equal("", vm.AutomationValue);
        session.Select(1);
        Assert.Equal("Taft", vm.AutomationValue);
        session.Apply(d => d with { Layers = d.Layers.SetItem(0, d.Layers[0] with { Name = "Spider Based Bruh" }) });
        Assert.Equal("Spider Based Bruh", vm.AutomationValue);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        var session = new EditorSession();
        var tools = new ToolsViewModel();
        var brush = new BrushViewModel(new FakeColorPicker());
        var status = new FakeStatus();
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(null!, tools, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, null!, brush, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, tools, null!, status));
        Assert.Throws<ArgumentNullException>(() => new CanvasViewModel(session, tools, brush, null!));
    }
}
