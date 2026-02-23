using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using Shared.Models;

namespace ManagementApp.Extensions
{
    public static class EmployeeExtensions
    {
        public static BitmapImage GetPhotoBitmap(this Employee employee, int size = 400)
        {
            if (employee.HasPhoto())
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(employee.PhotoPath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = size;
                    bitmap.DecodePixelHeight = size;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    // Fall back to placeholder
                }
            }

            return CreatePlaceholderBitmap(employee, size);
        }

        public static ImageSource GetPhotoImageSource(this Employee employee, int size = 400)
        {
            return employee.GetPhotoBitmap(size);
        }

        private static BitmapImage CreatePlaceholderBitmap(Employee employee, int size)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Background
                drawingContext.DrawRectangle(Brushes.LightGray, null, new System.Windows.Rect(0, 0, size, size));
                
                // Text
                var formattedText = new FormattedText(
                    $"{employee.FirstName}\n{employee.LastName}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.RightToLeft,
                    new Typeface("Tahoma"),
                    12,
                    Brushes.DarkGray,
                    96);

                var textRect = new System.Windows.Rect(0, 0, size, size);
                drawingContext.DrawText(formattedText, new System.Windows.Point(
                    (size - formattedText.Width) / 2,
                    (size - formattedText.Height) / 2));
            }

            var renderTargetBitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();

            return ConvertToBitmapImage(renderTargetBitmap);
        }

        private static BitmapImage ConvertToBitmapImage(RenderTargetBitmap renderTarget)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
    }
}
