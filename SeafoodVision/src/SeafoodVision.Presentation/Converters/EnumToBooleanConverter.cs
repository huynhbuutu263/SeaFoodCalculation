using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SeafoodVision.Presentation.Converters;

/// <summary>
/// Converts an Enum to a boolean for RadioButton bindings.
/// Supply the matching enum name in the ConverterParameter string.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter == null || value == null)
            return false;

        string expectedStr = parameter.ToString()!;
        string actualStr = value.ToString()!;

        return expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }

        // Return Binding.DoNothing ensures standard two-way behavior without wiping state
        return DependencyProperty.UnsetValue;
    }
}
