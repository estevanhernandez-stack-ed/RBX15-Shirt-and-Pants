using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RBX15.App.ViewModels;

namespace RBX15.App.Views;

/// <summary>
/// The Text tab pane (Text, Colors &amp; Size, Style, Preview). The tab shows once <see cref="HasGroups"/> is true. The code-behind does
/// two view jobs: it tells the view-model the display's scale so the preview is drawn at device pixels (ruling T2), and it
/// commits the size box on lost focus and Enter.
/// </summary>
public partial class TextRibbon : UserControl, IRibbonPane
{
    public TextRibbon()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncScale();
        Loaded += (_, _) => SyncScale();
        SizeBox.LostFocus += (_, _) => (DataContext as TextViewModel)?.CommitSize();
        SizeBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            (DataContext as TextViewModel)?.CommitSize();
            e.Handled = true;
        };
    }

    public bool HasGroups => Groups.Children.Count > 0;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        SyncScale();
    }

    void SyncScale()
    {
        if (DataContext is TextViewModel vm) vm.DeviceScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
    }
}
