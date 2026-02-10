using System;
using System.Globalization;
using System.Windows.Data;
using Shared.Utils;

namespace DisplayApp.Converters
{
    /// <summary>
    /// Converts a resource key (ConverterParameter) to the localized string.
    /// Used with Binding to CurrentLanguage so the UI updates when language changes.
    /// </summary>
    public class ResKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = parameter as string;
            return string.IsNullOrEmpty(key) ? string.Empty : ResourceManager.GetString(key, "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
