using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>
/// Binds a RadioButton's IsChecked to "this enum property equals the parameter" (the ribbon tabs bind two-way to
/// <c>MainViewModel.ActiveTab</c>). Checking writes the parameter's value back; unchecking writes nothing, because the
/// checked sibling has already set the new value.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null && targetType.IsEnum
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}
