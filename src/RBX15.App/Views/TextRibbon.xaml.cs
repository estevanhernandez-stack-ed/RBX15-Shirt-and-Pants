using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Text tab pane (empty until its groups arrive). The tab shows once <see cref="HasGroups"/> is true.</summary>
public partial class TextRibbon : UserControl, IRibbonPane
{
    public TextRibbon() => InitializeComponent();

    public bool HasGroups => Groups.Children.Count > 0;
}
