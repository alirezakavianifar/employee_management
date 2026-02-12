using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Utils;

namespace ManagementApp.Services
{
    public class BadgeGeneratorService
    {
        private readonly ILogger<BadgeGeneratorService> _logger;

        public BadgeGeneratorService()
        {
            _logger = LoggingService.CreateLogger<BadgeGeneratorService>();
        }

        /// <summary>
        /// Generates a badge by compositing employee photo and name into a badge template
        /// </summary>
        /// <param name="templatePath">Path to the badge template image</param>
        /// <param name="employeePhotoPath">Path to the employee photo</param>
        /// <param name="employeeName">Full name of the employee</param>
        /// <param name="outputPath">Path where the generated badge should be saved</param>
        /// <returns>True if badge was generated successfully, false otherwise</returns>
        public bool GenerateBadge(string templatePath, string employeePhotoPath, string employeeName, string outputPath)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    _logger.LogError("Badge template not found: {TemplatePath}", templatePath);
                    return false;
                }

                if (string.IsNullOrEmpty(employeePhotoPath) || !File.Exists(employeePhotoPath))
                {
                    _logger.LogError("Employee photo not found: {PhotoPath}", employeePhotoPath);
                    return false;
                }

                if (string.IsNullOrEmpty(employeeName))
                {
                    _logger.LogError("Employee name is required");
                    return false;
                }

                // Load template image
                var templateBitmap = new BitmapImage();
                templateBitmap.BeginInit();
                templateBitmap.UriSource = new Uri(templatePath, UriKind.Absolute);
                templateBitmap.CacheOption = BitmapCacheOption.OnLoad;
                templateBitmap.EndInit();
                templateBitmap.Freeze();

                var templateWidth = templateBitmap.PixelWidth;
                var templateHeight = templateBitmap.PixelHeight;

                _logger.LogInformation("Loaded badge template: {Width}x{Height}", templateWidth, templateHeight);

                // Calculate rectangle positions based on template dimensions
                // Large rectangle: top 2/3 of the template
                // Small rectangle: bottom 1/3 of the template
                // Shield icon overlaps the boundary, so we need to leave space for it
                var largeRectTop = 0;
                var largeRectHeight = (int)(templateHeight * 2.0 / 3.0);
                var smallRectTop = largeRectHeight;
                var smallRectHeight = templateHeight - largeRectHeight;

                // Add some padding/margin from edges (assuming 5% margin)
                var marginX = (int)(templateWidth * 0.05);
                var marginY = (int)(templateHeight * 0.05);
                
                // Shield typically overlaps ~10-15% of the boundary area
                // Adjust photo and name rectangles to leave space for shield
                var shieldOverlap = (int)(templateHeight * 0.12); // ~12% overlap for shield
                
                var largeRectLeft = marginX;
                var largeRectWidth = templateWidth - (2 * marginX);
                // Reduce photo height to leave space for shield at bottom
                var largeRectActualHeight = largeRectHeight - marginY - shieldOverlap;

                var smallRectLeft = marginX;
                var smallRectWidth = templateWidth - (2 * marginX);
                // Reduce name height to leave space for shield at top
                var smallRectActualHeight = smallRectHeight - marginY - shieldOverlap;

                // Load employee photo
                var photoBitmap = new BitmapImage();
                photoBitmap.BeginInit();
                photoBitmap.UriSource = new Uri(employeePhotoPath, UriKind.Absolute);
                photoBitmap.CacheOption = BitmapCacheOption.OnLoad;
                photoBitmap.EndInit();
                photoBitmap.Freeze();

                _logger.LogInformation("Loaded employee photo: {Width}x{Height}", photoBitmap.PixelWidth, photoBitmap.PixelHeight);

                // Create drawing visual for compositing
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Draw template as background
                    drawingContext.DrawImage(templateBitmap, new System.Windows.Rect(0, 0, templateWidth, templateHeight));

