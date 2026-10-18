using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Details tab pane (Settings, Neckline, Edges, Seams, Accents). The tab shows once <see cref="HasGroups"/> is true.</summary>
public partial class DetailsRibbon : UserControl, IRibbonPane
{
    public DetailsRibbon() => InitializeComponent();

    public bool HasGroups => Groups.Children.Count > 0;
}
