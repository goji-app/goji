using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GodjiSetup;

/// Булев флаг VM → видимость. Других конвертеров установщику не нужно.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
