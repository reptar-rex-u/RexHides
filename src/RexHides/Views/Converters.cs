using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RexHides.Views;

public static class Converters
{
    public static readonly IValueConverter NullToCollapsed = new NullToCollapsedConverter();
    public static readonly IValueConverter BoolToVisibility = new BooleanToVisibilityConverter();

    private sealed class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is null or "" ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
