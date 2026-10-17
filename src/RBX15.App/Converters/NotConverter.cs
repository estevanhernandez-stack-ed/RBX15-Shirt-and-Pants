using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>Boolean negation for bindings (M4a: the window's input is disabled while an Open reads, interfaces R4).</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class NotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
