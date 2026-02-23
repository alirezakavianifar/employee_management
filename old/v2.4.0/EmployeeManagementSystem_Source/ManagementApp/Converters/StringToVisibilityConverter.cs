using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagementApp.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? strValue = value as string;
            bool isVisible = !string.IsNullOrEmpty(strValue);

            // Check for inverse parameter
            if (parameter != null && parameter.ToString()?.ToLower() == "inverse")
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
