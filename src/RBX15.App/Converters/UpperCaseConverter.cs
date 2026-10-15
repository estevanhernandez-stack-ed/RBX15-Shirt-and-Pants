using System.Globalization;
using System.Windows.Data;

namespace RBX15.App.Converters;

/// <summary>
/// CSS <c>text-transform: uppercase</c>, which 4.1 puts on the header byline, ribbon tabs, group
/// labels, panel headings and the footer (styles.css:153, :180, :215, :258, :686). The view-models
/// keep 4.1's source strings; the view upper-cases them. Invariant culture, as Chromium upper-cases
/// with the document language (en, editor.html:2). One-way only.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? s.ToUpperInvariant() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("UpperCaseConverter is one-way.");
}
