using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>
/// True when the two bound values are equal (<see cref="object.Equals(object, object)"/>). The asset library's
/// subfolder buttons use it to highlight the button whose group is on show (btn.title === folder, editor.js:2464).
/// One-way only.
/// </summary>
public sealed class EqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is { Length: 2 } && Equals(values[0], values[1]);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("EqualsMultiConverter is one-way.");
}
