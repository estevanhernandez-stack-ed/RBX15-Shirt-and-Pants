using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The canvas area: the scrolled, centered template frame and the zoom pill. DataContext is a CanvasViewModel.</summary>
public partial class CanvasView : UserControl
{
    public CanvasView()
    {
        InitializeComponent();
    }
}
