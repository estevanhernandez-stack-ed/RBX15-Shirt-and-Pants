using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RBX15.App.Controls;

/// <summary>
/// Names the two page buttons inside WPF's stock Slider template ("Decrease" and "Increase") so a screen reader does not meet two
/// unnamed buttons per slider (ledger a11y-focus-rings). The sliders keep the stock template and look; the scrollbars' own page
/// buttons are named in Themes/Controls.xaml. The implicit Slider style in Themes/Controls.xaml switches it on.
/// </summary>
public static class SliderPartNames
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SliderPartNames), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider || e.NewValue is not true) return;
        slider.Loaded += (_, _) => NameParts(slider);   // the template is applied by the time a slider loads
    }

    static void NameParts(Slider slider)
    {
        slider.ApplyTemplate();
        if (slider.Template?.FindName("PART_Track", slider) is not Track track) return;
        Name(track.DecreaseRepeatButton, "Decrease");
        Name(track.IncreaseRepeatButton, "Increase");
    }

    static void Name(RepeatButton? button, string name)
    {
        if (button is not null && string.IsNullOrEmpty(AutomationProperties.GetName(button))) AutomationProperties.SetName(button, name);
    }
}