                    // Center crop and resize employee photo to fit large rectangle
                    var photoRect = CalculateCenterCropRect(photoBitmap, largeRectWidth, largeRectActualHeight);
                    var croppedPhoto = new CroppedBitmap(photoBitmap, photoRect);
                    
                    // Resize to fit exactly
                    var resizedPhoto = new TransformedBitmap(croppedPhoto, new ScaleTransform(
                        (double)largeRectWidth / photoRect.Width,
                        (double)largeRectActualHeight / photoRect.Height
                    ));

                    // Draw employee photo in large rectangle (leaving space for shield at bottom)
                    drawingContext.DrawImage(resizedPhoto, new System.Windows.Rect(
                        largeRectLeft,
                        largeRectTop + marginY,
                        largeRectWidth,
                        largeRectActualHeight
                    ));

                    // Draw employee name in small rectangle (leaving space for shield at top)
                    var formattedText = new FormattedText(
                        employeeName,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight, // English only logic
                        new Typeface("Tahoma"),
                        CalculateFontSize(smallRectWidth, smallRectActualHeight, employeeName),
                        Brushes.Black,
                        96.0 // Standard DPI
                    );

                    // Center the text in the small rectangle (accounting for shield overlap at top)
                    var textX = smallRectLeft + (smallRectWidth - formattedText.Width) / 2;
                    var textY = smallRectTop + marginY + shieldOverlap + (smallRectActualHeight - formattedText.Height) / 2;

                    drawingContext.DrawText(formattedText, new System.Windows.Point(textX, textY));
                    
                    // Note: The shield icon should already be part of the template image
                    // and will show through since we're drawing the template first as background
                    // If the template doesn't include the shield, it needs to be added to the template image
                }

                // Render to bitmap
                var renderBitmap = new RenderTargetBitmap(
                    templateWidth,
                    templateHeight,
                    96, // DPI
                    96,
                    PixelFormats.Pbgra32
                );
                renderBitmap.Render(drawingVisual);
                renderBitmap.Freeze();

                // Save to file
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using (var fileStream = new FileStream(outputPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                _logger.LogInformation("Badge generated successfully: {OutputPath}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating badge");
                return false;
            }
        }

        /// <summary>
        /// Calculates a center crop rectangle for the photo to fit the target dimensions
        /// </summary>
        private Int32Rect CalculateCenterCropRect(BitmapSource source, int targetWidth, int targetHeight)
        {
            var sourceWidth = source.PixelWidth;
            var sourceHeight = source.PixelHeight;

            // Calculate aspect ratios
            var sourceAspect = (double)sourceWidth / sourceHeight;
            var targetAspect = (double)targetWidth / targetHeight;

            int cropWidth, cropHeight, cropX, cropY;

            if (sourceAspect > targetAspect)
            {
                // Source is wider - crop width
                cropHeight = sourceHeight;
                cropWidth = (int)(sourceHeight * targetAspect);
                cropX = (sourceWidth - cropWidth) / 2;
                cropY = 0;
            }
            else
            {
                // Source is taller - crop height
                cropWidth = sourceWidth;
                cropHeight = (int)(sourceWidth / targetAspect);
                cropX = 0;
                cropY = (sourceHeight - cropHeight) / 2;
            }

            return new Int32Rect(cropX, cropY, cropWidth, cropHeight);
        }

        /// <summary>
        /// Calculates appropriate font size based on rectangle dimensions and text length
        /// </summary>
        private double CalculateFontSize(int rectWidth, int rectHeight, string text)
        {
            // Start with a base size
            var baseSize = Math.Min(rectWidth, rectHeight) * 0.15;
            
            // Adjust based on text length (longer text needs smaller font)
            var lengthFactor = Math.Max(0.5, Math.Min(1.0, 20.0 / Math.Max(1, text.Length)));
            
            var fontSize = baseSize * lengthFactor;
            
            // Ensure minimum and maximum sizes
            return Math.Max(8, Math.Min(24, fontSize));
        }
    }
}

