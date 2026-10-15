using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using RBX15.App.ViewModels;

namespace RBX15.App.Controls;

/// <summary>
/// The color picker's saturation/value square. A Grid draws nothing to UI Automation, so this one brings
/// a peer: a read-only Value pattern that reads "saturation N%, brightness M%" from the
/// <see cref="ColorPickerViewModel"/> in its DataContext (spec §4: every control has an automation name).
/// The arrow keys change it (<see cref="Views.ColorPickerPopup"/>).
/// </summary>
public sealed class SaturationValueSurface : Grid
{
    /// <summary>The spoken value for a saturation and value (0 to 1 each), as whole percents.</summary>
    public static string Describe(double saturation, double value) => string.Create(CultureInfo.InvariantCulture,
        $"saturation {Math.Round(saturation * 100, MidpointRounding.AwayFromZero)}%, brightness {Math.Round(value * 100, MidpointRounding.AwayFromZero)}%");

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    internal sealed class Peer(SaturationValueSurface owner) : FrameworkElementAutomationPeer(owner), IValueProvider
    {
        ColorPickerViewModel? Model => ((SaturationValueSurface)Owner).DataContext as ColorPickerViewModel;

        protected override string GetClassNameCore() => nameof(SaturationValueSurface);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override string GetLocalizedControlTypeCore() => "color area";

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => true;

        public string Value => Model is { } m ? Describe(m.Saturation, m.Value) : "";

        public void SetValue(string value) => throw new ElementNotEnabledException("The color area is set with the arrow keys or the pointer.");
    }
}

/// <summary>
/// The color picker's hue strip, with a peer that exposes the RangeValue pattern in degrees (0 to 360,
/// small change 1, large change 10) over the <see cref="ColorPickerViewModel"/> in its DataContext, so a
/// screen reader can read and set the hue.
/// </summary>
public sealed class HueSurface : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    internal sealed class Peer(HueSurface owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        ColorPickerViewModel? Model => ((HueSurface)Owner).DataContext as ColorPickerViewModel;

        protected override string GetClassNameCore() => nameof(HueSurface);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public double Value => Model?.Hue ?? 0;

        public bool IsReadOnly => Model is null || Model.IsFinished;

        public double Maximum => 360;

        public double Minimum => 0;

        public double LargeChange => ColorPickerViewModel.HueBigStep;

        public double SmallChange => ColorPickerViewModel.HueStep;

        public void SetValue(double value)
        {
            if (IsReadOnly) throw new ElementNotEnabledException();
            if (double.IsNaN(value) || value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Model!.Hue = value;
        }
    }
}
