using System.Windows;
using System.Windows.Controls;
using RBX15.App.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The drop zone. A file drag highlights it (the .dragover class, editor.js:955-956); a drop hands
/// the paths to <see cref="LayersViewModel.DropFiles"/> at the default placement (drop-panel). A drag
/// that carries no files shows the no-drop cursor and does nothing.
/// </summary>
public partial class DropZone : UserControl
{
    public DropZone() => InitializeComponent();

    void OnDragOver(object sender, DragEventArgs e)
    {
        bool files = TemplateCanvasHost.Files(e.Data) is not null;
        e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
        Zone.Tag = files ? "dragover" : null;
        e.Handled = true;
    }

    void OnDragLeave(object sender, DragEventArgs e) => Zone.Tag = null;

    void OnDrop(object sender, DragEventArgs e)
    {
        Zone.Tag = null;
        e.Handled = true;
        if (TemplateCanvasHost.Files(e.Data) is { } paths && DataContext is LayersViewModel vm) vm.DropFiles(paths);
    }
}
