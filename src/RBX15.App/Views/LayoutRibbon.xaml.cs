using System.Windows.Controls;

namespace RBX15.App.Views;

/// <summary>The Layout tab pane, shown as "Background" (Presets, Background, BG Settings, Fill Style, Template Regions). The tab shows once <see cref="HasGroups"/> is true.</summary>
public partial class LayoutRibbon : UserControl, IRibbonPane
{
    public LayoutRibbon() => InitializeComponent();

    public bool HasGroups => Groups.Children.Count > 0;
}
