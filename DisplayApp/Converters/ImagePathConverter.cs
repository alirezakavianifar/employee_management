using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DisplayApp.Converters
{
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string photoPath && !string.IsNullOrEmpty(photoPath))
            {
                try
                {
                    if (System.IO.File.Exists(photoPath))
                    {
                        return new BitmapImage(new Uri(photoPath, UriKind.Absolute));
                    }
                }
                catch
                {
                    // Fall through to placeholder
                }
            }
            
            // Return placeholder image
            return CreatePlaceholderImage();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private BitmapImage CreatePlaceholderImage()
        {
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(System.Windows.Media.Brushes.DarkGray, null, new System.Windows.Rect(0, 0, 35, 35));
                drawingContext.DrawText(
                    new System.Windows.Media.FormattedText("Worker", 
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Tahoma"),
                        6,
                        System.Windows.Media.Brushes.White,
                        96),
                    new System.Windows.Point(6, 12));
            }
            
            var renderTargetBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(35, 35, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();
            
            return ConvertToBitmapImage(renderTargetBitmap);
        }

        private BitmapImage ConvertToBitmapImage(System.Windows.Media.Imaging.RenderTargetBitmap renderTarget)
        {
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTarget));
            
            using var stream = new System.IO.MemoryStream();
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
