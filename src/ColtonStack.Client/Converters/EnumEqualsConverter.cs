using System.Globalization;
using System.Windows.Data;

namespace ColtonStack.Client.Converters;

/// <summary>
/// Two-way "is this enum value selected?" converter for RadioButton-style bindings:
/// Convert compares the bound enum to the ConverterParameter; ConvertBack writes the
/// parameter value when the button becomes checked.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string name
            ? Enum.Parse(targetType, name)
            : Binding.DoNothing;
}
