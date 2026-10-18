using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.App.Views;

namespace RBX15.App.Tests;

/// <summary>The v5.1 3D preview column and its "3D preview" Settings box (ledger avatar-preview-3d, new).</summary>
[Collection("MainWindow")]
public class ShellPreviewTests
{
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    static MainWindow Shell(EditorSession session, double width, double height = 761)
    {
        var w = new MainWindow(TestShell.Main(session, previewVisible: true), new FakeSettingsStore(), [new Rect(0, 0, 1920, 1040)]);
        StaThread.Pump();
        Lay(w, width, height);
        return w;
    }

    static void Lay(MainWindow w, double width, double height = 761)
    {
        w.ApplyColumnWidths(width);
        var root = (FrameworkElement)w.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    static ColumnDefinition PreviewColumn(MainWindow w) => (ColumnDefinition)w.FindName("PreviewColumn");

    static CheckBox PreviewBox(MainWindow w) => Descendants<CheckBox>((DependencyObject)w.Content)
        .Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "3D preview");

    [Fact] // done-when 1: the default window shows the panel right of the canvas, 260 wide, 200 at the breakpoint
    public void The_preview_column_holds_the_panel_right_of_the_canvas_at_260_and_200() => StaThread.Run(() =>
    {
        var w = Shell(new EditorSession(), 1184);
        var panel = Descendants<AvatarPreviewPanel>((DependencyObject)w.Content).Single();
        var canvasArea = (FrameworkElement)w.FindName("CanvasArea");
        var host = (FrameworkElement)w.FindName("PreviewPanelHost");
        Assert.Equal(ShellLayout.PreviewWidth, PreviewColumn(w).Width.Value);
        Assert.Equal(260, ShellLayout.PreviewWidth);
        Assert.Equal(Visibility.Visible, host.Visibility);
        Assert.True(panel.IsDescendantOf(host));
        Assert.Same(w.ViewModel.AvatarPreview, panel.DataContext);
        Assert.Equal(2, Grid.GetColumn(host));
        Assert.True(Grid.GetColumn(host) > Grid.GetColumn(canvasArea));

        Lay(w, ShellLayout.Breakpoint);
        Assert.Equal(ShellLayout.NarrowPreviewWidth, PreviewColumn(w).Width.Value);
        Assert.Equal(200, ShellLayout.NarrowPreviewWidth);
        Lay(w, ShellLayout.Breakpoint + 1);
        Assert.Equal(ShellLayout.PreviewWidth, PreviewColumn(w).Width.Value);
    });

    [Fact] // the panel is styled like the left panel: 1px Border1 on its left
    public void The_preview_host_has_a_left_border_and_the_left_panel_look() => StaThread.Run(() =>
    {
        var w = Shell(new EditorSession(), 1184);
        var host = (Border)w.FindName("PreviewPanelHost");
        var left = (Border)w.FindName("LeftPanel");
        Assert.Equal(new Thickness(1, 0, 0, 0), host.BorderThickness);
        Assert.Same(left.BorderBrush, host.BorderBrush);
        Assert.IsType<LinearGradientBrush>(host.Background);
        Assert.Equal(14, ((FrameworkElement)w.FindName("PreviewPanel")).Margin.Left);
    });

    [Fact] // done-when 2: the box is on by default; unchecking collapses the column and hides the VM; rechecking restores both
    public void The_3D_preview_box_collapses_and_restores_the_column_and_the_view_model() => StaThread.Run(() =>
    {
        var w = Shell(new EditorSession(), 1184);
        var box = PreviewBox(w);
        var host = (FrameworkElement)w.FindName("PreviewPanelHost");
        Assert.True(box.IsChecked);
        Assert.True(w.ViewModel.AvatarPreview.IsVisible);

        box.IsChecked = false;
        Lay(w, 1184);
        Assert.False(w.ViewModel.AvatarPreview.IsVisible);
        Assert.Equal(0, PreviewColumn(w).Width.Value);
        Assert.Equal(Visibility.Collapsed, host.Visibility);

        box.IsChecked = true;
        Lay(w, 1184);
        Assert.True(w.ViewModel.AvatarPreview.IsVisible);
        Assert.Equal(ShellLayout.PreviewWidth, PreviewColumn(w).Width.Value);
        Assert.Equal(Visibility.Visible, host.Visibility);

        // A hidden column stays hidden across a resize through the breakpoint.
        box.IsChecked = false;
        Lay(w, ShellLayout.Breakpoint);
        Assert.Equal(0, PreviewColumn(w).Width.Value);
    });

    [Fact] // done-when 3: no undo step, no document change, not dirty; the VM change drives the box back too
    public void Toggling_the_preview_is_session_state_only() => StaThread.Run(() =>
    {
        var session = new EditorSession();
        var w = Shell(session, 1184);
        var before = session.Document;
        var undo = session.UndoCount;
        var box = PreviewBox(w);

        box.IsChecked = false;
        box.IsChecked = true;
        box.IsChecked = false;

        Assert.Equal(undo, session.UndoCount);
        Assert.Same(before, session.Document);
        Assert.False(session.IsDirty);

        w.ViewModel.AvatarPreview.IsVisible = true;
        Assert.True(box.IsChecked);
    });

    [Fact]
    public void The_settings_group_exposes_the_toggle_on_by_default()
    {
        var main = TestShell.Main(new EditorSession(), previewVisible: true);
        Assert.True(main.Home.Settings.ShowPreview);
        main.Home.Settings.ShowPreview = false;
        Assert.False(main.AvatarPreview.IsVisible);
    }
}
