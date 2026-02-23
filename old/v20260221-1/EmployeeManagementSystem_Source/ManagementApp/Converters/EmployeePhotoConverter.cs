using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media.TextFormatting;
using Shared.Models;

namespace ManagementApp.Converters
{
    public class EmployeePhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Employee employee)
            {
                // Inline the extension method logic to avoid XAML parser issues
                if (employee.HasPhoto())
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(employee.PhotoPath, UriKind.Absolute);
                        bitmap.DecodePixelWidth = 40;
                        bitmap.DecodePixelHeight = 40;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                    catch
                    {
                        // Fall through to placeholder
                    }
                }
                
                // Return placeholder
                return CreatePlaceholderImage(employee, 40);
            }
            
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        
        private ImageSource CreatePlaceholderImage(Employee employee, int size)
        {
            // Create a simple placeholder bitmap
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, size, size));
                var text = string.IsNullOrEmpty(employee?.FirstName) ? "?" : employee.FirstName[0].ToString();
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    size * 0.4,
                    Brushes.DarkGray,
                    1.0); // Use default DPI scaling factor
                drawingContext.DrawText(formattedText, new Point(size * 0.3, size * 0.3));
            }
            
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }
    }
}

