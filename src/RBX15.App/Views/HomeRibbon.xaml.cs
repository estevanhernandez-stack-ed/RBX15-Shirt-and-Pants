using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Home tab pane. DataContext: <see cref="ViewModels.HomeViewModel"/>. Home always has content.</summary>
public partial class HomeRibbon : UserControl, IRibbonPane
{
    public HomeRibbon() => InitializeComponent();

    public bool HasGroups => true;
}
